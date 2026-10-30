using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

public class CountryRulesTests
{
    private static readonly MatchingProfile Tech = new(
        "tech", PositiveSignals: ["C#"], NegativeSignals: [], DisqualifySignals: [], QualifiedTitles: [],
        EntryLevelBoost: false, AllowSenior: true, PreferredLocations: []);

    [Fact]
    public void A_listing_is_read_by_its_own_market()
    {
        var listing = new Listing(new ListingId("seek", "1"), "C# Developer", null,
            new ListingLocation(Country.AU, null, "Perth", "WA", null), WorkType.Contract, null, null,
            new Uri("https://example.test/1"), null);

        Assert.Equal(Country.AU, ScoringInput.From(listing).Country);
        Assert.Same(CountryRules.Australia, CountryRules.For(Country.AU));
    }

    [Fact]
    public void Australia_keeps_the_prototype_phrases()
    {
        // The extraction moved where the phrases live, not what they say: parity depends on it.
        var input = new ScoringInput("Senior C# Developer", "Australian citizens only. Remote.", "Perth WA", "Contract", null, []);

        var score = FitScorer.Score(input, Tech, ScoringPolicy.Default, new DateOnly(2026, 10, 21));

        Assert.Equal(0, score.Final);
        Assert.Equal(ScoreRule.CitizenshipRequired, Assert.Single(score.Items).Rule);
    }

    [Fact]
    public void A_market_without_rules_is_refused_instead_of_read_as_Australian()
    {
        // Reading a Brazilian ad with Australian phrases would score it on rules that do not apply.
        var input = new ScoringInput("Desenvolvedor C#", null, "São Paulo SP", null, null, []) { Country = Country.BR };

        Assert.Throws<NotSupportedException>(() => FitScorer.Score(input, Tech, ScoringPolicy.Default, new DateOnly(2026, 10, 21)));
    }
}
