using System.Text.Json;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Postgres.Listings;
using JobRadar.Infrastructure.Postgres.Matching;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class ListingScoreRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SeedListingsAsync(params string[] ids)
    {
        await using var ctx = db.NewContext();
        await new PostgresListingRepository(ctx).RecordObservationsAsync(
            ids.Select(id => new SourcedListing(ListingMapperTests.Sample(id), $$"""{"id":"{{id}}"}""")).ToList(),
            "search @ perth", Monday, Ct);
    }

    private static ListingScore Score(string id, string profile, int final, string hash = "aaaa", params ScoreItem[] items) =>
        new(new ListingId("seek", id), profile, new FitScore(final, items), hash, Monday);

    private async Task SaveAsync(params ListingScore[] scores)
    {
        await using var ctx = db.NewContext();
        await new PostgresListingScoreRepository(ctx).SaveAsync(scores, Ct);
    }

    private async Task<List<ListingScoreEntity>> RowsAsync()
    {
        await using var ctx = db.NewContext();
        return await ctx.ListingScores.OrderBy(r => r.SourceId).ThenBy(r => r.Profile).ToListAsync(Ct);
    }

    [DatabaseFact]
    public async Task One_row_per_listing_and_profile()
    {
        await db.ResetAsync();
        await SeedListingsAsync("1", "2");

        await SaveAsync(Score("1", "tech", 66), Score("1", "fifo", 30), Score("2", "tech", 50));

        Assert.Equal([("1", "fifo", 30), ("1", "tech", 66), ("2", "tech", 50)],
            (await RowsAsync()).Select(r => (r.SourceId, r.Profile, r.Score)));
    }

    [DatabaseFact]
    public async Task Saving_again_replaces_the_score_and_its_rules_hash()
    {
        await db.ResetAsync();
        await SeedListingsAsync("1");
        await SaveAsync(Score("1", "tech", 66, hash: "old0"));

        await SaveAsync(Score("1", "tech", 61, hash: "new0") with { ScoredAt = Monday.AddDays(1) });

        var row = Assert.Single(await RowsAsync());
        Assert.Equal((61, "new0", Monday.AddDays(1)), (row.Score, row.RulesHash, row.ScoredAt));
    }

    [DatabaseFact]
    public async Task Scores_saved_before_the_pass_are_deleted_and_the_pass_own_are_kept()
    {
        await db.ResetAsync();
        await SeedListingsAsync("1", "2");
        // Sub-microsecond ticks: Postgres stores microseconds, and a score of this very pass must survive.
        var pass = Monday.AddDays(1).AddTicks(7);
        await SaveAsync(Score("1", "tech", 66), Score("2", "removed", 40));
        await SaveAsync(Score("2", "tech", 50) with { ScoredAt = pass });

        await using var ctx = db.NewContext();
        var deleted = await new PostgresListingScoreRepository(ctx).DeleteScoredBeforeAsync(pass, Ct);

        Assert.Equal(2, deleted);
        Assert.Equal([("2", "tech")], (await RowsAsync()).Select(r => (r.SourceId, r.Profile)));
    }

    [DatabaseFact]
    public async Task The_explanation_is_stored_as_json_with_rule_names()
    {
        await db.ResetAsync();
        await SeedListingsAsync("1");

        await SaveAsync(Score("1", "tech", 66, "aaaa", new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 16)));

        using var json = JsonDocument.Parse(Assert.Single(await RowsAsync()).Explanation);
        var item = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("PositiveSignal", item.GetProperty("rule").GetString());
        Assert.Equal("Title", item.GetProperty("where").GetString());
        Assert.Equal(16, item.GetProperty("points").GetInt32());
    }

    [DatabaseFact]
    public async Task Large_passes_are_saved_in_chunks()
    {
        await db.ResetAsync();
        var ids = Enumerable.Range(1, 2500).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        await SeedListingsAsync(ids);

        await SaveAsync(ids.Select(id => Score(id, "tech", 50)).ToArray());

        Assert.Equal(2500, (await RowsAsync()).Count);
    }
}
