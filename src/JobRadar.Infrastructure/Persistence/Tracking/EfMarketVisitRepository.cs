using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Tracking;

internal sealed class EfMarketVisitRepository(JobRadarDbContext db) : IMarketVisitRepository
{
    public async Task<IReadOnlyDictionary<JobMarket, DateTimeOffset>> GetLastVisitsAsync(CancellationToken ct = default)
    {
        var rows = await db.MarketVisits.ToListAsync(ct);
        return rows.ToDictionary(r => MarketVisitMapper.ToMarket(r.Market), r => r.VisitedAt);
    }

    public async Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default)
    {
        var key = MarketVisitMapper.ToColumn(market);
        var row = await db.MarketVisits.AsTracking().SingleOrDefaultAsync(v => v.Market == key, ct);
        if (row is null)
        {
            row = new MarketVisitEntity { Market = key };
            db.MarketVisits.Add(row);
        }
        row.VisitedAt = at.ToUniversalTime();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
