using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Gupy.Api;

/// <summary>
/// The search the portal's own page calls (<c>portal.gupy.io/job-search</c>): open JSON, no login. Undocumented,
/// so the fields used are only the ones the translator needs. Retries and timeouts come from the resilience
/// pipeline registered in <see cref="GupyServiceCollectionExtensions"/>.
/// </summary>
internal sealed class GupySearchClient(HttpClient http, IOptions<GupyOptions> options)
{
    internal const string SearchPath = "api/job-search/jobs";

    /// <summary>The location that means "work from anywhere": the search filters by work arrangement instead.</summary>
    internal const string Remote = "Remoto";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<GupySearchPage> SearchAsync(ListingSearch search, int offset, CancellationToken ct)
    {
        using var response = await http.GetAsync(BuildUri(search, offset, options.Value.PageSize), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GupySearchResponse>(Json, ct)
            ?? throw new InvalidOperationException("Gupy returned an empty body.");
        var jobs = (body.Data ?? []).Select(e => (Job: e.Deserialize<GupyJob>(Json)!, Raw: e.GetRawText())).ToList();
        return new GupySearchPage(body.Pagination?.Total ?? jobs.Count, jobs);
    }

    /// <summary>
    /// Keywords go in <c>jobName</c>. A location is a city (<c>city</c>), "Remoto" (<c>workplaceType=remote</c>) or
    /// "Brasil", which searches the whole country.
    /// </summary>
    internal static string BuildUri(ListingSearch search, int offset, int pageSize)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("jobName", search.Keywords),
            new("limit", pageSize.ToString(CultureInfo.InvariantCulture)),
            new("offset", offset.ToString(CultureInfo.InvariantCulture)),
        };
        var location = search.Location.Trim();
        if (location.Equals(Remote, StringComparison.OrdinalIgnoreCase))
            query.Add(new("workplaceType", "remote"));
        else if (location.Length > 0 && !location.Equals("Brasil", StringComparison.OrdinalIgnoreCase))
            query.Add(new("city", location));
        return $"{SearchPath}?{string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"))}";
    }
}

internal sealed record GupySearchPage(int Total, IReadOnlyList<(GupyJob Job, string Raw)> Jobs);

internal sealed record GupySearchResponse(List<JsonElement>? Data, GupyPagination? Pagination);

internal sealed record GupyPagination(int Total, int Limit, int Offset);

internal sealed record GupyJob(
    long Id,
    string? Name,
    string? Description,
    string? CareerPageName,
    string? Type,
    DateTimeOffset? PublishedDate,
    string? WorkplaceType,
    string? City,
    string? State,
    string? JobUrl)
{
    /// <summary>Not an open vacancy: a pool of candidates the company may call one day.</summary>
    [JsonIgnore]
    public bool IsTalentPool => Type == "vacancy_type_talent_pool";
}
