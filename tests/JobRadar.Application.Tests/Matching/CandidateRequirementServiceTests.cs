using JobRadar.Application.Collection;
using JobRadar.Application.Matching;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Matching;

public class CandidateRequirementServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryCandidateRequirements _repository = new();
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryTerms _terms = new();
    private readonly InMemoryScores _scores = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero));

    private CandidateRequirementService Service()
    {
        var fifo = ProfileCatalogTests.Dotnet();
        fifo.Keywords = ["Offsider"];
        fifo.Requirements = new RequirementOptions { Items = ["hrwl", "truck-hr", "trade-qualification"] };
        var catalog = ProfileCatalogTests.Catalog(("fifo", fifo), ("tech", ProfileCatalogTests.Dotnet()));
        var scoring = new ListingScoringService(catalog, _listings, _terms, new InMemoryDescriptions(), _scores, _repository, _time,
            NullLogger<ListingScoringService>.Instance);
        return new(catalog, _repository, scoring, Options.Create(new CollectionOptions()), _time);
    }

    /// <summary>An offsider ad found by the fifo search, whose full text asks for the HRWL.</summary>
    private async Task<ListingId> HrwlAdAsync(string id = "1", string title = "Offsider")
    {
        await _listings.RecordObservationsAsync([Listings.Of("seek", id, title, _time.GetUtcNow().AddDays(-1))],
            "offsider @ perth wa", _time.GetUtcNow(), Ct);
        var listing = new ListingId("seek", id);
        _terms.Marks.Add((listing, "HRWL"));
        return listing;
    }

    private static CandidateFacts Facts(
        string[]? held = null, string[]? obtainable = null, string[]? outOfReach = null, string[]? heavy = null,
        CandidateEducation[]? education = null) =>
        new(held ?? [], obtainable ?? [], outOfReach ?? [], heavy ?? [], education ?? []);

    [Fact]
    public async Task Every_catalog_item_is_listed_at_its_default_until_set_with_the_profiles_that_search_it()
    {
        await Service().SetAsync("HRWL", RequirementState.Have, Ct);

        var list = (await Service().ListAsync(Ct)).ToDictionary(s => s.Item.Id);

        Assert.Equal(RequirementCatalog.Defaults.Count, list.Count);
        Assert.Equal((RequirementState.Have, false), (list["hrwl"].State, list["hrwl"].IsDefault));
        Assert.Equal(["fifo"], list["hrwl"].Profiles);
        Assert.Equal((RequirementState.OutOfReach, true), (list["truck-hr"].State, list["truck-hr"].IsDefault));
        Assert.Equal((RequirementState.Missing, true), (list["degree"].State, list["degree"].IsDefault));
        Assert.Empty(list["degree"].Profiles);
    }

    [Fact]
    public async Task An_item_is_set_by_id_or_phrase_and_cleared_back_to_its_default()
    {
        Assert.Equal("refrigerant-licence", (await Service().SetAsync("arctick", RequirementState.OutOfReach, Ct)).Item.Id);
        await Service().SetAsync("refrigerant-licence", null, Ct);

        Assert.Empty(_repository.States);
        await Assert.ThrowsAsync<UnknownRequirementException>(() => Service().SetAsync("forklift", RequirementState.Have, Ct));
    }

    [Fact]
    public async Task Setting_a_state_rescores_at_once_the_listings_that_may_ask_for_the_item()
    {
        var asks = await HrwlAdAsync("1");
        await _listings.RecordObservationsAsync([Listings.Of("seek", "2", "Offsider")], "offsider @ perth wa", _time.GetUtcNow(), Ct);

        var (_, rescored) = await Service().SetAsync("hrwl", RequirementState.Have, Ct);

        var score = Assert.Single(_scores.Saved);
        Assert.Equal(1, rescored);
        Assert.Equal((asks, "fifo"), (score.Id, score.Profile));
        Assert.Contains(score.Score.Items, i => i.Rule == ScoreRule.RequirementHeld);
    }

    [Fact]
    public async Task Impact_counts_the_listings_holding_an_item_would_move_and_how_far()
    {
        var asks = await HrwlAdAsync("1");
        await HrwlAdAsync("2", "Offsider citizens only");   // excluded either way: does not move
        _scores.Judged.Add(new JudgedScore(asks, "Offsider", "fifo", new FitScore(0, []), Verdict.Good));

        var impact = Assert.Single(await Service().ImpactAsync(Ct), i => i.ItemId == "hrwl");

        Assert.Equal(("fifo", 1, 1, 0), (impact.Profile, impact.Affected, impact.Good, impact.Bad));
        Assert.Equal(1, impact.VisibleWith);
        Assert.True(impact.VisibleWith >= impact.VisibleWithout);
        Assert.DoesNotContain(await Service().ImpactAsync(Ct), i => i.ItemId == "hrwl" && i.Profile == "tech");
        Assert.Empty(_repository.States);
        Assert.Empty(_scores.Saved);
    }

    [Fact]
    public async Task Import_sets_what_the_facts_imply_and_names_what_the_catalog_lacks()
    {
        _repository.States["hrwl"] = RequirementState.Obtainable;
        var facts = Facts(
            held: ["white card"],
            obtainable: ["HRWL", "verification of competency"],
            outOfReach: ["electrical licence"],
            heavy: ["MR"],
            education: [new("technical", RecognisedInAustralia: false), new("trade", RecognisedInAustralia: true)]);

        var dry = await Service().ImportAsync(facts, dryRun: true, Ct);
        Assert.Equal([("hrwl", RequirementState.Obtainable)], _repository.States.Select(s => (s.Key, s.Value)));

        var report = await Service().ImportAsync(facts, dryRun: false, Ct);

        Assert.Equal(dry.Changes, report.Changes);
        Assert.Equal(dry.NotInCatalog, report.NotInCatalog);
        Assert.Equal(
            [
                ("electrical-licence", (RequirementState?)null, RequirementState.OutOfReach),
                ("trade-qualification", null, RequirementState.Have),
                ("truck-mr", null, RequirementState.Have),
            ],
            report.Changes.Select(c => (c.ItemId, c.From, c.To)));   // hrwl already set, voc at its default: obtainable
        Assert.Equal(["ticket held: white card"], report.NotInCatalog);
        Assert.Equal(4, _repository.States.Count);
    }

    [Fact]
    public async Task What_the_candidate_holds_wins_over_a_more_cautious_mention()
    {
        var report = await Service().ImportAsync(Facts(held: ["HRWL"], outOfReach: ["HRWL"]), dryRun: false, Ct);

        Assert.Equal(RequirementState.Have, Assert.Single(report.Changes).To);
    }

    [Fact]
    public void The_example_candidate_facts_parse()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "candidate", "candidate.example.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var facts = CandidateFacts.Parse(File.ReadAllText(Path.Combine(dir.FullName, "candidate", "candidate.example.json")));

        Assert.Equal(["white card"], facts.TicketsHeld);
        Assert.Equal(["HRWL", "forklift"], facts.TicketsObtainable);
        Assert.Equal(["electrical licence"], facts.TicketsOutOfReach);
        Assert.Empty(facts.HeavyLicences);
        Assert.Equal([new CandidateEducation("technical", false)], facts.Education);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "tickets": {} }""")]
    [InlineData("not json")]
    public void A_file_that_is_not_candidate_facts_is_refused(string json) =>
        Assert.Throws<FormatException>(() => CandidateFacts.Parse(json));
}
