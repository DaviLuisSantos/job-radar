using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Triage;

/// <summary>What the triage list shows for one profile.</summary>
/// <param name="OnlyUntriaged">Hide listings the candidate already acted on (any status or label).</param>
/// <param name="Text">Case-insensitive match on title or company.</param>
public sealed record TriageFilter(
    string Profile,
    bool IncludeExcluded = false,
    bool OnlyUntriaged = true,
    int MinScore = 0,
    string? Text = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>One line of the triage list: best scores first.</summary>
/// <param name="Status">Application status, if the candidate acted on it.</param>
public sealed record TriageRow(
    ListingId Id,
    string Title,
    string? Company,
    ListingLocation Location,
    WorkType WorkType,
    string? SalaryText,
    DateTimeOffset? PostedAt,
    DateTimeOffset FirstSeenAt,
    int Score,
    ApplicationStatus? Status,
    Verdict? Verdict)
{
    /// <summary>Other listings of the same job (reposts, other cities); see <see cref="Duplicates"/>.</summary>
    public IReadOnlyList<ListingId> Copies { get; init; } = [];

    /// <summary>Locations of the copies that differ from this row.</summary>
    public IReadOnlyList<string> OtherLocations { get; init; } = [];

    /// <summary>This listing and its copies: what a triage action applies to.</summary>
    public IEnumerable<ListingId> AllIds => Copies.Prepend(Id);
}

/// <param name="Total">Rows after collapsing duplicates, across all pages.</param>
public sealed record TriagePage(IReadOnlyList<TriageRow> Rows, int Total);

public sealed record ProfileScore(string Profile, FitScore Score, DateTimeOffset ScoredAt);

/// <summary>Everything known about one listing, for the detail view.</summary>
public sealed record ListingDetail(
    ListingRecord Record,
    IReadOnlyList<ProfileScore> Scores,
    IReadOnlyList<string> Terms,
    JobApplication? Application,
    ListingLabel? Label);
