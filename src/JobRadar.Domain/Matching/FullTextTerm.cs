namespace JobRadar.Domain.Matching;

/// <summary>
/// A phrase looked for in the full ad text. "a + b" means both phrases in the same ad, as in
/// "12 months + working rights".
/// </summary>
public sealed record FullTextTerm(string Text)
{
    public IReadOnlyList<string> Phrases { get; } = Text
        .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Stable identity of the term search, used as its run key.</summary>
    public string RunKey => $"term: {Text.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Every distinct term of the markets' groups (those of <see cref="ScoringPolicy.ContextGroups"/> also with each
    /// profile context word) and of the profiles' requirements, in a stable order.
    /// </summary>
    public static IReadOnlyList<FullTextTerm> AllOf(
        IEnumerable<MarketRules> markets, ScoringPolicy policy, IEnumerable<MatchingProfile>? profiles = null)
    {
        var groups = markets.SelectMany(m => m.FullTextGroups).ToList();
        var all = (profiles ?? []).ToList();
        var contexts = all.SelectMany(p => p.Requirements.Contexts).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var narrowed = groups
            .Where(g => policy.ContextGroups.Contains(g.Key))
            .SelectMany(g => g.Value)
            .SelectMany(term => contexts.Select(c => $"{term} + {c}"));
        return groups
            .SelectMany(g => g.Value)
            .Concat(narrowed)
            .Concat(all.SelectMany(p => p.Requirements.Terms().Select(r => r.Term)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .Select(t => new FullTextTerm(t))
            .ToList();
    }
}

/// <summary>Ids, within one source, of the listings whose full text contains a term.</summary>
/// <param name="Complete">False when the source capped the results and some may be missing.</param>
public sealed record FullTextResult(IReadOnlySet<string> SourceIds, bool Complete);
