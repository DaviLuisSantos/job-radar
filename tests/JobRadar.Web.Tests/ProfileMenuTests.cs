using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Web.Tests;

public class ProfileMenuTests
{
    private static ProfileDefinition Profile(string name, Country country) =>
        new(new MatchingProfile(name, [], [], [], [], false, false, []), [], "hash") { Country = country };

    [Fact]
    public void Profiles_are_grouped_by_market_and_a_new_one_shows_before_its_first_pass()
    {
        var groups = ProfileMenu.Groups(
            [Profile("fifo", Country.AU), Profile("dotnet-br", Country.BR), Profile("dotnet", Country.AU)],
            scored: ["dotnet", "fifo"]);

        Assert.Equal(
            [(Country.AU, "Australia", "dotnet,fifo"), (Country.BR, "Brazil", "dotnet-br")],
            groups.Select(g => (g.Country, g.Label, string.Join(",", g.Profiles))));
    }

    [Fact]
    public void A_scored_profile_no_longer_configured_stays_under_Australia()
    {
        var groups = ProfileMenu.Groups([Profile("dotnet-br", Country.BR)], scored: ["old"]);

        Assert.Equal(["old"], groups.Single(g => g.Country == Country.AU).Profiles);
    }
}
