using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Worker.Tests;

/// <summary>
/// Like <see cref="LiveRemoteTests"/>, for the Greenhouse boards: a sweep of a few real boards through the real
/// composition (storage replaced by memory), a search over what it found, the ad of one job, and a second sweep that
/// must cost no body. Notices when Greenhouse changes under us.
/// </summary>
public class LiveAtsTests
{
    // Boards with Australian and Brazilian jobs on 2026-10-30, and one that does not exist.
    private static readonly string[] Slugs = ["gitlab", "vtex", "stripe", "this-board-does-not-exist-jobradar"];

    [LiveFact]
    public async Task Real_boards_are_swept_searched_and_read_without_a_body_the_second_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var boards = new InMemoryBoards(Slugs);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused" });
        builder.Services.AddJobRadar(builder.Configuration);
        builder.Services.AddSingleton<IAtsBoardRepository>(boards);
        builder.Services.AddSingleton<ICollectionRunRepository>(new InMemoryRuns());
        using var host = builder.Build();
        await host.StartAsync(ct);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetServices<IBoardSweep>().Single(s => s.Source == "greenhouse").SweepAsync(ignoreETags: false, ct);

        Assert.Equal(200, boards.LastStatus["gitlab"]);
        Assert.Equal(200, boards.LastStatus["vtex"]);
        Assert.Equal(404, boards.LastStatus[Slugs[3]]);

        var source = host.Services.GetServices<IJobSource>().Single(s => s.Name == "greenhouse");
        var australia = await source.SearchAsync(new ListingSearch("engineer", "Sydney", Market: JobMarket.AU), ct);
        var brazil = await source.SearchAsync(new ListingSearch("", "São Paulo", Market: JobMarket.BR), ct);

        Assert.True(australia.Complete);
        Assert.NotEmpty(australia.Listings);
        Assert.All(australia.Listings, l =>
        {
            Assert.Equal(Country.AU, l.Listing.Location.Country);
            Assert.StartsWith("greenhouse:", l.Listing.Id.ToString());
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Title));
            Assert.StartsWith("{", l.RawPayload);
        });
        Assert.NotEmpty(brazil.Listings);
        Assert.All(brazil.Listings, l => Assert.Equal(Country.BR, l.Listing.Location.Country));
        // The accents of Brazilian ads survive the trip.
        Assert.DoesNotContain(brazil.Listings, l => l.Listing.Title.Contains('�'));

        var first = australia.Listings[0].Listing;
        var text = await host.Services.GetServices<IDescriptionSource>().Single(s => s.Source == "greenhouse")
            .FetchAsync(first.Id.SourceId, ct);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("&lt;", text);
        Assert.DoesNotContain("<p>", text);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetServices<IBoardSweep>().Single(s => s.Source == "greenhouse").SweepAsync(ignoreETags: false, ct);

        Assert.Equal(304, boards.LastStatus["gitlab"]);
        Assert.Equal(304, boards.LastStatus["vtex"]);

        await host.StopAsync(ct);
    }

    private sealed class InMemoryBoards(string[] slugs) : IAtsBoardRepository
    {
        private readonly Dictionary<string, AtsBoard> _boards = slugs.ToDictionary(s => s, s => new AtsBoard("greenhouse", s));

        public Dictionary<string, int?> LastStatus { get; } = [];

        public Task<IReadOnlyList<AtsBoard>> ListToSweepAsync(string platform, int maxFailures, DateTimeOffset idleBefore, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AtsBoard>>(_boards.Values.ToList());

        public Task<int> AddMissingAsync(IReadOnlyCollection<AtsBoard> boards, CancellationToken ct = default) => throw new NotSupportedException();

        public Task RecordSweepAsync(IReadOnlyCollection<AtsBoardSweepOutcome> outcomes, CancellationToken ct = default)
        {
            foreach (var o in outcomes)
            {
                LastStatus[o.Slug] = o.Status;
                if (o.Status == 200)
                    _boards[o.Slug] = _boards[o.Slug] with { ETag = o.ETag };
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRuns : ICollectionRunRepository
    {
        public Task RecordAsync(CollectionRun run, CancellationToken ct = default) => Task.CompletedTask;
        public Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CollectionRun>> ListStartedSinceAsync(DateTimeOffset since, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateTimeOffset?> GetLastCompleteStartAsync(string source, string searchKey, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
