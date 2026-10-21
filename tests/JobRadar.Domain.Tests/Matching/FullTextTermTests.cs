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
}
