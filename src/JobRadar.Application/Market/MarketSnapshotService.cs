using JobRadar.Application.Abstractions;
using JobRadar.Domain.Market;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Market;

/// <summary>
/// Takes the day's Raio-X: for each category and city, the open listings, how many mention each
/// term, and (for the full cities) every listing. The snapshot is assessed before it is stored:
/// only good ones feed the monthly report.
/// </summary>
public sealed class MarketSnapshotService(
    IEnumerable<IMarketSource> sources,
    IMarketRepository market,
    IOptions<MarketOptions> options,
    TimeProvider time,
    ILogger<MarketSnapshotService> log)
{
    public async Task<IReadOnlyList<MarketSnapshot>> TakeAsync(DateOnly day, CancellationToken ct)
    {
        var taken = new List<MarketSnapshot>();
        foreach (var source in sources)
            taken.Add(await TakeAsync(source, day, ct));
        return taken;
    }

    private async Task<MarketSnapshot> TakeAsync(IMarketSource source, DateOnly day, CancellationToken ct)
    {
        var o = options.Value;
        var cells = o.Categories
            .Where(c => c.Codes.ContainsKey(source.Source))
            .SelectMany(c => o.AllCities.Select(city => (Category: c, Code: c.Codes[source.Source], City: city)))
            .ToList();

        // Every count and every listing at once: the source paces its own requests. Results keep the
        // configured order (category, city, total then terms) because they are collected per cell.
        var countTasks = cells
            .SelectMany(cell => o.Terms.Select(t => (MarketTermOptions?)t).Prepend(null).Select(term => (cell, term)))
            .Select(async x => new MarketCount(x.cell.Category.Name, x.cell.City, x.term?.Name,
                await TryCountAsync(source, x.cell.Code, x.cell.City, x.term, ct)))
            .ToList();
        var listingTasks = cells
            .Where(cell => o.FullCities.Contains(cell.City))
            .Select(async cell =>
            {
                try
                {
                    var result = await source.ListAsync(cell.Code, cell.City, ct);
                    return (cell.Category.Name, cell.City, Listings: result.Listings.Select(l => new MarketListing(cell.Category.Name, cell.City, l.Listing)).ToList(), result.Complete);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    log.LogWarning(ex, "Market listing {Category} / {City} failed", cell.Category.Name, cell.City);
                    return (cell.Category.Name, cell.City, Listings: new List<MarketListing>(), Complete: false);
                }
            })
            .ToList();

        var counts = (await Task.WhenAll(countTasks)).ToList();
        var listed = await Task.WhenAll(listingTasks);
        var listings = listed.SelectMany(l => l.Listings).ToList();
        var incomplete = listed.Where(l => !l.Complete).Select(l => (l.Name, l.City)).ToList();

        // One more chance for counts that failed: usually a passing rate limit.
        if (counts.Any(c => c.Total is null))
        {
            await Task.Delay(o.RetryPause, time, ct);
            var retried = await Task.WhenAll(counts.Select(async c =>
            {
                if (c.Total is not null)
                    return c;
                var code = o.Categories.First(cat => cat.Name == c.Category).Codes[source.Source];
                var term = c.Term is null ? null : o.Terms.First(t => t.Name == c.Term);
                return c with { Total = await TryCountAsync(source, code, c.City, term, ct) };
            }));
            counts = [.. retried];
        }

        var previous = await market.GetLastGoodTotalsAsync(source.Source, day, ct);
        var assessment = SnapshotQuality.Assess(counts, previous, incomplete);
        var saved = counts.Where(c => c.Total is not null).ToList();
        var snapshot = new MarketSnapshot(source.Source, day, assessment.Status, counts.Count, saved.Count, listings.Count, assessment.Issues);
        await market.SaveSnapshotAsync(snapshot, saved, listings, ct);

        log.LogInformation("Market snapshot {Source} {Day}: {Status}, {Saved}/{Expected} counts, {Listings} listings",
            source.Source, day, snapshot.Status, snapshot.Saved, snapshot.Expected, snapshot.Listings);
        foreach (var issue in snapshot.Issues)
            log.LogWarning("Market snapshot issue: {Issue}", issue);
        return snapshot;
    }

    private async Task<int?> TryCountAsync(IMarketSource source, string code, string city, MarketTermOptions? term, CancellationToken ct)
    {
        try
        {
            return await source.CountAsync(code, city, term is null ? null : new FullTextTerm(term.Phrase), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Market count {Code} / {City} / {Term} failed", code, city, term?.Name);
            return null;
        }
    }
}
