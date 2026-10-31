using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Collection;

internal sealed class EfAtsBoardRepository(JobRadarDbContext db) : IAtsBoardRepository
{
    /// <summary>Slugs per query: well under the parameter limits of both databases.</summary>
    private const int Chunk = 500;

    public async Task<IReadOnlyList<AtsBoard>> ListToSweepAsync(
        string platform, int maxFailures, DateTimeOffset idleBefore, int limit, CancellationToken ct = default)
    {
        var idle = idleBefore.ToUniversalTime();
        var rows = await db.AtsBoards
            .Where(b => b.Platform == platform
                && b.ConsecutiveFailures < maxFailures
                && (b.LastSweptAt == null || b.LastHitAt != null || b.LastSweptAt < idle))
            // Never swept first on both databases (NULL sorts first on one and last on the other).
            .OrderBy(b => b.LastSweptAt == null ? 0 : 1)
            .ThenBy(b => b.LastSweptAt)
            .ThenBy(b => b.Slug)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<int> AddMissingAsync(IReadOnlyCollection<AtsBoard> boards, CancellationToken ct = default)
    {
        var added = 0;
        foreach (var group in boards.GroupBy(b => b.Platform, StringComparer.Ordinal))
        {
            var platform = group.Key;
            var known = (await db.AtsBoards.Where(b => b.Platform == platform).Select(b => b.Slug).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var board in group)
                if (known.Add(board.Slug))
                {
                    db.AtsBoards.Add(new AtsBoardEntity { Platform = platform, Slug = board.Slug });
                    added++;
                }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
        return added;
    }

    public async Task RecordSweepAsync(IReadOnlyCollection<AtsBoardSweepOutcome> outcomes, CancellationToken ct = default)
    {
        try
        {
            foreach (var group in outcomes.GroupBy(o => o.Platform, StringComparer.Ordinal))
            {
                var bySlug = group.GroupBy(o => o.Slug, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
                foreach (var slugs in bySlug.Keys.Chunk(Chunk))
                {
                    var platform = group.Key;
                    var rows = await db.AtsBoards.AsTracking()
                        .Where(b => b.Platform == platform && slugs.Contains(b.Slug))
                        .ToListAsync(ct);
                    foreach (var row in rows)
                        Apply(row, bySlug[row.Slug]);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static void Apply(AtsBoardEntity row, AtsBoardSweepOutcome o)
    {
        var at = o.At.ToUniversalTime();
        switch (o.Status)
        {
            case 200:
                row.ETag = o.ETag;
                row.JobsTotal = o.JobsTotal;
                row.JobsKept = o.JobsKept;
                Answered(row, 200, at);
                break;
            case 304:
                Answered(row, 304, at);
                break;
            case 404:
                row.LastStatus = 404;
                row.LastSweptAt = at;
                row.ConsecutiveFailures++;
                break;
            default:
                // No answer, or the platform failed: say so, but keep the board in line for the next sweep.
                row.LastStatus = o.Status;
                break;
        }
    }

    private static void Answered(AtsBoardEntity row, int status, DateTimeOffset at)
    {
        row.LastStatus = status;
        row.LastSweptAt = at;
        row.ConsecutiveFailures = 0;
        if (row.JobsKept > 0)
            row.LastHitAt = at;
    }

    internal static AtsBoard ToDomain(AtsBoardEntity e) => new(
        e.Platform, e.Slug, e.ETag, e.LastStatus, e.LastSweptAt, e.LastHitAt, e.ConsecutiveFailures, e.JobsTotal, e.JobsKept);
}
