using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>Full-text terms found per listing (today in Postgres, table <c>listing_terms</c>).</summary>
public interface IListingTermRepository
{
    /// <summary>
    /// Marks these listings of <paramref name="source"/> as containing <paramref name="term"/>.
    /// Ids of listings this system never collected are ignored. Returns how many marks are new.
    /// </summary>
    Task<int> RecordAsync(string source, string term, IReadOnlyCollection<string> sourceIds, DateTimeOffset foundAt, CancellationToken ct = default);

    /// <summary>Terms of each listing; listings with none are absent.</summary>
    Task<IReadOnlyDictionary<ListingId, IReadOnlyList<string>>> GetTermsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default);
}
