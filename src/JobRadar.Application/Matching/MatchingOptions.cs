using JobRadar.Domain.Matching;

namespace JobRadar.Application.Matching;

/// <summary>
/// Candidate profiles. Section: <c>Matching</c>. Real profiles describe a person's plans, so they
/// live in <c>appsettings.Local.json</c> (not versioned); <c>appsettings.Local.example.json</c>
/// shows the shape.
/// </summary>
public sealed class MatchingOptions
{
    public const string Section = "Matching";

    public Dictionary<string, ProfileOptions> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What each market reads as data, by market (AU, BR, LatamRemote, GlobalRemote). Who may apply is said in each
    /// market's own words, so those words live here, in the local configuration, not in code.
    /// </summary>
    public Dictionary<string, MarketRulesOptions> Markets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The numbers every profile's <see cref="ProfileOptions.Compensation"/> is read with.</summary>
    public CompensationOptions Compensation { get; set; } = new();
}

/// <summary>Section <c>Matching:Markets:&lt;market&gt;</c>.</summary>
public sealed class MarketRulesOptions
{
    /// <summary>Who may apply, in the market's words (<see cref="EligibilityRules"/>).</summary>
    public EligibilityOptions? Eligibility { get; set; }
}

/// <summary>
/// Section <c>Matching:Markets:&lt;market&gt;:Eligibility</c>, the shape of <see cref="EligibilityRules"/>: patterns are
/// .NET regular expressions, phrases are searched in the full ad by group (exclude, doubt, ambiguous, welcome,
/// welcome_retracted, tenure). <c>eligibility &lt;market&gt;</c> prints the block in use.
/// </summary>
public sealed class EligibilityOptions
{
    public string? RestrictedPattern { get; set; }
    public string? IncompatiblePattern { get; set; }
    public string? ReservedTitlePattern { get; set; }
    public string? WelcomePattern { get; set; }
    public string? AssistanceDeniedPattern { get; set; }
    public List<string> AssistanceOfferedPhrases { get; set; } = [];
    public string? AssistanceWord { get; set; }
    public Dictionary<string, List<string>> Phrases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public EligibilityRules ToRules() => new()
    {
        RestrictedPattern = RestrictedPattern,
        IncompatiblePattern = IncompatiblePattern,
        ReservedTitlePattern = ReservedTitlePattern,
        WelcomePattern = WelcomePattern,
        AssistanceDeniedPattern = AssistanceDeniedPattern,
        AssistanceOfferedPhrases = AssistanceOfferedPhrases,
        AssistanceWord = AssistanceWord,
        Phrases = Phrases.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
    };
}

/// <summary>Section <c>Matching:Compensation</c>: how pay in dollars and by the hour compares with a monthly band.</summary>
public sealed class CompensationOptions
{
    /// <summary>Reais per dollar. Update it when the rate moves enough to matter; it is in the rules hash.</summary>
    public decimal UsdToBrl { get; set; } = 5.5m;

    /// <summary>Hours in a month of full-time work, for hourly rates.</summary>
    public decimal HoursPerMonth { get; set; } = 168;
}

/// <summary>What a profile wants to earn in one engagement, per month.</summary>
public sealed class PayBandOptions
{
    public string Currency { get; set; } = "USD";
    public decimal MonthlyFloor { get; set; }
    public decimal MonthlyTarget { get; set; }
}

/// <summary>A profile's pay expectations, by engagement (<c>Compensation</c> in the domain).</summary>
public sealed class ProfileCompensationOptions
{
    public PayBandOptions? Contractor { get; set; }
    public PayBandOptions? Eor { get; set; }
}

/// <summary>One line of work: what to search for and how to score what comes back.</summary>
public sealed class ProfileOptions
{
    /// <summary>
    /// The market searched and scored: AU, BR, LatamRemote or GlobalRemote. Only the sources serving it run its searches,
    /// and its rules read the ads. Empty: <see cref="Country"/>, then AU.
    /// </summary>
    public string? Market { get; set; }

    /// <summary>Before markets (2026-10-29 to 10-30): AU or BR, read as the market when <see cref="Market"/> is empty.</summary>
    public string? Country { get; set; }

    /// <summary>Every keyword is searched in every location.</summary>
    public List<string> Keywords { get; set; } = [];

    public List<string> Locations { get; set; } = [];

    public List<string> PositiveSignals { get; set; } = [];
    public List<string> NegativeSignals { get; set; } = [];
    public List<string> DisqualifySignals { get; set; } = [];
    public List<string> QualifiedTitles { get; set; } = [];
    public bool EntryLevelBoost { get; set; }
    public bool AllowSenior { get; set; }
    public List<string> PreferredLocations { get; set; } = [];

    /// <summary>Locations that rule a listing out ("Perth WA" for regional work that must count for a visa).</summary>
    public List<string> ExcludedLocations { get; set; } = [];

    /// <summary>Title words that rule a listing out: a level ("júnior") or a main stack ("java").</summary>
    public List<string> ExcludedTitles { get; set; } = [];

    /// <summary>Where on-site work is possible; on-site anywhere else rules the listing out. Empty: anywhere.</summary>
    public List<string> OnSiteLocations { get; set; } = [];

    /// <summary>Remote/hybrid bonus; turn off for site work, where "remote" means the place.</summary>
    public bool RewardRemote { get; set; } = true;

    /// <summary>Requirements looked for in the full ad (see <c>ProfileRequirements</c>).</summary>
    public RequirementOptions Requirements { get; set; } = new();

    /// <summary>Pay expectations; without it the profile reads neither the engagement nor the pay.</summary>
    public ProfileCompensationOptions? Compensation { get; set; }
}

public sealed class RequirementOptions
{
    /// <summary>Each phrase is searched together with each of these words ("FIFO").</summary>
    public List<string> Contexts { get; set; } = [];

    /// <summary>
    /// Ids from the requirement catalog (<c>ScoringPolicy.Requirements</c>): "hrwl", "truck-hr"… The phrase
    /// lists below add to the items' phrases, for a phrase the catalog does not have yet.
    /// </summary>
    public List<string> Items { get; set; } = [];

    public List<string> Experience { get; set; } = [];
    public List<string> Qualification { get; set; } = [];
    public List<string> Licence { get; set; } = [];
}
