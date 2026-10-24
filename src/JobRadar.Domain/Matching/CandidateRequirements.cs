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

    /// <summary>The state of a requirement phrase: its catalog item's, else the default of its kind.</summary>
    public RequirementState Of(RequirementKind kind, string phrase, ScoringPolicy policy)
    {
        if (States.Count > 0
            && policy.Requirements.FirstOrDefault(i => i.Phrases.Contains(phrase, StringComparer.OrdinalIgnoreCase)) is { } item
            && States.TryGetValue(item.Id, out var state))
            return state;
        return Default(kind, phrase);
    }

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
