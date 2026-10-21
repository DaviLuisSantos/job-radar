using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Collection;

/// <summary>
/// The collection flow: every search on every source, each run logged. A failing search is
/// logged and recorded, and the next one still runs.
/// </summary>
public sealed class ListingCollector(
    IEnumerable<IJobSource> sources,
    IListingRepository listings,
    ICollectionRunRepository runs,
    TimeProvider time,
    ILogger<ListingCollector> log)
{
    public async Task<CollectionSummary> CollectAsync(IReadOnlyList<ListingSearch> searches, CancellationToken ct)
    {
        var done = new List<CollectionRun>();
        foreach (var source in sources)
        {
            foreach (var search in searches)
            {
                var run = await RunOneAsync(source, search, ct);
                await runs.RecordAsync(run, ct);
                done.Add(run);
            }
        }

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
        var last = await runs.GetLastSuccessfulStartAsync(source.Name, search.Key, ct);
        var window = IncrementalWindow(last, started);

        try
        {
            var result = await source.SearchAsync(search with { PostedWithinDays = window }, ct);
            var observed = await listings.RecordObservationsAsync(result.Listings, search.Key, started, ct);
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

    /// <summary>
    /// Days since the last successful run, plus one so runs always overlap by a day. A search
    /// that never succeeded fetches everything.
    /// </summary>
    internal static int? IncrementalWindow(DateTimeOffset? lastSuccess, DateTimeOffset now) =>
        lastSuccess is { } last ? Math.Max(1, (now - last).Days + 1) : null;
}
