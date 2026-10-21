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
