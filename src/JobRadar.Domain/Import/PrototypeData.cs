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

/// <param name="TakenAt">When the prototype took it (<c>market_runs.ran_at</c>); null in copies without the column.</param>
public sealed record PrototypeSnapshot(MarketSnapshot Snapshot, IReadOnlyList<MarketCount> Counts, DateTimeOffset? TakenAt = null);

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

    /// <summary>
    /// Market days moved to the schedule time zone. The prototype named a day by the date in Brazil, where
    /// it ran in the evening, already the next morning in Perth; this system names it by the Perth date.
    /// Kept as they were, the same morning would be two days (found 2026-10-23: the prototype's 21 Oct and
    /// this system's 22 Oct were taken half an hour apart). Each snapshot takes the zone date of when it was
    /// taken; listing first and last seen, which are prototype days, follow their snapshot. A day without a
    /// time stays as it is.
    /// </summary>
    public static PrototypeData InScheduleDays(PrototypeData data, TimeZoneInfo zone)
    {
        var days = new Dictionary<DateOnly, DateOnly>();
        foreach (var s in data.Snapshots)
            if (s.TakenAt is { } at)
                days[s.Snapshot.Date] = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
        DateOnly Move(DateOnly day) => days.GetValueOrDefault(day, day);

        return data with
        {
            Snapshots = data.Snapshots.Select(s => s with { Snapshot = s.Snapshot with { Date = Move(s.Snapshot.Date) } }).ToList(),
            MarketListings = data.MarketListings.Select(l => l with { FirstSeen = Move(l.FirstSeen), LastSeen = Move(l.LastSeen) }).ToList(),
        };
    }
}
