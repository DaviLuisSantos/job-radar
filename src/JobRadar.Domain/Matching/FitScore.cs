namespace JobRadar.Domain.Matching;

/// <summary>
/// A 0–100 fit score and every rule that moved it. 0 means excluded (never shown);
/// listings that are not excluded score 1–100.
/// </summary>
public sealed record FitScore(int Final, IReadOnlyList<ScoreItem> Items)
{
    public const int Base = 50;

    public bool Excluded => Final == 0;
}

/// <param name="Detail">What matched: the signal, the phrase found, the age in days.</param>
public sealed record ScoreItem(ScoreRule Rule, string Detail, ScoreWhere Where, int Points);

/// <summary>Where in the listing a rule matched.</summary>
public enum ScoreWhere
{
    None = 0,
    Title,
    Body,
    /// <summary>Title and body together.</summary>
    Text,
    /// <summary>The full ad text, known only through phrase searches.</summary>
    FullText,
    WorkType,
    Location,
    PostedDate,
    /// <summary>The full ad text the candidate captured (see <c>ListingDescription</c>).</summary>
    Description,
}

/// <summary>
/// Every rule the scorer applies. The explanation carries these, not prose, so the UI can
/// word them in any language.
/// </summary>
public enum ScoreRule
{
    // Exclusions (score 0)
    CitizenshipRequired,
    WorkRightsIncompatible,
    IdentifiedRole,
    LeadershipTitle,
    SeniorTitle,
    LocationExcluded,

    // Points
    SeniorPenalty,
    PositiveSignal,
    NegativeSignal,
    SponsorshipMentioned,
    SponsorshipDenied,
    Remote,
    Hybrid,
    Fifo,
    EntryLevel,
    WorkingHolidayFriendly,
    FlexibleWorkType,
    PreferredLocation,
    Compression,

    // Restrictions applied after compression
    FullTextCitizenship,
    FullTextWhvWithResidency,
    FullTextAmbiguousRights,
    FullTextTwelveMonthRights,
    FullTextCommission,
    FullTextTruckLicence,
    FullTextExperience,
    FullTextQualification,
    FullTextLicence,
    Apprenticeship,
    LongContract,
    CommissionOnly,
    TruckLicence,
    NoSponsorship,
    Aging,
    Stale,

    // Cap
    QualificationRequired,
}
