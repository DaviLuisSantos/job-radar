using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Matching;

public class ListingScoringServiceTests
{
    private static readonly TimeZoneInfo Perth = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class InMemoryScores : IListingScoreRepository
    {
        public List<ListingScore> Saved { get; } = [];

        public Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default)
        {
            Saved.AddRange(scores);
            return Task.CompletedTask;
        }
    }

    // 23:00 UTC on the 20th is 07:00 on the 21st in Perth.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 23, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryScores _scores = new();
    private readonly InMemoryTerms _terms = new();

    private static SourcedListing Ad(string id, string title, DateTimeOffset? posted = null) => new(
        new Listing(new ListingId("seek", id), title, null, ListingLocation.Unknown, WorkType.Unknown, null,
            posted, new Uri($"https://example.test/{id}"), null),
        "{}");

    private ListingScoringService Service(ProfileCatalog catalog) =>
        new(catalog, _listings, _terms, _scores, _time, NullLogger<ListingScoringService>.Instance);

    [Fact]
    public async Task Each_profile_scores_only_what_its_own_searches_found()
    {
        var hospo = new ProfileOptions { Keywords = ["Barista"], Locations = ["Perth WA"] };
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()), ("hospo", hospo));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        await _listings.RecordObservationsAsync([Ad("2", "Barista")], "barista @ perth wa", _time.GetUtcNow(), Ct);

        await Service(catalog).ScoreAllAsync(Perth, Ct);

        Assert.Equal([("hospo", "2"), ("tech", "1")], _scores.Saved.Select(s => (s.Profile, s.Id.SourceId)).Order());
    }

    [Fact]
    public async Task Scores_carry_the_profile_rules_hash_and_the_explanation()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);

        await Service(catalog).ScoreAllAsync(Perth, Ct);

        var saved = Assert.Single(_scores.Saved);
        Assert.Equal(catalog.Profiles[0].RulesHash, saved.RulesHash);
        Assert.Equal(66, saved.Score.Final);   // 50 + 16 for C# in the title
        Assert.Equal(_time.GetUtcNow(), saved.ScoredAt);
    }

    [Fact]
    public async Task Age_counts_from_the_local_date_of_the_scoring_time_zone()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        // Posted 15 days before the 21st (Perth): aging. From the UTC date (20th) it would be 14 days.
        var posted = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        await _listings.RecordObservationsAsync([Ad("1", "Developer", posted)], "c# developer @ perth wa", _time.GetUtcNow(), Ct);

        await Service(catalog).ScoreAllAsync(Perth, Ct);

        Assert.Contains(Assert.Single(_scores.Saved).Score.Items, i => i.Rule == ScoreRule.Aging);
    }

    [Fact]
    public async Task Full_text_terms_of_a_listing_reach_the_scorer()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer"), Ad("2", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        await _terms.RecordAsync("seek", "australian citizen", ["1"], _time.GetUtcNow(), Ct);

        await Service(catalog).ScoreAllAsync(Perth, Ct);

        var byId = _scores.Saved.ToDictionary(s => s.Id.SourceId);
        Assert.Equal(11, byId["1"].Score.Final);   // 66 − 55
        Assert.Contains(byId["1"].Score.Items, i => i.Rule == ScoreRule.FullTextCitizenship);
        Assert.Equal(66, byId["2"].Score.Final);
    }
}
