namespace JobRadar.Domain.Listings;

/// <summary>
/// Canonical location. Sources disagree on shape (suburb, area, region); each
/// integration maps what it has, and leaves the rest null.
/// </summary>
public sealed record ListingLocation(string? City, AustralianState State, string? Postcode)
{
    public static ListingLocation Unknown { get; } = new(null, AustralianState.Unknown, null);
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
