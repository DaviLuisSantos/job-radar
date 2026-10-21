using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// A job site that lists ads for a search. Each site is one integration; the core never
/// knows which one it is talking to beyond <see cref="Name"/>.
/// </summary>
public interface IJobSource
{
    /// <summary>Same value as <see cref="ListingId.Source"/> of the listings it returns.</summary>
    string Name { get; }

    /// <summary>
    /// Every listing matching the search, each once, paging and source quirks handled inside.
    /// </summary>
    IAsyncEnumerable<SourcedListing> SearchAsync(ListingSearch search, CancellationToken ct = default);
}
