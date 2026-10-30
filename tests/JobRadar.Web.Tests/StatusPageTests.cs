using Bunit;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Application.Operations;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Market;
using JobRadar.Web.Components.Pages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace JobRadar.Web.Tests;

public class StatusPageTests : BunitContext
{
    private static readonly DateTimeOffset Pass = new(2026, 10, 23, 22, 0, 0, TimeSpan.Zero);

    private static CollectionRun Run(DateTimeOffset at, string key, bool complete = true, string? error = null) =>
        new("seek", key, PostedWithinDays: 3, at, at.AddSeconds(30), Found: 12, New: 1, complete, error);

    private IRenderedComponent<StatusPage> RenderWith(params CollectionRun[] runs)
    {
        Services.AddSingleton<ICollectionRunRepository>(new Runs(runs));
        Services.AddSingleton<IMarketRepository>(new NoSnapshots());
        Services.AddSingleton(Options.Create(new CollectionOptions()));
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<StatusService>();
        Services.AddSingleton<Backend>();
        Services.AddSingleton<IWebHostEnvironment>(new Host(AppContext.BaseDirectory));
        return Render<StatusPage>();
    }

    [Fact]
    public void Lists_what_failed_or_was_cut_in_the_last_pass_only()
    {
        var cut = RenderWith(
            Run(Pass.AddDays(-1), "term: australian citizen", complete: false),   // the pass before: not listed
            Run(Pass, "fifo @ perth"),
            Run(Pass.AddMinutes(1), "term: australian citizen", complete: false),
            Run(Pass.AddMinutes(2), "labourer @ perth", error: "HttpRequestException: 503\n   at Polly"));

        cut.WaitForAssertion(() => Assert.Contains("3 execuções", cut.Markup, StringComparison.Ordinal));
        var rows = cut.FindAll("h2 + p + table tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("labourer @ perth", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Falhou", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("HttpRequestException: 503", rows[0].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Polly", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("term: australian citizen", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Incompleta", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clean_pass_says_so_and_the_build_is_shown()
    {
        var cut = RenderWith(Run(Pass, "fifo @ perth"));

        cut.WaitForAssertion(() => Assert.Contains("todas deram certo", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("este build", cut.Markup, StringComparison.Ordinal);
    }

    private sealed class Runs(IReadOnlyList<CollectionRun> runs) : ICollectionRunRepository
    {
        public Task RecordAsync(CollectionRun run, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default) =>
            Task.FromResult(runs.Select(r => (DateTimeOffset?)r.StartedAt).Max());

        public Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DailyRunSummary>>([]);

        public Task<IReadOnlyList<CollectionRun>> ListStartedSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionRun>>(runs.Where(r => r.StartedAt >= since).ToList());

        public Task<DateTimeOffset?> GetLastCompleteStartAsync(string source, string searchKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoSnapshots : IMarketRepository
    {
        public Task<IReadOnlyList<MarketSnapshot>> GetSnapshotsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketSnapshot>>([]);

        public Task SaveSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts,
            IReadOnlyCollection<MarketListing> listings, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> ImportSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> ImportListingHistoryAsync(string source, IReadOnlyCollection<ImportedMarketListing> listings, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<(string Category, string City), int>> GetLastGoodTotalsAsync(string source, DateOnly before, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DailyCount>> GetGoodCountsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SeenListing>> GetListingsSeenAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Host(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "JobRadar.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
    }
}
