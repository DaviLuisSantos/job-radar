using JobRadar.Domain.Listings;

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
    /// Locations that rule a listing out for this profile ("Perth WA" for farm work outside the city), matched as a
    /// whole token in the location label.
    /// </summary>
    public IReadOnlyList<string> ExcludedLocations { get; init; } = [];

    /// <summary>
    /// Words that rule a listing out by its title, matched as a whole token: a level the candidate is past
    /// ("júnior", "estágio") or a main stack they do not work in ("java").
    /// </summary>
    public IReadOnlyList<string> ExcludedTitles { get; init; } = [];

    /// <summary>
    /// Where on-site work is possible; an ad that says it is on-site (<see cref="MarketRules.OnSite"/>) anywhere
    /// else is ruled out. Empty: on-site work anywhere is fine.
    /// </summary>
    public IReadOnlyList<string> OnSiteLocations { get; init; } = [];

    /// <summary>What the profile looks for: picks the sources searched and the <see cref="MarketRules"/> that read the ads.</summary>
    public JobMarket Market { get; init; } = JobMarket.AU;

    /// <summary>
    /// Pay expectations by engagement. Only profiles that have it read the engagement and the pay; null (every AU and
    /// BR profile today) leaves both out.
    /// </summary>
    public Compensation? Compensation { get; init; }
}

/// <summary>
/// Weights shared by every profile. Defaults are the values calibrated in the prototype for the author's first
/// market, where the employer's assistance is a bonus, not a requirement; the weights added after (truck licence,
/// tickets, eligibility) are the ones calibrated against the candidate's labels since.
/// Bonuses and penalties are magnitudes: the scorer adds the first and subtracts the second.
/// </summary>
public sealed record ScoringPolicy
{
    public bool ExcludeSeniorTitles { get; init; } = true;
    /// <summary>The profile's status needs the employer's assistance: an ad that does not confirm it costs points.</summary>
    public bool AssistanceRequired { get; init; }
    public int AssistanceBonus { get; init; } = 10;
    public int NoAssistancePenalty { get; init; } = 10;

    /// <summary>When assistance is required and the ad turns it down.</summary>
    public int AssistanceDeniedPenalty { get; init; } = 15;
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
    /// <summary>The ad welcomes the profile's status.</summary>
    public int WelcomeBonus { get; init; } = 15;

    /// <summary>Contract, temp and casual: short engagements, for a profile that cannot stay long with one employer.</summary>
    public int FlexibleWorkTypeBonus { get; init; } = 5;
    public int PreferredLocationBonus { get; init; } = 10;

    /// <summary>
    /// Points above this are divided by <see cref="CompressionDivisor"/>, so the best listings keep their
    /// order instead of tying at 100.
    /// </summary>
    public int CompressAbove { get; init; } = 80;
    public int CompressionDivisor { get; init; } = 3;

    // Eligibility and pay, from the full-text phrases (FullTextGroup) or a captured ad.
    public int EligibilityDoubtPenalty { get; init; } = 55;
    public int WelcomeRetractedPenalty { get; init; } = 55;
    public int EligibilityAmbiguousPenalty { get; init; } = 10;
    public int EligibilityTenurePenalty { get; init; } = 40;
    public int CommissionOnlyPenalty { get; init; } = 30;
    /// <summary>English as a condition, in a market whose ads are not in English: something to train, not a no.</summary>
    public int EnglishRequiredPenalty { get; init; } = 10;

    // Remote markets.
    /// <summary>Time zones that leave Brazil out (more than an hour away): working hours would not overlap.</summary>
    public int TimeZonePenalty { get; init; } = 10;

    /// <summary>The top of the pay range below the profile's floor: as heavy as "senior", a reason, not a no.</summary>
    public int PayBelowFloorPenalty { get; init; } = 15;

    /// <summary>The bottom of the pay range at the profile's target or above.</summary>
    public int PayAtTargetBonus { get; init; } = 8;

    // A requirement of the profile found in the full ad, by the candidate's state on it
    // (CandidateRequirements); a requirement the candidate has costs nothing. Moderate below out of reach:
    // the search gives no context, and "cert III an advantage" matches as well as "cert III essential".

    /// <summary>
    /// An ad asking for something the candidate has: fewer applicants can meet it. A fit point, once per
    /// kind, counted before the compression. 10 is a first guess, as large as the penalty of a ticket the
    /// candidate would still have to get; calibrate it once labels cover listings that ask for what they have.
    /// </summary>
    public int HaveRequirementBonus { get; init; } = 10;

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
    /// Groups whose phrases pass a source's result cap even state by state (thousands of ads a month): also searched
    /// with each profile's context words ("phrase + FIFO"), which stays under it. The phrase alone is still searched,
    /// for profiles without context words. The phrases themselves are the market's (<see cref="MarketRules.FullTextGroups"/>).
    /// </summary>
    public IReadOnlyList<string> ContextGroups { get; init; } = [FullTextGroup.Doubt, FullTextGroup.Ambiguous];

    /// <summary>Requirements profiles pick by id, so a phrase is written once, not in every profile.</summary>
    public IReadOnlyList<RequirementItem> Requirements { get; init; } = RequirementCatalog.Defaults;

    public static ScoringPolicy Default { get; } = new();
}

/// <summary>
/// Groups of full-text phrases and what each does to the score. The eligibility groups are a market's words and come
/// from its <see cref="EligibilityRules.Phrases"/>; the others are in code (<see cref="Code"/>).
/// </summary>
public static class FullTextGroup
{
    /// <summary>A condition the profile's status cannot meet: excludes, like <see cref="EligibilityRules.IncompatiblePattern"/>.</summary>
    public const string Exclude = "exclude";
    /// <summary><see cref="ScoringPolicy.EligibilityDoubtPenalty"/>: a status the profile lacks, unless the ad also welcomes the profile.</summary>
    public const string Doubt = "doubt";
    /// <summary><see cref="ScoringPolicy.EligibilityAmbiguousPenalty"/>: wording that may or may not leave the profile out.</summary>
    public const string Ambiguous = "ambiguous";
    /// <summary><see cref="ScoringPolicy.WelcomeBonus"/>, unless the same ad also has a <see cref="WelcomeRetracted"/> phrase.</summary>
    public const string Welcome = "welcome";
    /// <summary>
    /// <see cref="ScoringPolicy.WelcomeRetractedPenalty"/>: a welcome and a reserved status in the same ad ("a + b"). In practice
    /// that is mostly turning the profile down, so the mention neither earns the welcome bonus nor cancels the doubt
    /// penalties.
    /// </summary>
    public const string WelcomeRetracted = "welcome_retracted";
    /// <summary><see cref="ScoringPolicy.EligibilityTenurePenalty"/>: a duration the profile's status cannot cover, unless the ad also welcomes it.</summary>
    public const string Tenure = "tenure";
    /// <summary><see cref="ScoringPolicy.CommissionOnlyPenalty"/>.</summary>
    public const string Commission = "commission";
    /// <summary><see cref="ScoringPolicy.TruckLicencePenalty"/>.</summary>
    public const string Licence = "licence";

    /// <summary>The groups a market's eligibility rules may fill.</summary>
    public static IReadOnlyList<string> Eligibility { get; } = [Exclude, Doubt, Ambiguous, Welcome, WelcomeRetracted, Tenure];

    /// <summary>The groups that are not about eligibility, the same in every market.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Code { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Commission] = ["commission only"],
            [Licence] = ["HR licence", "heavy rigid"],
        };
}
