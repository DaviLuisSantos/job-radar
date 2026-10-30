namespace JobRadar.Domain.Listings;

/// <summary>
/// Canonical location. Sources disagree on shape; each integration maps what it has and
/// leaves the rest null. The country is always known: it is the source's market.
/// </summary>
/// <param name="Country">Where the job is; <see cref="Country.Remote"/> for a remote job open across countries.</param>
/// <param name="Suburb">Most specific place named, e.g. "Osborne Park".</param>
/// <param name="Region">Metro or regional area, e.g. "Perth" or "Mornington Peninsula &amp; Bass Coast".</param>
/// <param name="State">State or territory as the country abbreviates it: "WA" in Australia, "SP" in Brazil.</param>
/// <param name="Postcode">Postcode or CEP, when the source gives one.</param>
public sealed record ListingLocation(Country Country, string? Suburb, string? Region, string? State, string? Postcode)
{
    public static ListingLocation Unknown(Country country) => new(country, null, null, null, null);
}

/// <summary>Where a job is, by its ISO 3166-1 code; <see cref="Remote"/> when it is in no country.</summary>
public enum Country
{
    AU,
    BR,

    /// <summary>
    /// A remote job open to people in several countries: it is nowhere in particular. Who may apply is the
    /// listing's <see cref="Listing.Eligibility"/>. Stored as "ZZ", the code ISO leaves for user use.
    /// </summary>
    Remote,
}

public enum WorkType
{
    Unknown = 0,
    FullTime,
    PartTime,
    Casual,
    Contract,
}
