namespace JobRadar.Domain.Listings;

/// <summary>
/// Canonical location. Sources disagree on shape; each integration maps what it has and
/// leaves the rest null. The country is always known: it is the source's market.
/// </summary>
/// <param name="Country">The job market the listing belongs to; scores and reports never mix two.</param>
/// <param name="Suburb">Most specific place named, e.g. "Osborne Park".</param>
/// <param name="Region">Metro or regional area, e.g. "Perth" or "Mornington Peninsula &amp; Bass Coast".</param>
/// <param name="State">State or territory as the country abbreviates it: "WA" in Australia, "SP" in Brazil.</param>
/// <param name="Postcode">Postcode or CEP, when the source gives one.</param>
public sealed record ListingLocation(Country Country, string? Suburb, string? Region, string? State, string? Postcode)
{
    public static ListingLocation Unknown(Country country) => new(country, null, null, null, null);
}

/// <summary>A job market, by its ISO 3166-1 code.</summary>
public enum Country
{
    AU,
    BR,
}

public enum WorkType
{
    Unknown = 0,
    FullTime,
    PartTime,
    Casual,
    Contract,
}
