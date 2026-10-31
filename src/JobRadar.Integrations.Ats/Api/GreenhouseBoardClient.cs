using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobRadar.Integrations.Ats.Api;

/// <summary>
/// The public job-board API of Greenhouse: <c>/v1/boards/{slug}/jobs</c> answers a company's whole board in one JSON,
/// and <c>/v1/boards/{slug}/jobs/{id}</c> one job with its ad. The board is asked without <c>content=true</c> (twelve
/// times the bytes) and with the tag the board sent last time: an unchanged board answers 304 with no body. Retries
/// and timeouts come from the resilience pipeline registered in <see cref="AtsServiceCollectionExtensions"/>; a
/// status that is not a success is returned, not thrown, since a 404 is the normal state of a dead board.
/// </summary>
internal sealed class GreenhouseBoardClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<GreenhouseBoardResponse> GetBoardAsync(string slug, string? etag, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/boards/{Uri.EscapeDataString(slug)}/jobs");
        if (etag is not null && EntityTagHeaderValue.TryParse(etag, out var tag))
            request.Headers.IfNoneMatch.Add(tag);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK)
            return new GreenhouseBoardResponse((int)response.StatusCode, null, null);

        // UTF-8 whatever the header says (the same care as the other sources).
        var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        return new GreenhouseBoardResponse(200, response.Headers.ETag?.ToString(), document);
    }

    /// <summary>The ad of one job as HTML; null when the job is gone. Other statuses are returned for the caller to judge.</summary>
    public async Task<(int Status, string? Content)> GetJobContentAsync(string slug, string jobId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"v1/boards/{Uri.EscapeDataString(slug)}/jobs/{Uri.EscapeDataString(jobId)}", ct);
        if (response.StatusCode != HttpStatusCode.OK)
            return ((int)response.StatusCode, null);

        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        var content = document.RootElement.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        return (200, content);
    }

    internal static GreenhouseJob? ReadJob(JsonElement element) => element.Deserialize<GreenhouseJob>(Json);
}

/// <summary>An answer of the board; on a 200 the document is owned by the caller and holds the jobs.</summary>
internal sealed class GreenhouseBoardResponse(int status, string? etag, JsonDocument? document) : IDisposable
{
    public int Status { get; } = status;
    public string? ETag { get; } = etag;

    /// <summary>Each job with its raw JSON, as the board sent it (kept as the payload).</summary>
    public IEnumerable<(GreenhouseJob Job, JsonElement Raw)> Jobs()
    {
        if (document is null || !document.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var element in jobs.EnumerateArray())
            if (GreenhouseBoardClient.ReadJob(element) is { } job)
                yield return (job, element);
    }

    public void Dispose() => document?.Dispose();
}

internal sealed record GreenhouseJob(
    long Id,
    string? Title,
    string? UpdatedAt,
    string? FirstPublished,
    string? AbsoluteUrl,
    string? CompanyName,
    GreenhouseLocation? Location,
    List<GreenhouseOffice>? Offices,
    List<GreenhouseDepartment>? Departments);

internal sealed record GreenhouseLocation(string? Name);

internal sealed record GreenhouseOffice(string? Name, string? Location);

internal sealed record GreenhouseDepartment(string? Name);
