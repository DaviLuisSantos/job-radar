using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Persistence.Listings;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class ListingDescriptionRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly ListingId One = new("seek", "1");

    private async Task SeedAsync(params string[] ids)
    {
        await using var ctx = db.NewContext();
        await new EfListingRepository(ctx).RecordObservationsAsync(
            ids.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")).ToList(),
            "labourer @ perth wa", Monday, Ct);
    }

    private EfListingDescriptionRepository Repo(Infrastructure.Persistence.JobRadarDbContext ctx) => new(ctx);

    [DatabaseFact]
    public async Task A_capture_is_stored_and_a_new_one_replaces_it()
    {
        await db.ResetAsync();
        await SeedAsync("1");

        await using (var ctx = db.NewContext())
            await Repo(ctx).SaveAsync(new ListingDescription(One, "first text", DescriptionOrigin.Pasted, Monday), Ct);
        await using (var ctx = db.NewContext())
            await Repo(ctx).SaveAsync(new ListingDescription(One, "second text", DescriptionOrigin.Browser, Monday.AddHours(1)), Ct);

        await using var read = db.NewContext();
        Assert.Equal(new ListingDescription(One, "second text", DescriptionOrigin.Browser, Monday.AddHours(1)),
            await Repo(read).FindAsync(One, Ct) is { } d ? d with { CapturedAt = d.CapturedAt.ToOffset(Monday.Offset) } : null);
        Assert.Null(await Repo(read).FindAsync(new ListingId("seek", "2"), Ct));
    }

    [DatabaseFact]
    public async Task Texts_come_back_only_for_the_listings_that_have_one()
    {
        await db.ResetAsync();
        await SeedAsync("1", "2");
        await using (var ctx = db.NewContext())
            await Repo(ctx).SaveAsync(new ListingDescription(One, "the ad", DescriptionOrigin.Browser, Monday), Ct);

        await using var read = db.NewContext();
        var texts = await Repo(read).GetTextsAsync([One, new ListingId("seek", "2")], Ct);

        Assert.Equal("the ad", Assert.Single(texts, t => t.Key == One).Value);
        Assert.Single(texts);
    }

    [DatabaseFact]
    public async Task To_fetch_are_recent_listings_without_text_whose_best_score_is_high_enough_best_first()
    {
        await db.ResetAsync();
        var posted = ListingMapperTests.Sample("1").PostedAt!.Value;
        await using (var ctx = db.NewContext())
        {
            await new EfListingRepository(ctx).RecordObservationsAsync(
            [
                .. new[] { "1", "2", "3", "4" }.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")),
                new SourcedListing(ListingMapperTests.Sample("5") with { PostedAt = posted.AddDays(-60) }, """{"id":"5"}"""),
            ], "labourer @ perth wa", Monday, Ct);
        }
        await using (var ctx = db.NewContext())
        {
            static Domain.Matching.ListingScore Score(string id, string profile, int final) =>
                new(new ListingId("seek", id), profile, new Domain.Matching.FitScore(final, []), "aaaa", Monday);
            await new Infrastructure.Persistence.Matching.EfListingScoreRepository(ctx).SaveAsync(
            [
                Score("1", "fifo", 45), Score("1", "trades", 80),   // best 80
                Score("2", "fifo", 60),
                Score("3", "fifo", 30),                             // too low
                Score("4", "fifo", 90),                             // already has text
                Score("5", "fifo", 95),                             // too old
            ], Ct);
        }
        await using (var ctx = db.NewContext())
            await Repo(ctx).SaveAsync(new ListingDescription(new ListingId("seek", "4"), "the ad", DescriptionOrigin.Pasted, Monday), Ct);

        await using var read = db.NewContext();
        var ids = await Repo(read).ListToFetchAsync("seek", 40, posted.AddDays(-31), 10, Ct);
        var limited = await Repo(read).ListToFetchAsync("seek", 40, posted.AddDays(-31), 1, Ct);

        Assert.Equal(["1", "2"], ids.Select(i => i.SourceId));
        Assert.Equal(["1"], limited.Select(i => i.SourceId));
    }

    [DatabaseFact]
    public async Task Labelled_or_tracked_listings_come_first_whatever_their_score_or_age()
    {
        await db.ResetAsync();
        var posted = ListingMapperTests.Sample("1").PostedAt!.Value;
        await using (var ctx = db.NewContext())
        {
            await new EfListingRepository(ctx).RecordObservationsAsync(
            [
                .. new[] { "1", "2", "3" }.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")),
                new SourcedListing(ListingMapperTests.Sample("4") with { PostedAt = posted.AddDays(-60) }, """{"id":"4"}"""),
            ], "labourer @ perth wa", Monday, Ct);
        }
        await using (var ctx = db.NewContext())
        {
            static Domain.Matching.ListingScore Score(string id, int final) =>
                new(new ListingId("seek", id), "fifo", new Domain.Matching.FitScore(final, []), "aaaa", Monday);
            await new Infrastructure.Persistence.Matching.EfListingScoreRepository(ctx).SaveAsync(
                [Score("1", 90), Score("2", 10), Score("4", 95)], Ct);   // 3 has no score at all
        }
        await using (var ctx = db.NewContext())
        {
            var tracking = new Infrastructure.Persistence.Tracking.EfTrackingRepository(ctx);
            await tracking.SetLabelAsync(new ListingId("seek", "2"),
                new Domain.Tracking.ListingLabel(new ListingId("seek", "2"), Domain.Tracking.Verdict.Good, null, Monday), Ct);
            await tracking.SaveApplicationAsync(
                Domain.Tracking.JobApplication.Start(new ListingId("seek", "4"), Domain.Tracking.ApplicationStatus.Dismissed, Monday), Ct);
        }

        await using var read = db.NewContext();
        var ids = await Repo(read).ListToFetchAsync("seek", 40, posted.AddDays(-31), 10, Ct);
        var limited = await Repo(read).ListToFetchAsync("seek", 40, posted.AddDays(-31), 2, Ct);

        Assert.Equal(["4", "2", "1"], ids.Select(i => i.SourceId));   // handled by best score, then the rest
        Assert.Equal(["4", "2"], limited.Select(i => i.SourceId));
    }

    [DatabaseFact]
    public async Task The_search_keys_of_a_listing_are_the_searches_that_found_it()
    {
        await db.ResetAsync();
        await SeedAsync("1");
        await using (var ctx = db.NewContext())
            await new EfListingRepository(ctx).RecordObservationsAsync(
                [new SourcedListing(ListingMapperTests.Sample("1"), """{"id":"1"}""")], "offsider @ perth wa", Monday, Ct);

        await using var read = db.NewContext();
        Assert.Equal(["labourer @ perth wa", "offsider @ perth wa"],
            (await new EfListingRepository(read).GetSearchKeysAsync(One, Ct)).Order(StringComparer.Ordinal));
    }
}
