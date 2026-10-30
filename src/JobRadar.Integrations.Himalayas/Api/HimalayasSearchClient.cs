using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Himalayas.Api;

/// <summary>
/// The public search (<c>himalayas.app/jobs/api/search</c>): 20 per page, page-based. The market picks the filter:
/// global asks for jobs open worldwide, LATAM for jobs that accept Brazil. Retries and timeouts come from the
/// resilience pipeline registered in <see cref="HimalayasServiceCollectionExtensions"/>.
/// </summary>
internal sealed class HimalayasSearchClient(HttpClient http)
{
    internal const string SearchPath = "jobs/api/search";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<HimalayasSearchPage> SearchAsync(ListingSearch search, int page, CancellationToken ct)
    {
        using var response = await http.GetAsync(BuildUri(search, page), ct);
        response.EnsureSuccessStatusCode();

        // Read as UTF-8 whatever the header says: accents came back mangled when trusted to the defaults.
        var text = System.Text.Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(ct));
        var body = JsonSerializer.Deserialize<HimalayasSearchResponse>(text, Json)
            ?? throw new InvalidOperationException("Himalayas returned an empty body.");
        var jobs = (body.Jobs ?? []).Select(e => (Job: e.Deserialize<HimalayasJob>(Json)!, Raw: e.GetRawText())).ToList();
        return new HimalayasSearchPage(body.TotalCount ?? jobs.Count, jobs);
    }

    /// <summary>Keywords go in <c>q</c>; the location is not used: a remote job is in no city.</summary>
    internal static string BuildUri(ListingSearch search, int page)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("q", search.Keywords),
            new("page", page.ToString(CultureInfo.InvariantCulture)),
        };
        query.Add(search.Market switch
        {
            JobMarket.GlobalRemote => new("worldwide", "true"),
            JobMarket.LatamRemote => new("country", "BR"),
            _ => throw new NotSupportedException($"Himalayas does not serve the {search.Market} market."),
        });
        return $"{SearchPath}?{string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"))}";
    }
}

internal sealed record HimalayasSearchPage(int Total, IReadOnlyList<(HimalayasJob Job, string Raw)> Jobs);

internal sealed record HimalayasSearchResponse(int? TotalCount, List<JsonElement>? Jobs);

internal sealed record HimalayasJob(
    string? Title,
    string? Excerpt,
    string? CompanyName,
    string? CompanySlug,
    string? EmploymentType,
    decimal? MinSalary,
    decimal? MaxSalary,
    string? Currency,
    string? SalaryPeriod,
    List<string>? Seniority,
    List<string>? LocationRestrictions,
    List<double>? TimezoneRestrictions,
    string? Description,
    long? PubDate,
    string? ApplicationLink,
    string? Guid);
