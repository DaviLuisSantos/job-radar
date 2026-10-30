using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Tests.Databases;
using JobRadar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Contracts;

/// <summary>
/// What the two databases must agree on below the repositories: the schema the migrations build, and text that
/// differs only in case or accent. SQL Server's default collation ignores both; Postgres ignores neither.
/// </summary>
public abstract class DatabaseSemanticsContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task The_migrations_built_exactly_the_model()
    {
        await using var ctx = db.NewContext();

        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync(Ct));
        Assert.False(ctx.Database.HasPendingModelChanges());
    }

    [DatabaseFact]
    public async Task Keys_that_differ_only_in_case_or_accent_are_different_listings()
    {
        await db.ResetAsync();
        string[] ids = ["cafe", "Cafe", "café"];

        var result = await RecordAsync(ids.Select(id => ListingMapperTests.Sample(id, title: id)).ToArray());

        Assert.Equal(3, result.New);
        foreach (var id in ids)
            Assert.Equal(id, (await FindAsync(new ListingId("seek", id)))?.Listing.Title);
    }

    [DatabaseFact]
    public async Task A_batch_of_thousands_of_ids_is_one_query_on_both()
    {
        // Batches look listings up with ids.Contains(...): an array on Postgres, OPENJSON on SQL Server, where
        // a parameter per id would stop at 2100.
        await db.ResetAsync();
        var listings = Enumerable.Range(1, 3000).Select(i => ListingMapperTests.Sample(i.ToString())).ToArray();

        Assert.Equal(3000, (await RecordAsync(listings)).New);
        Assert.Equal(new ObservationResult(0, 3000), await RecordAsync(listings));
    }

    private async Task<ObservationResult> RecordAsync(Listing[] listings)
    {
        await using var ctx = db.NewContext();
        var sourced = listings.Select(l => new SourcedListing(l, $$"""{"id":"{{l.Id.SourceId}}"}""")).ToArray();
        return await new EfListingRepository(ctx).RecordObservationsAsync(sourced, "semantics @ perth", Monday, Ct);
    }

    private async Task<ListingRecord?> FindAsync(ListingId id)
    {
        await using var ctx = db.NewContext();
        return await new EfListingRepository(ctx).FindAsync(id, Ct);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresDatabaseSemanticsContractTests(PostgresFixture db) : DatabaseSemanticsContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerDatabaseSemanticsContractTests(SqlServerFixture db) : DatabaseSemanticsContractTests(db);
