namespace JobRadar.Domain.Listings;

/// <summary>
/// A listing brought from another store (the prototype) with its own history: when it was first and
/// last seen there, and the keys that tie it to searches or profiles.
/// </summary>
public sealed record ImportedListing(
    SourcedListing Listing,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    IReadOnlyCollection<string> HitKeys);
