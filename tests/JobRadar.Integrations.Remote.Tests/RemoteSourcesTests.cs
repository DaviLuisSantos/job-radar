using System.Net;
using System.Text;
using System.Web;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Himalayas;
using JobRadar.Integrations.Himalayas.Api;
using JobRadar.Integrations.Jobicy;
using JobRadar.Integrations.Jobicy.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Remote.Tests;

/// <summary>Serves a recorded page and records every request.</summary>
internal sealed class FakeApi(Func<Uri, string> respond) : HttpMessageHandler
{
    private readonly List<Uri> _requests = [];

    public List<Uri> Requests { get { lock (_requests) return [.. _requests]; } }

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (_requests)
            _requests.Add(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(respond(request.RequestUri!), Encoding.UTF8, "application/json"),
        });
    }
}

public class HimalayasTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (HimalayasJobSource Source, FakeApi Api) Source(Func<Uri, string>? respond = null, int maxPages = 15)
    {
        var api = new FakeApi(respond ?? (_ => FakeApi.Fixture("himalayas-search.json")));
        var options = Options.Create(new HimalayasOptions { MaxPages = maxPages });
        var http = new HttpClient(api) { BaseAddress = options.Value.BaseAddress };
        return (new HimalayasJobSource(new HimalayasSearchClient(http), options, NullLogger<HimalayasJobSource>.Instance), api);
    }

    [Fact]
    public async Task Translates_jobs_into_remote_listings_with_eligibility_time_zones_and_the_ad_as_text()
    {
        var (source, _) = Source();

        var result = await source.SearchAsync(new ListingSearch(".net", "Remote", Market: JobMarket.LatamRemote), Ct);

        Assert.True(result.Complete);
        var br = result.Listings[0];
        Assert.Equal(new ListingId("himalayas", "acme-saude/desenvolvedor-net-pleno"), br.Listing.Id);
        Assert.Equal(("Desenvolvedor .NET Pleno", "Acme Saúde"), (br.Listing.Title, br.Listing.Company));
        Assert.Equal(Country.Remote, br.Listing.Location.Country);
        Assert.Equal("Brazil", br.Listing.Eligibility);
        Assert.Equal("Empresa fictícia de saúde busca pessoa desenvolvedora. | Remote | UTC-5 to UTC-2", br.Listing.Summary);
        Assert.Equal(new TimeZoneWindow(-5, -2), ListingConditions.Read(br.Listing.Summary).TimeZones);
        Assert.Equal("Vaga fictícia.\n\n• C#\n• Azure", br.Description);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792838011), br.Listing.PostedAt);

        var world = result.Listings[1];
        Assert.Equal(RemoteEligibility.Worldwide, world.Listing.Eligibility);
        Assert.Equal("USD 4,000–6,000 per month", world.Listing.SalaryText);
        Assert.Equal(WorkType.Contract, world.Listing.WorkType);
        Assert.DoesNotContain("UTC", world.Listing.Summary);   // every time zone: no restriction
        Assert.Equal("Brazil, Colombia, Mexico", result.Listings[2].Listing.Eligibility);
        Assert.Null(result.Listings[2].Description);
    }

    [Theory]
    [InlineData(JobMarket.GlobalRemote, "worldwide=true")]
    [InlineData(JobMarket.LatamRemote, "country=BR")]
    public void The_market_picks_the_filter(JobMarket market, string filter) =>
        Assert.Equal($"jobs/api/search?q=c%23%20developer&page=1&{filter}",
            HimalayasSearchClient.BuildUri(new ListingSearch("c# developer", "Remote", Market: market), 1));

    [Fact]
    public async Task Pages_until_the_total_and_stops_at_the_cap()
    {
        static string Page(int total, int from, int count) =>
            $$"""{"totalCount":{{total}},"jobs":[{{string.Join(",", Enumerable.Range(from, count).Select(i =>
                $$"""{"title":"Dev {{i}}","guid":"https://himalayas.app/companies/c{{i}}/jobs/dev"}"""))}}]}""";
        var (source, api) = Source(u => HttpUtility.ParseQueryString(u.Query)["page"] switch
        {
            "1" => Page(30, 0, 20),
            _ => Page(30, 20, 10),
        });

        var all = await source.SearchAsync(new ListingSearch("dev", "Remote", Market: JobMarket.GlobalRemote), Ct);
        var (capped, _) = Source(_ => Page(100, 0, 20), maxPages: 2);
        var partial = await capped.SearchAsync(new ListingSearch("dev", "Remote", Market: JobMarket.GlobalRemote), Ct);

        Assert.Equal((30, true, 2), (all.Listings.Count, all.Complete, api.Requests.Count));
        Assert.False(partial.Complete);
    }

    [Fact]
    public void A_long_id_is_shortened_the_same_way_every_time()
    {
        var job = new HimalayasJob("t", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            $"https://himalayas.app/companies/a-very-long-company-name/jobs/{new string('x', 80)}");

        var id = HimalayasListingTranslator.Id(job);

        Assert.Equal(64, id.Length);
        Assert.Equal(id, HimalayasListingTranslator.Id(job));
        Assert.StartsWith("a-very-long-company-name/", id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brazil_only_markets_are_refused()
    {
        var (source, _) = Source();

        Assert.DoesNotContain(JobMarket.BR, source.Markets);
        await Assert.ThrowsAsync<NotSupportedException>(() => source.SearchAsync(new ListingSearch("x", "Rio", Market: JobMarket.BR), Ct));
    }
}

public class JobicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (JobicyJobSource Source, FakeApi Api) Source(Func<Uri, string>? respond = null)
    {
        var api = new FakeApi(respond ?? (_ => FakeApi.Fixture("jobicy-feed.json")));
        var options = Options.Create(new JobicyOptions());
        var http = new HttpClient(api) { BaseAddress = options.Value.BaseAddress };
        return (new JobicyJobSource(new JobicySearchClient(http, options), options, NullLogger<JobicyJobSource>.Instance), api);
    }

    [Fact]
    public async Task Keeps_the_jobs_whose_title_or_ad_has_every_keyword()
    {
        var (source, api) = Source();

        var result = await source.SearchAsync(new ListingSearch(".net developer", "Remote", Market: JobMarket.LatamRemote), Ct);

        Assert.Equal(["900001", "900003"], result.Listings.Select(l => l.Listing.Id.SourceId));
        Assert.Equal("jobicy.com/api/v2/remote-jobs?count=100&geo=latam&industry=engineering",
            api.Requests.Single().Host + api.Requests.Single().PathAndQuery);
    }

    [Fact]
    public async Task Translates_with_eligibility_salary_and_the_ad_as_text()
    {
        var (source, _) = Source();

        var result = await source.SearchAsync(new ListingSearch(".net", "Remote", Market: JobMarket.LatamRemote), Ct);

        var latam = result.Listings[0];
        Assert.Equal(("LATAM", "USD 60,000–80,000 per year", WorkType.FullTime), (latam.Listing.Eligibility, latam.Listing.SalaryText, latam.Listing.WorkType));
        Assert.Equal("Build services in C# and .NET.", latam.Description);
        Assert.Equal(Country.Remote, latam.Listing.Location.Country);
        Assert.Equal(RemoteEligibility.Worldwide, result.Listings[1].Listing.Eligibility);   // "Anywhere"
    }

    [Fact]
    public async Task Follows_the_cursor_until_the_last_page()
    {
        var (source, api) = Source(u => HttpUtility.ParseQueryString(u.Query)["cursor"] is null
            ? """{"hasMore":true,"nextCursor":"abc","jobs":[{"id":1,"jobTitle":"C# Dev"}]}"""
            : """{"hasMore":false,"jobs":[{"id":2,"jobTitle":"C# Dev 2"}]}""");

        var result = await source.SearchAsync(new ListingSearch("c#", "Remote", Market: JobMarket.LatamRemote), Ct);

        Assert.Equal((2, true), (result.Listings.Count, result.Complete));
        Assert.Equal("abc", HttpUtility.ParseQueryString(api.Requests[1].Query)["cursor"]);
    }
}
