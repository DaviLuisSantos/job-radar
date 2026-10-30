using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Triage;

/// <summary>A status change of an application, with what the "Today" page shows of its listing.</summary>
public sealed record RecentStatusChange(ListingId Id, string Title, string? Company, ApplicationStatus? From, ApplicationStatus To, DateTimeOffset At);

/// <summary>What arrived for one profile since the last visit: how many, and the best of them.</summary>
public sealed record ProfileToday(string Profile, int New, IReadOnlyList<TriageRow> Best);

/// <summary>The "Today" page of one market.</summary>
/// <param name="Since">The last visit; on the first, a week back (<see cref="FirstVisitWindow"/>).</param>
public sealed record MarketToday(
    JobMarket Market,
    DateTimeOffset Since,
    bool FirstVisit,
    IReadOnlyList<ProfileToday> Profiles,
    IReadOnlyList<RecentStatusChange> Changes)
{
    public static readonly TimeSpan FirstVisitWindow = TimeSpan.FromDays(7);

    /// <summary>Distinct listings new to the market: a job two profiles found counts once.</summary>
    public int New { get; init; }
}
