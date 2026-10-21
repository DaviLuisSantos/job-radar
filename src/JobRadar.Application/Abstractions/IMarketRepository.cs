using JobRadar.Domain.Market;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// The Raio-X history (today in Postgres, tables <c>market_snapshots</c>, <c>market_counts</c>,
/// <c>market_listings</c>).
/// </summary>
public interface IMarketRepository
{
    /// <summary>
    /// Stores a day's snapshot with its counts and listings, in one transaction. Taking the snapshot
    /// again on the same day replaces that day; listings keep their first-seen date.
    /// </summary>
    Task SaveSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts,
        IReadOnlyCollection<MarketListing> listings, CancellationToken ct = default);

    /// <summary>Totals (no term) of the last good snapshot before <paramref name="before"/>, by (category, city).</summary>
    Task<IReadOnlyDictionary<(string Category, string City), int>> GetLastGoodTotalsAsync(
        string source, DateOnly before, CancellationToken ct = default);

    /// <summary>Counts of the good snapshots between the dates, inclusive.</summary>
    Task<IReadOnlyList<DailyCount>> GetGoodCountsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Listings open at some point between the dates.</summary>
    Task<IReadOnlyList<SeenListing>> GetListingsSeenAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<MarketSnapshot>> GetSnapshotsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default);
}
