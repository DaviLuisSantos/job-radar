using JobRadar.Application.Abstractions;
using JobRadar.Application.Market;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Market;

public class MarketSnapshotServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateOnly Today = new(2026, 10, 21);

    private sealed class FakeMarket(string source = "seek") : IMarketSource
    {
        public string Source => source;
        public List<(string Code, string City, string? Phrase)> Counted { get; } = [];
        public Func<string, string, FullTextTerm?, int> Count { get; set; } = (_, _, t) => t is null ? 100 : 10;
        public Func<string, string, SearchResult> List { get; set; } = (_, _) => new SearchResult([Listings.Of("seek", "1")], true);

        public Task<int> CountAsync(string categoryCode, string city, FullTextTerm? term, CancellationToken ct = default)
        {
            Counted.Add((categoryCode, city, term?.Text));
            return Task.FromResult(Count(categoryCode, city, term));
        }

        public Task<SearchResult> ListAsync(string categoryCode, string city, CancellationToken ct = default) =>
            Task.FromResult(List(categoryCode, city));
    }

    private sealed class InMemoryMarket : IMarketRepository
    {
        public List<(MarketSnapshot Snapshot, IReadOnlyCollection<MarketCount> Counts, IReadOnlyCollection<MarketListing> Listings)> Saved { get; } = [];
        public Dictionary<(string, string), int> LastGood { get; } = [];

        public Task SaveSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts, IReadOnlyCollection<MarketListing> listings, CancellationToken ct = default)
        {
            Saved.Add((snapshot, counts, listings));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<(string Category, string City), int>> GetLastGoodTotalsAsync(string source, DateOnly before, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(string Category, string City), int>>(LastGood);

        public Task<bool> ImportSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> ImportListingHistoryAsync(string source, IReadOnlyCollection<ImportedMarketListing> listings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DailyCount>> GetGoodCountsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeenListing>> GetListingsSeenAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MarketSnapshot>> GetSnapshotsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static MarketOptions Options()
    {
        var o = new MarketOptions
        {
            Categories = [new() { Name = "HVAC-R", Codes = new() { ["seek"] = "1313" } }, new() { Name = "Only elsewhere", Codes = new() { ["adzuna"] = "x" } }],
            FullCities = ["Perth WA"],
            CountOnlyCities = ["All Australia"],
            Terms = [new() { Name = "fifo", Phrase = "fifo" }, new() { Name = "night_shift", Phrase = "night shift" }],
            RetryPause = TimeSpan.Zero,
        };
        return o;
    }

    private readonly InMemoryMarket _repo = new();

    private MarketSnapshotService Service(FakeMarket source, MarketOptions? options = null) =>
        new([source], _repo, Microsoft.Extensions.Options.Options.Create(options ?? Options()), new FakeTimeProvider(), NullLogger<MarketSnapshotService>.Instance);

    [Fact]
    public void Defaults_are_the_prototype_categories_and_cities_and_no_terms()
    {
        var o = new MarketOptions();
        new MarketDefaults().PostConfigure(null, o);

        Assert.Equal(["HVAC-R", "Elétrica", "Desenvolvimento"], o.Categories.Select(c => c.Name));
        Assert.Equal("6287,6290", o.Categories[2].Codes["seek"]);
        Assert.Equal(["Perth WA"], o.FullCities);
        Assert.Equal(["All Australia"], o.CountOnlyCities);
        Assert.Empty(o.Terms);
    }

    [Fact]
    public void Configured_lists_replace_the_defaults()
    {
        var o = new MarketOptions { FullCities = ["Brisbane QLD"] };
        new MarketDefaults().PostConfigure(null, o);

        Assert.Equal(["Brisbane QLD"], o.FullCities);
    }

    [Fact]
    public async Task Counts_every_category_city_and_term_and_lists_the_full_cities()
    {
        var seek = new FakeMarket();

        var snapshot = Assert.Single(await Service(seek).TakeAsync(Today, Ct));

        // 1 category with a Seek code × 2 cities × (total + 2 terms); the Adzuna-only one is skipped.
        Assert.Equal(6, seek.Counted.Count);
        Assert.All(seek.Counted, c => Assert.Equal("1313", c.Code));
        Assert.Contains(("1313", "All Australia", "night shift"), seek.Counted);
        Assert.Equal((SnapshotStatus.Ok, 6, 6, 1), (snapshot.Status, snapshot.Expected, snapshot.Saved, snapshot.Listings));
        Assert.Equal("Perth WA", Assert.Single(_repo.Saved[0].Listings).City);
    }

    [Fact]
    public async Task A_failed_count_gets_a_second_chance()
    {
        var calls = 0;
        var seek = new FakeMarket { Count = (_, city, term) => city == "All Australia" && term is null && calls++ == 0 ? throw new HttpRequestException("429") : 50 };

        var snapshot = Assert.Single(await Service(seek).TakeAsync(Today, Ct));

        Assert.Equal(SnapshotStatus.Ok, snapshot.Status);
        Assert.Equal(7, seek.Counted.Count);
    }

    [Fact]
    public async Task A_count_failing_twice_leaves_it_out_and_marks_the_snapshot_partial()
    {
        var seek = new FakeMarket { Count = (_, _, term) => term?.Text == "fifo" ? throw new HttpRequestException("503") : 50 };

        var snapshot = Assert.Single(await Service(seek).TakeAsync(Today, Ct));

        Assert.Equal(SnapshotStatus.Partial, snapshot.Status);
        Assert.Equal((6, 4), (snapshot.Expected, snapshot.Saved));
        Assert.Equal(4, _repo.Saved[0].Counts.Count);
    }

    [Fact]
    public async Task A_cut_listing_marks_it_partial_and_a_big_swing_marks_it_suspect()
    {
        var cut = new FakeMarket { List = (_, _) => new SearchResult([], Complete: false) };
        Assert.Equal(SnapshotStatus.Partial, (await Service(cut).TakeAsync(Today, Ct))[0].Status);

        _repo.LastGood[("HVAC-R", "Perth WA")] = 300;
        Assert.Equal(SnapshotStatus.Suspect, (await Service(new FakeMarket()).TakeAsync(Today, Ct))[0].Status);
    }
}
