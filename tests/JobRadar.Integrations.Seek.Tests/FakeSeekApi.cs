using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Tests;

/// <summary>Serves recorded pages (by page number, or by any rule on the query) and records every request.</summary>
internal sealed class FakeSeekApi : HttpMessageHandler
{
    private readonly Dictionary<int, string> _pages = [];

    public List<Uri> Requests { get; } = [];

    public List<NameValueCollection> Queries => Requests.Select(r => HttpUtility.ParseQueryString(r.Query)).ToList();

    /// <summary>When set, answers every request instead of the recorded pages.</summary>
    public Func<NameValueCollection, string>? Respond { get; set; }

    public FakeSeekApi Page(int page, string fixture)
    {
        _pages[page] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return this;
    }

    /// <summary>A minimal page: the reported total and ads with these ids.</summary>
    public static string PageOf(int totalCount, IEnumerable<string> ids) =>
        $$"""{ "totalCount": {{totalCount}}, "data": [{{string.Join(",", ids.Select(id => $$"""{"id":"{{id}}","title":"Fictional"}"""))}}] }""";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var body = Respond?.Invoke(query)
            ?? (_pages.TryGetValue(int.Parse(query["page"]!), out var json) ? json : """{ "totalCount": 0, "data": [] }""");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    private (IOptions<SeekOptions> Options, SeekSearchClient Client) Client(int pageSize, int maxPages)
    {
        var options = Options.Create(new SeekOptions
        {
            PageSize = pageSize,
            MaxPages = maxPages,
            DelayBetweenPages = TimeSpan.Zero,
        });
        var http = new HttpClient(this) { BaseAddress = options.Value.BaseAddress };
        return (options, new SeekSearchClient(http, options));
    }

    public SeekJobSource Source(int pageSize = 3, int maxPages = 5)
    {
        var (options, client) = Client(pageSize, maxPages);
        return new SeekJobSource(client, options, TimeProvider.System, NullLogger<SeekJobSource>.Instance);
    }

    public SeekFullTextSearch FullText(int pageSize = 3, int maxPages = 5)
    {
        var (options, client) = Client(pageSize, maxPages);
        return new SeekFullTextSearch(client, options, TimeProvider.System, NullLogger<SeekFullTextSearch>.Instance);
    }
}
