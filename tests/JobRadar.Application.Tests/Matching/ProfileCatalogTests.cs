using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tests.Matching;
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

    /// <summary>A profile built with the fictional AU market: Australia scores only with an eligibility block.</summary>
    private static ProfileDefinition Build(string name, ProfileOptions o, ScoringPolicy policy) =>
        ProfileCatalog.Build(name, o, policy, rules: TestMarkets.Au);

    internal static ProfileCatalog Catalog(params (string Name, ProfileOptions Options)[] profiles) =>
        new(Options.Create(new MatchingOptions
        {
            Profiles = profiles.ToDictionary(p => p.Name, p => p.Options),
            Markets = FictionalMarkets(),
        }), ScoringPolicy.Default);

    /// <summary>The fictional AU market of the scorer tests (<see cref="TestMarkets"/>), as configuration.</summary>
    internal static Dictionary<string, MarketRulesOptions> FictionalMarkets()
    {
        var r = TestMarkets.Fictional;
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["AU"] = new MarketRulesOptions
            {
                Eligibility = new EligibilityOptions
                {
                    RestrictedPattern = r.RestrictedPattern,
                    IncompatiblePattern = r.IncompatiblePattern,
                    ReservedTitlePattern = r.ReservedTitlePattern,
                    WelcomePattern = r.WelcomePattern,
                    AssistanceDeniedPattern = r.AssistanceDeniedPattern,
                    AssistanceOfferedPhrases = [.. r.AssistanceOfferedPhrases],
                    AssistanceWord = r.AssistanceWord,
                    Phrases = r.Phrases.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.OrdinalIgnoreCase),
                },
            },
        };
    }

    [Fact]
    public void Every_keyword_is_searched_in_every_location()
    {
        var profile = Assert.Single(Catalog(("tech", Dotnet())).Profiles);

        Assert.Equal(
            [".net developer @ perth wa", ".net developer @ all australia", "c# developer @ perth wa", "c# developer @ all australia"],
            profile.Searches.Select(s => s.Key));
    }

    [Theory]
    [InlineData(null, null, JobMarket.AU)]
    [InlineData("GlobalRemote", null, JobMarket.GlobalRemote)]
    [InlineData("latamremote", null, JobMarket.LatamRemote)]
    [InlineData(null, "BR", JobMarket.BR)]        // "Country" from before markets still reads
    [InlineData("BR", "AU", JobMarket.BR)]         // Market wins
    public void The_market_comes_from_the_profile_and_reaches_its_searches(string? market, string? country, JobMarket expected)
    {
        var options = Dotnet();
        options.Market = market;
        options.Country = country;

        var profile = Assert.Single(Catalog(("tech", options)).Profiles);

        Assert.Equal(expected, profile.Market);
        Assert.All(profile.Searches, s => Assert.Equal(expected, s.Market));
    }

    [Fact]
    public void An_unknown_market_fails_naming_the_valid_ones()
    {
        var options = Dotnet();
        options.Market = "Mars";

        var error = Assert.Throws<InvalidOperationException>(() => Catalog(("tech", options)));
        Assert.Contains("LatamRemote", error.Message);
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
        var baseline = Build("tech", Dotnet(), ScoringPolicy.Default).RulesHash;

        var moreSearches = Dotnet();
        moreSearches.Locations.Add("Sydney NSW");
        var newSignal = Dotnet();
        newSignal.PositiveSignals.Add("Azure");

        Assert.Equal(baseline, Build("tech", moreSearches, ScoringPolicy.Default).RulesHash);
        Assert.NotEqual(baseline, Build("tech", newSignal, ScoringPolicy.Default).RulesHash);
        Assert.NotEqual(baseline, Build("tech", Dotnet(), ScoringPolicy.Default with { AssistanceBonus = 20 }).RulesHash);
        Assert.Equal(baseline, Build("tech", Dotnet(), ScoringPolicy.Default).RulesHash);
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

    [Fact]
    public void Requirement_items_bring_their_phrases_and_own_phrases_add_to_them()
    {
        var options = Dotnet();
        options.Requirements = new RequirementOptions
        {
            Contexts = ["FIFO"],
            Items = ["truck-hr", " HRWL ", "degree"],
            Licence = ["white card", "HR licence"],
        };

        var requirements = Build("fifo", options, ScoringPolicy.Default).Profile.Requirements;

        Assert.Equal(["FIFO"], requirements.Contexts);
        Assert.Empty(requirements.Experience);
        Assert.Equal(["degree in", "tertiary qualification", "relevant degree"], requirements.Qualification);
        Assert.Equal(["HR licence", "HR license", "heavy rigid", "HRWL", "white card"], requirements.Licence);
    }

    [Fact]
    public void An_unknown_requirement_stops_the_start()
    {
        var options = Dotnet();
        options.Requirements = new RequirementOptions { Items = ["hrlw"] };

        var error = Assert.Throws<InvalidOperationException>(() => Build("fifo", options, ScoringPolicy.Default));

        Assert.Contains("'hrlw'", error.Message, StringComparison.Ordinal);
        Assert.Contains("hrwl", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_catalog_is_part_of_the_rules_hash()
    {
        var options = Dotnet();
        options.Requirements = new RequirementOptions { Items = ["hrwl"] };
        var wider = ScoringPolicy.Default with
        {
            Requirements = [.. RequirementCatalog.Defaults.Where(i => i.Id != "hrwl"),
                new RequirementItem("hrwl", RequirementKind.Licence, ["HRWL", "high risk work licence"])],
        };

        Assert.NotEqual(Build("fifo", options, ScoringPolicy.Default).RulesHash,
            Build("fifo", options, wider).RulesHash);
        Assert.Equal(["HRWL", "high risk work licence"], Build("fifo", options, wider).Profile.Requirements.Licence);
    }

    private static ProfileCatalog WithMarkets(Dictionary<string, MarketRulesOptions> markets, params (string Name, ProfileOptions Options)[] profiles) =>
        new(Options.Create(new MatchingOptions
        {
            Profiles = profiles.ToDictionary(p => p.Name, p => p.Options),
            Markets = new(markets, StringComparer.OrdinalIgnoreCase),
        }), ScoringPolicy.Default);

    private static Dictionary<string, MarketRulesOptions> Au(EligibilityOptions eligibility) =>
        new() { ["AU"] = new MarketRulesOptions { Eligibility = eligibility } };

    private static EligibilityOptions Fictional() => new()
    {
        RestrictedPattern = @"guild\s+members?\s+only",
        AssistanceWord = "relocation",
        Phrases = new(StringComparer.OrdinalIgnoreCase) { ["Doubt"] = ["guild member"], ["welcome"] = ["seasonal member", " "] },
    };

    [Fact]
    public void A_market_reads_its_configured_eligibility_block()
    {
        var rules = Assert.Single(WithMarkets(Au(Fictional()), ("tech", Dotnet())).Profiles).Rules;

        Assert.Equal(JobMarket.AU, rules.Market);
        Assert.Equal("relocation", rules.AssistanceWord);
        Assert.Matches(rules.Restricted, "guild members only");
        // Group keys in lower case, blank phrases dropped, the code's groups kept.
        Assert.Equal(["seasonal member"], rules.FullTextGroups[FullTextGroup.Welcome]);
        Assert.Equal(["guild member"], rules.FullTextGroups[FullTextGroup.Doubt]);
        Assert.Contains(FullTextGroup.Licence, rules.FullTextGroups.Keys);
    }

    [Fact]
    public void A_configured_market_without_profiles_is_still_built_and_checked()
    {
        var catalog = WithMarkets(Au(Fictional()));

        Assert.Empty(catalog.Profiles);
        Assert.Equal("relocation", catalog.RulesOf(JobMarket.AU).AssistanceWord);
    }

    [Theory]
    [InlineData("RestrictedPattern", "Matching:Markets:AU:Eligibility:RestrictedPattern is not a valid regular expression")]
    [InlineData("Phrases", "unknown groups: members")]
    public void A_block_that_would_not_score_stops_the_start(string broken, string message)
    {
        var block = Fictional();
        if (broken == "RestrictedPattern")
            block.RestrictedPattern = "guild (members";
        else
            block.Phrases["members"] = ["x"];

        var error = Assert.Throws<InvalidOperationException>(() => WithMarkets(Au(block), ("tech", Dotnet())));
        Assert.Contains(message, error.Message);
        Assert.Contains(message, new MatchingOptionsValidation().Validate(null, new MatchingOptions { Markets = Au(block) }).FailureMessage);
    }

    [Fact]
    public void Markets_that_read_no_such_rules_refuse_a_block()
    {
        Assert.Contains("unknown market", Assert.Throws<InvalidOperationException>(
            () => WithMarkets(new() { ["Mars"] = new MarketRulesOptions { Eligibility = Fictional() } })).Message);
        Assert.Contains("BR reads no eligibility", Assert.Throws<InvalidOperationException>(
            () => WithMarkets(new() { ["BR"] = new MarketRulesOptions { Eligibility = Fictional() } })).Message);
        Assert.Contains("may only set RestrictedPattern", Assert.Throws<InvalidOperationException>(
            () => WithMarkets(new() { ["LatamRemote"] = new MarketRulesOptions { Eligibility = Fictional() } })).Message);
    }

    [Fact]
    public void A_remote_block_adds_to_the_residency_phrases()
    {
        var latam = new MarketRulesOptions { Eligibility = new EligibilityOptions { RestrictedPattern = @"\bmars\s+guild\s+only\b" } };

        var rules = WithMarkets(new() { ["LatamRemote"] = latam }).RulesOf(JobMarket.LatamRemote);

        Assert.Matches(rules.ResidencyRestricted, "open to mars guild only");
        Assert.Matches(rules.ResidencyRestricted, "us only");
    }

    [Fact]
    public void Rules_hash_follows_the_market_eligibility_block()
    {
        var other = Fictional();
        other.Phrases["welcome"] = ["seasonal members"];

        var hash = Assert.Single(WithMarkets(Au(Fictional()), ("tech", Dotnet())).Profiles).RulesHash;

        Assert.Equal(hash, Assert.Single(WithMarkets(Au(Fictional()), ("tech", Dotnet())).Profiles).RulesHash);
        Assert.NotEqual(hash, Assert.Single(WithMarkets(Au(other), ("tech", Dotnet())).Profiles).RulesHash);
    }
}
