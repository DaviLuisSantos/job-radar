using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Postgres.Listings;

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
        await new PostgresListingRepository(ctx).RecordObservationsAsync(
            ids.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")).ToList(),
            "labourer @ perth wa", Monday, Ct);
    }

    private PostgresListingDescriptionRepository Repo(Infrastructure.Postgres.JobRadarDbContext ctx) => new(ctx);

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
    public async Task The_search_keys_of_a_listing_are_the_searches_that_found_it()
    {
        await db.ResetAsync();
        await SeedAsync("1");
        await using (var ctx = db.NewContext())
            await new PostgresListingRepository(ctx).RecordObservationsAsync(
                [new SourcedListing(ListingMapperTests.Sample("1"), """{"id":"1"}""")], "offsider @ perth wa", Monday, Ct);

        await using var read = db.NewContext();
        Assert.Equal(["labourer @ perth wa", "offsider @ perth wa"],
            (await new PostgresListingRepository(read).GetSearchKeysAsync(One, Ct)).Order(StringComparer.Ordinal));
    }
}
