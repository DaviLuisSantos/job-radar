using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Tests;

/// <summary>The versioned example of the local configuration is what a newcomer copies: it must start as it is.</summary>
public class ExampleConfigurationTests
{
    internal static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "JobRadar.slnx")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("JobRadar.slnx not found above the test output.");
    }

    private static IConfiguration Example() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(RepositoryRoot(), "src", "JobRadar.Worker", "appsettings.Local.example.json"), optional: false)
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused" })
        .Build();

    [Fact]
    public void The_example_profiles_and_markets_build()
    {
        var catalog = new ProfileCatalog(Options.Create(Example().GetSection(MatchingOptions.Section).Get<MatchingOptions>()!), ScoringPolicy.Default);

        Assert.Contains(catalog.Profiles, p => p.Market == JobMarket.AU);
        Assert.Contains(catalog.Profiles, p => p.Market == JobMarket.BR);
        Assert.Equal("relocation", catalog.RulesOf(JobMarket.AU).AssistanceWord);
        Assert.Matches(catalog.RulesOf(JobMarket.LatamRemote).ResidencyRestricted, "us guild members only");
    }

    [Fact]
    public void The_example_passes_the_start_checks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobRadar(Example());
        using var sp = services.BuildServiceProvider();

        // What ValidateOnStart runs when the host starts.
        _ = sp.GetRequiredService<IOptions<MatchingOptions>>().Value;
    }
}
