using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Infrastructure.Postgres.Listings;
using JobRadar.Infrastructure.Postgres.Matching;
using JobRadar.Infrastructure.Postgres.Tracking;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class TrackingRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));
    private static readonly ListingId One = new("seek", "1");
    private static readonly ListingId Two = new("seek", "2");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SeedAsync(params string[] ids)
    {
        await using var ctx = db.NewContext();
        await new PostgresListingRepository(ctx).RecordObservationsAsync(
            ids.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")).ToList(), "s @ p", Monday, Ct);
    }

    private async Task<T> WithRepo<T>(Func<PostgresTrackingRepository, Task<T>> work)
    {
        await using var ctx = db.NewContext();
        return await work(new PostgresTrackingRepository(ctx));
    }

    private Task Do(Func<PostgresTrackingRepository, Task> work) => WithRepo(async r => { await work(r); return 0; });

    [DatabaseFact]
    public async Task An_application_round_trips_with_its_whole_history()
    {
        await db.ResetAsync();
        await SeedAsync("1");
        var app = JobApplication.Start(One, ApplicationStatus.Reviewed, Monday);
        app.ChangeStatus(ApplicationStatus.Applied, Monday.AddDays(1));
        app.SetNotes("Sent CV v3");

        await Do(r => r.SaveApplicationAsync(app, Ct));
        var loaded = await WithRepo(r => r.FindApplicationAsync(One, Ct));

        Assert.NotNull(loaded);
        Assert.Equal((ApplicationStatus.Applied, Monday.AddDays(1), "Sent CV v3"), (loaded.Status, loaded.AppliedAt, loaded.Notes));
        Assert.Equal(app.History, loaded.History);
    }

    [DatabaseFact]
    public async Task Saving_again_appends_only_the_new_history()
    {
        await db.ResetAsync();
        await SeedAsync("1");
        var app = JobApplication.Start(One, ApplicationStatus.Applied, Monday);
        await Do(r => r.SaveApplicationAsync(app, Ct));

        var loaded = (await WithRepo(r => r.FindApplicationAsync(One, Ct)))!;
        loaded.ChangeStatus(ApplicationStatus.Declined, Monday.AddDays(7));
        await Do(r => r.SaveApplicationAsync(loaded, Ct));

        var again = (await WithRepo(r => r.FindApplicationAsync(One, Ct)))!;
        Assert.Equal([ApplicationStatus.Applied, ApplicationStatus.Declined], again.History.Select(h => h.To));
        Assert.Equal(ApplicationStatus.Declined, again.Status);
    }

    [DatabaseFact]
    public async Task A_listing_without_application_has_none()
    {
        await db.ResetAsync();
        await SeedAsync("1");

        Assert.Null(await WithRepo(r => r.FindApplicationAsync(One, Ct)));
    }

    [DatabaseFact]
    public async Task Labels_are_replaced_keep_their_note_and_can_be_cleared()
    {
        await db.ResetAsync();
        await SeedAsync("1");
        await Do(r => r.SetLabelAsync(One, new ListingLabel(One, Verdict.Good, "my stack", Monday), Ct));
        await Do(r => r.SetLabelAsync(One, new ListingLabel(One, Verdict.Bad, null, Monday.AddDays(1)), Ct));

        await using (var ctx = db.NewContext())
        {
            var row = Assert.Single(ctx.ListingLabels);
            Assert.Equal(("bad", "my stack"), (row.Verdict, row.Note));
        }

        await Do(r => r.SetLabelAsync(One, null, Ct));
        await using (var ctx = db.NewContext())
            Assert.Empty(ctx.ListingLabels);
    }

    [DatabaseFact]
    public async Task Judged_scores_use_the_label_first_then_the_status()
    {
        await db.ResetAsync();
        await SeedAsync("1", "2", "3", "4");
        await using (var ctx = db.NewContext())
        {
            await new PostgresListingScoreRepository(ctx).SaveAsync(
                new[] { "1", "2", "3", "4" }.Select(id => new ListingScore(new ListingId("seek", id), "tech",
                    new FitScore(60, [new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 16)]), "h", Monday)).ToList(), Ct);
        }
        // 1: label good. 2: dismissed → bad. 3: declined, labelled bad → bad. 4: only reviewed → not judged.
        await Do(r => r.SetLabelAsync(One, new ListingLabel(One, Verdict.Good, null, Monday), Ct));
        await Do(r => r.SaveApplicationAsync(JobApplication.Start(Two, ApplicationStatus.Dismissed, Monday), Ct));
        var three = new ListingId("seek", "3");
        await Do(r => r.SaveApplicationAsync(JobApplication.Start(three, ApplicationStatus.Declined, Monday), Ct));
        await Do(r => r.SetLabelAsync(three, new ListingLabel(three, Verdict.Bad, null, Monday), Ct));
        var four = new ListingId("seek", "4");
        await Do(r => r.SaveApplicationAsync(JobApplication.Start(four, ApplicationStatus.Reviewed, Monday), Ct));

        await using var read = db.NewContext();
        var judged = (await new PostgresListingScoreRepository(read).ListJudgedAsync(Ct)).OrderBy(j => j.Id.SourceId).ToList();

        Assert.Equal([("1", Verdict.Good), ("2", Verdict.Bad), ("3", Verdict.Bad)],
            judged.Select(j => (j.Id.SourceId, j.Verdict)));
        Assert.Equal(ScoreRule.PositiveSignal, judged[0].Score.Items.Single().Rule);
        Assert.Equal("Backend Developer (.NET)", judged[0].Title);
    }
}
