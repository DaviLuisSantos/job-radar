using JobRadar.Application.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Collection;

public class ListingCollectorTests
{
    private static readonly ListingSearch Dev = new(".net developer", "Perth");
    private static readonly ListingSearch Hospo = new("hospitality", "Perth");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryRuns _runs = new();

    private ListingCollector Collector(params FakeSource[] sources) =>
        new(sources, _listings, _runs, _time, NullLogger<ListingCollector>.Instance);

    [Fact]
    public async Task Every_search_runs_on_every_source_and_each_run_is_recorded()
    {
        var seek = new FakeSource("seek");
        var adzuna = new FakeSource("adzuna");

        var summary = await Collector(seek, adzuna).CollectAsync([Dev, Hospo], Ct);

        Assert.Equal(4, summary.Runs);
        Assert.Equal([Dev.Key, Hospo.Key], seek.Received.Select(s => s.Key));
        Assert.Equal([Dev.Key, Hospo.Key], adzuna.Received.Select(s => s.Key));
        Assert.Equal(4, _runs.Runs.Count);
    }

    [Fact]
    public async Task Counts_found_and_new_per_run()
    {
        var seek = new FakeSource("seek")
        {
            Respond = s => new SearchResult(
                s == Dev ? [Listings.Of("seek", "1"), Listings.Of("seek", "2")] : [Listings.Of("seek", "2"), Listings.Of("seek", "3")],
                Complete: true),
        };

        var summary = await Collector(seek).CollectAsync([Dev, Hospo], Ct);

        Assert.Equal([(2, 2), (2, 1)], _runs.Runs.Select(r => (r.Found, r.New)));
        Assert.Equal((4, 3), (summary.Found, summary.New));
    }

    [Fact]
    public async Task First_run_fetches_everything_and_later_runs_only_the_days_since_the_last_success()
    {
        var seek = new FakeSource("seek");
        var collector = Collector(seek);

        await collector.CollectAsync([Dev], Ct);
        _time.Advance(TimeSpan.FromDays(1));
        await collector.CollectAsync([Dev], Ct);

        Assert.Null(seek.Received[0].PostedWithinDays);
        Assert.Equal(2, seek.Received[1].PostedWithinDays);
        Assert.Equal(2, _runs.Runs[1].PostedWithinDays);
    }

    [Fact]
    public async Task A_failing_search_is_recorded_and_the_next_one_still_runs()
    {
        var seek = new FakeSource("seek")
        {
            Respond = s => s == Dev
                ? throw new HttpRequestException("503 Service Unavailable")
                : new SearchResult([Listings.Of("seek", "9")], Complete: true),
        };

        var summary = await Collector(seek).CollectAsync([Dev, Hospo], Ct);

        Assert.Equal(1, summary.Failed);
        Assert.Equal("HttpRequestException: 503 Service Unavailable", _runs.Runs[0].Error);
        Assert.True(_runs.Runs[1].Succeeded);
        Assert.Equal(1, summary.New);
    }

    [Fact]
    public async Task A_failed_run_does_not_move_the_incremental_window()
    {
        var calls = 0;
        var seek = new FakeSource("seek")
        {
            Respond = _ => ++calls == 2 ? throw new HttpRequestException("timeout") : new SearchResult([], true),
        };
        var collector = Collector(seek);

        await collector.CollectAsync([Dev], Ct);   // ok, day 0
        _time.Advance(TimeSpan.FromDays(1));
        await collector.CollectAsync([Dev], Ct);   // fails, day 1
        _time.Advance(TimeSpan.FromDays(1));
        await collector.CollectAsync([Dev], Ct);   // day 2: window still counts from day 0

        Assert.Equal(3, seek.Received[2].PostedWithinDays);
    }

    [Fact]
    public async Task An_incomplete_result_is_recorded_as_such()
    {
        var seek = new FakeSource("seek") { Respond = _ => new SearchResult([Listings.Of("seek", "1")], Complete: false) };

        var summary = await Collector(seek).CollectAsync([Dev], Ct);

        Assert.False(_runs.Runs[0].Complete);
        Assert.Equal(1, summary.Incomplete);
    }

    [Fact]
    public async Task Cancellation_stops_the_pass_instead_of_being_recorded_as_a_failure()
    {
        using var cts = new CancellationTokenSource();
        var seek = new FakeSource("seek")
        {
            Respond = _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Collector(seek).CollectAsync([Dev, Hospo], cts.Token));
        Assert.Empty(_runs.Runs);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(12, 1)]
    [InlineData(24, 2)]
    [InlineData(24 * 7 + 1, 8)]
    public void Window_is_days_since_last_success_plus_one(int hoursAgo, int expected)
    {
        var now = new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, ListingCollector.IncrementalWindow(now.AddHours(-hoursAgo), now));
    }
}
