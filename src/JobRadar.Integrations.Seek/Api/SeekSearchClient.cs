using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Api;

/// <summary>
/// Thin client over the search endpoint. Retries, backoff on 429/5xx and timeouts come from
/// the resilience pipeline registered in <see cref="SeekServiceCollectionExtensions"/>.
/// </summary>
internal sealed class SeekSearchClient(HttpClient http, IOptions<SeekOptions> options)
{
    internal const string SearchPath = "api/jobsearch/v5/search";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SeekSearchPage> SearchAsync(ListingSearch search, int page, CancellationToken ct)
    {
        using var response = await http.GetAsync(BuildUri(search, page, options.Value.PageSize), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SeekSearchResponse>(Json, ct)
            ?? throw new InvalidOperationException("Seek returned an empty body.");

        var jobs = (body.Data ?? [])
            .Select(e => (Job: e.Deserialize<SeekJob>(Json)!, Raw: e.GetRawText()))
            .ToList();
        return new SeekSearchPage(body.TotalCount, jobs);
    }

    internal static string BuildUri(ListingSearch search, int page, int pageSize)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("siteKey", "AU-Main"),
            new("sourcesystem", "houston"),
            new("keywords", search.Keywords),
            new("where", search.Location),
            new("page", page.ToString(CultureInfo.InvariantCulture)),
            new("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            new("locale", "en-AU"),
        };
        if (search.PostedWithinDays is { } days)
            query.Add(new("dateRange", days.ToString(CultureInfo.InvariantCulture)));

        return SearchPath + "?" + string.Join("&",
            query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }
}
