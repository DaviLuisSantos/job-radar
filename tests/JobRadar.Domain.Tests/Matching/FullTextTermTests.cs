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

    [Fact]
    public void All_terms_of_a_policy_once_each_in_a_stable_order()
    {
        var policy = ScoringPolicy.Default with
        {
            FullTextGroups = new Dictionary<string, IReadOnlyList<string>>
            {
                ["a"] = ["working holiday", "backpackers"],
                ["b"] = ["Working Holiday"],
            },
        };

        Assert.Equal(["backpackers", "working holiday"], FullTextTerm.AllOf(policy).Select(t => t.Text));
    }

    [Fact]
    public void Terms_of_the_context_groups_are_also_searched_with_each_profile_context_word()
    {
        var policy = ScoringPolicy.Default with
        {
            FullTextGroups = new Dictionary<string, IReadOnlyList<string>>
            {
                [FullTextGroup.Citizen] = ["australian citizen"],
                [FullTextGroup.Whv] = ["working holiday"],
            },
        };
        static MatchingProfile Profile(string name, params string[] contexts) =>
            new(name, [], [], [], [], false, false, []) { Requirements = ProfileRequirements.None with { Contexts = contexts } };

        var terms = FullTextTerm.AllOf(policy, [Profile("fifo", "FIFO"), Profile("construction", "labourer", "fifo"), Profile("tech")]);

        Assert.Equal(["australian citizen", "australian citizen + FIFO", "australian citizen + labourer", "working holiday"],
            terms.Select(t => t.Text));
    }
}
