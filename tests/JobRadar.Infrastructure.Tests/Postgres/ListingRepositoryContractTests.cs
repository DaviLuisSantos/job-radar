using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Postgres.Listings;

namespace JobRadar.Infrastructure.Tests.Postgres;

public class ListingRepositoryContractTests(PostgresFixture db) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ObservationResult> RecordAsync(DateTimeOffset at, params Listing[] listings)
    {
        await using var ctx = db.NewContext();
        return await new PostgresListingRepository(ctx).RecordObservationsAsync(listings, at, Ct);
    }

    private async Task<ListingRecord?> FindAsync(ListingId id)
    {
        await using var ctx = db.NewContext();
        return await new PostgresListingRepository(ctx).FindAsync(id, Ct);
    }

    [DatabaseFact]
    public async Task New_listings_are_stored_and_read_back_unchanged()
    {
        await db.ResetAsync();
        var listing = ListingMapperTests.Sample();

        var result = await RecordAsync(Monday, listing);
        var found = await FindAsync(listing.Id);

        Assert.Equal(new ObservationResult(New: 1, AlreadyKnown: 0), result);
        Assert.NotNull(found);
        Assert.Equal(listing, found.Listing);
        Assert.Equal(Monday, found.FirstSeenAt);
    }

    [DatabaseFact]
    public async Task Seeing_a_known_listing_again_refreshes_it_and_keeps_first_seen()
    {
        await db.ResetAsync();
        await RecordAsync(Monday, ListingMapperTests.Sample());

        var result = await RecordAsync(Monday.AddDays(1), ListingMapperTests.Sample(title: "Renamed"));
        var found = await FindAsync(new ListingId("seek", "1"));

        Assert.Equal(new ObservationResult(New: 0, AlreadyKnown: 1), result);
        Assert.NotNull(found);
        Assert.Equal("Renamed", found.Listing.Title);
        Assert.Equal(Monday, found.FirstSeenAt);
        Assert.Equal(Monday.AddDays(1), found.LastSeenAt);
    }

    [DatabaseFact]
    public async Task The_same_ad_twice_in_one_batch_counts_once_and_the_last_copy_wins()
    {
        await db.ResetAsync();

        var result = await RecordAsync(Monday, ListingMapperTests.Sample(), ListingMapperTests.Sample(title: "Again"));
        var found = await FindAsync(new ListingId("seek", "1"));

        Assert.Equal(new ObservationResult(New: 1, AlreadyKnown: 0), result);
        Assert.Equal("Again", found?.Listing.Title);
    }

    [DatabaseFact]
    public async Task Same_source_id_on_different_sources_are_different_listings()
    {
        await db.ResetAsync();
        var seek = ListingMapperTests.Sample();
        var adzuna = seek with { Id = new ListingId("adzuna", "1") };

        var result = await RecordAsync(Monday, seek, adzuna);

        Assert.Equal(2, result.New);
    }

    [DatabaseFact]
    public async Task Unknown_listing_is_null()
    {
        await db.ResetAsync();

        Assert.Null(await FindAsync(new ListingId("seek", "404")));
    }
}
