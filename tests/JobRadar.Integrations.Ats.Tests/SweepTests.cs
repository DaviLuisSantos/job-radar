using System.Net;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Ats.Tests;

internal sealed class FakeBoardRepository(params AtsBoard[] known) : IAtsBoardRepository
{
    public List<AtsBoardSweepOutcome> Recorded { get; } = [];
    public (int MaxFailures, DateTimeOffset IdleBefore, int Limit)? Asked { get; private set; }

    public Task<IReadOnlyList<AtsBoard>> ListToSweepAsync(string platform, int maxFailures, DateTimeOffset idleBefore, int limit, CancellationToken ct = default)
    {
        Asked = (maxFailures, idleBefore, limit);
        return Task.FromResult<IReadOnlyList<AtsBoard>>(known.Where(b => b.Platform == platform).Take(limit).ToList());
    }

    public Task<int> AddMissingAsync(IReadOnlyCollection<AtsBoard> boards, CancellationToken ct = default) => throw new NotSupportedException();

    public Task RecordSweepAsync(IReadOnlyCollection<AtsBoardSweepOutcome> outcomes, CancellationToken ct = default)
    {
        Recorded.AddRange(outcomes);
        return Task.CompletedTask;
    }
}

internal sealed class FakeRuns : ICollectionRunRepository
{
    public List<CollectionRun> Recorded { get; } = [];

    public Task RecordAsync(CollectionRun run, CancellationToken ct = default)
    {
        Recorded.Add(run);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CollectionRun>> ListStartedSinceAsync(DateTimeOffset since, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<DateTimeOffset?> GetLastCompleteStartAsync(string source, string searchKey, CancellationToken ct = default) => throw new NotSupportedException();
}

public class SweepTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static AtsBoard Board(string slug, string? etag = null) => new("greenhouse", slug, etag);

    private static (AtsBoardSweepService Service, AtsSnapshot Snapshot, FakeBoardRepository Boards, FakeRuns Runs, FakeBoards Api) Build(
        Func<HttpRequestMessage, HttpResponseMessage> answer, params AtsBoard[] known)
    {
        var api = new FakeBoards(answer);
        var snapshot = new AtsSnapshot();
        var boards = new FakeBoardRepository(known);
        var runs = new FakeRuns();
        var service = new AtsBoardSweepService(
            api.Client(), boards, runs, snapshot, Options.Create(new AtsOptions()), TimeProvider.System, NullLogger<AtsBoardSweepService>.Instance);
        return (service, snapshot, boards, runs, api);
    }

    private static HttpResponseMessage ByBoard(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/v1/boards/example/jobs" => FakeBoards.Json(FakeBoards.Fixture("greenhouse-board.json"), "W/\"v1\""),
        "/v1/boards/unchanged/jobs" => new HttpResponseMessage(HttpStatusCode.NotModified),
        "/v1/boards/dead/jobs" => new HttpResponseMessage(HttpStatusCode.NotFound),
        "/v1/boards/broken/jobs" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
        _ => throw new HttpRequestException("connection reset"),
    };

    private static async Task<IReadOnlyList<SourcedListing>> FoundAsync(AtsSnapshot snapshot, string keywords, JobMarket market)
    {
        var result = await new GreenhouseJobSource(snapshot).SearchAsync(new ListingSearch(keywords, "Perth", Market: market), Ct);
        return result.Listings;
    }

    [Fact]
    public async Task A_board_that_answers_leaves_its_jobs_in_our_markets_and_its_tag()
    {
        var (service, snapshot, boards, runs, _) = Build(ByBoard, Board("example"));

        await service.SweepAsync(ignoreETags: false, Ct);

        var outcome = Assert.Single(boards.Recorded);
        Assert.Equal((200, "W/\"v1\"", 10, 6), (outcome.Status, outcome.ETag, outcome.JobsTotal, outcome.JobsKept));
        Assert.Equal(6, snapshot.Current!.Jobs.Count);
        var run = Assert.Single(runs.Recorded);
        Assert.Equal("greenhouse", run.Source);
        Assert.Equal(AtsBoardSweepService.RunKey, run.SearchKey);
        Assert.Equal(6, run.Found);
        Assert.True(run.Complete);
        Assert.Null(run.Error);
    }

    [Fact]
    public async Task A_board_that_is_unchanged_or_gone_adds_no_jobs_and_a_gone_board_does_not_make_the_sweep_incomplete()
    {
        var (service, snapshot, boards, runs, _) = Build(ByBoard, Board("unchanged", "W/\"old\""), Board("dead"));

        await service.SweepAsync(ignoreETags: false, Ct);

        Assert.Equal([304, 404], boards.Recorded.Select(o => o.Status!.Value).Order().ToList());
        Assert.Empty(snapshot.Current!.Jobs);
        Assert.True(snapshot.Current.Complete);
        Assert.True(runs.Recorded.Single().Complete);
    }

    [Fact]
    public async Task A_server_error_or_a_dropped_connection_makes_the_sweep_incomplete_but_keeps_the_rest()
    {
        var (service, snapshot, boards, runs, _) = Build(ByBoard, Board("example"), Board("broken"), Board("unreachable"));

        await service.SweepAsync(ignoreETags: false, Ct);

        Assert.Equal(3, boards.Recorded.Count);
        Assert.Contains(boards.Recorded, o => o is { Slug: "broken", Status: 503 });
        Assert.Contains(boards.Recorded, o => o is { Slug: "unreachable", Status: null });
        Assert.Equal(6, snapshot.Current!.Jobs.Count);
        Assert.False(snapshot.Current.Complete);
        Assert.False(runs.Recorded.Single().Complete);
    }

    [Fact]
    public async Task The_tag_is_sent_unless_the_whole_board_is_asked_for()
    {
        var (service, _, _, _, api) = Build(ByBoard, Board("unchanged", "W/\"old\""));

        await service.SweepAsync(ignoreETags: false, Ct);
        await service.SweepAsync(ignoreETags: true, Ct);

        Assert.Equal(["W/\"old\"", null], api.Requests.Select(r => r.Headers.IfNoneMatch.SingleOrDefault()?.ToString()).ToList());
    }

    [Fact]
    public async Task The_slice_is_cut_by_the_options()
    {
        var (service, _, boards, _, _) = Build(ByBoard, Board("example"));

        await service.SweepAsync(ignoreETags: false, Ct);

        var asked = boards.Asked!.Value;
        var defaults = new AtsOptions();
        Assert.Equal(defaults.MaxBoardsPerSweep, asked.Limit);
        Assert.Equal(defaults.MaxFailures, asked.MaxFailures);
        Assert.True(asked.IdleBefore < DateTimeOffset.UtcNow.AddDays(-defaults.IdleRecheckDays + 1));
    }

    [Fact]
    public async Task A_search_before_any_sweep_says_it_is_incomplete_instead_of_finding_nothing()
    {
        var result = await new GreenhouseJobSource(new AtsSnapshot())
            .SearchAsync(new ListingSearch(".net", "Perth"), Ct);

        Assert.Empty(result.Listings);
        Assert.False(result.Complete);
    }

    [Fact]
    public async Task A_new_sweep_empties_the_snapshot_until_it_ends_so_a_failed_one_is_not_read_as_fresh()
    {
        var (service, snapshot, boards, _, _) = Build(ByBoard, Board("example"));
        await service.SweepAsync(ignoreETags: false, Ct);
        Assert.NotNull(snapshot.Current);

        boards.Recorded.Clear();
        var failing = new FakeBoardRepositoryThatFails();
        var broken = new AtsBoardSweepService(
            new FakeBoards(ByBoard).Client(), failing, new FakeRuns(), snapshot, Options.Create(new AtsOptions()),
            TimeProvider.System, NullLogger<AtsBoardSweepService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => broken.SweepAsync(ignoreETags: false, Ct));

        Assert.Null(snapshot.Current);
    }

    [Fact]
    public async Task A_search_takes_the_jobs_of_its_market_with_every_word_in_the_title_or_department()
    {
        var (service, snapshot, _, _, _) = Build(ByBoard, Board("example"));
        await service.SweepAsync(ignoreETags: false, Ct);

        var australia = await FoundAsync(snapshot, ".net developer", JobMarket.AU);
        var brazil = await FoundAsync(snapshot, ".net", JobMarket.BR);
        var latam = await FoundAsync(snapshot, "engineer", JobMarket.LatamRemote);
        var global = await FoundAsync(snapshot, "engineering", JobMarket.GlobalRemote);
        var department = await FoundAsync(snapshot, "engineering frontend", JobMarket.AU);

        Assert.Equal(["greenhouse:example/1001"], australia.Select(s => s.Listing.Id.ToString()));
        Assert.Equal(["greenhouse:example/1002"], brazil.Select(s => s.Listing.Id.ToString()));
        Assert.Equal(["greenhouse:example/1003", "greenhouse:example/1004"], latam.Select(s => s.Listing.Id.ToString()).Order());
        Assert.Empty(global);   // the one worldwide job is a Site Reliability Engineer in the Platform department
        Assert.Equal(["greenhouse:example/1010"], department.Select(s => s.Listing.Id.ToString()));
        Assert.All(australia.Concat(brazil), s => Assert.StartsWith("{", s.RawPayload));
        Assert.All(australia, s => Assert.Null(s.Description));
    }

    [Fact]
    public void The_source_is_opt_in_and_serves_all_four_markets()
    {
        var source = new GreenhouseJobSource(new AtsSnapshot());

        Assert.True(source.OptIn);
        Assert.Equal("greenhouse", source.Name);
        Assert.Equal(4, source.Markets.Count);
    }

    private sealed class FakeBoardRepositoryThatFails : IAtsBoardRepository
    {
        public Task<IReadOnlyList<AtsBoard>> ListToSweepAsync(string platform, int maxFailures, DateTimeOffset idleBefore, int limit, CancellationToken ct = default) =>
            throw new InvalidOperationException("database down");

        public Task<int> AddMissingAsync(IReadOnlyCollection<AtsBoard> boards, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecordSweepAsync(IReadOnlyCollection<AtsBoardSweepOutcome> outcomes, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
