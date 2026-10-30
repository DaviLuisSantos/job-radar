using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// A fictional market's eligibility rules, shaped like a real block: a guild whose members the jobs may be reserved
/// for, seasonal members it welcomes, and employers that may help with relocation. The public tests check the
/// mechanism with these words; the real ones are checked locally (<see cref="LocalScoringCasesTests"/>).
/// </summary>
public static class TestMarkets
{
    public static EligibilityRules Fictional { get; } = new()
    {
        RestrictedPattern = @"guild\s+members?\s+only|must\s+be\s+(?:a\s+)?guild\s+member|guild\s+pass|full\s+guild\s+standing\s*[-–—]?\s*essential",
        IncompatiblePattern = @"(?:lifetime|permanent|long[\s-]term)\s+guild\s+standing"
            + @"|(?:24|twenty-four)\s+months?\s+(?:of\s+)?(?:valid\s+)?guild\s+standing"
            + @"|no\s+seasonal\s+members?",
        ReservedTitlePattern = @"\bguild\s+scholar\b",
        WelcomePattern = @"seasonal\s+members?|newcomers?\s+welcome",
        AssistanceDeniedPattern = @"\b(?:no|not|without)\s+relocation|relocation\s+(?:is\s+)?not\s+\w+",
        AssistanceOfferedPhrases = ["relocation package", "we relocate"],
        AssistanceWord = "relocation",
        Phrases = new Dictionary<string, IReadOnlyList<string>>
        {
            [FullTextGroup.Exclude] = ["lifetime guild standing"],
            [FullTextGroup.Doubt] = ["guild member", "guild pass"],
            [FullTextGroup.Ambiguous] = ["full guild standing"],
            [FullTextGroup.Welcome] = ["seasonal member", "newcomers"],
            [FullTextGroup.WelcomeRetracted] = ["seasonal member + guild member", "seasonal member + guild pass"],
            [FullTextGroup.Tenure] = ["24 months + guild standing"],
        },
    };

    public static MarketRules Au { get; } = MarketRules.For(JobMarket.AU, Fictional);
}
