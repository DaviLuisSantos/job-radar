using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Persistence.Tracking;
using JobRadar.Infrastructure.Tests.Databases;
using JobRadar.Infrastructure.Tests.Persistence;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class TriageQueriesContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SeedAsync(params (string Id, string Title, string? Company, string Profile, int Score)[] rows)
    {
        await using (var ctx = db.NewContext())
        {
            await new EfListingRepository(ctx).RecordObservationsAsync(
                rows.DistinctBy(r => r.Id).Select(r => new SourcedListing(
                    ListingMapperTests.Sample(r.Id, r.Title) with { Company = r.Company, PostedAt = Monday.AddHours(int.Parse(r.Id)) },
                    $$"""{"id":"{{r.Id}}"}""")).ToList(),
                "s @ p", Monday, Ct);
        }
        await using (var ctx = db.NewContext())
        {
            await new EfListingScoreRepository(ctx).SaveAsync(rows.Select(r => new ListingScore(
                new ListingId("seek", r.Id), r.Profile, new FitScore(r.Score, []), "h", Monday)).ToList(), Ct);
        }
    }

    private async Task<IReadOnlyList<TriageRow>> ListAsync(TriageFilter filter)
    {
        await using var ctx = db.NewContext();
        return await new EfListingScoreRepository(ctx).ListForTriageAsync(filter, Ct);
    }

    private static string[] Ids(IEnumerable<TriageRow> rows) => rows.Select(r => r.Id.SourceId).ToArray();

    [DatabaseFact]
    public async Task Lists_a_profile_best_first_without_excluded_ones()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60), ("2", "Dev 2", "B", "tech", 90), ("3", "Gone", "C", "tech", 0), ("4", "Chef", "D", "hospo", 99));

        Assert.Equal(["2", "1"], Ids(await ListAsync(new TriageFilter("tech"))));
        Assert.Equal(["2", "1", "3"], Ids(await ListAsync(new TriageFilter("tech", IncludeExcluded: true))));
    }

    [DatabaseFact]
    public async Task Excluded_rows_say_why_and_every_row_carries_its_conditions()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60), ("2", "Lead Dev", "B", "tech", 0));
        await using (var ctx = db.NewContext())
        {
            await new EfListingScoreRepository(ctx).SaveAsync(
            [
                new ListingScore(new ListingId("seek", "1"), "tech",
                    new FitScore(60, [new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 10)]), "h", Monday),
                new ListingScore(new ListingId("seek", "2"), "tech",
                    new FitScore(0, [new ScoreItem(ScoreRule.LeadershipTitle, "lead", ScoreWhere.Title, -50)]), "h", Monday),
            ], Ct);
        }

        var rows = (await ListAsync(new TriageFilter("tech", IncludeExcluded: true))).ToDictionary(r => r.Id.SourceId);

        Assert.Null(rows["1"].ExcludedBy);   // only an excluded row brings its explanation
        Assert.Equal(new ScoreItem(ScoreRule.LeadershipTitle, "lead", ScoreWhere.Title, -50), rows["2"].ExcludedBy);
        Assert.Equal(WorkArrangement.Hybrid, rows["1"].Conditions.Arrangement);   // from the sample's summary
    }

    [DatabaseFact]
    public async Task Seen_since_keeps_only_listings_first_seen_after_the_moment()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60));   // first seen on Monday
        await using (var ctx = db.NewContext())
        {
            await new EfListingRepository(ctx).RecordObservationsAsync(
                [new SourcedListing(ListingMapperTests.Sample("2", "Dev 2"), """{"id":"2"}""")], "s @ p", Monday.AddDays(2), Ct);
            await new EfListingScoreRepository(ctx).SaveAsync(
                [new ListingScore(new ListingId("seek", "2"), "tech", new FitScore(70, []), "h", Monday)], Ct);
        }

        Assert.Equal(["2"], Ids(await ListAsync(new TriageFilter("tech", SeenSince: Monday.AddDays(1)))));
        Assert.Equal(["2", "1"], Ids(await ListAsync(new TriageFilter("tech", SeenSince: Monday.AddDays(-1)))));
    }

    [DatabaseFact]
    public async Task Equal_scores_show_the_most_recent_first()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Older", "A", "tech", 70), ("2", "Newer", "B", "tech", 70));

        Assert.Equal(["2", "1"], Ids(await ListAsync(new TriageFilter("tech"))));
    }

    [DatabaseFact]
    public async Task Handled_listings_leave_the_queue_unless_asked_for()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60), ("2", "Dev 2", "B", "tech", 70), ("3", "Dev 3", "C", "tech", 80));
        await using (var ctx = db.NewContext())
        {
            var repo = new EfTrackingRepository(ctx);
            await repo.SaveApplicationAsync(JobApplication.Start(new ListingId("seek", "3"), ApplicationStatus.Applied, Monday), Ct);
            await repo.SetLabelAsync(new ListingId("seek", "2"), new ListingLabel(new ListingId("seek", "2"), Verdict.Bad, null, Monday), Ct);
        }

        Assert.Equal(["1"], Ids(await ListAsync(new TriageFilter("tech"))));
        var all = await ListAsync(new TriageFilter("tech", OnlyUntriaged: false));
        Assert.Equal(["3", "2", "1"], Ids(all));
        Assert.Equal((ApplicationStatus.Applied, (Verdict?)null), (all[0].Status, all[0].Verdict));
        Assert.Equal(((ApplicationStatus?)null, Verdict.Bad), (all[1].Status, all[1].Verdict));
    }

    [DatabaseFact]
    public async Task Minimum_score_and_text_filter_title_or_company()
    {
        await db.ResetAsync();
        await SeedAsync(("1", ".NET Developer", "Acme", "tech", 60), ("2", "Barista", "Bean 100% Co", "tech", 75), ("3", "C# Engineer", "Zed", "tech", 40));

        Assert.Equal(["2", "1"], Ids(await ListAsync(new TriageFilter("tech", MinScore: 50))));
        Assert.Equal(["1"], Ids(await ListAsync(new TriageFilter("tech", Text: "developer"))));
        Assert.Equal(["2"], Ids(await ListAsync(new TriageFilter("tech", Text: "bean"))));
        // LIKE wildcards in the text are literal.
        Assert.Equal(["2"], Ids(await ListAsync(new TriageFilter("tech", Text: "100%"))));
        Assert.Empty(await ListAsync(new TriageFilter("tech", Text: "_")));
    }

    [DatabaseFact]
    public async Task Profiles_and_the_scores_of_one_listing()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60), ("1", "Dev", "A", "fifo", 20), ("2", "Chef", "B", "hospo", 70));

        await using var ctx = db.NewContext();
        var repo = new EfListingScoreRepository(ctx);
        Assert.Equal(["fifo", "hospo", "tech"], await repo.ListProfilesAsync(Ct));
        Assert.Equal([("tech", 60), ("fifo", 20)], (await repo.GetScoresAsync(new ListingId("seek", "1"), Ct)).Select(s => (s.Profile, s.Score.Final)));
    }

    [DatabaseFact]
    public async Task Find_label_returns_what_was_set()
    {
        await db.ResetAsync();
        await SeedAsync(("1", "Dev", "A", "tech", 60));
        var id = new ListingId("seek", "1");

        await using var ctx = db.NewContext();
        var repo = new EfTrackingRepository(ctx);
        Assert.Null(await repo.FindLabelAsync(id, Ct));
        await repo.SetLabelAsync(id, new ListingLabel(id, Verdict.Good, "nice", Monday), Ct);
        Assert.Equal((Verdict.Good, "nice"), ((await repo.FindLabelAsync(id, Ct))!.Verdict, (await repo.FindLabelAsync(id, Ct))!.Note));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresTriageQueriesContractTests(PostgresFixture db) : TriageQueriesContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerTriageQueriesContractTests(SqlServerFixture db) : TriageQueriesContractTests(db);
