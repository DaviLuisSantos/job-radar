namespace JobRadar.Domain.Listings;

/// <summary>
/// A job ad as any source describes it, already translated to the neutral model.
/// Nullability follows the sources: what some sites omit is nullable here.
/// </summary>
/// <param name="Summary">Short text the source shows in results (not the full ad, which is
/// not collected).</param>
/// <param name="Eligibility">Where the employer accepts people from, as the source says it: "Worldwide", "LATAM",
/// "Brazil, Colombia, Mexico". Only remote boards give it; see <see cref="RemoteEligibility"/>.</param>
public sealed record Listing(
    ListingId Id,
    string Title,
    string? Company,
    ListingLocation Location,
    WorkType WorkType,
    string? SalaryText,
    DateTimeOffset? PostedAt,
    Uri Url,
    string? SourceCategory,
    string? Summary = null,
    string? Eligibility = null);

/// <summary>A listing as stored, with when this system first and last saw it.</summary>
public sealed record ListingRecord(
    Listing Listing,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);

/// <summary>Outcome of recording a batch of observed listings.</summary>
public readonly record struct ObservationResult(int New, int AlreadyKnown);
