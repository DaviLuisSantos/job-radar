using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Worker.Tests;

/// <summary>Like <see cref="LiveSeekTests"/>, for Gupy: notices when the portal changes its search under us.</summary>
public class LiveGupyTests
{
    [LiveFact]
    public async Task Real_search_returns_Brazilian_listings_with_the_full_ad()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            ["Gupy:PageSize"] = "5",
            ["Gupy:MaxPages"] = "1",
        });
        builder.Services.AddJobRadar(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var source = host.Services.GetServices<IJobSource>().Single(s => s.Name == "gupy");
        var listings = (await source.SearchAsync(
            new ListingSearch("desenvolvedor", "Brasil", Market: JobMarket.BR), TestContext.Current.CancellationToken)).Listings;

        Assert.NotEmpty(listings);
        Assert.All(listings, l =>
        {
            Assert.Equal(Country.BR, l.Listing.Location.Country);
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Title));
            Assert.NotNull(l.Listing.PostedAt);
            Assert.StartsWith("{", l.RawPayload);
        });
        // The point of the source: the whole ad comes with the search. Catches the field moving away.
        Assert.True(listings.Count(l => ListingDescription.Normalize(l.Description) is not null) * 2 > listings.Count);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
