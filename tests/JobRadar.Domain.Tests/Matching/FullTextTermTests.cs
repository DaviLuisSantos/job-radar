using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

public class FullTextTermTests
{
    [Fact]
    public void Plus_joins_phrases_that_must_all_appear()
    {
        Assert.Equal(["12 months", "working rights"], new FullTextTerm("12 months + working rights").Phrases);
        Assert.Equal(["backpackers"], new FullTextTerm("backpackers").Phrases);
    }

    [Fact]
    public void Run_key_ignores_case_and_spacing()
    {
        Assert.Equal("term: hr licence", new FullTextTerm(" HR licence ").RunKey);
    }

    private static MarketRules Market(params (string Group, string[] Phrases)[] groups) => MarketRules.Australia(new EligibilityRules
    {
        Phrases = groups.ToDictionary(g => g.Group, g => (IReadOnlyList<string>)g.Phrases),
    });

    [Fact]
    public void All_terms_of_the_markets_once_each_in_a_stable_order()
    {
        var au = Market((FullTextGroup.Welcome, ["seasonal member", "newcomers", "Guild Member"]), (FullTextGroup.Doubt, ["guild member"]));

        // The code's groups, then the market's phrases; "Guild Member" is "guild member" again.
        Assert.Equal(["HR licence", "commission only", "guild member", "heavy rigid", "newcomers", "seasonal member"],
            FullTextTerm.AllOf([au, MarketRules.Brazil], ScoringPolicy.Default).Select(t => t.Text));
    }

    [Fact]
    public void Terms_of_the_context_groups_are_also_searched_with_each_profile_context_word()
    {
        var au = Market((FullTextGroup.Doubt, ["guild member"]), (FullTextGroup.Welcome, ["seasonal member"]));
        static MatchingProfile Profile(string name, params string[] contexts) =>
            new(name, [], [], [], [], false, false, []) { Requirements = ProfileRequirements.None with { Contexts = contexts } };

        var terms = FullTextTerm.AllOf([au], ScoringPolicy.Default,
            [Profile("fifo", "FIFO"), Profile("construction", "labourer", "fifo"), Profile("tech")]);

        Assert.Equal(
            ["HR licence", "commission only", "guild member", "guild member + FIFO", "guild member + labourer", "heavy rigid", "seasonal member"],
            terms.Select(t => t.Text));
    }
}
