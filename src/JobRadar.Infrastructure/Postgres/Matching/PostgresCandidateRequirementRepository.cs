using JobRadar.Application.Abstractions;
using JobRadar.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Matching;

internal sealed class PostgresCandidateRequirementRepository(JobRadarDbContext db) : ICandidateRequirementRepository
{
    public async Task<CandidateRequirements> GetAsync(CancellationToken ct = default)
    {
        var rows = await db.CandidateRequirements.ToListAsync(ct);
        return new CandidateRequirements(rows.ToDictionary(
            r => r.ItemId, r => CandidateRequirementMapper.ToState(r.State), StringComparer.OrdinalIgnoreCase));
    }

    public async Task SetAsync(string itemId, RequirementState? state, DateTimeOffset at, CancellationToken ct = default)
    {
        var id = itemId.Trim().ToLowerInvariant();
        var row = await db.CandidateRequirements.AsTracking().SingleOrDefaultAsync(r => r.ItemId == id, ct);
        if (state is null)
        {
            if (row is not null)
                db.CandidateRequirements.Remove(row);
        }
        else
        {
            if (row is null)
            {
                row = new CandidateRequirementEntity { ItemId = id };
                db.CandidateRequirements.Add(row);
            }
            row.State = CandidateRequirementMapper.ToColumn(state.Value);
            row.UpdatedAt = at.ToUniversalTime();
        }

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
