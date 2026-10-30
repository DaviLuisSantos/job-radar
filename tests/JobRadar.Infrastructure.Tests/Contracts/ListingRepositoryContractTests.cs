using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Tests.Databases;
using JobRadar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class ListingRepositoryContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SourcedListing Sourced(Listing listing, string? raw = null) =>
        new(listing, raw ?? $$"""{"id":"{{listing.Id.SourceId}}","title":"{{listing.Title}}"}""");

    private Task<ObservationResult> RecordAsync(DateTimeOffset at, params Listing[] listings) =>
        RecordAsync(at, listings.Select(l => Sourced(l)).ToArray());

    private const string Search = ".net developer @ perth";

    private Task<ObservationResult> RecordAsync(DateTimeOffset at, params SourcedListing[] listings) =>
        RecordAsync(at, Search, listings);

    private async Task<ObservationResult> RecordAsync(DateTimeOffset at, string searchKey, params SourcedListing[] listings)
    {
        await using var ctx = db.NewContext();
        return await new EfListingRepository(ctx).RecordObservationsAsync(listings, searchKey, at, Ct);
    }

    private async Task<IReadOnlyList<Listing>> FoundByAsync(params string[] searchKeys)
    {
        await using var ctx = db.NewContext();
        return await new EfListingRepository(ctx).ListFoundByAsync(searchKeys, Ct);
    }

    private async Task<ListingRecord?> FindAsync(ListingId id)
    {
        await using var ctx = db.NewContext();
        return await new EfListingRepository(ctx).FindAsync(id, Ct);
    }

    private async Task<List<ListingPayloadEntity>> PayloadsAsync()
    {
        await using var ctx = db.NewContext();
        return await ctx.ListingPayloads.OrderBy(p => p.FirstSeenAt).ToListAsync(Ct);
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

    [DatabaseFact]
    public async Task An_unchanged_payload_is_stored_once_however_often_it_is_seen()
    {
        await db.ResetAsync();
        var listing = Sourced(ListingMapperTests.Sample(), """{"id":"1","teaser":"same"}""");

        await RecordAsync(Monday, listing);
        await RecordAsync(Monday.AddDays(1), listing);
        await RecordAsync(Monday.AddDays(2), listing);

        var payload = Assert.Single(await PayloadsAsync());
        Assert.Equal(Monday, payload.FirstSeenAt);
        Assert.Equal(ListingPayloadEntity.Hash("""{"id":"1","teaser":"same"}"""), payload.PayloadHash);
    }

    [DatabaseFact]
    public async Task An_edited_ad_adds_a_payload_version_and_keeps_the_old_one()
    {
        await db.ResetAsync();
        var listing = ListingMapperTests.Sample();

        await RecordAsync(Monday, Sourced(listing, """{"id":"1","teaser":"v1"}"""));
        await RecordAsync(Monday.AddDays(1), Sourced(listing, """{"id":"1","teaser":"v2"}"""));

        var payloads = await PayloadsAsync();
        Assert.Equal(2, payloads.Count);
        Assert.Contains("v1", payloads[0].Payload);
        Assert.Contains("v2", payloads[1].Payload);
    }

    [DatabaseFact]
    public async Task A_failed_save_leaves_the_context_usable()
    {
        await db.ResetAsync();
        await using var ctx = db.NewContext();
        var repo = new EfListingRepository(ctx);
        // Not valid JSON: Postgres rejects it for the jsonb column.
        var broken = Sourced(ListingMapperTests.Sample(), "not json");

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => repo.RecordObservationsAsync([broken], Search, Monday, Ct));
        var result = await repo.RecordObservationsAsync([Sourced(ListingMapperTests.Sample("2"))], Search, Monday, Ct);

        Assert.Equal(1, result.New);
        Assert.Null(await FindAsync(new ListingId("seek", "1")));
    }

    [DatabaseFact]
    public async Task Each_search_remembers_the_listings_it_found()
    {
        await db.ResetAsync();
        var shared = ListingMapperTests.Sample("1");
        await RecordAsync(Monday, ".net developer @ perth", Sourced(shared), Sourced(ListingMapperTests.Sample("2")));
        await RecordAsync(Monday, "c# developer @ perth", Sourced(shared));
        await RecordAsync(Monday, "hospitality @ perth", Sourced(ListingMapperTests.Sample("3")));
        // Seen again by the same search: no duplicate hit.
        await RecordAsync(Monday.AddDays(1), "c# developer @ perth", Sourced(shared));

        Assert.Equal(["1"], (await FoundByAsync("c# developer @ perth")).Select(l => l.Id.SourceId));
        Assert.Equal(["1", "2"], (await FoundByAsync(".net developer @ perth", "c# developer @ perth"))
            .Select(l => l.Id.SourceId).Order());
        Assert.Empty(await FoundByAsync("unknown @ nowhere"));
        Assert.Empty(await FoundByAsync());
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresListingRepositoryContractTests(PostgresFixture db) : ListingRepositoryContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerListingRepositoryContractTests(SqlServerFixture db) : ListingRepositoryContractTests(db);
