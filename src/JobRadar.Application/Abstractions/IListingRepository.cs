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

    /// <summary>
    /// Brings listings in with their own history. New ones are stored as given. Known ones keep their
    /// current details and last-seen time; only the first-seen time moves back if the import saw it earlier.
    /// Payloads and hit keys are added when new.
    /// </summary>
    Task<ObservationResult> ImportAsync(IReadOnlyCollection<ImportedListing> listings, CancellationToken ct = default);

    /// <summary>Every listing that any of these searches ever found.</summary>
    Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default);

    /// <summary>Keys of the searches that ever found this listing: they decide which profiles score it.</summary>
    Task<IReadOnlyCollection<string>> GetSearchKeysAsync(ListingId id, CancellationToken ct = default);

    /// <summary>
    /// Earliest posting date among the listings of <paramref name="source"/> first collected after
    /// <paramref name="since"/>; null when there are none (or none has a date).
    /// </summary>
    Task<DateTimeOffset?> GetOldestPostedAmongFirstSeenSinceAsync(string source, DateTimeOffset since, CancellationToken ct = default);
}
