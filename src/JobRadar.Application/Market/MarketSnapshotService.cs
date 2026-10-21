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
        var counts = new List<MarketCount>();
        var listings = new List<MarketListing>();
        var incomplete = new List<(string, string)>();

        foreach (var category in o.Categories)
        {
            if (!category.Codes.TryGetValue(source.Source, out var code))
                continue;
            foreach (var city in o.AllCities)
            {
                counts.Add(new MarketCount(category.Name, city, null, await TryCountAsync(source, code, city, null, ct)));
                foreach (var term in o.Terms)
                    counts.Add(new MarketCount(category.Name, city, term.Name, await TryCountAsync(source, code, city, term, ct)));

                if (o.FullCities.Contains(city))
                {
                    try
                    {
                        var result = await source.ListAsync(code, city, ct);
                        listings.AddRange(result.Listings.Select(l => new MarketListing(category.Name, city, l.Listing)));
                        if (!result.Complete)
                            incomplete.Add((category.Name, city));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                        log.LogWarning(ex, "Market listing {Category} / {City} failed", category.Name, city);
                        incomplete.Add((category.Name, city));
                    }
                }
            }
        }

        // One more chance for counts that failed: usually a passing rate limit.
        if (counts.Any(c => c.Total is null))
        {
            await Task.Delay(o.RetryPause, time, ct);
            for (var i = 0; i < counts.Count; i++)
            {
                if (counts[i].Total is not null)
                    continue;
                var c = counts[i];
                var code = o.Categories.First(cat => cat.Name == c.Category).Codes[source.Source];
                var term = c.Term is null ? null : o.Terms.First(t => t.Name == c.Term);
                counts[i] = c with { Total = await TryCountAsync(source, code, c.City, term, ct) };
            }
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
