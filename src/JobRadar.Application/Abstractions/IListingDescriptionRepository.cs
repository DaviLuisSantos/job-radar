using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>Full ad texts the candidate captured (today in Postgres, table <c>listing_descriptions</c>).</summary>
public interface IListingDescriptionRepository
{
    /// <summary>Stores the text, replacing an earlier capture of the same listing.</summary>
    Task SaveAsync(ListingDescription description, CancellationToken ct = default);

    Task<ListingDescription?> FindAsync(ListingId id, CancellationToken ct = default);

    /// <summary>The captured text of each of these listings that has one.</summary>
    Task<IReadOnlyDictionary<ListingId, string>> GetTextsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default);
}
