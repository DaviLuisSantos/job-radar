using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Jobicy.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Jobicy;

/// <summary>
/// Remote listings from Jobicy for the LATAM market. The API has no free-text search (its tag reads titles only), so
/// each search reads the region's feed and keeps the jobs whose title or ad has every word of the keywords. The feed
/// is the last 7 days: the incremental window is not used. The full ad comes as HTML and is stored as text.
/// </summary>
internal sealed class JobicyJobSource(
    JobicySearchClient client,
    IOptions<JobicyOptions> options,
    ILogger<JobicyJobSource> log) : IJobSource
{
    public string Name => JobicyListingTranslator.SourceName;

    public IReadOnlySet<JobMarket> Markets { get; } = new HashSet<JobMarket> { JobMarket.LatamRemote };

    public async Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        var words = search.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var listings = new List<SourcedListing>();
        var seen = new HashSet<long>();
        string? cursor = null;

        for (var page = 1; page <= options.Value.MaxPages; page++)
        {
            var result = await client.PageAsync(cursor, ct);
            foreach (var (job, raw) in result.Jobs)
            {
                var description = HtmlText.ToPlain(job.JobDescription);
                var text = $"{job.JobTitle} {description}";
                if (words.All(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)) && seen.Add(job.Id))
                    listings.Add(new SourcedListing(JobicyListingTranslator.ToListing(job), raw, description));
            }

            if (result.NextCursor is null)
                return new SearchResult(listings, Complete: true);
            cursor = result.NextCursor;
        }

        log.LogWarning("Jobicy search {SearchKey} stopped at the {MaxPages}-page cap; more listings exist", search.Key, options.Value.MaxPages);
        return new SearchResult(listings, Complete: false);
    }
}
