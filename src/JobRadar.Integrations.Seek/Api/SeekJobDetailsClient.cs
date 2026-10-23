using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobRadar.Application.Abstractions;

namespace JobRadar.Integrations.Seek.Api;

/// <summary>
/// The ad text, from the GraphQL endpoint Seek's own site loads it from. Goes through the same
/// resilience pipeline and request budget as the search (see <see cref="SeekServiceCollectionExtensions"/>).
/// Never the ad page: that one sits behind an anti-bot challenge.
/// </summary>
internal sealed class SeekJobDetailsClient(HttpClient http)
{
    internal const string GraphQlPath = "graphql";

    internal const string Query = "query jobDetails($jobId: ID!) { jobDetails(id: $jobId) { job { id content(platform: WEB) } } }";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The ad body as HTML; null when Seek no longer has the ad.</summary>
    /// <exception cref="SourceBlockedException">Seek turned the request away: challenge, 403, or 429 past the retries.</exception>
    public async Task<string?> GetContentAsync(string jobId, CancellationToken ct)
    {
        var body = new { operationName = "jobDetails", variables = new { jobId }, query = Query };
        using var response = await http.PostAsJsonAsync(GraphQlPath, body, Json, ct);

        if (response.Headers.TryGetValues("cf-mitigated", out var mitigated))
            throw new SourceBlockedException(SeekListingTranslator.SourceName, $"challenge: {string.Join(",", mitigated)}");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new SourceBlockedException(SeekListingTranslator.SourceName, $"HTTP {(int)response.StatusCode}");
        response.EnsureSuccessStatusCode();
        // A challenge page instead of data, whatever the status.
        if (response.Content.Headers.ContentType?.MediaType is not "application/json")
            throw new SourceBlockedException(SeekListingTranslator.SourceName, $"answered {response.Content.Headers.ContentType?.MediaType ?? "no content type"}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return doc.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("jobDetails", out var details) && details.ValueKind == JsonValueKind.Object
            && details.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object
            && job.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString()
                : null;
    }
}
