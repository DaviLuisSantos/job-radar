using System.Text.Json;
using System.Web;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekJobSourceTests
{
    private static readonly ListingSearch Search = new(".net developer", "Perth");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<SourcedListing>> CollectAsync(SeekJobSource source, ListingSearch? search = null) =>
        (await source.SearchAsync(search ?? Search, Ct)).Listings;

    [Fact]
    public async Task Pages_until_the_total_is_reached_and_returns_each_ad_once()
    {
        var api = new FakeSeekApi().Page(1, "search-page1.json").Page(2, "search-page2.json");

        var result = await api.Source().SearchAsync(Search, Ct);

        // 5 items over two pages, the promoted ad repeated on page 2.
        Assert.Equal(["90000001", "90000002", "90000003", "90000004"], result.Listings.Select(l => l.Listing.Id.SourceId));
        Assert.True(result.Complete);
        Assert.Equal(2, api.Requests.Count);
    }

    [Fact]
    public async Task A_short_page_ends_the_search()
    {
        var api = new FakeSeekApi().Page(1, "search-page2.json");

        var listings = await CollectAsync(api.Source(pageSize: 3));

        Assert.Equal(2, listings.Count);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Stops_at_the_page_cap_and_reports_the_result_as_incomplete()
    {
        var api = new FakeSeekApi().Page(1, "search-page1.json").Page(2, "search-page1.json");

        var result = await api.Source(maxPages: 1).SearchAsync(Search, Ct);

        Assert.False(result.Complete);
        Assert.Equal(3, result.Listings.Count);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Keeps_the_verbatim_payload_of_each_ad()
    {
        var api = new FakeSeekApi().Page(1, "search-page1.json").Page(2, "search-page2.json");

        var first = (await CollectAsync(api.Source()))[0];

        using var raw = JsonDocument.Parse(first.RawPayload);
        Assert.Equal("90000001", raw.RootElement.GetProperty("id").GetString());
        // Fields the translator ignores are still there for later reprocessing...
        Assert.Equal("Hybrid", raw.RootElement.GetProperty("workArrangements").GetProperty("displayText").GetString());
        // ...but not the per-request ones.
        Assert.False(raw.RootElement.TryGetProperty("tracking", out _));
        Assert.False(raw.RootElement.TryGetProperty("solMetadata", out _));
        Assert.False(raw.RootElement.TryGetProperty("listingDateDisplay", out _));
    }

    [Fact]
    public void The_same_ad_from_two_requests_has_the_same_payload()
    {
        // Real shape: each request stamps its own tokens; "2d ago" changes daily.
        const string first = """{"id":"1","title":"Chef","tracking":"ZXdv1","listingDateDisplay":"2d ago","solMetadata":{"token":"0~aaa","sectionRank":8}}""";
        const string second = """{"id":"1","title":"Chef","tracking":"ZXdv2","listingDateDisplay":"3d ago","solMetadata":{"token":"0~bbb","sectionRank":10}}""";

        static string Stable(string json) => Api.SeekSearchClient.StableRaw(JsonDocument.Parse(json).RootElement);

        Assert.Equal(Stable(first), Stable(second));
        Assert.Equal("""{"id":"1","title":"Chef"}""", Stable(first));
    }

    [Fact]
    public async Task Sends_the_search_and_the_date_window()
    {
        var api = new FakeSeekApi().Page(1, "search-page2.json");

        await CollectAsync(api.Source(), Search with { PostedWithinDays = 3 });

        var query = HttpUtility.ParseQueryString(api.Requests[0].Query);
        Assert.Equal(".net developer", query["keywords"]);
        Assert.Equal("Perth", query["where"]);
        Assert.Equal("3", query["dateRange"]);
        Assert.Equal("3", query["pageSize"]);
    }

    [Fact]
    public async Task No_date_window_means_no_dateRange_parameter()
    {
        var api = new FakeSeekApi().Page(1, "search-page2.json");

        await CollectAsync(api.Source());

        Assert.Null(HttpUtility.ParseQueryString(api.Requests[0].Query)["dateRange"]);
    }
}
