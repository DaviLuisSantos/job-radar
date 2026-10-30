using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Gupy.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Gupy.Tests;

/// <summary>Serves a recorded page, or pages by offset, and records every request.</summary>
internal sealed class FakeGupyApi : HttpMessageHandler
{
    private readonly List<Uri> _requests = [];

    public List<Uri> Requests { get { lock (_requests) return [.. _requests]; } }

    public List<NameValueCollection> Queries => Requests.Select(r => HttpUtility.ParseQueryString(r.Query)).ToList();

    public Func<NameValueCollection, string> Respond { get; set; } = _ => Fixture("search.json");

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A minimal page: the reported total and vacancies with these ids.</summary>
    public static string PageOf(int total, IEnumerable<int> ids) =>
        $$"""{ "data": [{{string.Join(",", ids.Select(id => $$"""{"id":{{id}},"name":"Fictícia","type":"vacancy_type_effective"}"""))}}], "pagination": { "total": {{total}} } }""";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (_requests)
            _requests.Add(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Respond(HttpUtility.ParseQueryString(request.RequestUri!.Query)), Encoding.UTF8, "application/json"),
        });
    }

    public GupyJobSource Source(int pageSize = 100, int maxPages = 10)
    {
        var options = Options.Create(new GupyOptions { PageSize = pageSize, MaxPages = maxPages });
        var http = new HttpClient(this) { BaseAddress = options.Value.BaseAddress };
        return new GupyJobSource(new GupySearchClient(http, options), options, NullLogger<GupyJobSource>.Instance);
    }
}

public class GupyTests
{
    private static readonly ListingSearch Rio = new("desenvolvedor .net", "Rio de Janeiro", Market: JobMarket.BR);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Translates_vacancies_into_Brazilian_listings_with_the_full_ad()
    {
        var api = new FakeGupyApi();

        var result = await api.Source().SearchAsync(Rio, Ct);

        var remote = result.Listings[0];
        Assert.Equal(new ListingId("gupy", "90000001"), remote.Listing.Id);
        Assert.Equal("Desenvolvedor(a) .NET Pleno", remote.Listing.Title);
        Assert.Equal("Empresa Fictícia", remote.Listing.Company);
        Assert.Equal(ListingLocation.Unknown(Country.BR), remote.Listing.Location);
        Assert.Equal(WorkType.FullTime, remote.Listing.WorkType);
        Assert.Equal("Remoto · CLT", remote.Listing.Summary);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), remote.Listing.PostedAt);
        Assert.Contains("3+ anos de experiência", remote.Description);
        Assert.Equal(90000001, System.Text.Json.JsonDocument.Parse(remote.RawPayload).RootElement.GetProperty("id").GetInt64());

        var hybrid = result.Listings[1].Listing;
        Assert.Equal(new ListingLocation(Country.BR, null, "Rio de Janeiro", "RJ", null), hybrid.Location);
        Assert.Equal(WorkType.Contract, hybrid.WorkType);
        Assert.Equal("Híbrido · PJ", hybrid.Summary);
    }

    [Fact]
    public async Task A_talent_pool_is_not_an_open_vacancy()
    {
        var result = await new FakeGupyApi().Source().SearchAsync(Rio, Ct);

        Assert.DoesNotContain(result.Listings, l => l.Listing.Id.SourceId == "90000003");
        Assert.Equal(2, result.Listings.Count);
        Assert.True(result.Complete);
    }

    [Theory]
    [InlineData("Rio de Janeiro", "Rio de Janeiro", null)]
    [InlineData("Remoto", null, "remote")]
    [InlineData("Brasil", null, null)]
    public async Task The_location_becomes_a_city_the_remote_filter_or_the_whole_country(string location, string? city, string? workplace)
    {
        var api = new FakeGupyApi();

        await api.Source().SearchAsync(new ListingSearch("c#", location, Market: JobMarket.BR), Ct);

        var query = Assert.Single(api.Queries);
        Assert.Equal("c#", query["jobName"]);
        Assert.Equal(city, query["city"]);
        Assert.Equal(workplace, query["workplaceType"]);
    }

    [Fact]
    public async Task Pages_by_offset_until_the_total_and_returns_each_vacancy_once()
    {
        var api = new FakeGupyApi
        {
            Respond = q => q["offset"] switch
            {
                "0" => FakeGupyApi.PageOf(5, [1, 2]),
                "2" => FakeGupyApi.PageOf(5, [2, 3]),
                _ => FakeGupyApi.PageOf(5, [4]),
            },
        };

        var result = await api.Source(pageSize: 2).SearchAsync(Rio, Ct);

        Assert.Equal(["1", "2", "3", "4"], result.Listings.Select(l => l.Listing.Id.SourceId));
        Assert.Equal(["0", "2", "4"], api.Queries.Select(q => q["offset"]));
        Assert.True(result.Complete);
    }

    [Fact]
    public async Task Stops_at_the_page_cap_and_says_the_search_is_incomplete()
    {
        var api = new FakeGupyApi { Respond = q => FakeGupyApi.PageOf(100, [int.Parse(q["offset"]!) + 1, int.Parse(q["offset"]!) + 2]) };

        var result = await api.Source(pageSize: 2, maxPages: 3).SearchAsync(Rio, Ct);

        Assert.Equal(3, api.Requests.Count);
        Assert.False(result.Complete);
    }

    [Theory]
    [InlineData("São Paulo", "SP")]
    [InlineData("distrito federal", "DF")]
    [InlineData("", null)]
    [InlineData("Lisboa", null)]
    public void States_are_written_as_two_letter_codes(string state, string? code) =>
        Assert.Equal(code, GupyListingTranslator.StateCode(state));
}
