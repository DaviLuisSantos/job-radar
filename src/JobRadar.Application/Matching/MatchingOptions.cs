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
}
