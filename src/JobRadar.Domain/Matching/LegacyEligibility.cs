namespace JobRadar.Domain.Matching;

/// <summary>
/// Transitional: the eligibility rules that lived in code until they moved to the local configuration. Used only when
/// a market has no configured block, and compared with the configured one by the parity test, so the move is proved
/// before this file is deleted.
/// </summary>
internal static class LegacyEligibility
{
    public static EligibilityRules Australia { get; } = new()
    {
        RestrictedPattern = ListingPatterns.CitizenshipRequired.ToString(),
        IncompatiblePattern = ListingPatterns.WorkRightsIncompatible.ToString(),
        ReservedTitlePattern = ListingPatterns.IdentifiedRole.ToString(),
        WelcomePattern = ListingPatterns.WorkingHolidayFriendly.ToString(),
        AssistanceDeniedPattern = ListingPatterns.SponsorshipDenied.ToString(),
        AssistanceOfferedPhrases = ListingPatterns.SponsorshipOffered,
        AssistanceWord = "sponsor",
        Phrases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [FullTextGroup.Exclude] =
            [
                "unlimited working rights", "unlimited australian working rights",
                "unrestricted working rights", "unrestricted australian working rights",
                "permanent working rights", "permanent australian working rights",
            ],
            [FullTextGroup.Doubt] = ["australian citizen", "permanent resident"],
            [FullTextGroup.Ambiguous] = ["full working rights", "full australian working rights"],
            [FullTextGroup.Welcome] = ["working holiday", "backpackers", "88 days"],
            [FullTextGroup.WelcomeRetracted] =
            [
                "working holiday + citizen", "working holiday + citizenship",
                "working holiday + resident", "working holiday + residency",
            ],
            [FullTextGroup.Tenure] = ["12 months + working rights"],
        },
    };

    /// <summary>The remote markets: a status elsewhere as a condition, added to <see cref="RemotePatterns.ResidencyRestricted"/>.</summary>
    public static EligibilityRules Remote { get; } = new()
    {
        RestrictedPattern = $@"\b{RemotePatterns.Elsewhere}\s+(?:citizens?|citizenship)\s+(?:only|required)\b",
    };
}
