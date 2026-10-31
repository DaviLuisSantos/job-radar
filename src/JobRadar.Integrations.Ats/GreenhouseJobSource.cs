using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Ats;

/// <summary>
/// Greenhouse listings, for all four markets. There is no keyword search to call: the sweep (a stage before the
/// collection, <see cref="AtsBoardSweepService"/>) reads whole boards, and this only filters what it left in memory:
/// the search's market, and every word of the search in the title or the department (the same rule as the Jobicy
/// feed). The incremental window is not used: the sweep reads every board it asks in full, and a place on a board is
/// free text that can change, so an old job can enter one of our markets today. Off by default (<c>OptIn</c>): a
/// profile asks for it with <c>"ExtraSources": ["greenhouse"]</c>. The ad is not here: the description stage fetches
/// it for the listings that score well.
/// </summary>
internal sealed class GreenhouseJobSource(AtsSnapshot snapshot) : IJobSource
{
    public string Name => GreenhouseListingTranslator.SourceName;

    public IReadOnlySet<JobMarket> Markets { get; } = new HashSet<JobMarket>
    {
        JobMarket.AU, JobMarket.BR, JobMarket.LatamRemote, JobMarket.GlobalRemote,
    };

    public bool OptIn => true;

    public Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        // No sweep in this pass (it failed, or has not run): say so instead of reporting "nothing found".
        if (snapshot.Current is not { } swept)
            return Task.FromResult(new SearchResult([], Complete: false));

        var words = search.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var found = swept.Jobs
            .Where(j => j.Market == search.Market && Matches(j.Listing, words))
            .Select(j => new SourcedListing(j.Listing, j.RawPayload))
            .ToList();
        return Task.FromResult(new SearchResult(found, swept.Complete));
    }

    internal static bool Matches(Listing listing, string[] words)
    {
        var text = $"{listing.Title} {listing.SourceCategory}";
        return words.All(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
    }
}
