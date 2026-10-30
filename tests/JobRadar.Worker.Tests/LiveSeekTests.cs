using System.Runtime.CompilerServices;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Worker.Tests;

/// <summary>
/// Hits a real source API (Seek, Gupy) through the real composition. Opt-in and never in CI: it depends on
/// a third party, and its job is to notice when a source changes its API under us.
/// </summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("JOBRADAR_LIVE_TESTS") != "1")
            Skip = "Set JOBRADAR_LIVE_TESTS=1 to call the real Seek and Gupy APIs.";
    }
}

public class LiveSeekTests
{
    [LiveFact]
    public async Task Real_search_returns_translated_listings()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            ["Seek:PageSize"] = "5",
            ["Seek:MaxPages"] = "1",
        });
        builder.Services.AddJobRadar(builder.Configuration);
        using var host = builder.Build();
        // Runs ValidateOnStart, like production boot.
        await host.StartAsync(TestContext.Current.CancellationToken);

        var source = host.Services.GetServices<IJobSource>().Single(s => s.Name == "seek");
        var listings = (await source.SearchAsync(new ListingSearch("developer", "Perth"), TestContext.Current.CancellationToken)).Listings;

        Assert.NotEmpty(listings);
        Assert.All(listings, l =>
        {
            Assert.Equal("seek", l.Listing.Id.Source);
            Assert.False(string.IsNullOrWhiteSpace(l.Listing.Title));
            Assert.NotNull(l.Listing.PostedAt);
            Assert.StartsWith("{", l.RawPayload);
        });
        // The search was for Perth: most ads must parse to WA. Catches a label format change.
        Assert.True(listings.Count(l => l.Listing.Location.State == "WA") * 2 > listings.Count);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
