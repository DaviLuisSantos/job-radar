using JobRadar.Domain.Collection;
using JobRadar.Infrastructure.Postgres.Collection;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class CollectionRunRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CollectionRun Run(DateTimeOffset at, string key = ".net developer @ perth", string? error = null) =>
        new("seek", key, PostedWithinDays: 2, at, at.AddMinutes(1), Found: 10, New: 3, Complete: true, error);

    private async Task RecordAsync(CollectionRun run)
    {
        await using var ctx = db.NewContext();
        await new PostgresCollectionRunRepository(ctx).RecordAsync(run, Ct);
    }

    private async Task<DateTimeOffset?> LastSuccessAsync(string key = ".net developer @ perth", string source = "seek")
    {
        await using var ctx = db.NewContext();
        return await new PostgresCollectionRunRepository(ctx).GetLastSuccessfulStartAsync(source, key, Ct);
    }

    [DatabaseFact]
    public async Task Never_run_search_has_no_last_success()
    {
        await db.ResetAsync();

        Assert.Null(await LastSuccessAsync());
    }

    [DatabaseFact]
    public async Task Last_success_ignores_failures_other_searches_and_other_sources()
    {
        await db.ResetAsync();
        await RecordAsync(Run(Monday));
        await RecordAsync(Run(Monday.AddDays(1)));
        await RecordAsync(Run(Monday.AddDays(2), error: "HttpRequestException: 503"));
        await RecordAsync(Run(Monday.AddDays(3), key: "hospitality @ perth"));
        await RecordAsync(Run(Monday.AddDays(4)) with { Source = "adzuna" });

        Assert.Equal(Monday.AddDays(1), await LastSuccessAsync());
    }

    [DatabaseFact]
    public async Task Runs_are_stored_as_they_happened()
    {
        await db.ResetAsync();
        await RecordAsync(Run(Monday) with { Complete = false });

        await using var ctx = db.NewContext();
        var row = await ctx.CollectionRuns.SingleAsync(Ct);
        Assert.Equal((10, 3, 2, false), (row.Found, row.New, row.PostedWithinDays, row.Complete));
        Assert.Equal(Monday.AddMinutes(1), row.FinishedAt);
    }

    [DatabaseFact]
    public async Task Long_errors_are_truncated_instead_of_failing_the_insert()
    {
        await db.ResetAsync();

        await RecordAsync(Run(Monday, error: new string('x', 5000)));

        await using var ctx = db.NewContext();
        Assert.Equal(2000, (await ctx.CollectionRuns.SingleAsync(Ct)).Error!.Length);
    }
}
