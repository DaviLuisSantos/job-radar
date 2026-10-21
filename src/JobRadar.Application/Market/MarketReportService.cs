using System.Globalization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Market;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Market;

/// <summary>Builds the monthly Raio-X of a source from its stored snapshots.</summary>
public sealed class MarketReportService(IMarketRepository market, IOptions<MarketOptions> options)
{
    /// <summary>Null when the month has no good snapshot.</summary>
    public async Task<MarketReport?> BuildAsync(string source, string month, CancellationToken ct = default)
    {
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var last = first.AddMonths(1).AddDays(-1);
        var previousFirst = first.AddMonths(-1);

        var current = await market.GetGoodCountsAsync(source, first, last, ct);
        var previous = await market.GetGoodCountsAsync(source, previousFirst, first.AddDays(-1), ct);
        var listings = await market.GetListingsSeenAsync(source, first, last, ct);
        var skipped = (await market.GetSnapshotsAsync(source, first, last, ct)).Where(s => s.Status != SnapshotStatus.Ok).ToList();

        var o = options.Value;
        return MonthlyReport.Build(month, o.Categories.Select(c => c.Name).ToList(), o.AllCities.ToList(), o.Terms.Select(t => t.Name).ToList(),
            current, previous, listings, skipped);
    }
}
