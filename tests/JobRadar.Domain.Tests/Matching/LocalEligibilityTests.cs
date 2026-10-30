using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// Transitional, until the rules that were in code are deleted: each block the local configuration has is the same
/// rules, written as data. Produce a block with <c>eligibility &lt;market&gt;</c> and paste it; this proves the paste.
/// </summary>
public class LocalEligibilityTests
{
    [LocalSettingsFact]
    public void Each_configured_block_is_the_rules_that_were_in_code()
    {
        var legacy = new Dictionary<JobMarket, EligibilityRules>
        {
            [JobMarket.AU] = LegacyEligibility.Australia,
            [JobMarket.LatamRemote] = LegacyEligibility.Remote,
            [JobMarket.GlobalRemote] = LegacyEligibility.Remote,
        };
        // A market without a block yet still scores with the rules in code: nothing to prove for it.
        foreach (var (market, code) in legacy)
        {
            if (LocalSettings.Eligibility(market) is { } local)
                Assert.Equal(LocalSettings.Canonical(code), LocalSettings.Canonical(local));
        }
    }
}
