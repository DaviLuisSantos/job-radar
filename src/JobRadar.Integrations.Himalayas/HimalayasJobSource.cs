using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Himalayas.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Himalayas;

/// <summary>
/// Remote listings from Himalayas, for both remote markets: the search filters by who may apply (worldwide, or
/// accepting Brazil), and the rules of each market sort out the rest from the eligibility. No date filter: each pass
/// reads the search's pages. The full ad comes with each listing, as HTML, and is stored as text.
/// </summary>
internal sealed class HimalayasJobSource(
    HimalayasSearchClient client,
    IOptions<HimalayasOptions> options,
    ILogger<HimalayasJobSource> log) : IJobSource
{
    public string Name => HimalayasListingTranslator.SourceName;

    public IReadOnlySet<JobMarket> Markets { get; } = new HashSet<JobMarket> { JobMarket.LatamRemote, JobMarket.GlobalRemote };

    public async Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        var o = options.Value;
        var listings = new List<SourcedListing>();
        var seen = new HashSet<ListingId>();

        for (var page = 1; page <= o.MaxPages; page++)
        {
            var result = await client.SearchAsync(search, page, ct);
            foreach (var (job, raw) in result.Jobs)
            {
                var listing = HimalayasListingTranslator.ToListing(job);
                if (seen.Add(listing.Id))
                    listings.Add(new SourcedListing(listing, raw, HtmlText.ToPlain(job.Description)));
            }

            if (result.Jobs.Count < HimalayasOptions.PageSize || page * HimalayasOptions.PageSize >= result.Total)
                return new SearchResult(listings, Complete: true);
        }

        log.LogWarning("Himalayas search {SearchKey} stopped at the {MaxPages}-page cap; more listings exist", search.Key, o.MaxPages);
        return new SearchResult(listings, Complete: false);
    }
}
