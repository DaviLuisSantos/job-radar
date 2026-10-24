namespace JobRadar.Domain.Matching;

/// <summary>
/// What a candidate is looking for in one line of work. Data, not code: profiles come from
/// configuration, so the same rules serve anyone.
/// </summary>
/// <param name="PositiveSignals">Bonus in the title, half in the body (<see cref="ScoringPolicy"/>); ignored when negated nearby.</param>
/// <param name="NegativeSignals">Penalty in the title, half in the body.</param>
/// <param name="DisqualifySignals">Formal qualification the candidate lacks: caps the score.</param>
/// <param name="QualifiedTitles">Qualified trade in the title (e.g. "electrician"): caps the score.</param>
/// <param name="EntryLevelBoost"><see cref="ScoringPolicy.EntryLevelBonus"/> per "no experience required"-style phrase.</param>
/// <param name="AllowSenior">"Senior" in the title subtracts instead of excluding.</param>
/// <param name="PreferredLocations"><see cref="ScoringPolicy.PreferredLocationBonus"/> when the location matches one of these.</param>
public sealed record MatchingProfile(
    string Name,
    IReadOnlyList<string> PositiveSignals,
    IReadOnlyList<string> NegativeSignals,
    IReadOnlyList<string> DisqualifySignals,
    IReadOnlyList<string> QualifiedTitles,
    bool EntryLevelBoost,
    bool AllowSenior,
    IReadOnlyList<string> PreferredLocations)
{
    /// <summary>Penalised per kind found in the full ad (see <see cref="ProfileRequirements"/>).</summary>
    public ProfileRequirements Requirements { get; init; } = ProfileRequirements.None;

    /// <summary>
    /// Remote and hybrid bonuses. Off for site work: there, "remote" is where the site is (Seek's work
    /// arrangement field says "Remote" for a remote mine), not working from home.
    /// </summary>
    public bool RewardRemote { get; init; } = true;

    /// <summary>
    /// Locations that rule a listing out for this profile ("Perth WA" for the 88 days of regional work, which
    /// do not count in the city), matched as a whole token in the location label.
    /// </summary>
    public IReadOnlyList<string> ExcludedLocations { get; init; } = [];
}

/// <summary>
/// Weights shared by every profile. Defaults are the values calibrated in the prototype for a
/// Working Holiday visa holder: sponsorship is a bonus, not a requirement, and the weights added after
/// (truck licence, tickets, work rights) are the ones calibrated against the candidate's labels since.
/// Bonuses and penalties are magnitudes: the scorer adds the first and subtracts the second.
/// </summary>
public sealed record ScoringPolicy
{
    public bool ExcludeSeniorTitles { get; init; } = true;
    public bool VisaRequired { get; init; }
    public int SponsorBonus { get; init; } = 10;
    public int NoSponsorPenalty { get; init; } = 10;

    /// <summary>When the visa is required and the ad turns sponsorship down.</summary>
    public int SponsorDeniedPenalty { get; init; } = 15;
    public int DisqualifyMaxScore { get; init; } = 35;
    public int ApprenticeshipPenalty { get; init; } = 25;
    public int LongContractPenalty { get; init; } = 10;
    public int AgingAfterDays { get; init; } = 14;
    public int AgingPenalty { get; init; } = 5;
    public int StaleAfterDays { get; init; } = 30;
    public int StalePenalty { get; init; } = 30;

    /// <summary>"Senior" in the title of a profile that allows senior roles.</summary>
    public int SeniorPenalty { get; init; } = 15;

    // Profile signals: the title is worth twice the body.
    public int PositiveSignalTitleBonus { get; init; } = 16;
    public int PositiveSignalBodyBonus { get; init; } = 8;
    public int NegativeSignalTitlePenalty { get; init; } = 30;
    public int NegativeSignalBodyPenalty { get; init; } = 15;

    public int RemoteBonus { get; init; } = 10;
    public int HybridBonus { get; init; } = 5;
    public int FifoBonus { get; init; } = 15;

    /// <summary>Per "no experience required"-style phrase, for profiles with an entry-level boost.</summary>
    public int EntryLevelBonus { get; init; } = 12;
    public int WorkingHolidayBonus { get; init; } = 15;

    /// <summary>Contract, temp and casual: they fit the six months per employer.</summary>
    public int FlexibleWorkTypeBonus { get; init; } = 5;
    public int PreferredLocationBonus { get; init; } = 10;

    /// <summary>
    /// Points above this are divided by <see cref="CompressionDivisor"/>, so the best listings keep their
    /// order instead of tying at 100.
    /// </summary>
    public int CompressAbove { get; init; } = 80;
    public int CompressionDivisor { get; init; } = 3;

    // Work rights and pay, from the full-text phrases (FullTextGroup) or a captured ad.
    public int CitizenshipPenalty { get; init; } = 55;
    public int WhvWithResidencyPenalty { get; init; } = 55;
    public int AmbiguousRightsPenalty { get; init; } = 10;
    public int TwelveMonthRightsPenalty { get; init; } = 40;
    public int CommissionOnlyPenalty { get; init; } = 30;

    // A requirement of the profile found in the full ad, by the candidate's state on it
    // (CandidateRequirements); a requirement the candidate has costs nothing. Moderate below out of reach:
    // the search gives no context, and "cert III an advantage" matches as well as "cert III essential".

    /// <summary>A ticket the candidate would get for the job with a short course (HRWL, VOC).</summary>
    public int ObtainableRequirementPenalty { get; init; } = 10;

    /// <summary>Something the candidate lacks: years of experience, a trade, a degree.</summary>
    public int MissingRequirementPenalty { get; init; } = 20;

    /// <summary>Something the candidate cannot get for the job: a truck licence class.</summary>
    public int OutOfReachRequirementPenalty { get; init; } = 40;

    /// <summary>
    /// A truck licence class (HR/HC/MR/MC) the ad asks for, read by the pattern in the ad's own text or the
    /// policy's licence group: it takes years of holding a car licence, so a newcomer cannot get one for the
    /// job. Told by the candidate (2026-10-23): these ads are bad.
    /// </summary>
    public int TruckLicencePenalty { get; init; } = 40;

    /// <summary>A truck licence the ad calls desirable ("MR licence highly regarded"): not a condition.</summary>
    public int DesirableTruckLicencePenalty { get; init; } = 10;

    /// <summary>
    /// Phrases searched in the full ad text, by group (<see cref="FullTextGroup"/>). A listing
    /// carries the phrases found for it in <see cref="ScoringInput.FullTextTerms"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FullTextGroups { get; init; } = FullTextGroup.Defaults;

    /// <summary>
    /// Groups whose phrases pass the 500-result cap even state by state ("australian citizen" is in
    /// ~2,400 ads a month): also searched with each profile's context words ("australian citizen + FIFO"),
    /// which stays under it. The phrase alone is still searched, for profiles without context words.
    /// </summary>
    public IReadOnlyList<string> ContextGroups { get; init; } = [FullTextGroup.Citizen, FullTextGroup.Ambiguous];

    /// <summary>Requirements profiles pick by id, so a phrase is written once, not in every profile.</summary>
    public IReadOnlyList<RequirementItem> Requirements { get; init; } = RequirementCatalog.Defaults;

    public static ScoringPolicy Default { get; } = new();
}

/// <summary>Groups of full-text phrases and what each does to the score.</summary>
public static class FullTextGroup
{
    /// <summary>Work rights a Working Holiday visa cannot give: excludes.</summary>
    public const string Exclude = "exclude";
    /// <summary><see cref="ScoringPolicy.CitizenshipPenalty"/>, unless the ad also mentions working holidays.</summary>
    public const string Citizen = "citizen";
    /// <summary><see cref="ScoringPolicy.AmbiguousRightsPenalty"/>: "full working rights" may or may not include a WHV.</summary>
    public const string Ambiguous = "ambiguous";
    /// <summary><see cref="ScoringPolicy.WorkingHolidayBonus"/>, unless the same ad also names citizenship or residency (<see cref="WhvWithResidency"/>).</summary>
    public const string Whv = "whv";
    /// <summary>
    /// <see cref="ScoringPolicy.WhvWithResidencyPenalty"/>: working holidays and citizenship or residency in the same ad. In practice that is mostly
    /// turning working holiday visas down ("citizens or PR only, no WHV"), so the mention neither
    /// earns the WHV bonus nor cancels the citizenship penalties. Seek matches citizen/citizens and
    /// resident/residents alike, but not citizenship or residency: those are terms of their own.
    /// </summary>
    public const string WhvWithResidency = "whv_residency";
    /// <summary><see cref="ScoringPolicy.CommissionOnlyPenalty"/>.</summary>
    public const string Commission = "commission";
    /// <summary><see cref="ScoringPolicy.TruckLicencePenalty"/>.</summary>
    public const string Licence = "licence";
    /// <summary><see cref="ScoringPolicy.TwelveMonthRightsPenalty"/>: 12 months of work rights, unless the ad also mentions working holidays.</summary>
    public const string Rights12Months = "rights_12m";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Defaults { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Exclude] =
            [
                "unlimited working rights", "unlimited australian working rights",
                "unrestricted working rights", "unrestricted australian working rights",
                "permanent working rights", "permanent australian working rights",
            ],
            [Citizen] = ["australian citizen", "permanent resident"],
            [Ambiguous] = ["full working rights", "full australian working rights"],
            [Whv] = ["working holiday", "backpackers", "88 days"],
            [WhvWithResidency] =
            [
                "working holiday + citizen", "working holiday + citizenship",
                "working holiday + resident", "working holiday + residency",
            ],
            [Commission] = ["commission only"],
            [Licence] = ["HR licence", "heavy rigid"],
            [Rights12Months] = ["12 months + working rights"],
        };
}
