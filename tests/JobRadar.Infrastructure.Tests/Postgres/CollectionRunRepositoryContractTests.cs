using JobRadar.Domain.Collection;
using JobRadar.Infrastructure.Persistence.Collection;
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
        await new EfCollectionRunRepository(ctx).RecordAsync(run, Ct);
    }

    private async Task<DateTimeOffset?> LastSuccessAsync(string key = ".net developer @ perth", string source = "seek")
    {
        await using var ctx = db.NewContext();
        return await new EfCollectionRunRepository(ctx).GetLastCompleteStartAsync(source, key, Ct);
    }

    [DatabaseFact]
    public async Task Never_run_search_has_no_last_success()
    {
        await db.ResetAsync();

        Assert.Null(await LastSuccessAsync());
    }

    [DatabaseFact]
    public async Task Last_complete_ignores_failures_cut_runs_other_searches_and_other_sources()
    {
        await db.ResetAsync();
        await RecordAsync(Run(Monday));
        await RecordAsync(Run(Monday.AddDays(1)));
        await RecordAsync(Run(Monday.AddDays(2), error: "HttpRequestException: 503"));
        await RecordAsync(Run(Monday.AddDays(2).AddHours(1)) with { Complete = false });
        await RecordAsync(Run(Monday.AddDays(3), key: "hospitality @ perth"));
        await RecordAsync(Run(Monday.AddDays(4)) with { Source = "adzuna" });

        Assert.Equal(Monday.AddDays(1), await LastSuccessAsync());
    }

    [DatabaseFact]
    public async Task Last_start_is_the_latest_of_anything_run()
    {
        await db.ResetAsync();
        await using (var empty = db.NewContext())
            Assert.Null(await new EfCollectionRunRepository(empty).GetLastStartAsync(Ct));

        await RecordAsync(Run(Monday));
        await RecordAsync(Run(Monday.AddDays(1), key: "term: backpackers", error: "boom"));

        await using var ctx = db.NewContext();
        Assert.Equal(Monday.AddDays(1), await new EfCollectionRunRepository(ctx).GetLastStartAsync(Ct));
    }

    [DatabaseFact]
    public async Task Days_are_summarised_in_the_schedule_time_zone()
    {
        await db.ResetAsync();
        var perth = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
        var today = DateTimeOffset.UtcNow;
        // 23:30 UTC is already the next day in Perth (UTC+8): both runs below are the same Perth day.
        var evening = new DateTimeOffset(today.Year, today.Month, today.Day, 23, 30, 0, TimeSpan.Zero).AddDays(-2);
        await RecordAsync(Run(evening));
        await RecordAsync(Run(evening.AddHours(2), error: "429") with { Found = 0, New = 0 });
        await RecordAsync(Run(evening.AddHours(3)) with { Complete = false });

        await using var ctx = db.NewContext();
        var day = Assert.Single(await new EfCollectionRunRepository(ctx).SummariseRecentDaysAsync(10, perth, Ct));

        Assert.Equal(DateOnly.FromDateTime(evening.AddDays(1).UtcDateTime), day.Day);
        Assert.Equal((3, 1, 1, 20, 6), (day.Runs, day.Failed, day.Incomplete, day.Found, day.New));
    }

    [DatabaseFact]
    public async Task Runs_since_a_moment_come_back_whole_and_oldest_first()
    {
        await db.ResetAsync();
        await RecordAsync(Run(Monday.AddMinutes(-1)));
        await RecordAsync(Run(Monday.AddMinutes(5), key: "term: australian citizen") with { Complete = false });
        await RecordAsync(Run(Monday, error: "HttpRequestException: 503") with { Source = "adzuna" });

        await using var ctx = db.NewContext();
        var runs = await new EfCollectionRunRepository(ctx).ListStartedSinceAsync(Monday, Ct);

        Assert.Equal(
            [
                Run(Monday, error: "HttpRequestException: 503") with { Source = "adzuna" },
                Run(Monday.AddMinutes(5), key: "term: australian citizen") with { Complete = false },
            ],
            runs);   // DateTimeOffset equality is by instant: the UTC offset read back does not matter
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
