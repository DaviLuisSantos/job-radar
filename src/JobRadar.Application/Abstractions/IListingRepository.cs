using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>Listings collected from every source (today in Postgres, table <c>listings</c>).</summary>
public interface IListingRepository
{
    /// <summary>
    /// Records that these listings were seen at <paramref name="observedAt"/>: new ones are
    /// stored, known ones have their details and last-seen time refreshed.
    /// </summary>
    Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<Listing> listings, DateTimeOffset observedAt, CancellationToken ct = default);

    Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default);
}
