namespace JobRadar.Domain.Listings;

/// <summary>A search to run against a job source, in neutral terms.</summary>
/// <param name="Keywords">Free text; empty means "everything in this location".</param>
/// <param name="Location">Place as a person would type it, e.g. "Perth" or "All Australia".</param>
/// <param name="PostedWithinDays">Only listings posted in the last N days; null for no limit.</param>
public sealed record ListingSearch(string Keywords, string Location, int? PostedWithinDays = null);

/// <summary>
/// A listing together with the payload the source sent for it, kept verbatim so scores and
/// market numbers can be recomputed later from what was actually collected.
/// </summary>
public sealed record SourcedListing(Listing Listing, string RawPayload);
