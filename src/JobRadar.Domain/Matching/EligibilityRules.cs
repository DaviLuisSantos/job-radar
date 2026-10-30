namespace JobRadar.Domain.Matching;

/// <summary>
/// What the ads of one market say about who may apply, as data: the market's rules (<see cref="MarketRules"/>) read it,
/// and the phrases are the market's own words, so they live in the local configuration
/// (<c>Matching:Markets:&lt;market&gt;:Eligibility</c>), not in code. Patterns are .NET regular expressions, matched
/// case-insensitively on lower-cased text; a pattern left out never matches.
/// </summary>
public sealed record EligibilityRules
{
    public static EligibilityRules None { get; } = new();

    /// <summary>
    /// A status the market reserves the job for, which the profile does not have. Excludes when the title or summary
    /// says it; in a captured ad, costs <see cref="ScoringPolicy.EligibilityDoubtPenalty"/> unless the ad also welcomes the
    /// profile (the same words often list who else may apply).
    /// </summary>
    public string? RestrictedPattern { get; init; }

    /// <summary>A condition the profile's status cannot meet. Excludes, from the title and summary or a captured ad.</summary>
    public string? IncompatiblePattern { get; init; }

    /// <summary>A job reserved for a group, read in the title only. Excludes.</summary>
    public string? ReservedTitlePattern { get; init; }

    /// <summary>
    /// Words that welcome the profile's status: a bonus, from the title and summary, else a captured ad where they are
    /// not negated just before, else the full-text <see cref="FullTextGroup.Welcome"/> phrases.
    /// </summary>
    public string? WelcomePattern { get; init; }

    /// <summary>The employer says it will not help with the status: the assistance phrases then earn nothing.</summary>
    public string? AssistanceDeniedPattern { get; init; }

    /// <summary>Substrings that confirm the employer helps with the status.</summary>
    public IReadOnlyList<string> AssistanceOfferedPhrases { get; init; } = [];

    /// <summary>A substring that alone earns the assistance bonus, unless denied. None: only the phrases do.</summary>
    public string? AssistanceWord { get; init; }

    /// <summary>
    /// Phrases searched in the full ad on the sources that can (<c>IFullTextSearch</c>), by group: the keys of
    /// <see cref="FullTextGroup.Eligibility"/>. "a + b" means both in the same ad. A phrase is also the key of its
    /// marks and runs, so changing its spelling starts its history again.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Phrases { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Nothing configured: the market reads no eligibility at all.</summary>
    public bool IsEmpty() => Normalized() is var n
        && n.RestrictedPattern is null && n.IncompatiblePattern is null && n.ReservedTitlePattern is null
        && n.WelcomePattern is null && n.AssistanceDeniedPattern is null && n.AssistanceWord is null
        && n.AssistanceOfferedPhrases.Count == 0 && n.Phrases.Count == 0;

    /// <summary>
    /// The same rules in one spelling: blank patterns as none, group keys lower-cased and in order, empty groups left
    /// out. Two blocks that score the same serialise the same, which the rules hash and the comparisons rely on.
    /// </summary>
    public EligibilityRules Normalized()
    {
        static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
        return new EligibilityRules
        {
            RestrictedPattern = Blank(RestrictedPattern),
            IncompatiblePattern = Blank(IncompatiblePattern),
            ReservedTitlePattern = Blank(ReservedTitlePattern),
            WelcomePattern = Blank(WelcomePattern),
            AssistanceDeniedPattern = Blank(AssistanceDeniedPattern),
            AssistanceOfferedPhrases = [.. AssistanceOfferedPhrases.Where(p => !string.IsNullOrWhiteSpace(p))],
            AssistanceWord = Blank(AssistanceWord),
            Phrases = Phrases
                .Select(g => (Key: g.Key.Trim().ToLowerInvariant(), Phrases: g.Value.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()))
                .Where(g => g.Phrases.Count > 0)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Phrases, StringComparer.Ordinal),
        };
    }
}
