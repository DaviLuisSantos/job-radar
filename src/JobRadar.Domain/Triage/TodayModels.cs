using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Triage;

/// <summary>A status change of an application, with what the "Today" page shows of its listing.</summary>
public sealed record RecentStatusChange(ListingId Id, string Title, string? Company, ApplicationStatus? From, ApplicationStatus To, DateTimeOffset At);

/// <summary>What one profile has to triage from the last week: how many, and the best of them.</summary>
public sealed record ProfileToday(string Profile, int New, IReadOnlyList<TriageRow> Best)
{
    /// <summary>How many of them arrived since the last visit to the market.</summary>
    public int Fresh { get; init; }
}

/// <summary>
/// The "Today" page of one market: the listings of the last week nobody triaged yet. A listing stays until it is
/// triaged (a label or a status) or older than <see cref="Window"/>: opening the page, or another market, takes
/// nothing away. The last visit only marks what arrived since.
/// </summary>
/// <param name="Since">Start of the window, a week back.</param>
/// <param name="LastVisit">The previous visit to the market, if any.</param>
public sealed record MarketToday(
    JobMarket Market,
    DateTimeOffset Since,
    DateTimeOffset? LastVisit,
    IReadOnlyList<ProfileToday> Profiles,
    IReadOnlyList<RecentStatusChange> Changes)
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>Distinct listings to triage: a job two profiles found counts once.</summary>
    public int New { get; init; }

    /// <summary>Distinct listings that arrived since the last visit.</summary>
    public int Fresh { get; init; }

    /// <summary>Whether a listing arrived since the last visit (all of them on the first one).</summary>
    public bool IsFresh(TriageRow row) => LastVisit is not { } visit || row.FirstSeenAt > visit;
}
