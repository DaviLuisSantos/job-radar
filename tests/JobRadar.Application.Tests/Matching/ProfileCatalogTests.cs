using JobRadar.Application.Matching;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Tests.Matching;

public class ProfileCatalogTests
{
    internal static ProfileOptions Dotnet() => new()
    {
        Keywords = [".NET Developer", "C# Developer"],
        Locations = ["Perth WA", "All Australia"],
        PositiveSignals = ["C#"],
    };

    internal static ProfileCatalog Catalog(params (string Name, ProfileOptions Options)[] profiles) =>
        new(Options.Create(new MatchingOptions
        {
            Profiles = profiles.ToDictionary(p => p.Name, p => p.Options),
        }), ScoringPolicy.Default);

    [Fact]
    public void Every_keyword_is_searched_in_every_location()
    {
        var profile = Assert.Single(Catalog(("tech", Dotnet())).Profiles);

        Assert.Equal(
            [".net developer @ perth wa", ".net developer @ all australia", "c# developer @ perth wa", "c# developer @ all australia"],
            profile.Searches.Select(s => s.Key));
    }

    [Fact]
    public void A_search_shared_by_two_profiles_runs_once_but_belongs_to_both()
    {
        var other = Dotnet();
        other.Keywords = ["C# developer", "Integration Developer"];
        var catalog = Catalog(("tech", Dotnet()), ("integration", other));

        Assert.Equal(6, catalog.Searches.Count);
        Assert.All(catalog.Profiles, p => Assert.Contains("c# developer @ perth wa", p.SearchKeys));
    }

    [Fact]
    public void Rules_hash_follows_the_scoring_rules_not_the_searches()
    {
        var baseline = ProfileCatalog.Build("tech", Dotnet(), ScoringPolicy.Default).RulesHash;

        var moreSearches = Dotnet();
        moreSearches.Locations.Add("Sydney NSW");
        var newSignal = Dotnet();
        newSignal.PositiveSignals.Add("Azure");

        Assert.Equal(baseline, ProfileCatalog.Build("tech", moreSearches, ScoringPolicy.Default).RulesHash);
        Assert.NotEqual(baseline, ProfileCatalog.Build("tech", newSignal, ScoringPolicy.Default).RulesHash);
        Assert.NotEqual(baseline, ProfileCatalog.Build("tech", Dotnet(), ScoringPolicy.Default with { SponsorBonus = 20 }).RulesHash);
        Assert.Equal(baseline, ProfileCatalog.Build("tech", Dotnet(), ScoringPolicy.Default).RulesHash);
    }

    [Fact]
    public void A_profile_also_scores_listings_attached_to_it_directly()
    {
        var profile = Assert.Single(Catalog(("Tech", Dotnet())).Profiles);

        Assert.Contains("profile: tech", profile.SearchKeys);
        Assert.Equal("profile: tech", ProfileCatalog.DirectKey(" TECH "));
        // Not a search: nothing is collected for it.
        Assert.DoesNotContain(profile.Searches, s => s.Key.StartsWith("profile:", StringComparison.Ordinal));
    }

    [Fact]
    public void No_profiles_means_nothing_to_collect()
    {
        var catalog = Catalog();

        Assert.Empty(catalog.Profiles);
        Assert.Empty(catalog.Searches);
    }
}
