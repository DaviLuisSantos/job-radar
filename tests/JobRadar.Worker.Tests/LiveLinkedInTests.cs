using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Worker.Tests;

/// <summary>
/// Like <see cref="LiveSeekTests"/>, for LinkedIn's guest endpoints: notices when the cards or the posting page change
/// the classes the translator reads. One page and one ad, to stay far from the login wall.
/// </summary>
public class LiveLinkedInTests
{
    [LiveFact]
    public async Task Real_search_returns_Australian_listings_and_an_ad_text()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            ["LinkedIn:MaxPages"] = "1",
        });
        builder.Services.AddJobRadar(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var source = host.Services.GetServices<IJobSource>().Single(s => s.Name == "linkedin");
        var listings = (await source.SearchAsync(
            new ListingSearch("developer", "Perth WA"), TestContext.Current.CancellationToken)).Listings;

        Assert.NotEmpty(listings);
        Assert.All(listings, l =>
        {
            Assert.Equal("linkedin", l.Listing.Id.Source);
            Assert.Equal(Country.AU, l.Listing.Location.Country);
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Title));
            Assert.NotNull(l.Listing.PostedAt);
            Assert.StartsWith("{", l.RawPayload);
        });
        // The search was for Perth: most cards must parse to WA. Catches a location format change.
        Assert.True(listings.Count(l => l.Listing.Location.State == "WA") * 2 > listings.Count);

        var descriptions = host.Services.GetServices<IDescriptionSource>().Single(s => s.Source == "linkedin");
        var text = await descriptions.FetchAsync(listings[0].Listing.Id.SourceId, TestContext.Current.CancellationToken);
        // The ad text is the reason to fetch the posting page: catches the markup class moving away.
        Assert.False(string.IsNullOrWhiteSpace(ListingDescription.Normalize(text)));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
