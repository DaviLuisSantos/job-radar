using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek;

internal sealed class SeekJobSource(
    SeekSearchClient client,
    IOptions<SeekOptions> options,
    TimeProvider time,
    ILogger<SeekJobSource> log) : IJobSource
{
    public string Name => SeekListingTranslator.SourceName;

    public async Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        var o = options.Value;
        // Promoted ads repeat on every page; each listing is returned once per search.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var listings = new List<SourcedListing>();
        var received = 0;

        for (var page = 1; page <= o.MaxPages; page++)
        {
            if (page > 1)
                await Task.Delay(Jitter(o.DelayBetweenPages), time, ct);

            var result = await client.SearchAsync(search, page, ct);
            foreach (var (job, raw) in result.Jobs)
            {
                if (seen.Add(job.Id))
                    listings.Add(new SourcedListing(SeekListingTranslator.ToListing(job), raw));
            }

            received += result.Jobs.Count;
            if (result.Jobs.Count < o.PageSize || received >= result.TotalCount)
                return new SearchResult(listings, Complete: true);
        }

        log.LogWarning(
            "Seek search {SearchKey} stopped at the {MaxPages}-page cap; more listings exist",
            search.Key, o.MaxPages);
        return new SearchResult(listings, Complete: false);
    }

    private static TimeSpan Jitter(TimeSpan delay) =>
        delay <= TimeSpan.Zero ? TimeSpan.Zero : delay * (0.7 + Random.Shared.NextDouble() * 0.6);
}
