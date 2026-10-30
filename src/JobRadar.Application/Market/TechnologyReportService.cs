using System.Globalization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Market;

/// <summary>
/// The technologies a month's ads of one country asked for (<see cref="TechnologyCount"/>), from the full ads stored.
/// Personal use, like the ads it reads: not part of the Raio-X.
/// </summary>
public sealed class TechnologyReportService(IListingDescriptionRepository descriptions)
{
    /// <param name="month">"yyyy-MM". The month is in UTC: hours of time zone move a handful of listings at most.</param>
    public async Task<TechnologyReport> BuildAsync(Country country, string month, CancellationToken ct = default)
    {
        var start = DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var from = new DateTimeOffset(start, TimeSpan.Zero);
        var ads = await descriptions.ListAdsFirstSeenAsync(country, from, from.AddMonths(1), ct);
        var report = TechnologyCount.Count(month, ads);
        if (country != Country.Remote)
            return report;
        // Remote ads also say how they hire: contractor, EOR or direct, read the way remote profiles read them.
        var engagements = ads
            .Where(a => a.Description is not null)
            .GroupBy(a => EngagementDetector.Detect($"{a.Title} {a.Description}", MarketRules.GlobalRemote))
            .ToDictionary(g => g.Key, g => g.Count());
        return report with { Engagements = engagements };
    }
}
