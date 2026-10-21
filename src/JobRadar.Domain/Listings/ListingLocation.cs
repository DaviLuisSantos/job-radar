namespace JobRadar.Domain.Listings;

/// <summary>
/// Canonical location. Sources disagree on shape; each integration maps what it has and
/// leaves the rest null.
/// </summary>
/// <param name="Suburb">Most specific place named, e.g. "Osborne Park".</param>
/// <param name="Region">Metro or regional area, e.g. "Perth" or "Mornington Peninsula &amp; Bass Coast".</param>
/// <param name="State">State or territory.</param>
/// <param name="Postcode">Four digits, when the source gives one.</param>
public sealed record ListingLocation(string? Suburb, string? Region, AustralianState State, string? Postcode)
{
    public static ListingLocation Unknown { get; } = new(null, null, AustralianState.Unknown, null);
}

public enum AustralianState
{
    Unknown = 0,
    NSW,
    VIC,
    QLD,
    WA,
    SA,
    TAS,
    ACT,
    NT,
}

public enum WorkType
{
    Unknown = 0,
    FullTime,
    PartTime,
    Casual,
    Contract,
}
