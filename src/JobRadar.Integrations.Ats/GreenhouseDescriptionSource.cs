using System.Net;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Ats.Api;

namespace JobRadar.Integrations.Ats;

/// <summary>
/// The ad of one Greenhouse job, from the job's own address on the board API. The id is "slug/id": it is never
/// shortened, for this reason. Same budget as the sweep.
/// </summary>
internal sealed class GreenhouseDescriptionSource(GreenhouseBoardClient client) : IDescriptionSource
{
    public string Source => GreenhouseListingTranslator.SourceName;

    public async Task<string?> FetchAsync(string sourceId, CancellationToken ct = default)
    {
        var slash = sourceId.LastIndexOf('/');
        if (slash <= 0 || slash == sourceId.Length - 1)
            return null;

        var (status, content) = await client.GetJobContentAsync(sourceId[..slash], sourceId[(slash + 1)..], ct);
        return status switch
        {
            // Greenhouse sends the HTML escaped ("&lt;p&gt;"): decoded once first, or the tags would survive as text.
            200 => HtmlText.ToPlain(WebUtility.HtmlDecode(content)),
            404 => null,
            403 or 429 => throw new SourceBlockedException(Source, $"HTTP {status}"),
            _ => throw new HttpRequestException($"Greenhouse answered {status} for {sourceId}", null, (HttpStatusCode)status),
        };
    }
}
