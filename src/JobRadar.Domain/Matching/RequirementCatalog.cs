namespace JobRadar.Domain.Matching;

/// <summary>
/// Something an ad can ask of a candidate, as ads write it. Market knowledge, the same for every candidate:
/// a profile picks the items its line of work runs into (<see cref="ScoringPolicy.Requirements"/>).
/// </summary>
/// <param name="Id">Stable name profiles use ("hrwl").</param>
/// <param name="Phrases">
/// Searched in the full ad like any requirement phrase. A truck licence class is told apart from a ticket
/// by <see cref="ListingPatterns.TruckLicence"/>, not by the item.
/// </param>
public sealed record RequirementItem(string Id, RequirementKind Kind, IReadOnlyList<string> Phrases)
{
    /// <summary>
    /// A truck licence class: read in the ad's own text for every profile, not only the ones that search it.
    /// </summary>
    public bool IsTruckLicence => Kind == RequirementKind.Licence && Phrases.Any(ListingPatterns.TruckLicence.IsMatch);

    /// <summary>
    /// Cheap and generous: the item's phrases (for a truck class, any class name) anywhere in the listing's text
    /// or searched terms. The scorer decides; this only spares it the listings that cannot ask for the item.
    /// </summary>
    public bool MayBeAskedIn(ScoringInput input)
    {
        var text = $"{input.Title} {input.Summary} {input.Description}";
        return IsTruckLicence
            ? ListingPatterns.TruckClass.IsMatch(text) || input.FullTextTerms.Any(ListingPatterns.TruckClass.IsMatch)
            : Phrases.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase)
                || input.FullTextTerms.Any(t => t.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
    }
}

/// <summary>
/// The requirements known so far. Phrases are the ones calibrated against the candidate's labels (see the
/// docs/plano.md diary): a phrase added to an item is searched for every profile that uses the item.
/// </summary>
public static class RequirementCatalog
{
    public static IReadOnlyList<RequirementItem> Defaults { get; } =
    [
        new("experience-years", RequirementKind.Experience,
            ["min 2 years", "minimum of 2 years", "minimum 2 years", "years experience", "years of experience"]),
        new("experience-proven", RequirementKind.Experience,
            ["experience in a similar role", "proven experience", "demonstrated experience"]),
        new("experience-operating", RequirementKind.Experience, ["experience operating", "operating experience"]),

        new("trade-qualification", RequirementKind.Qualification,
            ["trade qualified", "trade qualification", "trade certificate", "cert III", "certificate III"]),
        new("degree", RequirementKind.Qualification, ["degree in", "tertiary qualification", "relevant degree"]),
        // Licences of a trade: they need the trade certificate plus supervised time.
        new("refrigerant-licence", RequirementKind.Qualification, ["refrigerant handling licence", "ARCtick"]),
        new("electrical-licence", RequirementKind.Qualification, ["electrical licence"]),

        new("truck-hr", RequirementKind.Licence, ["HR licence", "HR license", "heavy rigid"]),
        new("truck-mr", RequirementKind.Licence, ["MR licence"]),
        new("truck-hc", RequirementKind.Licence, ["HC licence"]),
        new("truck-mc", RequirementKind.Licence, ["MC licence"]),
        new("hrwl", RequirementKind.Licence, ["HRWL"]),
        new("voc", RequirementKind.Licence, ["verification of competency"]),
    ];
}
