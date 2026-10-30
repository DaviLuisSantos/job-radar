using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Gupy.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Gupy;

/// <summary>
/// Brazilian listings from Gupy's portal. The search has no date filter, so the incremental window is ignored:
/// each pass reads the search's pages, a few hundred listings at most. The full ad comes with each listing and
/// is stored as its description; talent pools (not open vacancies) are left out.
/// </summary>
internal sealed class GupyJobSource(
    GupySearchClient client,
    IOptions<GupyOptions> options,
    ILogger<GupyJobSource> log) : IJobSource
{
    public string Name => GupyListingTranslator.SourceName;

    public Country Country => Country.BR;

    public async Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        var o = options.Value;
        var seen = new HashSet<long>();
        var listings = new List<SourcedListing>();
        var offset = 0;

        for (var page = 1; page <= o.MaxPages; page++)
        {
            var result = await client.SearchAsync(search, offset, ct);
            foreach (var (job, raw) in result.Jobs)
            {
                if (!job.IsTalentPool && seen.Add(job.Id))
                    listings.Add(new SourcedListing(GupyListingTranslator.ToListing(job), raw, job.Description));
            }

            offset += result.Jobs.Count;
            if (result.Jobs.Count < o.PageSize || offset >= result.Total)
                return new SearchResult(listings, Complete: true);
        }

        log.LogWarning("Gupy search {SearchKey} stopped at the {MaxPages}-page cap; more listings exist", search.Key, o.MaxPages);
        return new SearchResult(listings, Complete: false);
    }
}
