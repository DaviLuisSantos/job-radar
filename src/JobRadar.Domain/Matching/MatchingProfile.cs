namespace JobRadar.Domain.Matching;

/// <summary>
/// What a candidate is looking for in one line of work. Data, not code: profiles come from
/// configuration, so the same rules serve anyone.
/// </summary>
/// <param name="PositiveSignals">+16 in the title, +8 in the body; ignored when negated nearby.</param>
/// <param name="NegativeSignals">−30 in the title, −15 in the body.</param>
/// <param name="DisqualifySignals">Formal qualification the candidate lacks: caps the score.</param>
/// <param name="QualifiedTitles">Qualified trade in the title (e.g. "electrician"): caps the score.</param>
/// <param name="EntryLevelBoost">+12 per "no experience required"-style phrase.</param>
/// <param name="AllowSenior">"Senior" in the title subtracts instead of excluding.</param>
/// <param name="PreferredLocations">+10 when the location matches one of these.</param>
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
    /// <summary>−20 per kind found in the full ad (see <see cref="ProfileRequirements"/>).</summary>
    public ProfileRequirements Requirements { get; init; } = ProfileRequirements.None;

    /// <summary>
    /// +10 remote, +5 hybrid. Off for site work: there, "remote" is where the site is (Seek's work
    /// arrangement field says "Remote" for a remote mine), not working from home.
    /// </summary>
    public bool RewardRemote { get; init; } = true;
}

/// <summary>
/// Weights shared by every profile. Defaults are the values calibrated in the prototype for a
/// Working Holiday visa holder: sponsorship is a bonus, not a requirement.
/// </summary>
public sealed record ScoringPolicy
{
    public bool ExcludeSeniorTitles { get; init; } = true;
    public bool VisaRequired { get; init; }
    public int SponsorBonus { get; init; } = 10;
    public int NoSponsorPenalty { get; init; } = 10;
    public int DisqualifyMaxScore { get; init; } = 35;
    public int ApprenticeshipPenalty { get; init; } = 25;
    public int LongContractPenalty { get; init; } = 10;
    public int AgingAfterDays { get; init; } = 14;
    public int AgingPenalty { get; init; } = 5;
    public int StaleAfterDays { get; init; } = 30;
    public int StalePenalty { get; init; } = 30;

    /// <summary>
    /// Phrases searched in the full ad text, by group (<see cref="FullTextGroup"/>). A listing
    /// carries the phrases found for it in <see cref="ScoringInput.FullTextTerms"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FullTextGroups { get; init; } = FullTextGroup.Defaults;

    public static ScoringPolicy Default { get; } = new();
}

/// <summary>Groups of full-text phrases and what each does to the score.</summary>
public static class FullTextGroup
{
    /// <summary>Work rights a Working Holiday visa cannot give: excludes.</summary>
    public const string Exclude = "exclude";
    /// <summary>−55, unless the ad also mentions working holidays.</summary>
    public const string Citizen = "citizen";
    /// <summary>−10: "full working rights" may or may not include a WHV.</summary>
    public const string Ambiguous = "ambiguous";
    /// <summary>+15, unless the same ad also names citizenship or residency (<see cref="WhvWithResidency"/>).</summary>
    public const string Whv = "whv";
    /// <summary>
    /// −55: working holidays and citizenship or residency in the same ad. In practice that is mostly
    /// turning working holiday visas down ("citizens or PR only, no WHV"), so the mention neither
    /// earns the WHV bonus nor cancels the citizenship penalties. Seek matches citizen/citizens and
    /// resident/residents alike, but not citizenship or residency: those are terms of their own.
    /// </summary>
    public const string WhvWithResidency = "whv_residency";
    /// <summary>−30.</summary>
    public const string Commission = "commission";
    /// <summary>−20 (truck licence).</summary>
    public const string Licence = "licence";
    /// <summary>−40: 12 months of work rights, unless the ad also mentions working holidays.</summary>
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
