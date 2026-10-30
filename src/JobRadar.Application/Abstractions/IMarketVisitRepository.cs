using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// When the candidate last opened each market's "Today" page (table <c>market_visits</c>). In the database, not the
/// browser, so it holds between the phone and the PC.
/// </summary>
public interface IMarketVisitRepository
{
    /// <summary>The last visit of every market visited at least once.</summary>
    Task<IReadOnlyDictionary<JobMarket, DateTimeOffset>> GetLastVisitsAsync(CancellationToken ct = default);

    Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default);
}
