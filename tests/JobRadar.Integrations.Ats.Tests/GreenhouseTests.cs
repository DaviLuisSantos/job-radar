using System.Net;
using System.Text;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Ats.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Ats.Tests;

/// <summary>The board answers a test gives: a status, a body, a tag; and the requests it saw.</summary>
internal sealed class FakeBoards(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static HttpResponseMessage Json(string body, string? etag = "W/\"abc\"")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (etag is not null)
            response.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(etag);
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(answer(request));
    }

    public GreenhouseBoardClient Client() => new(new HttpClient(this) { BaseAddress = new Uri("https://boards-api.greenhouse.io/") });
}

public class PlaceTests
{
    [Theory]
    [InlineData("Sydney, Australia", JobMarket.AU, "Sydney", "NSW")]
    [InlineData("Perth, WA", JobMarket.AU, "Perth", "WA")]
    [InlineData("Melbourne", JobMarket.AU, "Melbourne", "VIC")]
    [InlineData("São Paulo, Brazil", JobMarket.BR, "São Paulo", "SP")]
    [InlineData("Sao Paulo", JobMarket.BR, "Sao Paulo", "SP")]
    [InlineData("Curitiba, PR", JobMarket.BR, "Curitiba", "PR")]
    public void A_place_in_one_of_the_countries_is_sorted_into_its_market(string text, JobMarket market, string city, string state)
    {
        var place = AtsPlace.Parse([text]);

        Assert.NotNull(place);
        Assert.Equal(market, place.Market);
        Assert.Equal(city, place.Location.Suburb);
        Assert.Equal(state, place.Location.State);
        Assert.Null(place.Eligibility);
    }

    [Theory]
    [InlineData("Remote - Brazil", "Brazil")]
    [InlineData("Remote, Brazil", "Brazil")]
    [InlineData("Brasil (Remoto)", "Brazil")]
    [InlineData("Remote (LATAM)", "LATAM")]
    [InlineData("Remote - Latin America", "LATAM")]
    public void A_remote_job_open_to_brazil_or_latam_is_latam_remote(string text, string eligibility)
    {
        var place = AtsPlace.Parse([text]);

        Assert.NotNull(place);
        Assert.Equal(JobMarket.LatamRemote, place.Market);
        Assert.Equal(Country.Remote, place.Location.Country);
        Assert.Equal(eligibility, place.Eligibility);
        Assert.Equal(EligibilityScope.IncludesBrazil, RemoteEligibility.Classify(place.Eligibility));
    }

    [Theory]
    [InlineData("Remote - Anywhere")]
    [InlineData("Worldwide")]
    public void A_remote_job_open_to_anywhere_is_global_remote(string text)
    {
        var place = AtsPlace.Parse([text]);

        Assert.NotNull(place);
        Assert.Equal(JobMarket.GlobalRemote, place.Market);
        Assert.Equal(EligibilityScope.Worldwide, RemoteEligibility.Classify(place.Eligibility));
    }

    [Theory]
    [InlineData("Dublin")]
    [InlineData("Melbourne, FL")]
    [InlineData("Perth, Scotland")]
    [InlineData("Remote")]
    [InlineData("Remote - USA")]
    [InlineData("Remote, Germany")]
    [InlineData("San Salvador, El Salvador")]
    [InlineData("")]
    public void A_place_in_none_of_our_markets_is_dropped(string text) => Assert.Null(AtsPlace.Parse([text]));

    [Fact]
    public void The_offices_count_when_the_location_is_vague()
    {
        var place = AtsPlace.Parse(["Multiple locations", null, "Sydney, New South Wales, Australia"]);

        Assert.NotNull(place);
        Assert.Equal(JobMarket.AU, place.Market);
        Assert.Equal("NSW", place.Location.State);
    }

    [Fact]
    public void A_remote_job_in_australia_stays_in_the_australian_market()
    {
        var place = AtsPlace.Parse(["Remote - Australia"]);

        Assert.NotNull(place);
        Assert.Equal(JobMarket.AU, place.Market);
    }
}

public class TranslatorTests
{
    private static List<GreenhouseJob> Board()
    {
        using var response = new GreenhouseBoardResponse(200, null, System.Text.Json.JsonDocument.Parse(FakeBoards.Fixture("greenhouse-board.json")));
        return response.Jobs().Select(j => j.Job).ToList();
    }

    private static (Listing Listing, JobMarket Market)? Translate(long id) =>
        GreenhouseListingTranslator.ToListing("example", Board().Single(j => j.Id == id));

    [Fact]
    public void An_australian_job_becomes_a_listing_with_the_board_identity()
    {
        var (listing, market) = Translate(1001)!.Value;

        Assert.Equal(JobMarket.AU, market);
        Assert.Equal("greenhouse:example/1001", listing.Id.ToString());
        Assert.Equal("Senior .NET Developer", listing.Title);
        Assert.Equal("Example Co", listing.Company);
        Assert.Equal(Country.AU, listing.Location.Country);
        Assert.Equal("Sydney", listing.Location.Suburb);
        Assert.Equal("NSW", listing.Location.State);
        Assert.Equal("Engineering", listing.SourceCategory);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-4)), listing.PostedAt);
        Assert.Equal("https://example-co.com/careers?gh_jid=1001", listing.Url.ToString());
        Assert.Contains("Engineering", listing.Summary);
        Assert.Contains("Sydney, Australia", listing.Summary);
        Assert.Null(listing.Eligibility);
    }

    [Fact]
    public void A_remote_brazil_job_carries_who_may_apply()
    {
        var (listing, market) = Translate(1003)!.Value;

        Assert.Equal(JobMarket.LatamRemote, market);
        Assert.Equal(Country.Remote, listing.Location.Country);
        Assert.Equal("Brazil", listing.Eligibility);
    }

    [Fact]
    public void A_job_without_a_publish_date_falls_back_to_the_update_date()
    {
        var job = Board().Single(j => j.Id == 1010);

        var (listing, _) = GreenhouseListingTranslator.ToListing("example", job)!.Value;

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), listing.PostedAt);
    }

    [Theory]
    [InlineData(1006)]  // "Remote" alone
    [InlineData(1007)]  // Dublin
    [InlineData(1008)]  // Melbourne, FL
    [InlineData(1009)]  // no location at all
    public void A_job_in_none_of_our_markets_is_not_a_listing(long id) => Assert.Null(Translate(id));

    [Fact]
    public void The_url_falls_back_to_the_board_page_when_the_job_has_none()
    {
        var job = Board().Single(j => j.Id == 1001) with { AbsoluteUrl = null };

        var (listing, _) = GreenhouseListingTranslator.ToListing("example", job)!.Value;

        Assert.Equal("https://boards.greenhouse.io/example/jobs/1001", listing.Url.ToString());
    }

    [Fact]
    public void An_id_that_does_not_fit_the_column_is_refused_not_shortened()
    {
        var slug = new string('a', 60);

        Assert.Null(GreenhouseListingTranslator.IdFor(slug, 123456789));
        Assert.Null(GreenhouseListingTranslator.ToListing(slug, Board().Single(j => j.Id == 1001)));
        Assert.Equal("example/1001", GreenhouseListingTranslator.IdFor("example", 1001));
    }
}

public class ClientTests
{
    [Fact]
    public async Task The_tag_the_board_sent_is_sent_back_and_a_304_has_no_body()
    {
        var boards = new FakeBoards(r => r.Headers.IfNoneMatch.Count > 0
            ? new HttpResponseMessage(HttpStatusCode.NotModified)
            : FakeBoards.Json(FakeBoards.Fixture("greenhouse-board.json"), etag: "W/\"3f6d\""));
        var client = boards.Client();

        using var first = await client.GetBoardAsync("example", etag: null, TestContext.Current.CancellationToken);
        using var second = await client.GetBoardAsync("example", first.ETag, TestContext.Current.CancellationToken);

        Assert.Equal(200, first.Status);
        Assert.Equal("W/\"3f6d\"", first.ETag);
        Assert.Equal(10, first.Jobs().Count());
        Assert.Equal(304, second.Status);
        Assert.Empty(second.Jobs());
        Assert.Equal("W/\"3f6d\"", boards.Requests[1].Headers.IfNoneMatch.Single().ToString());
        Assert.Equal("/v1/boards/example/jobs", boards.Requests[0].RequestUri!.AbsolutePath);
        Assert.DoesNotContain("content", boards.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task A_missing_board_is_a_status_not_an_exception()
    {
        var boards = new FakeBoards(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var response = await boards.Client().GetBoardAsync("gone", null, TestContext.Current.CancellationToken);

        Assert.Equal(404, response.Status);
    }

    [Fact]
    public async Task The_ad_of_a_job_is_read_from_its_own_address()
    {
        var boards = new FakeBoards(_ => FakeBoards.Json("""{"id":1001,"content":"&lt;p&gt;Hello&lt;/p&gt;"}""", etag: null));

        var (status, content) = await boards.Client().GetJobContentAsync("example", "1001", TestContext.Current.CancellationToken);

        Assert.Equal(200, status);
        Assert.Equal("&lt;p&gt;Hello&lt;/p&gt;", content);
        Assert.Equal("/v1/boards/example/jobs/1001", boards.Requests.Single().RequestUri!.AbsolutePath);
    }
}

public class DescriptionSourceTests
{
    private static GreenhouseDescriptionSource Source(Func<HttpRequestMessage, HttpResponseMessage> answer, out FakeBoards boards)
    {
        boards = new FakeBoards(answer);
        return new GreenhouseDescriptionSource(boards.Client());
    }

    [Fact]
    public async Task The_ad_comes_back_as_plain_text()
    {
        // Greenhouse sends the HTML escaped once more inside the JSON string.
        var source = Source(_ => FakeBoards.Json("""{"content":"&lt;p&gt;Build &lt;b&gt;things&lt;/b&gt;&lt;/p&gt;"}""", null), out var boards);

        var text = await source.FetchAsync("example/1001", TestContext.Current.CancellationToken);

        Assert.Equal("Build things", text?.Trim());
        Assert.Equal("/v1/boards/example/jobs/1001", boards.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_job_that_is_gone_has_no_text() =>
        Assert.Null(await Source(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _)
            .FetchAsync("example/1001", TestContext.Current.CancellationToken));

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_refusal_stops_the_stage(HttpStatusCode status)
    {
        var source = Source(_ => new HttpResponseMessage(status), out _);

        var blocked = await Assert.ThrowsAsync<SourceBlockedException>(
            () => source.FetchAsync("example/1001", TestContext.Current.CancellationToken));

        Assert.Equal("greenhouse", blocked.SourceName);
    }

    [Fact]
    public async Task An_id_without_a_slug_has_no_text() =>
        Assert.Null(await Source(_ => new HttpResponseMessage(HttpStatusCode.OK), out _)
            .FetchAsync("1001", TestContext.Current.CancellationToken));
}

public class BoardListTests
{
    [Fact]
    public void The_shipped_list_is_unique_and_made_of_slug_characters()
    {
        var boards = AtsBoardList.Greenhouse();

        Assert.True(boards.Count > 5000);
        Assert.All(boards, b => Assert.Equal("greenhouse", b.Platform));
        Assert.Equal(boards.Count, boards.Select(b => b.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.All(boards, b => Assert.Matches("^[a-z0-9][a-z0-9_-]+$", b.Slug));
        Assert.Contains(boards, b => b.Slug == "stripe");
    }
}
