namespace JobRadar.Domain.Listings;

/// <summary>Whether someone living in Brazil may apply, read from a listing's <see cref="Listing.Eligibility"/>.</summary>
public enum EligibilityScope
{
    /// <summary>The source does not say.</summary>
    Unknown,

    /// <summary>Open to anyone, anywhere: the global remote market.</summary>
    Worldwide,

    /// <summary>Restricted to a region or a list of countries that includes Brazil: the LATAM remote market.</summary>
    IncludesBrazil,

    /// <summary>Restricted to places that leave Brazil out ("USA", "Europe", "Canada, Mexico").</summary>
    ExcludesBrazil,
}

/// <summary>
/// Reads the eligibility the remote boards give, as one text: "Worldwide", "LATAM", "Brazil, Colombia, Mexico",
/// "EMEA, LATAM, Canada, USA". Each part is compared whole, so "North America" is not "Americas".
/// </summary>
public static class RemoteEligibility
{
    /// <summary>How the translators write "open to the world", whatever the source called it.</summary>
    public const string Worldwide = "Worldwide";

    private static readonly HashSet<string> World = new(StringComparer.OrdinalIgnoreCase)
    {
        "worldwide", "anywhere", "anywhere in the world", "global", "globally", "remote", "world",
    };

    private static readonly HashSet<string> WithBrazil = new(StringComparer.OrdinalIgnoreCase)
    {
        "brazil", "brasil", "latam", "latin america", "south america", "americas", "the americas", "america latina",
        "américa latina",
    };

    /// <summary>The places named, one per part, as the source wrote them.</summary>
    public static IReadOnlyList<string> Parts(string? eligibility) =>
        string.IsNullOrWhiteSpace(eligibility)
            ? []
            : eligibility.Split([',', ';', '/', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static EligibilityScope Classify(string? eligibility)
    {
        var parts = Parts(eligibility);
        if (parts.Count == 0)
            return EligibilityScope.Unknown;
        if (parts.Any(World.Contains))
            return EligibilityScope.Worldwide;
        return parts.Any(WithBrazil.Contains) ? EligibilityScope.IncludesBrazil : EligibilityScope.ExcludesBrazil;
    }
}
