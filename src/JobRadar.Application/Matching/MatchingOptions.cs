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
}

/// <summary>One line of work: what to search for and how to score what comes back.</summary>
public sealed class ProfileOptions
{
    /// <summary>The market searched and scored: "AU" or "BR". Only the sources of that country run its searches.</summary>
    public string Country { get; set; } = "AU";

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
