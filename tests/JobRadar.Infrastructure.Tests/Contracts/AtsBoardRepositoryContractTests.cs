using JobRadar.Domain.Collection;
using JobRadar.Infrastructure.Persistence.Collection;
using JobRadar.Infrastructure.Tests.Databases;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class AtsBoardRepositoryContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static readonly DateTimeOffset LongAgo = Monday.AddDays(-90);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AtsBoard Board(string slug, string platform = "greenhouse") => new(platform, slug);

    private async Task<int> AddAsync(params AtsBoard[] boards)
    {
        await using var ctx = db.NewContext();
        return await new EfAtsBoardRepository(ctx).AddMissingAsync(boards, Ct);
    }

    private async Task SweepAsync(params AtsBoardSweepOutcome[] outcomes)
    {
        await using var ctx = db.NewContext();
        await new EfAtsBoardRepository(ctx).RecordSweepAsync(outcomes, Ct);
    }

    private async Task<IReadOnlyList<AtsBoard>> ToSweepAsync(int limit = 100, int maxFailures = 3, string platform = "greenhouse")
    {
        await using var ctx = db.NewContext();
        return await new EfAtsBoardRepository(ctx).ListToSweepAsync(platform, maxFailures, Monday.AddDays(-30), limit, Ct);
    }

    private async Task<AtsBoardEntity> RowAsync(string slug, string platform = "greenhouse")
    {
        await using var ctx = db.NewContext();
        return await ctx.AtsBoards.SingleAsync(b => b.Platform == platform && b.Slug == slug, Ct);
    }

    private static AtsBoardSweepOutcome Ok(string slug, DateTimeOffset at, int total = 10, int kept = 0, string? etag = "W/\"a\"") =>
        new("greenhouse", slug, 200, at, etag, total, kept);

    private static AtsBoardSweepOutcome Status(string slug, DateTimeOffset at, int? status) => new("greenhouse", slug, status, at);

    [DatabaseFact]
    public async Task Seeding_twice_adds_each_board_once()
    {
        await db.ResetAsync();

        Assert.Equal(2, await AddAsync(Board("stripe"), Board("figma"), Board("stripe")));
        Assert.Equal(0, await AddAsync(Board("stripe"), Board("figma")));

        Assert.Equal(2, (await ToSweepAsync()).Count);
    }

    [DatabaseFact]
    public async Task A_slug_is_the_same_board_only_on_the_same_platform()
    {
        await db.ResetAsync();

        Assert.Equal(2, await AddAsync(Board("acme", "greenhouse"), Board("acme", "lever")));

        Assert.Single(await ToSweepAsync(platform: "greenhouse"));
        Assert.Single(await ToSweepAsync(platform: "lever"));
    }

    [DatabaseFact]
    public async Task Slugs_that_differ_only_in_case_are_two_boards()
    {
        await db.ResetAsync();

        Assert.Equal(2, await AddAsync(Board("Acme"), Board("acme")));
    }

    [DatabaseFact]
    public async Task Never_swept_boards_come_first_then_the_longest_ago()
    {
        await db.ResetAsync();
        await AddAsync(Board("old"), Board("recent"), Board("new"));
        await SweepAsync(Ok("old", Monday.AddDays(-5), kept: 1), Ok("recent", Monday.AddDays(-1), kept: 1));

        var order = (await ToSweepAsync()).Select(b => b.Slug).ToList();

        Assert.Equal(["new", "old", "recent"], order);
    }

    [DatabaseFact]
    public async Task The_limit_cuts_the_list_after_the_ordering()
    {
        await db.ResetAsync();
        await AddAsync(Board("a"), Board("b"), Board("c"));
        await SweepAsync(Ok("a", Monday.AddDays(-3), kept: 1), Ok("b", Monday.AddDays(-2), kept: 1));

        var order = (await ToSweepAsync(limit: 2)).Select(b => b.Slug).ToList();

        Assert.Equal(["c", "a"], order);
    }

    [DatabaseFact]
    public async Task A_board_without_listings_in_our_markets_waits_until_it_is_idle_for_long_enough()
    {
        await db.ResetAsync();
        await AddAsync(Board("recent-miss"), Board("old-miss"), Board("recent-hit"));
        await SweepAsync(
            Ok("recent-miss", Monday.AddDays(-2), kept: 0),
            Ok("old-miss", LongAgo, kept: 0),
            Ok("recent-hit", Monday.AddDays(-2), kept: 3));

        var slugs = (await ToSweepAsync()).Select(b => b.Slug).ToHashSet();

        Assert.Equal(["old-miss", "recent-hit"], slugs.Order().ToList());
    }

    [DatabaseFact]
    public async Task A_board_found_gone_too_many_times_in_a_row_leaves_the_sweep()
    {
        await db.ResetAsync();
        await AddAsync(Board("dead"), Board("alive"));

        for (var day = 1; day <= 3; day++)
            await SweepAsync(Status("dead", Monday.AddDays(day), 404));

        var row = await RowAsync("dead");
        Assert.Equal(3, row.ConsecutiveFailures);
        Assert.Equal(404, row.LastStatus);
        Assert.Equal(["alive"], (await ToSweepAsync(maxFailures: 3)).Select(b => b.Slug).ToList());
    }

    [DatabaseFact]
    public async Task A_board_that_answers_again_starts_its_failures_over()
    {
        await db.ResetAsync();
        await AddAsync(Board("flaky"));
        await SweepAsync(Status("flaky", Monday, 404));
        await SweepAsync(Status("flaky", Monday.AddDays(1), 404));

        await SweepAsync(Ok("flaky", Monday.AddDays(2)));

        Assert.Equal(0, (await RowAsync("flaky")).ConsecutiveFailures);
    }

    [DatabaseFact]
    public async Task A_server_error_or_no_answer_does_not_retire_the_board_nor_move_its_turn()
    {
        await db.ResetAsync();
        await AddAsync(Board("down"));
        await SweepAsync(Ok("down", Monday, kept: 1));

        await SweepAsync(Status("down", Monday.AddDays(1), 503));
        await SweepAsync(Status("down", Monday.AddDays(2), null));

        var row = await RowAsync("down");
        Assert.Equal(0, row.ConsecutiveFailures);
        Assert.Equal(Monday.ToUniversalTime(), row.LastSweptAt);
        Assert.Null(row.LastStatus);
    }

    [DatabaseFact]
    public async Task The_etag_and_the_counts_survive_a_restart_and_a_304_keeps_them()
    {
        await db.ResetAsync();
        await AddAsync(Board("stripe"));
        await SweepAsync(Ok("stripe", Monday, total: 746, kept: 12, etag: "W/\"3f6d\""));

        await SweepAsync(Status("stripe", Monday.AddDays(1), 304));

        var board = Assert.Single(await ToSweepAsync());
        Assert.Equal("W/\"3f6d\"", board.ETag);
        Assert.Equal(746, board.JobsTotal);
        Assert.Equal(12, board.JobsKept);
        Assert.Equal(304, board.LastStatus);
        Assert.Equal(Monday.AddDays(1).ToUniversalTime(), board.LastSweptAt);
        Assert.Equal(Monday.AddDays(1).ToUniversalTime(), board.LastHitAt);
    }

    [DatabaseFact]
    public async Task An_outcome_for_an_unknown_board_is_ignored()
    {
        await db.ResetAsync();
        await AddAsync(Board("known"));

        await SweepAsync(Ok("ghost", Monday, kept: 1));

        await using var ctx = db.NewContext();
        Assert.Equal(["known"], await ctx.AtsBoards.Select(b => b.Slug).ToListAsync(Ct));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresAtsBoardRepositoryContractTests(PostgresFixture db) : AtsBoardRepositoryContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerAtsBoardRepositoryContractTests(SqlServerFixture db) : AtsBoardRepositoryContractTests(db);
