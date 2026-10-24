using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
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

        public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default) =>
            Task.FromResult(Saved.RemoveAll(s => s.ScoredAt < before));

        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JobRadar.Domain.Triage.TriageRow>> ListForTriageAsync(JobRadar.Domain.Triage.TriageFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JobRadar.Domain.Triage.ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // 23:00 UTC on the 20th is 07:00 on the 21st in Perth.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 23, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryScores _scores = new();
    private readonly InMemoryTerms _terms = new();
    private readonly InMemoryDescriptions _descriptions = new();
    private readonly InMemoryCandidateRequirements _requirements = new();

    private static SourcedListing Ad(string id, string title, DateTimeOffset? posted = null) => new(
        new Listing(new ListingId("seek", id), title, null, ListingLocation.Unknown, WorkType.Unknown, null,
            posted, new Uri($"https://example.test/{id}"), null),
        "{}");

    private ListingScoringService Service(ProfileCatalog catalog) =>
        new(catalog, _listings, _terms, _descriptions, _scores, _requirements, _time, NullLogger<ListingScoringService>.Instance);

    [Fact]
    public async Task One_listing_is_scored_for_the_profiles_that_found_it_with_its_captured_text()
    {
        var hospo = new ProfileOptions { Keywords = ["Barista"], Locations = ["Perth WA"] };
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()), ("hospo", hospo));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        var id = new ListingId("seek", "1");
        _descriptions.Saved[id] = new ListingDescription(id, "You must hold Australian citizenship to be considered for this role.",
            DescriptionOrigin.Pasted, _time.GetUtcNow());

        var scored = await Service(catalog).ScoreOneAsync(id, Perth, Ct);

        var only = Assert.Single(scored);
        Assert.Equal("tech", only.Profile);
        Assert.Contains(only.Score.Items, i => i.Where == ScoreWhere.Description);
        Assert.Equal(scored, _scores.Saved);
        Assert.Empty(await Service(catalog).ScoreOneAsync(new ListingId("seek", "404"), Perth, Ct));
    }

    [Fact]
    public async Task Rescoring_judged_listings_uses_the_current_rules_and_saves_nothing()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        var id = new ListingId("seek", "1");
        _descriptions.Saved[id] = new ListingDescription(id, "You must hold Australian citizenship to be considered for this role.",
            DescriptionOrigin.Pasted, _time.GetUtcNow());
        var stale = new FitScore(90, []);
        JudgedScore[] judged =
        [
            new(id, "C# Developer", "tech", stale, Verdict.Bad),
            new(id, "C# Developer", "hospo", stale, Verdict.Bad),                        // profile no longer configured
            new(new ListingId("seek", "404"), "Gone", "tech", stale, Verdict.Good),      // never collected
        ];

        var rescored = await Service(catalog).RescoreAsync(judged, Perth, Ct);

        var only = Assert.Single(rescored);
        Assert.Equal((id, "tech", Verdict.Bad), (only.Id, only.Profile, only.Verdict));
        Assert.Contains(only.Score.Items, i => i.Where == ScoreWhere.Description);
        Assert.Empty(_scores.Saved);
    }

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
    public async Task Scores_the_pass_did_not_write_are_deleted()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        var yesterday = _time.GetUtcNow().AddDays(-1);
        // Found yesterday by a search the profile dropped, and a profile no longer configured.
        _scores.Saved.Add(new ListingScore(new ListingId("seek", "9"), "tech", new FitScore(90, []), "old0", yesterday));
        _scores.Saved.Add(new ListingScore(new ListingId("seek", "1"), "removed", new FitScore(70, []), "old0", yesterday));

        await Service(catalog).ScoreAllAsync(Perth, Ct);

        Assert.Equal([("tech", "1")], _scores.Saved.Select(s => (s.Profile, s.Id.SourceId)));
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

    [Fact]
    public async Task Requirement_states_change_the_score_and_the_rules_hash_only_when_the_profile_searches_them()
    {
        var tech = ProfileCatalogTests.Dotnet();
        tech.Requirements = new RequirementOptions { Items = ["hrwl"] };
        var catalog = ProfileCatalogTests.Catalog(("tech", tech));
        var profile = Assert.Single(catalog.Profiles);
        await _listings.RecordObservationsAsync([Ad("1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);
        var id = new ListingId("seek", "1");
        _terms.Marks.Add((id, "HRWL"));

        var before = Assert.Single(await Service(catalog).ScoreOneAsync(id, Perth, Ct));
        _requirements.States["degree"] = RequirementState.Have;   // the profile does not search it
        var unrelated = Assert.Single(await Service(catalog).ScoreOneAsync(id, Perth, Ct));
        _requirements.States["hrwl"] = RequirementState.Have;
        var after = Assert.Single(await Service(catalog).ScoreOneAsync(id, Perth, Ct));

        Assert.Equal(profile.RulesHash, before.RulesHash);
        Assert.Equal((profile.RulesHash, before.Score.Final), (unrelated.RulesHash, unrelated.Score.Final));
        Assert.NotEqual(profile.RulesHash, after.RulesHash);
        Assert.Equal(-10, Assert.Single(before.Score.Items, i => i.Rule == ScoreRule.FullTextLicence).Points);
        Assert.Equal(before.Score.Final + 10, after.Score.Final);
    }
}
