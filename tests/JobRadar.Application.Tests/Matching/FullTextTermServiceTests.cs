using JobRadar.Application.Matching;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Matching;

public class FullTextTermServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ScoringPolicy TwoTerms = ScoringPolicy.Default with
    {
        FullTextGroups = new Dictionary<string, IReadOnlyList<string>>
        {
            [FullTextGroup.Citizen] = ["australian citizen"],
            [FullTextGroup.Whv] = ["working holiday"],
        },
    };

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryTerms _terms = new();
    private readonly InMemoryRuns _runs = new();

    private FullTextTermService Service(FakeFullTextSearch search, ScoringPolicy? policy = null) =>
        new([search], policy ?? TwoTerms, _listings, _terms, _runs, _time, NullLogger<FullTextTermService>.Instance);

    [Fact]
    public async Task Every_policy_term_is_searched_once_and_logged_as_a_run()
    {
        var seek = new FakeFullTextSearch("seek");

        await Service(seek).TagAsync(Ct);

        Assert.Equal(["australian citizen", "working holiday"], seek.Received.Select(r => r.Term));
        Assert.Equal(["term: australian citizen", "term: working holiday"], _runs.Runs.Select(r => r.SearchKey));
    }

    [Fact]
    public async Task The_default_policy_has_every_prototype_term_once()
    {
        var seek = new FakeFullTextSearch("seek");

        await Service(seek, ScoringPolicy.Default).TagAsync(Ct);

        Assert.Equal(17, seek.Received.Count);   // 6 exclude + 2 citizen + 2 ambiguous + 3 whv + 1 + 2 + 1
        Assert.Contains(seek.Received, r => r.Term == "12 months + working rights");
    }

    [Fact]
    public async Task Marks_are_counted_as_new_only_the_first_time()
    {
        var seek = new FakeFullTextSearch("seek") { Respond = _ => new FullTextResult(new HashSet<string> { "1", "2" }, true) };
        var service = Service(seek);

        await service.TagAsync(Ct);
        _time.Advance(TimeSpan.FromDays(1));
        await service.TagAsync(Ct);

        Assert.Equal([2, 2, 0, 0], _runs.Runs.Select(r => r.New));
        Assert.Equal(4, _terms.Marks.Count);
    }

    [Fact]
    public async Task A_failed_term_is_recorded_and_the_next_term_still_runs()
    {
        var seek = new FakeFullTextSearch("seek")
        {
            Respond = t => t.Text == "australian citizen"
                ? throw new HttpRequestException("429")
                : new FullTextResult(new HashSet<string> { "1" }, true),
        };

        await Service(seek).TagAsync(Ct);

        Assert.Equal("HttpRequestException: 429", _runs.Runs[0].Error);
        Assert.True(_runs.Runs[1].Succeeded);
    }

    [Fact]
    public async Task The_window_is_the_maximum_first_then_days_since_the_last_success()
    {
        var seek = new FakeFullTextSearch("seek");
        var service = Service(seek);

        await service.TagAsync(Ct);
        _time.Advance(TimeSpan.FromDays(3));
        await service.TagAsync(Ct);

        Assert.Equal([31, 31, 5, 5], seek.Received.Select(r => r.Days));
    }

    [Fact]
    public async Task The_window_widens_to_cover_older_ads_collected_after_the_last_search()
    {
        var seek = new FakeFullTextSearch("seek");
        var service = Service(seek);
        await service.TagAsync(Ct);
        _time.Advance(TimeSpan.FromDays(1));
        // A new profile brings in an ad posted 20 days ago.
        await _listings.RecordObservationsAsync(
            [Listings.Of("seek", "1", posted: _time.GetUtcNow().AddDays(-20))], "new @ search", _time.GetUtcNow(), Ct);

        await service.TagAsync(Ct);

        Assert.Equal(22, seek.Received[^1].Days);
    }

    [Theory]
    [InlineData(null, null, 0, 31)]
    [InlineData(1, null, 0, 3)]
    [InlineData(0, null, 0, 2)]
    [InlineData(100, null, 0, 31)]
    [InlineData(1, 40, 0, 31)]
    public void Window_rules(int? lastDaysAgo, int? oldestDaysAgo, int _, int expected)
    {
        var now = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, FullTextTermService.Window(
            lastDaysAgo is { } l ? now.AddDays(-l) : null,
            oldestDaysAgo is { } o ? now.AddDays(-o) : null,
            now));
    }
}
