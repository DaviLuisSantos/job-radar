using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Triage;

/// <summary>What the triage list shows for one profile.</summary>
/// <param name="OnlyUntriaged">Hide listings the candidate already acted on (any status or label).</param>
/// <param name="Text">Case-insensitive match on title or company.</param>
/// <param name="IncludeExcluded">For the repository: also the listings scored 0.</param>
/// <param name="Excluded">For the screen: the excluded listings instead of the open ones.</param>
public sealed record TriageFilter(
    string Profile,
    bool IncludeExcluded = false,
    bool OnlyUntriaged = true,
    int MinScore = 0,
    string? Text = null,
    int Page = 1,
    int PageSize = 50,
    TriageOrder Order = TriageOrder.BestFirst,
    bool Excluded = false);

public enum TriageOrder
{
    /// <summary>Highest score first: for deciding what to apply to.</summary>
    BestFirst,

    /// <summary>A few of every score band per page: for labeling (see <see cref="LabelSample"/>).</summary>
    AcrossScores,
}

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

    /// <summary>Pay, roster, accommodation and the like, read from the full ad (<see cref="ListingFacts"/>).</summary>
    public IReadOnlyList<ListingFact> Facts { get; init; } = [];

    /// <summary>Arrangement and contract, as the source gives them (<see cref="ListingConditions"/>).</summary>
    public ListingConditions Conditions { get; init; } = ListingConditions.None;

    /// <summary>The rule that excluded the listing, when it scored 0.</summary>
    public ScoreItem? ExcludedBy { get; init; }
}

/// <param name="Total">Rows after collapsing duplicates, across all pages.</param>
/// <param name="Excluded">Excluded rows under the same filters, shown apart from the list.</param>
public sealed record TriagePage(IReadOnlyList<TriageRow> Rows, int Total, int Excluded = 0);

public sealed record ProfileScore(string Profile, FitScore Score, DateTimeOffset ScoredAt);

/// <summary>Everything known about one listing, for the detail view.</summary>
/// <param name="Description">The full ad text, if the candidate captured it.</param>
public sealed record ListingDetail(
    ListingRecord Record,
    IReadOnlyList<ProfileScore> Scores,
    IReadOnlyList<string> Terms,
    JobApplication? Application,
    ListingLabel? Label,
    ListingDescription? Description = null)
{
    /// <summary>
    /// Pay, roster, accommodation and the like, read from the full ad, or from the summary when there is none; pay
    /// only when the source gives no amount.
    /// </summary>
    public IReadOnlyList<ListingFact> Facts { get; } =
        ListingFacts.Read(Description?.Text ?? Record.Listing.Summary, hasPayField: ListingFacts.GivesPay(Record.Listing.SalaryText));
}
