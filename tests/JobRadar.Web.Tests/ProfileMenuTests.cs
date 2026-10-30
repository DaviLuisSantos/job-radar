using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Web.Tests;

public class ProfileMenuTests
{
    private static ProfileDefinition Profile(string name, JobMarket market) =>
        new(new MatchingProfile(name, [], [], [], [], false, false, []) { Market = market }, [], "hash");

    [Fact]
    public void Profiles_are_grouped_by_market_and_a_new_one_shows_before_its_first_pass()
    {
        var groups = ProfileMenu.Groups(
            [Profile("fifo", JobMarket.AU), Profile("dotnet-br", JobMarket.BR), Profile("dotnet", JobMarket.AU)],
            scored: ["dotnet", "fifo"]);

        Assert.Equal(
            [(JobMarket.AU, "Austrália", "dotnet,fifo"), (JobMarket.BR, "Brasil", "dotnet-br")],
            groups.Select(g => (g.Market, g.Label, string.Join(",", g.Profiles))));
    }

    [Fact]
    public void A_scored_profile_no_longer_configured_stays_under_Australia()
    {
        var groups = ProfileMenu.Groups([Profile("dotnet-br", JobMarket.BR)], scored: ["old"]);

        Assert.Equal(["old"], groups.Single(g => g.Market == JobMarket.AU).Profiles);
    }

    [Theory]
    [InlineData("triage/dotnet-br", JobMarket.BR)]
    [InlineData("triage/Dotnet?page=2", JobMarket.AU)]
    [InlineData("triage/dotnet%2Dbr", JobMarket.BR)]
    [InlineData("status", null)]
    [InlineData("triage/unknown", null)]
    public void The_market_of_a_page_is_the_market_of_its_profile(string path, JobMarket? expected)
    {
        var groups = ProfileMenu.Groups([Profile("dotnet", JobMarket.AU), Profile("dotnet-br", JobMarket.BR)], scored: []);

        Assert.Equal(expected, ProfileMenu.MarketOf(groups, path));
    }
}
