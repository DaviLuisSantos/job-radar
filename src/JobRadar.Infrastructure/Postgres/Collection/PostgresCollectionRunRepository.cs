using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Collection;

internal sealed class PostgresCollectionRunRepository(JobRadarDbContext db) : ICollectionRunRepository
{
    private const int MaxErrorLength = 2000;

    public async Task RecordAsync(CollectionRun run, CancellationToken ct = default)
    {
        db.CollectionRuns.Add(ToEntity(run));
        await db.SaveChangesAsync(ct);
    }

    public Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default) =>
        db.CollectionRuns.MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct);

    public async Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default)
    {
        // A day in the schedule time zone: Perth days do not line up with UTC ones.
        var since = DateTimeOffset.UtcNow.AddDays(-(days + 1));
        var rows = await db.CollectionRuns
            .Where(r => r.StartedAt >= since)
            .Select(r => new { r.StartedAt, r.FinishedAt, r.Error, r.Complete, r.Found, r.New })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.StartedAt, zone).DateTime))
            .OrderByDescending(g => g.Key)
            .Take(days)
            .Select(g => new DailyRunSummary(
                g.Key, g.Count(), g.Count(r => r.Error != null), g.Count(r => r.Error == null && !r.Complete),
                g.Sum(r => r.Found), g.Sum(r => r.New), g.Max(r => r.FinishedAt)))
            .ToList();
    }

    public Task<DateTimeOffset?> GetLastSuccessfulStartAsync(string source, string searchKey, CancellationToken ct = default) =>
        db.CollectionRuns
            .Where(r => r.Source == source && r.SearchKey == searchKey && r.Error == null)
            .MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct);

    internal static CollectionRunEntity ToEntity(CollectionRun run) => new()
    {
        Source = run.Source,
        SearchKey = run.SearchKey,
        PostedWithinDays = run.PostedWithinDays,
        StartedAt = run.StartedAt.ToUniversalTime(),
        FinishedAt = run.FinishedAt.ToUniversalTime(),
        Found = run.Found,
        New = run.New,
        Complete = run.Complete,
        Error = run.Error is { Length: > MaxErrorLength } e ? e[..MaxErrorLength] : run.Error,
    };
}
