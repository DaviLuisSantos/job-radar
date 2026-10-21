using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Infrastructure.Postgres.Market;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class MarketRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateOnly D1 = new(2026, 10, 1), D2 = new(2026, 10, 2), D3 = new(2026, 10, 3);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MarketSnapshot Snap(DateOnly day, SnapshotStatus status = SnapshotStatus.Ok) =>
        new("seek", day, status, 2, 2, 1, status == SnapshotStatus.Ok ? [] : ["listagem incompleta: HVAC-R / Perth WA"]);

    private static MarketListing Ad(string id, string salary = "$40 - $45 per hour") =>
        new("HVAC-R", "Perth WA", Postgres.ListingMapperTests.Sample(id) with { SalaryText = salary });

    private async Task SaveAsync(MarketSnapshot s, MarketCount[] counts, params MarketListing[] listings)
    {
        await using var ctx = db.NewContext();
        await new PostgresMarketRepository(ctx).SaveSnapshotAsync(s, counts, listings, Ct);
    }

    private async Task<T> Read<T>(Func<PostgresMarketRepository, Task<T>> read)
    {
        await using var ctx = db.NewContext();
        return await read(new PostgresMarketRepository(ctx));
    }

    [DatabaseFact]
    public async Task Only_good_snapshots_feed_the_counts_and_the_last_good_totals()
    {
        await db.ResetAsync();
        await SaveAsync(Snap(D1), [new("HVAC-R", "Perth WA", null, 100), new("HVAC-R", "Perth WA", "482", 5)]);
        await SaveAsync(Snap(D2, SnapshotStatus.Partial), [new("HVAC-R", "Perth WA", null, 999)]);

        var counts = await Read(r => r.GetGoodCountsAsync("seek", D1, D3, Ct));
        Assert.Equal([(D1, (string?)null, 100), (D1, "482", 5)], counts.OrderBy(c => c.Term).Select(c => (c.Date, c.Term, c.Total)));

        var last = await Read(r => r.GetLastGoodTotalsAsync("seek", D3, Ct));
        Assert.Equal(100, last[("HVAC-R", "Perth WA")]);
        Assert.Empty(await Read(r => r.GetLastGoodTotalsAsync("seek", D1, Ct)));
    }

    [DatabaseFact]
    public async Task Taking_the_snapshot_again_the_same_day_replaces_it()
    {
        await db.ResetAsync();
        await SaveAsync(Snap(D1, SnapshotStatus.Partial), [new("HVAC-R", "Perth WA", null, 100)]);
        await SaveAsync(Snap(D1), [new("HVAC-R", "Perth WA", null, 120)]);

        var snapshot = Assert.Single(await Read(r => r.GetSnapshotsAsync("seek", D1, D1, Ct)));
        Assert.Equal(SnapshotStatus.Ok, snapshot.Status);
        Assert.Equal(120, Assert.Single(await Read(r => r.GetGoodCountsAsync("seek", D1, D1, Ct))).Total);
    }

    [DatabaseFact]
    public async Task Listings_keep_first_seen_move_last_seen_and_store_the_annual_salary()
    {
        await db.ResetAsync();
        await SaveAsync(Snap(D1), [], Ad("1"));
        await SaveAsync(Snap(D3), [], Ad("1", "$100k - $120k"), Ad("2"));

        var seen = (await Read(r => r.GetListingsSeenAsync("seek", D1, D3, Ct))).OrderBy(s => s.Title).ToList();
        var one = seen.Single(s => s.FirstSeen == D1);
        Assert.Equal((D1, D3, 100_000, 120_000), (one.FirstSeen, one.LastSeen, one.AnnualMin, one.AnnualMax));
        Assert.Equal(2, seen.Count);

        // Seen only on the 1st: open in a window that ends the 1st, not in one starting the 2nd.
        Assert.Single(await Read(r => r.GetListingsSeenAsync("seek", D1, D1, Ct)));
    }

    [DatabaseFact]
    public async Task Issues_round_trip()
    {
        await db.ResetAsync();
        await SaveAsync(Snap(D2, SnapshotStatus.Suspect) with { Issues = ["zerou: X", "variação brusca: Y"] }, []);

        Assert.Equal(["zerou: X", "variação brusca: Y"], Assert.Single(await Read(r => r.GetSnapshotsAsync("seek", D1, D3, Ct))).Issues);
    }
}
