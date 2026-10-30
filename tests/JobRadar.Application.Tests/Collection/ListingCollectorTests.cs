using JobRadar.Application;
using JobRadar.Application.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    private readonly InMemoryDescriptions _descriptions = new();

    // One search at a time unless a test is about parallelism: order is then deterministic.
    private ListingCollector Collector(params FakeSource[] sources) => Collector(1, sources);

    private ListingCollector Collector(int parallel, params FakeSource[] sources) =>
        new(sources, _listings, _descriptions, _runs, Options.Create(new CollectionOptions { MaxParallelSearches = parallel }), _time,
            NullLogger<ListingCollector>.Instance);

    [Fact]
    public async Task Each_run_counts_what_it_found_new_and_known_and_its_failures()
    {
        // The meter is process-wide: the source name keeps other tests' measurements out.
        var source = $"metrics-{Guid.NewGuid():N}";
        var measured = new List<(string Name, long Value, string? Error)>();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == JobRadarTelemetry.Name)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var all = tags.ToArray();
            if (all.Any(t => t.Key == "source" && (string?)t.Value == source))
                lock (measured)
                    measured.Add((instrument.Name, value, all.FirstOrDefault(t => t.Key == "error").Value as string));
        });
        listener.Start();

        var fake = new FakeSource(source);
        fake.Respond = s => s == Hospo
            ? throw new HttpRequestException("down")
            : new SearchResult([Listings.Of(source, "1")], Complete: true);
        await Collector(fake).CollectAsync([Dev, Hospo], Ct);
        await Collector(fake).CollectAsync([Dev], Ct);

        Assert.Equal(2, measured.Where(m => m.Name == "jobradar.jobs.collected").Sum(m => m.Value));
        Assert.Equal(1, measured.Where(m => m.Name == "jobradar.jobs.new").Sum(m => m.Value));
        Assert.Equal(1, measured.Where(m => m.Name == "jobradar.jobs.known").Sum(m => m.Value));
        Assert.Equal([("jobradar.source.errors", 1L, "HttpRequestException")],
            measured.Where(m => m.Name == "jobradar.source.errors").ToList());
    }

    [Fact]
    public async Task A_search_runs_only_on_the_sources_of_its_country()
    {
        var seek = new FakeSource("seek");
        var gupy = new FakeSource("gupy", JobMarket.BR);
        var rio = new ListingSearch("desenvolvedor .net", "Rio de Janeiro", Market: JobMarket.BR);

        await Collector(seek, gupy).CollectAsync([Dev, rio], Ct);

        Assert.Equal([Dev.Key], seek.Received.Select(s => s.Key));
        Assert.Equal([rio.Key], gupy.Received.Select(s => s.Key));
        Assert.Equal("desenvolvedor .net @ rio de janeiro (br)", rio.Key);
    }

    [Fact]
    public async Task A_full_ad_the_search_returns_is_stored_once_and_replaced_when_it_changes()
    {
        var ad = new string('x', ListingDescription.MinLength) + " C# .NET";
        var gupy = new FakeSource("gupy", JobMarket.BR);
        var listing = new Listing(new ListingId("gupy", "1"), "Desenvolvedor .NET", null, ListingLocation.Unknown(Country.BR),
            WorkType.FullTime, null, null, new Uri("https://example.test/1"), null);
        gupy.Respond = _ => new SearchResult([new SourcedListing(listing, "{}", ad)], Complete: true);
        var rio = new ListingSearch("desenvolvedor .net", "Rio de Janeiro", Market: JobMarket.BR);

        await Collector(gupy).CollectAsync([rio], Ct);
        var first = _descriptions.Saved[listing.Id];
        _time.Advance(TimeSpan.FromDays(1));
        await Collector(gupy).CollectAsync([rio], Ct);

        Assert.Equal(DescriptionOrigin.Source, first.Origin);
        Assert.Same(first, _descriptions.Saved[listing.Id]);

        gupy.Respond = _ => new SearchResult([new SourcedListing(listing, "{}", ad + " Azure")], Complete: true);
        await Collector(gupy).CollectAsync([rio], Ct);
        Assert.EndsWith("Azure", _descriptions.Saved[listing.Id].Text);
    }

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

    [Fact]
    public async Task Searches_run_in_parallel_and_every_run_is_still_recorded()
    {
        var inFlight = 0;
        var peak = 0;
        var seek = new FakeSource("seek")
        {
            RespondAsync = async s =>
            {
                var now = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref peak, now);
                await Task.Delay(50);
                Interlocked.Decrement(ref inFlight);
                return new SearchResult([Listings.Of("seek", s.Keywords)], Complete: true);
            },
        };
        var searches = Enumerable.Range(1, 12).Select(i => new ListingSearch($"k{i}", "Perth")).ToList();

        var summary = await Collector(4, seek).CollectAsync(searches, Ct);

        Assert.Equal((12, 12), (summary.Runs, summary.New));
        Assert.Equal(12, _runs.Runs.Count);
        Assert.InRange(peak, 2, 4);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        for (var current = target; value > current; current = target)
            if (Interlocked.CompareExchange(ref target, value, current) == current)
                return;
    }

    [Fact]
    public async Task A_run_cut_at_the_cap_does_not_move_the_window()
    {
        var calls = 0;
        var seek = new FakeSource("seek") { Respond = _ => new SearchResult([], Complete: ++calls != 1) };
        var collector = Collector(seek);

        await collector.CollectAsync([Dev], Ct);   // day 0: cut at the cap
        _time.Advance(TimeSpan.FromDays(1));
        await collector.CollectAsync([Dev], Ct);   // day 1: must fetch everything again

        Assert.Null(seek.Received[1].PostedWithinDays);
    }

    [Fact]
    public async Task Full_history_ignores_the_windows()
    {
        var seek = new FakeSource("seek");
        var collector = Collector(seek);
        await collector.CollectAsync([Dev], Ct);
        _time.Advance(TimeSpan.FromDays(1));

        await collector.CollectAsync([Dev], fullHistory: true, Ct);

        Assert.Null(seek.Received[1].PostedWithinDays);
    }
}
