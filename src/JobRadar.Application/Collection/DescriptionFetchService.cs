using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Collection;

/// <param name="Blocked">The source turned the system away and the stage stopped (see <see cref="SourceBlockedException"/>).</param>
public sealed record DescriptionFetchSummary(int Asked, int Saved, int Gone, int Failed, bool Blocked);

/// <summary>
/// Fetches the full text of the listings that matter (good enough score, recent, no text yet), so
/// the scoring stage reads the whole ad. Runs after collection and before scoring. Requests go in
/// parallel through the source's own budget; storage is used one call at a time. The first refusal
/// stops the stage: every further request would be refused too.
/// </summary>
public sealed class DescriptionFetchService(
    IEnumerable<IDescriptionSource> sources,
    IListingDescriptionRepository descriptions,
    IOptions<DescriptionOptions> options,
    IOptions<CollectionOptions> collection,
    TimeProvider time,
    ILogger<DescriptionFetchService> log)
{
    private readonly SemaphoreSlim _storage = new(1, 1);

    public async Task<IReadOnlyList<DescriptionFetchSummary>> FetchAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled)
            return [];

        var summaries = new List<DescriptionFetchSummary>();
        foreach (var source in sources)
            summaries.Add(await FetchFromAsync(source, o, ct));
        return summaries;
    }

    private async Task<DescriptionFetchSummary> FetchFromAsync(IDescriptionSource source, DescriptionOptions o, CancellationToken ct)
    {
        var postedSince = time.GetUtcNow().AddDays(-o.MaxAgeDays);
        var ids = await descriptions.ListToFetchAsync(source.Source, o.MinScore, postedSince, o.MaxPerPass, ct);

        int saved = 0, gone = 0, failed = 0;
        SourceBlockedException? blocked = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = collection.Value.MaxParallelSearches, CancellationToken = stop.Token };

        try
        {
            await Parallel.ForEachAsync(ids, parallel, async (id, token) =>
            {
                try
                {
                    var text = ListingDescription.Normalize(await source.FetchAsync(id.SourceId, token));
                    if (text is null)
                    {
                        Interlocked.Increment(ref gone);
                        return;
                    }
                    await StoredAsync(() => descriptions.SaveAsync(new ListingDescription(id, text, DescriptionOrigin.Source, time.GetUtcNow()), token), token);
                    Interlocked.Increment(ref saved);
                }
                catch (SourceBlockedException ex)
                {
                    Interlocked.CompareExchange(ref blocked, ex, null);
                    await stop.CancelAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One ad that fails (timeout, odd answer) does not stop the others; it is asked again next pass.
                    Interlocked.Increment(ref failed);
                    log.LogDebug(ex, "Description of {Id} failed", id);
                }
            });
        }
        catch (OperationCanceledException) when (blocked is not null && !ct.IsCancellationRequested)
        {
            // Stopped on purpose, after a refusal.
        }

        var summary = new DescriptionFetchSummary(ids.Count, saved, gone, failed, blocked is not null);
        if (blocked is not null)
            log.LogWarning("Descriptions on {Source} stopped: {Reason}. {Saved} saved before that", source.Source, blocked.Reason, saved);
        log.LogInformation(
            "Descriptions on {Source}: {Asked} to fetch, {Saved} saved, {Gone} no longer online, {Failed} failed",
            source.Source, ids.Count, saved, gone, failed);
        return summary;
    }

    /// <summary>One storage call at a time: the network runs in parallel, the database context cannot.</summary>
    private async Task StoredAsync(Func<Task> call, CancellationToken ct)
    {
        await _storage.WaitAsync(ct);
        try
        {
            await call();
        }
        finally
        {
            _storage.Release();
        }
    }
}
