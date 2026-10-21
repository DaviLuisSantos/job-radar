using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Matching;

/// <summary>
/// Marks listings with the full-text phrases of the scoring policy (work rights, citizenship,
/// working holiday...). Runs after collection, so the listings it marks are already stored. Each
/// term search is a run in <c>collection_runs</c> (key <c>term: phrase</c>): a failed one is
/// retried with a wider window next time, and the next term still runs.
/// </summary>
public sealed class FullTextTermService(
    IEnumerable<IFullTextSearch> searches,
    ScoringPolicy policy,
    IListingRepository listings,
    IListingTermRepository terms,
    ICollectionRunRepository runs,
    TimeProvider time,
    ILogger<FullTextTermService> log)
{
    /// <summary>Ads stay up about 30 days; older ones are gone, there is nothing to mark.</summary>
    internal const int MaxWindowDays = 31;

    /// <summary>Overlap with the previous search, so an ad posted around it is not missed.</summary>
    internal const int OverlapDays = 2;

    public async Task TagAsync(CancellationToken ct)
    {
        var all = FullTextTerm.AllOf(policy);
        foreach (var search in searches)
        {
            var marked = 0;
            foreach (var term in all)
            {
                var run = await RunOneAsync(search, term, ct);
                await runs.RecordAsync(run, ct);
                marked += run.New;
            }
            log.LogInformation("Full-text terms on {Source}: {Terms} terms, {Marked} new marks", search.Source, all.Count, marked);
        }
    }

    private async Task<CollectionRun> RunOneAsync(IFullTextSearch search, FullTextTerm term, CancellationToken ct)
    {
        var started = time.GetUtcNow();
        var last = await runs.GetLastSuccessfulStartAsync(search.Source, term.RunKey, ct);
        var oldest = last is { } since ? await listings.GetOldestPostedAmongFirstSeenSinceAsync(search.Source, since, ct) : null;
        var window = Window(last, oldest, started);

        try
        {
            var result = await search.FindAsync(term, window, ct);
            var added = await terms.RecordAsync(search.Source, term.Text, result.SourceIds.ToList(), started, ct);
            return new CollectionRun(search.Source, term.RunKey, window, started, time.GetUtcNow(),
                result.SourceIds.Count, added, result.Complete, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(ex, "Full-text term {Term} on {Source} failed", term.Text, search.Source);
            return new CollectionRun(search.Source, term.RunKey, window, started, time.GetUtcNow(),
                Found: 0, New: 0, Complete: false, Error: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Days since the last successful search of the term, plus the overlap; the maximum the first
    /// time. A listing collected after that search may have been posted before it (a new profile
    /// or keyword brings older ads), so the window widens to cover the oldest of those.
    /// </summary>
    internal static int Window(DateTimeOffset? lastSuccess, DateTimeOffset? oldestPostedSince, DateTimeOffset now)
    {
        if (lastSuccess is not { } last)
            return MaxWindowDays;
        var elapsed = (now - last).Days;
        if (oldestPostedSince is { } oldest)
            elapsed = Math.Max(elapsed, (now - oldest).Days);
        return Math.Clamp(elapsed + OverlapDays, OverlapDays, MaxWindowDays);
    }
}
