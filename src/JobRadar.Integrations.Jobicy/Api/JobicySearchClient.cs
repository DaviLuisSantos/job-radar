using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Jobicy.Api;

/// <summary>
/// The public feed (<c>jobicy.com/api/v2/remote-jobs</c>): jobs of the last 7 days, one region and industry, paged by
/// a cursor that lasts a day. Retries and timeouts come from the resilience pipeline registered in
/// <see cref="JobicyServiceCollectionExtensions"/>.
/// </summary>
internal sealed class JobicySearchClient(HttpClient http, IOptions<JobicyOptions> options)
{
    internal const string FeedPath = "api/v2/remote-jobs";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<JobicyPage> PageAsync(string? cursor, CancellationToken ct)
    {
        using var response = await http.GetAsync(BuildUri(options.Value, cursor), ct);
        response.EnsureSuccessStatusCode();

        var text = System.Text.Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(ct));
        var body = JsonSerializer.Deserialize<JobicyResponse>(text, Json)
            ?? throw new InvalidOperationException("Jobicy returned an empty body.");
        var jobs = (body.Jobs ?? []).Select(e => (Job: e.Deserialize<JobicyJob>(Json)!, Raw: e.GetRawText())).ToList();
        return new JobicyPage(jobs, body.HasMore == true ? body.NextCursor : null);
    }

    internal static string BuildUri(JobicyOptions o, string? cursor)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("count", o.PageSize.ToString(CultureInfo.InvariantCulture)),
            new("geo", o.Geo),
            new("industry", o.Industry),
        };
        if (cursor is not null)
            query.Add(new("cursor", cursor));
        return $"{FeedPath}?{string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"))}";
    }
}

/// <param name="NextCursor">Null on the last page.</param>
internal sealed record JobicyPage(IReadOnlyList<(JobicyJob Job, string Raw)> Jobs, string? NextCursor);

internal sealed record JobicyResponse(List<JsonElement>? Jobs, bool? HasMore, string? NextCursor);

internal sealed record JobicyJob(
    long Id,
    string? Url,
    string? JobTitle,
    string? CompanyName,
    List<string>? JobType,
    string? JobGeo,
    string? JobLevel,
    string? JobExcerpt,
    string? JobDescription,
    DateTimeOffset? PubDate,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string? SalaryPeriod);
