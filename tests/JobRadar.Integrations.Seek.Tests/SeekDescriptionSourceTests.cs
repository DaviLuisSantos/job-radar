using System.Net;
using System.Text;
using System.Text.Json;
using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Seek.Api;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekDescriptionSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Answers every request with one response; records the bodies sent.</summary>
    private sealed class OneAnswer(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(ct)));
            return answer();
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (SeekDescriptionSource Source, OneAnswer Handler) Source(Func<HttpResponseMessage> answer)
    {
        var handler = new OneAnswer(answer);
        var http = new HttpClient(handler) { BaseAddress = new SeekOptions().BaseAddress };
        return (new SeekDescriptionSource(new SeekJobDetailsClient(http)), handler);
    }

    [Fact]
    public async Task Asks_graphql_for_the_job_and_returns_the_ad_as_plain_text()
    {
        var (source, handler) = Source(() => Json("""
            {"data":{"jobDetails":{"job":{"id":"123","content":"<p><strong>Labourer</strong></p><ul><li>Own car &amp; licence</li><li>Min 2 years</li></ul>"}}}}
            """));

        var text = await source.FetchAsync("123", Ct);

        // Trailing blank lines are left for ListingDescription.Normalize, which runs before saving.
        Assert.Equal("Labourer\n\n• Own car & licence\n\n• Min 2 years", text!.TrimEnd());
        var (uri, body) = Assert.Single(handler.Sent);
        Assert.Equal("https://www.seek.com.au/graphql", uri.ToString());
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("123", sent.RootElement.GetProperty("variables").GetProperty("jobId").GetString());
    }

    [Theory]
    [InlineData("""{"data":{"jobDetails":null}}""")]
    [InlineData("""{"data":null,"errors":[{"message":"not found"}]}""")]
    public async Task An_ad_seek_no_longer_has_is_null(string body)
    {
        var (source, _) = Source(() => Json(body));

        Assert.Null(await source.FetchAsync("123", Ct));
    }

    [Fact]
    public async Task A_challenge_a_403_or_an_html_page_is_a_refusal()
    {
        var challenge = Json("{}");
        challenge.Headers.Add("cf-mitigated", "challenge");

        foreach (var answer in new[]
                 {
                     challenge,
                     Json("{}", HttpStatusCode.Forbidden),
                     new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Just a moment...</html>", Encoding.UTF8, "text/html") },
                 })
        {
            var (source, _) = Source(() => answer);
            var ex = await Assert.ThrowsAsync<SourceBlockedException>(() => source.FetchAsync("123", Ct));
            Assert.Equal("seek", ex.SourceName);
        }
    }
}
