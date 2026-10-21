namespace JobRadar.Domain.Listings;

/// <summary>A search to run against a job source, in neutral terms.</summary>
/// <param name="Keywords">Free text; empty means "everything in this location".</param>
/// <param name="Location">Place as a person would type it, e.g. "Perth" or "All Australia".</param>
/// <param name="PostedWithinDays">Only listings posted in the last N days; null for no limit.</param>
public sealed record ListingSearch(string Keywords, string Location, int? PostedWithinDays = null)
{
    /// <summary>Stable identity of the search, ignoring the date window: "keywords @ location".</summary>
    public string Key => $"{Keywords.Trim().ToLowerInvariant()} @ {Location.Trim().ToLowerInvariant()}";
}

/// <summary>
/// A listing together with the payload the source sent for it, kept verbatim so scores and
/// market numbers can be recomputed later from what was actually collected.
/// </summary>
public sealed record SourcedListing(Listing Listing, string RawPayload);

/// <summary>Everything a search returned, each listing once.</summary>
/// <param name="Complete">False when the source stopped early (page cap) and more listings exist.</param>
public sealed record SearchResult(IReadOnlyList<SourcedListing> Listings, bool Complete);
