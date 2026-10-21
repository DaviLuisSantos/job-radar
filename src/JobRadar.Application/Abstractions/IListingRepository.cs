using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Listings collected from every source and the payloads they came with
/// (today in Postgres, tables <c>listings</c> and <c>listing_payloads</c>).
/// </summary>
public interface IListingRepository
{
    /// <summary>
    /// Records that these listings were seen at <paramref name="observedAt"/>: new ones are
    /// stored, known ones have their details and last-seen time refreshed. A payload is stored
    /// once per distinct content, so an unchanged ad adds nothing.
    /// </summary>
    Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, DateTimeOffset observedAt, CancellationToken ct = default);

    Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default);
}
