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

    /// <summary>The markets it lists jobs for: it runs only the searches of those (a remote board may serve two).</summary>
    IReadOnlySet<JobMarket> Markets { get; }

    /// <summary>
    /// Runs only the searches of the profiles that name it in their extra sources (<see cref="ListingSearch.ExtraSources"/>),
    /// not every search of its markets: a source with a tight budget or a higher risk (LinkedIn) is turned on per profile.
    /// </summary>
    bool OptIn => false;

    /// <summary>
    /// Every listing matching the search, each once, with paging and source quirks handled
    /// inside. Sources cap results (Seek: 500), so the result says whether it is complete.
    /// </summary>
    Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default);
}
