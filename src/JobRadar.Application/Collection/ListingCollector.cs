using System.Collections.Concurrent;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Collection;

/// <summary>
/// The collection flow: every search on every source, each run logged. Searches run in parallel
/// (<see cref="CollectionOptions.MaxParallelSearches"/>); the source keeps its own request budget,
/// and storage is used one call at a time (the repositories share one database context). A failing
/// search is logged and recorded, and the others still run.
/// </summary>
public sealed class ListingCollector(
    IEnumerable<IJobSource> sources,
    IListingRepository listings,
    ICollectionRunRepository runs,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    ILogger<ListingCollector> log)
{
    private readonly SemaphoreSlim _storage = new(1, 1);

    public async Task<CollectionSummary> CollectAsync(IReadOnlyList<ListingSearch> searches, CancellationToken ct)
    {
        var done = new ConcurrentBag<CollectionRun>();
        var work = sources.SelectMany(source => searches.Select(search => (source, search)));
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxParallelSearches, CancellationToken = ct };

        await Parallel.ForEachAsync(work, parallel, async (item, token) =>
        {
            var run = await RunOneAsync(item.source, item.search, token);
            await StoredAsync(() => runs.RecordAsync(run, token), token);
            done.Add(run);
        });

        var summary = new CollectionSummary(
            Runs: done.Count,
            Failed: done.Count(r => !r.Succeeded),
            Incomplete: done.Count(r => r.Succeeded && !r.Complete),
            Found: done.Sum(r => r.Found),
            New: done.Sum(r => r.New));
        log.LogInformation(
            "Collection finished: {Runs} runs, {Failed} failed, {Incomplete} incomplete, {Found} found, {New} new",
            summary.Runs, summary.Failed, summary.Incomplete, summary.Found, summary.New);
        return summary;
    }

    private async Task<CollectionRun> RunOneAsync(IJobSource source, ListingSearch search, CancellationToken ct)
    {
        var started = time.GetUtcNow();
        var last = await StoredAsync(() => runs.GetLastSuccessfulStartAsync(source.Name, search.Key, ct), ct);
        var window = IncrementalWindow(last, started);

        try
        {
            var result = await source.SearchAsync(search with { PostedWithinDays = window }, ct);
            var observed = await StoredAsync(() => listings.RecordObservationsAsync(result.Listings, search.Key, started, ct), ct);
            return new CollectionRun(source.Name, search.Key, window, started, time.GetUtcNow(),
                result.Listings.Count, observed.New, result.Complete, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(ex, "Search {SearchKey} on {Source} failed", search.Key, source.Name);
            return new CollectionRun(source.Name, search.Key, window, started, time.GetUtcNow(),
                Found: 0, New: 0, Complete: false, Error: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>One storage call at a time: the network runs in parallel, the database context cannot.</summary>
    private async Task<T> StoredAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        await _storage.WaitAsync(ct);
        try
        {
            return await call();
        }
        finally
        {
            _storage.Release();
        }
    }

    private Task StoredAsync(Func<Task> call, CancellationToken ct) =>
        StoredAsync(async () => { await call(); return true; }, ct);

    /// <summary>
    /// Days since the last successful run, plus one so runs always overlap by a day. A search
    /// that never succeeded fetches everything.
    /// </summary>
    internal static int? IncrementalWindow(DateTimeOffset? lastSuccess, DateTimeOffset now) =>
        lastSuccess is { } last ? Math.Max(1, (now - last).Days + 1) : null;
}
