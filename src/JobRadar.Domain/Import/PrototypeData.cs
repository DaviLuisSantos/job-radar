using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Import;

/// <summary>Everything worth keeping from the prototype store.</summary>
public sealed record PrototypeData(
    IReadOnlyList<PrototypeJob> Jobs,
    IReadOnlyList<PrototypeSnapshot> Snapshots,
    IReadOnlyList<ImportedMarketListing> MarketListings);

/// <summary>
/// One prototype job, already translated to a listing. Status, label and notes stay as the prototype
/// spelled them; <see cref="PrototypeMapping"/> decides what they become.
/// </summary>
/// <param name="Profiles">Profiles that found it (the prototype's comma-separated <c>profiles</c>).</param>
/// <param name="Status">new | reviewed | applied | interview | rejected.</param>
public sealed record PrototypeJob(
    SourcedListing Listing,
    DateTimeOffset FirstSeenAt,
    IReadOnlyList<string> Profiles,
    IReadOnlyList<string> Terms,
    string? Status,
    bool Applied,
    DateTimeOffset? AppliedAt,
    string? Notes,
    Verdict? Label,
    string? LabelNote,
    DateTimeOffset? LabeledAt);

public sealed record PrototypeSnapshot(MarketSnapshot Snapshot, IReadOnlyList<MarketCount> Counts);

/// <summary>How the prototype vocabulary maps onto this system.</summary>
public static class PrototypeMapping
{
    /// <summary>
    /// The prototype status as an application, or null for "new" (never touched). "rejected" meant the
    /// candidate rejected the listing (the prototype calibration counted it as a bad match), unless they
    /// had applied: then the employer said no, which here is Declined and still a good match.
    /// </summary>
    public static ApplicationStatus? Status(string? status, bool applied) => status?.Trim().ToLowerInvariant() switch
    {
        "reviewed" => ApplicationStatus.Reviewed,
        "applied" => ApplicationStatus.Applied,
        "interview" => ApplicationStatus.Interview,
        "rejected" => applied ? ApplicationStatus.Declined : ApplicationStatus.Dismissed,
        _ => applied ? ApplicationStatus.Applied : null,
    };
}
