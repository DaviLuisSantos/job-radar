using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Tests.Databases;
using JobRadar.Infrastructure.Tests.Persistence;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class ListingTermRepositoryContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SeedAsync(DateTimeOffset at, params (string Id, DateTimeOffset? Posted)[] listings)
    {
        await using var ctx = db.NewContext();
        await new EfListingRepository(ctx).RecordObservationsAsync(
            listings.Select(l => new SourcedListing(ListingMapperTests.Sample(l.Id) with { PostedAt = l.Posted }, $$"""{"id":"{{l.Id}}"}""")).ToList(),
            "search @ perth", at, Ct);
    }

    private async Task<int> RecordAsync(string term, params string[] ids)
    {
        await using var ctx = db.NewContext();
        return await new EfListingTermRepository(ctx).RecordAsync("seek", term, ids, Monday, Ct);
    }

    private async Task<IReadOnlyDictionary<ListingId, IReadOnlyList<string>>> TermsAsync(params string[] ids)
    {
        await using var ctx = db.NewContext();
        return await new EfListingTermRepository(ctx).GetTermsAsync(ids.Select(i => new ListingId("seek", i)).ToList(), Ct);
    }

    [DatabaseFact]
    public async Task Only_collected_listings_are_marked_and_only_once()
    {
        await db.ResetAsync();
        await SeedAsync(Monday, ("1", null), ("2", null));

        // "999" is a Seek ad this system never collected.
        Assert.Equal(2, await RecordAsync("australian citizen", "1", "2", "999"));
        Assert.Equal(0, await RecordAsync("australian citizen", "1", "2"));
        Assert.Equal(1, await RecordAsync("working holiday", "1"));

        var terms = await TermsAsync("1", "2", "999");
        Assert.Equal(["australian citizen", "working holiday"], terms[new ListingId("seek", "1")]);
        Assert.Equal(["australian citizen"], terms[new ListingId("seek", "2")]);
        Assert.False(terms.ContainsKey(new ListingId("seek", "999")));
    }

    [DatabaseFact]
    public async Task Oldest_posting_among_listings_first_seen_after_a_moment()
    {
        await db.ResetAsync();
        await SeedAsync(Monday, ("old", Monday.AddDays(-25)));
        await SeedAsync(Monday.AddDays(2), ("a", Monday.AddDays(-12)), ("b", Monday.AddDays(-3)), ("undated", null));

        await using var ctx = db.NewContext();
        var repo = new EfListingRepository(ctx);

        Assert.Equal(Monday.AddDays(-12), await repo.GetOldestPostedAmongFirstSeenSinceAsync("seek", Monday.AddDays(1), Ct));
        Assert.Null(await repo.GetOldestPostedAmongFirstSeenSinceAsync("seek", Monday.AddDays(5), Ct));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresListingTermRepositoryContractTests(PostgresFixture db) : ListingTermRepositoryContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerListingTermRepositoryContractTests(SqlServerFixture db) : ListingTermRepositoryContractTests(db);
