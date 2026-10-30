using System.Collections.Concurrent;
using System.Diagnostics;
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
    IListingDescriptionRepository descriptions,
    ICollectionRunRepository runs,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    ILogger<ListingCollector> log)
{
    private readonly SemaphoreSlim _storage = new(1, 1);

    public Task<CollectionSummary> CollectAsync(IReadOnlyList<ListingSearch> searches, CancellationToken ct) =>
        CollectAsync(searches, fullHistory: false, ct);

    /// <param name="fullHistory">Ignore the incremental windows: fetch everything the source still has.</param>
    public async Task<CollectionSummary> CollectAsync(IReadOnlyList<ListingSearch> searches, bool fullHistory, CancellationToken ct)
    {
        var done = new ConcurrentBag<CollectionRun>();
        var work = sources.SelectMany(source => searches.Where(s => source.Markets.Contains(s.Market)).Select(search => (source, search)));
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxParallelSearches, CancellationToken = ct };

        await Parallel.ForEachAsync(work, parallel, async (item, token) =>
        {
            var run = await RunOneAsync(item.source, item.search, fullHistory, token);
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

    private async Task<CollectionRun> RunOneAsync(IJobSource source, ListingSearch search, bool fullHistory, CancellationToken ct)
    {
        using var activity = JobRadarTelemetry.Source.StartActivity("collect search");
        activity?.SetTag("jobradar.source", source.Name);
        activity?.SetTag("jobradar.market", search.Market.ToString());
        activity?.SetTag("jobradar.search", search.Key);
        var tags = new TagList { { "source", source.Name }, { "market", search.Market.ToString() } };
        var started = time.GetUtcNow();
        var last = fullHistory ? null : await StoredAsync(() => runs.GetLastCompleteStartAsync(source.Name, search.Key, ct), ct);
        var window = IncrementalWindow(last, started);

        try
        {
            var result = await source.SearchAsync(search with { PostedWithinDays = window }, ct);
            var observed = await StoredAsync(() => listings.RecordObservationsAsync(result.Listings, search.Key, started, ct), ct);
            await StoredAsync(() => SaveDescriptionsAsync(result.Listings, started, ct), ct);
            JobRadarTelemetry.JobsCollected.Add(result.Listings.Count, tags);
            JobRadarTelemetry.JobsNew.Add(observed.New, tags);
            JobRadarTelemetry.JobsKnown.Add(observed.AlreadyKnown, tags);
            activity?.SetTag("jobradar.found", result.Listings.Count);
            activity?.SetTag("jobradar.new", observed.New);
            return new CollectionRun(source.Name, search.Key, window, started, time.GetUtcNow(),
                result.Listings.Count, observed.New, result.Complete, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(ex, "Search {SearchKey} on {Source} ({Market}) failed", search.Key, source.Name, search.Market);
            tags.Add("error", ex.GetType().Name);
            JobRadarTelemetry.SourceErrors.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            return new CollectionRun(source.Name, search.Key, window, started, time.GetUtcNow(),
                Found: 0, New: 0, Complete: false, Error: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The full ads a search returned with its listings: stored as fetched from the source, replacing an older
    /// text only when it changed.
    /// </summary>
    private async Task SaveDescriptionsAsync(IReadOnlyList<SourcedListing> found, DateTimeOffset at, CancellationToken ct)
    {
        var texts = found
            .Select(l => (l.Listing.Id, Text: ListingDescription.Normalize(l.Description)))
            .Where(l => l.Text is not null)
            .DistinctBy(l => l.Id)
            .ToList();
        if (texts.Count == 0)
            return;
        var stored = await descriptions.GetTextsAsync(texts.Select(t => t.Id).ToList(), ct);
        foreach (var (id, text) in texts)
        {
            if (!stored.TryGetValue(id, out var old) || old != text)
                await descriptions.SaveAsync(new ListingDescription(id, text!, DescriptionOrigin.Source, at), ct);
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
    /// Days since the last complete run, plus one so runs always overlap by a day. A search that never
    /// completed fetches everything: a run cut at the source cap did not see its whole window.
    /// </summary>
    internal static int? IncrementalWindow(DateTimeOffset? lastSuccess, DateTimeOffset now) =>
        lastSuccess is { } last ? Math.Max(1, (now - last).Days + 1) : null;
}
