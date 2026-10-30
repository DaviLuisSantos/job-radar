using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.LinkedIn.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Integrations.LinkedIn.Tests;

/// <summary>Serves recorded pages, or pages by offset, and records every request.</summary>
internal sealed class FakeLinkedIn : HttpMessageHandler
{
    private readonly List<Uri> _requests = [];

    public List<Uri> Requests { get { lock (_requests) return [.. _requests]; } }

    public List<NameValueCollection> Queries => Requests.Select(r => HttpUtility.ParseQueryString(r.Query)).ToList();

    /// <summary>The answer to a request; its status and where it landed (a redirect) can be set.</summary>
    public Func<HttpRequestMessage, (HttpStatusCode Status, string Body, Uri? Landed)> Respond { get; set; } =
        r => (HttpStatusCode.OK, r.RequestUri!.AbsolutePath.Contains("jobPosting") ? Fixture("posting.html") : Fixture("search.html"), null);

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A page of minimal cards in Australia with these ids.</summary>
    public static string PageOf(IEnumerable<int> ids, string location = "Perth, Western Australia, Australia") => string.Concat(ids.Select(id => $"""
        <li><div class="base-card job-search-card" data-entity-urn="urn:li:jobPosting:{id}">
          <a class="base-card__full-link" href="https://au.linkedin.com/jobs/view/job-{id}?position=1"></a>
          <h3 class="base-search-card__title">Job {id}</h3>
          <span class="job-search-card__location">{location}</span>
          <time class="job-search-card__listdate" datetime="2026-10-29">1 day ago</time>
        </div></li>
        """));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (_requests)
            _requests.Add(request.RequestUri!);
        var (status, body, landed) = Respond(request);
        if (landed is not null)
            request.RequestUri = landed;   // what HttpClient does after following a redirect
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/html"),
            RequestMessage = request,
        });
    }

    public LinkedInGuestClient Client(LinkedInOptions? options = null)
    {
        var o = Options.Create(options ?? new LinkedInOptions());
        return new LinkedInGuestClient(new HttpClient(this) { BaseAddress = o.Value.BaseAddress }, new LinkedInPacing(o), Time);
    }

    public LinkedInJobSource Source(int maxPages = 40, List<JobMarket>? markets = null)
    {
        var o = new LinkedInOptions { MaxPages = maxPages };
        if (markets is not null)
            o.Markets = markets;
        return new LinkedInJobSource(Client(o), Options.Create(o), NullLogger<LinkedInJobSource>.Instance);
    }
}

public class LinkedInTests
{
    private static readonly ListingSearch Perth = new(".net developer", "Perth WA");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Translates_cards_into_Australian_listings_and_leaves_out_the_ones_from_elsewhere()
    {
        var result = await new FakeLinkedIn().Source().SearchAsync(Perth, Ct);

        Assert.Equal(["4000000001", "4000000002"], result.Listings.Select(l => l.Listing.Id.SourceId));
        var first = result.Listings[0].Listing;
        Assert.Equal(new ListingId("linkedin", "4000000001"), first.Id);
        Assert.Equal(".NET Developer & Azure", first.Title);
        Assert.Equal("Acme Fictional Pty Ltd", first.Company);
        Assert.Equal(new ListingLocation(Country.AU, "Osborne Park", null, "WA", null), first.Location);
        Assert.Equal("A$120,000.00 - A$140,000.00", first.SalaryText);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero), first.PostedAt);
        Assert.Equal(new Uri("https://au.linkedin.com/jobs/view/net-developer-at-acme-4000000001"), first.Url);
        Assert.Null(first.Summary);
        Assert.Null(result.Listings[0].Description);

        Assert.Equal(new ListingLocation(Country.AU, null, "Perth", null, null), result.Listings[1].Listing.Location);
        Assert.True(result.Complete);
    }

    [Fact]
    public async Task The_payload_is_the_card_as_json_without_what_changes_every_day()
    {
        var first = (await new FakeLinkedIn().Source().SearchAsync(Perth, Ct)).Listings[0];

        var payload = JsonDocument.Parse(first.RawPayload).RootElement;
        Assert.Equal("4000000001", payload.GetProperty("id").GetString());
        Assert.Equal("2026-10-29", payload.GetProperty("postedOn").GetString());
        // "Be an early applicant" comes and goes: kept, it would store a new payload on every pass.
        Assert.DoesNotContain("applicant", first.RawPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trackingId", first.RawPayload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Perth WA", JobMarket.AU, "Perth WA", null)]
    [InlineData("All Australia", JobMarket.AU, "Australia", null)]
    [InlineData("Rio de Janeiro", JobMarket.BR, "Rio de Janeiro, Brazil", null)]
    [InlineData("Brasil", JobMarket.BR, "Brazil", null)]
    [InlineData("Remoto", JobMarket.BR, "Brazil", "2")]
    public void The_location_is_one_LinkedIn_resolves(string location, JobMarket market, string sent, string? workplace)
    {
        var query = HttpUtility.ParseQueryString(
            LinkedInGuestClient.BuildUri(new ListingSearch("c#", location, PostedWithinDays: 2, Market: market), 20).Split('?')[1]);

        Assert.Equal("c#", query["keywords"]);
        Assert.Equal(sent, query["location"]);
        Assert.Equal(workplace, query["f_WT"]);
        Assert.Equal("r172800", query["f_TPR"]);
        Assert.Equal("DD", query["sortBy"]);
        Assert.Equal("20", query["start"]);
    }

    [Fact]
    public void Without_a_window_the_search_has_no_date_filter() =>
        Assert.DoesNotContain("f_TPR", LinkedInGuestClient.BuildUri(Perth, 0));

    [Fact]
    public async Task A_remote_search_in_Brazil_says_so_in_the_summary_and_reads_the_state()
    {
        var api = new FakeLinkedIn { Respond = _ => (HttpStatusCode.OK, FakeLinkedIn.PageOf([7], "Campinas, São Paulo, Brazil"), null) };

        var result = await api.Source().SearchAsync(new ListingSearch("desenvolvedor .net", "Remoto", Market: JobMarket.BR), Ct);

        var listing = Assert.Single(result.Listings).Listing;
        Assert.Equal(new ListingLocation(Country.BR, "Campinas", null, "SP", null), listing.Location);
        Assert.Equal("Remoto", listing.Summary);
    }

    [Fact]
    public async Task Pages_by_ten_until_a_short_page_and_returns_each_listing_once()
    {
        var api = new FakeLinkedIn
        {
            Respond = r => (HttpStatusCode.OK, HttpUtility.ParseQueryString(r.RequestUri!.Query)["start"] switch
            {
                "0" => FakeLinkedIn.PageOf(Enumerable.Range(1, 10)),
                "10" => FakeLinkedIn.PageOf(Enumerable.Range(10, 10)),
                _ => FakeLinkedIn.PageOf([20, 21]),
            }, null),
        };

        var result = await api.Source().SearchAsync(Perth, Ct);

        Assert.Equal(Enumerable.Range(1, 21).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            result.Listings.Select(l => l.Listing.Id.SourceId));
        Assert.Equal(["0", "10", "20"], api.Queries.Select(q => q["start"]));
        Assert.True(result.Complete);
    }

    [Fact]
    public async Task Stops_at_the_page_cap_and_says_the_search_is_incomplete()
    {
        var api = new FakeLinkedIn
        {
            Respond = r => (HttpStatusCode.OK,
                FakeLinkedIn.PageOf(Enumerable.Range(int.Parse(HttpUtility.ParseQueryString(r.RequestUri!.Query)["start"]!) + 1, 10)), null),
        };

        var result = await api.Source(maxPages: 3).SearchAsync(Perth, Ct);

        Assert.Equal(3, api.Requests.Count);
        Assert.Equal(30, result.Listings.Count);
        Assert.False(result.Complete);
    }

    [Fact]
    public async Task A_location_LinkedIn_does_not_resolve_fails_the_search_instead_of_returning_another_country()
    {
        var api = new FakeLinkedIn { Respond = _ => (HttpStatusCode.OK, FakeLinkedIn.PageOf([1, 2], "Atlanta, GA"), null) };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.Source().SearchAsync(new ListingSearch("c#", "Xyzzyland"), Ct));

        Assert.Contains("Xyzzyland", error.Message);
    }

    [Fact]
    public void Serves_the_countries_it_is_configured_for_and_never_a_remote_market()
    {
        Assert.Equal([JobMarket.AU, JobMarket.BR], new FakeLinkedIn().Source().Markets.Order());
        Assert.Equal([JobMarket.BR], new FakeLinkedIn().Source(markets: [JobMarket.BR, JobMarket.LatamRemote]).Markets);
    }

    [Fact]
    public async Task The_ad_is_the_body_as_text_with_the_pay_and_the_criteria()
    {
        var text = await new LinkedInDescriptionSource(new FakeLinkedIn().Client()).FetchAsync("4000000001", Ct);

        Assert.NotNull(text);
        Assert.StartsWith("About the role", text);
        Assert.Contains("• 3+ years of experience with ASP.NET Core", text);
        Assert.Contains("• SQL Server & Azure", text);
        Assert.Contains("Hybrid, two days in the office.", text);
        Assert.DoesNotContain("Show more", text);
        Assert.EndsWith("Pay: A$120,000.00/yr - A$140,000.00/yr\nSeniority level: Mid-Senior level\nEmployment type: Full-time", text);
    }

    [Fact]
    public async Task An_ad_LinkedIn_no_longer_has_is_null()
    {
        var api = new FakeLinkedIn { Respond = _ => (HttpStatusCode.NotFound, "", null) };

        Assert.Null(await new LinkedInDescriptionSource(api.Client()).FetchAsync("1", Ct));
    }

    [Theory]
    [InlineData(429, null)]
    [InlineData(999, null)]
    [InlineData(200, "https://www.linkedin.com/authwall?trk=gf")]
    public async Task A_refusal_stops_every_request_for_hours_without_asking_again(int status, string? landed)
    {
        var api = new FakeLinkedIn { Respond = _ => ((HttpStatusCode)status, "", landed is null ? null : new Uri(landed)) };
        var client = api.Client();

        await Assert.ThrowsAsync<SourceBlockedException>(() => client.SearchAsync(Perth, 0, Ct));
        api.Respond = _ => (HttpStatusCode.OK, FakeLinkedIn.Fixture("search.html"), null);
        var again = await Assert.ThrowsAsync<SourceBlockedException>(() => client.GetPostingAsync("1", Ct));

        Assert.Single(api.Requests);
        Assert.Equal("linkedin", again.SourceName);

        api.Time.Advance(LinkedInGuestClient.BlockedFor);
        Assert.NotEmpty(await client.SearchAsync(Perth, 0, Ct));
    }

    [Theory]
    [InlineData("Osborne Park, Western Australia, Australia", "Osborne Park", null, "WA")]
    [InlineData("Western Australia, Australia", null, null, "WA")]
    [InlineData("Australia", null, null, null)]
    [InlineData("Greater Sydney Area", null, "Sydney", null)]
    public void Australian_places_become_suburb_region_and_state(string text, string? suburb, string? region, string? state) =>
        Assert.Equal(new ListingLocation(Country.AU, suburb, region, state, null), LinkedInLocation.Parse(text, Country.AU));

    [Theory]
    [InlineData("Brasília, Federal District, Brazil", "Brasília", "DF")]
    [InlineData("Rio de Janeiro, Rio de Janeiro, Brazil", "Rio de Janeiro", "RJ")]
    public void Brazilian_states_are_written_as_two_letter_codes(string text, string suburb, string state) =>
        Assert.Equal(new ListingLocation(Country.BR, suburb, null, state, null), LinkedInLocation.Parse(text, Country.BR));

    [Theory]
    [InlineData("Columbus, OH")]
    [InlineData("São Paulo, Brazil")]
    public void A_place_in_another_country_is_not_this_market(string text) =>
        Assert.Null(LinkedInLocation.Parse(text, Country.AU));
}
