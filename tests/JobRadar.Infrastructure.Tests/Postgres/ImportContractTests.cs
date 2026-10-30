using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Market;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class ImportContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset June = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset October = new(2026, 10, 21, 6, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImportedListing Imported(string id, DateTimeOffset firstSeen, params string[] keys) =>
        new(new SourcedListing(ListingMapperTests.Sample(id, "Imported title"), $$$"""{"importedFrom":"job-tracker","row":{"id":"seek_{{{id}}}"}}"""),
            firstSeen, firstSeen, keys);

    private async Task<T> Listings<T>(Func<EfListingRepository, Task<T>> work)
    {
        await using var ctx = db.NewContext();
        return await work(new EfListingRepository(ctx));
    }

    [DatabaseFact]
    public async Task New_listings_come_in_with_their_history_and_profile_keys()
    {
        await db.ResetAsync();

        var result = await Listings(r => r.ImportAsync([Imported("1", June, "profile: tech", "profile: fifo")], Ct));

        Assert.Equal(1, result.New);
        var found = await Listings(r => r.FindAsync(new ListingId("seek", "1"), Ct));
        Assert.Equal((June, "Imported title"), (found!.FirstSeenAt, found.Listing.Title));
        Assert.Single(await Listings(r => r.ListFoundByAsync(["profile: fifo"], Ct)));
    }

    [DatabaseFact]
    public async Task A_listing_already_here_keeps_its_details_and_only_moves_first_seen_back()
    {
        await db.ResetAsync();
        await Listings(r => r.RecordObservationsAsync(
            [new SourcedListing(ListingMapperTests.Sample("1", "Current title"), """{"id":"1"}""")], "dev @ perth", October, Ct));

        var result = await Listings(r => r.ImportAsync([Imported("1", June, "profile: tech")], Ct));
        await Listings(r => r.ImportAsync([Imported("1", June.AddDays(30))], Ct));   // a later sighting changes nothing

        var found = await Listings(r => r.FindAsync(new ListingId("seek", "1"), Ct));
        Assert.Equal(1, result.AlreadyKnown);
        Assert.Equal(("Current title", June, October), (found!.Listing.Title, found.FirstSeenAt, found.LastSeenAt));
        Assert.Single(await Listings(r => r.ListFoundByAsync(["profile: tech"], Ct)));
        Assert.Single(await Listings(r => r.ListFoundByAsync(["dev @ perth"], Ct)));
    }

    [DatabaseFact]
    public async Task Importing_twice_adds_nothing_the_second_time()
    {
        await db.ResetAsync();
        await Listings(r => r.ImportAsync([Imported("1", June, "profile: tech")], Ct));

        var again = await Listings(r => r.ImportAsync([Imported("1", June, "profile: tech")], Ct));

        Assert.Equal((0, 1), (again.New, again.AlreadyKnown));
        await using var ctx = db.NewContext();
        Assert.Equal((1, 1), (await ctx.ListingPayloads.CountAsync(Ct), await ctx.ListingSearchHits.CountAsync(Ct)));
    }

    [DatabaseFact]
    public async Task Market_days_already_here_are_kept_and_listing_ranges_widen()
    {
        await db.ResetAsync();
        var day = new DateOnly(2026, 10, 1);
        await using (var ctx = db.NewContext())
        {
            var repo = new EfMarketRepository(ctx);
            await repo.SaveSnapshotAsync(new MarketSnapshot("seek", day, SnapshotStatus.Ok, 1, 1, 0, []), [new("HVAC-R", "Perth WA", null, 72)], [], Ct);
        }

        await using (var ctx = db.NewContext())
        {
            var repo = new EfMarketRepository(ctx);
            Assert.False(await repo.ImportSnapshotAsync(new MarketSnapshot("seek", day, SnapshotStatus.Ok, 1, 1, 0, []), [new("HVAC-R", "Perth WA", null, 999)], Ct));
            Assert.True(await repo.ImportSnapshotAsync(new MarketSnapshot("seek", day.AddDays(-1), SnapshotStatus.Ok, 1, 1, 0, []), [new("HVAC-R", "Perth WA", null, 70)], Ct));

            var listing = new MarketListing("HVAC-R", "Perth WA", ListingMapperTests.Sample("9") with { SalaryText = "$100k" });
            await repo.ImportListingHistoryAsync("seek", [new ImportedMarketListing(listing, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 20))], Ct);
            await repo.ImportListingHistoryAsync("seek", [new ImportedMarketListing(listing, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15))], Ct);
        }

        await using var read = db.NewContext();
        var readRepo = new EfMarketRepository(read);
        Assert.Equal([70, 72], (await readRepo.GetGoodCountsAsync("seek", day.AddDays(-1), day, Ct)).OrderBy(c => c.Date).Select(c => c.Total));
        var seen = Assert.Single(await readRepo.GetListingsSeenAsync("seek", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Ct));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20), 100_000), (seen.FirstSeen, seen.LastSeen, seen.AnnualMin!.Value));
    }
}
