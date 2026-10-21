using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Optional capability of a job source: count and list a whole category of the market, by the
/// source's own category code. Pacing between requests is the source's concern.
/// </summary>
public interface IMarketSource
{
    /// <summary>Same value as <c>ListingId.Source</c>.</summary>
    string Source { get; }

    /// <summary>Open listings of the category in the city; with a term, those that mention it anywhere in the ad.</summary>
    Task<int> CountAsync(string categoryCode, string city, FullTextTerm? term, CancellationToken ct = default);

    /// <summary>Every open listing of the category in the city, up to the source's cap.</summary>
    Task<SearchResult> ListAsync(string categoryCode, string city, CancellationToken ct = default);
}
