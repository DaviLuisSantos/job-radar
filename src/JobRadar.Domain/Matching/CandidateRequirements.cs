using static JobRadar.Domain.Matching.ListingPatterns;

namespace JobRadar.Domain.Matching;

/// <summary>Where the candidate stands on a requirement an ad asks for.</summary>
public enum RequirementState
{
    /// <summary>Has it: the ad asking for it is no obstacle.</summary>
    Have,

    /// <summary>Would get it for the job with a short course (HRWL, VOC).</summary>
    Obtainable,

    /// <summary>Does not have it (years of experience, a trade, a degree).</summary>
    Missing,

    /// <summary>Cannot get it on this visa or soon (a truck licence needs years of a car licence).</summary>
    OutOfReach,
}

/// <summary>
/// The candidate's state on each catalog requirement (<see cref="ScoringPolicy.Requirements"/>), by item id.
/// An item without a state, and a profile's own phrase outside the catalog, take the default of their
/// kind, which is how every requirement was weighed before states existed: a truck licence out of reach, any
/// other licence or ticket obtainable, experience and qualifications missing.
/// </summary>
public sealed record CandidateRequirements(IReadOnlyDictionary<string, RequirementState> States)
{
    public static CandidateRequirements None { get; } = new(new Dictionary<string, RequirementState>());

    public static RequirementState Default(RequirementKind kind, string phrase) => kind switch
    {
        RequirementKind.Licence when TruckLicence.IsMatch(phrase) => RequirementState.OutOfReach,
        RequirementKind.Licence => RequirementState.Obtainable,
        _ => RequirementState.Missing,
    };

    /// <summary>
    /// The state of a requirement phrase: its catalog item's, else the default of its kind. A truck licence the
    /// candidate covers (<see cref="CoversTruck"/>) is had, whatever the state of its own item.
    /// </summary>
    public RequirementState Of(RequirementKind kind, string phrase, ScoringPolicy policy)
    {
        if (kind == RequirementKind.Licence && TruckLicence.IsMatch(phrase) && CoversTruck(phrase))
            return RequirementState.Have;
        if (States.Count > 0
            && policy.Requirements.FirstOrDefault(i => i.Phrases.Contains(phrase, StringComparer.OrdinalIgnoreCase)) is { } item
            && States.TryGetValue(item.Id, out var state))
            return state;
        return Default(kind, phrase);
    }

    /// <summary>
    /// Whether a truck licence the candidate holds (catalog items <c>truck-mr</c> to <c>truck-mc</c> set to
    /// have) allows driving the class <paramref name="asked"/> names: in Australia each class includes the lower
    /// ones (MR, HR, HC, MC). An ad naming two ("HR/HC licence") takes either, so the lower one counts.
    /// </summary>
    public bool CoversTruck(string asked)
    {
        var held = States
            .Where(s => s.Value == RequirementState.Have && s.Key.StartsWith("truck-", StringComparison.OrdinalIgnoreCase))
            .Select(s => TruckRank(s.Key["truck-".Length..]))
            .DefaultIfEmpty(0)
            .Max();
        var asks = TruckClass.Matches(asked).Select(m => TruckRank(m.Value)).Where(r => r > 0).ToList();
        return held > 0 && asks.Count > 0 && held >= asks.Min();
    }

    private static int TruckRank(string truckClass) => string.Join(' ', truckClass.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) switch
    {
        "mr" => 1,
        "hr" or "heavy rigid" => 2,
        "hc" or "heavy combination" => 3,
        "mc" => 4,
        _ => 0,
    };

    /// <summary>
    /// The states that differ from the default for the items <paramref name="profile"/> searches, in id
    /// order: what a profile's rules hash must follow. Empty for a candidate who set nothing, so the hash stays
    /// what it was before states existed.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, RequirementState>> Relevant(MatchingProfile profile, ScoringPolicy policy)
    {
        var phrases = profile.Requirements.Terms().Select(t => t.Phrase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return policy.Requirements
            .Where(i => i.Phrases.Any(phrases.Contains) && States.TryGetValue(i.Id, out var s) && s != Default(i.Kind, i.Phrases[0]))
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .Select(i => KeyValuePair.Create(i.Id, States[i.Id]))
            .ToList();
    }
}
