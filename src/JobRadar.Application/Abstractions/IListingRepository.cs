using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Listings collected from every source, the payloads they came with and the searches that found
/// them (today in Postgres, tables <c>listings</c>, <c>listing_payloads</c>, <c>listing_search_hits</c>).
/// </summary>
public interface IListingRepository
{
    /// <summary>
    /// Records that <paramref name="searchKey"/> found these listings at <paramref name="observedAt"/>:
    /// new ones are stored, known ones have their details and last-seen time refreshed. A payload is
    /// stored once per distinct content, so an unchanged ad adds nothing.
    /// </summary>
    Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, string searchKey, DateTimeOffset observedAt, CancellationToken ct = default);

    Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default);

    /// <summary>Every listing that any of these searches ever found.</summary>
    Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default);
}
