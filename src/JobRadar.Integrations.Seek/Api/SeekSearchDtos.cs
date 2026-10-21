using System.Text.Json;

namespace JobRadar.Integrations.Seek.Api;

// Shape of GET /api/jobsearch/v5/search (undocumented). Only the fields this system reads;
// everything else stays in the raw payload.

internal sealed record SeekSearchResponse(int TotalCount, IReadOnlyList<JsonElement>? Data);

internal sealed record SeekJob(
    string Id,
    string? Title,
    string? CompanyName,
    SeekAdvertiser? Advertiser,
    IReadOnlyList<SeekLocation>? Locations,
    IReadOnlyList<string>? WorkTypes,
    string? SalaryLabel,
    DateTimeOffset? ListingDate,
    IReadOnlyList<SeekClassificationPair>? Classifications,
    string? DisplayType,
    string? Teaser = null,
    IReadOnlyList<string>? BulletPoints = null,
    SeekWorkArrangements? WorkArrangements = null);

internal sealed record SeekWorkArrangements(string? DisplayText);

internal sealed record SeekAdvertiser(string? Id, string? Description);

internal sealed record SeekLocation(string? Label, string? CountryCode);

internal sealed record SeekClassificationPair(SeekClassification? Classification, SeekClassification? Subclassification);

internal sealed record SeekClassification(string? Id, string? Description);

/// <summary>One page: the total the API reports and each ad with its verbatim JSON.</summary>
internal sealed record SeekSearchPage(int TotalCount, IReadOnlyList<(SeekJob Job, string Raw)> Jobs);
