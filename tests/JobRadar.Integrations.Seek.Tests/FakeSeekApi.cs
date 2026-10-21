using System.Net;
using System.Text;
using System.Web;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Tests;

/// <summary>Serves recorded pages by page number and records every request.</summary>
internal sealed class FakeSeekApi : HttpMessageHandler
{
    private readonly Dictionary<int, string> _pages = [];

    public List<Uri> Requests { get; } = [];

    public FakeSeekApi Page(int page, string fixture)
    {
        _pages[page] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        var page = int.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]!);
        var body = _pages.TryGetValue(page, out var json) ? json : """{ "totalCount": 0, "data": [] }""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    public SeekJobSource Source(int pageSize = 3, int maxPages = 5)
    {
        var options = Options.Create(new SeekOptions
        {
            PageSize = pageSize,
            MaxPages = maxPages,
            DelayBetweenPages = TimeSpan.Zero,
        });
        var http = new HttpClient(this) { BaseAddress = options.Value.BaseAddress };
        return new SeekJobSource(
            new SeekSearchClient(http, options), options, TimeProvider.System, NullLogger<SeekJobSource>.Instance);
    }
}
