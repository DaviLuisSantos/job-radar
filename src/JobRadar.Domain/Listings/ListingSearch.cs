namespace JobRadar.Domain.Listings;

/// <summary>A search to run against a job source, in neutral terms.</summary>
/// <param name="Keywords">Free text; empty means "everything in this location".</param>
/// <param name="Location">Place as a person would type it, e.g. "Perth" or "All Australia".</param>
/// <param name="PostedWithinDays">Only listings posted in the last N days; null for no limit.</param>
/// <param name="SourceCategory">A category code of the source being searched (opaque to the core).</param>
/// <param name="Country">The market searched: only the sources of that country run it.</param>
public sealed record ListingSearch(
    string Keywords, string Location, int? PostedWithinDays = null, string? SourceCategory = null, Country Country = Country.AU)
{
    /// <summary>
    /// Stable identity of the search, ignoring the date window: "keywords @ location". Another market than
    /// Australia is named at the end, so the same words searched in two countries stay two searches; the
    /// Australian keys predate the countries and stay as they were.
    /// </summary>
    public string Key
    {
        get
        {
            var key = $"{Keywords.Trim().ToLowerInvariant()} @ {Location.Trim().ToLowerInvariant()}";
            if (SourceCategory is not null)
                key += $" [{SourceCategory}]";
            return Country == Country.AU ? key : $"{key} ({Country.ToString().ToLowerInvariant()})";
        }
    }
}

/// <summary>
/// A listing together with the payload the source sent for it, kept verbatim so scores and
/// market numbers can be recomputed later from what was actually collected.
/// </summary>
/// <param name="Description">The full ad, when the search itself returns it (Gupy does); stored like a fetched one.</param>
public sealed record SourcedListing(Listing Listing, string RawPayload, string? Description = null);

/// <summary>Everything a search returned, each listing once.</summary>
/// <param name="Complete">False when the source stopped early (page cap) and more listings exist.</param>
public sealed record SearchResult(IReadOnlyList<SourcedListing> Listings, bool Complete);
