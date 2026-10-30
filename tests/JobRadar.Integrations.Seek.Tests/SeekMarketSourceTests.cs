using JobRadar.Domain.Matching;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekMarketSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SeekMarketSource Market(FakeSeekApi api, int pageSize = 100)
    {
        var options = Options.Create(new SeekOptions { PageSize = pageSize });
        var client = new SeekSearchClient(new HttpClient(api) { BaseAddress = options.Value.BaseAddress }, options);
        var listings = new SeekJobSource(client, options, NullLogger<SeekJobSource>.Instance);
        return new SeekMarketSource(client, listings, options);
    }

    [Fact]
    public async Task Counts_read_total_count_by_subclassification_at_100_per_page()
    {
        var api = new FakeSeekApi { Respond = _ => FakeSeekApi.PageOf(418, ["1"]) };

        var total = await Market(api).CountAsync("6287,6290", "Perth WA", new FullTextTerm("night shift"), Ct);

        Assert.Equal(418, total);
        var q = Assert.Single(api.Queries);
        Assert.Equal(("6287,6290", "Perth WA", "\"night shift\"", "100", "1"), (q["subclassification"], q["where"], q["keywords"], q["pageSize"], q["page"]));
    }

    [Fact]
    public async Task Counting_with_another_page_size_is_refused()
    {
        // totalCount includes the promoted ads repeated per page: smaller pages inflate it.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Market(new FakeSeekApi(), pageSize: 20).CountAsync("1313", "Perth WA", null, Ct));
    }

    [Fact]
    public async Task Listing_a_category_pages_through_it()
    {
        var api = new FakeSeekApi { Respond = q => q["page"] == "1" ? FakeSeekApi.PageOf(2, ["1", "2"]) : FakeSeekApi.PageOf(2, []) };

        var result = await Market(api).ListAsync("1313", "Perth WA", Ct);

        Assert.Equal(["1", "2"], result.Listings.Select(l => l.Listing.Id.SourceId));
        Assert.True(result.Complete);
        Assert.Equal("1313", api.Queries[0]["subclassification"]);
    }
}
