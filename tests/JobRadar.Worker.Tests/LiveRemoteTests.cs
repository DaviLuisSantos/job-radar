using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Worker.Tests;

/// <summary>Like <see cref="LiveGupyTests"/>, for the remote boards: notices when Himalayas or Jobicy change under us.</summary>
public class LiveRemoteTests
{
    private static async Task<IReadOnlyList<SourcedListing>> SearchAsync(string source, ListingSearch search)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            ["Himalayas:MaxPages"] = "1",
            ["Jobicy:MaxPages"] = "1",
        });
        builder.Services.AddJobRadar(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var listings = (await host.Services.GetServices<IJobSource>().Single(s => s.Name == source)
            .SearchAsync(search, TestContext.Current.CancellationToken)).Listings;

        await host.StopAsync(TestContext.Current.CancellationToken);
        return listings;
    }

    [LiveFact]
    public async Task Himalayas_returns_remote_listings_that_accept_Brazil_with_their_eligibility()
    {
        var listings = await SearchAsync("himalayas", new ListingSearch(".net", "Remote", Market: JobMarket.LatamRemote));

        Assert.NotEmpty(listings);
        Assert.All(listings, l =>
        {
            Assert.Equal(Country.Remote, l.Listing.Location.Country);
            Assert.NotEqual(EligibilityScope.Unknown, RemoteEligibility.Classify(l.Listing.Eligibility));
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Title));
            Assert.NotNull(l.Listing.PostedAt);
        });
        Assert.True(listings.Count(l => ListingDescription.Normalize(l.Description) is not null) * 2 > listings.Count);
        // The accents of Brazilian ads survive the trip (they once came back as "S�nior").
        Assert.DoesNotContain(listings, l => $"{l.Listing.Title} {l.Description}".Contains('�'));
    }

    [LiveFact]
    public async Task Jobicy_returns_the_LATAM_feed_with_its_eligibility()
    {
        var listings = await SearchAsync("jobicy", new ListingSearch("developer", "Remote", Market: JobMarket.LatamRemote));

        Assert.NotEmpty(listings);
        Assert.All(listings, l =>
        {
            Assert.Equal(Country.Remote, l.Listing.Location.Country);
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Eligibility));
            Assert.Contains("developer", $"{l.Listing.Title} {l.Description}", StringComparison.OrdinalIgnoreCase);
        });
    }
}
