using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek;

/// <summary>
/// Market counts and listings by Seek subclassification (the official category, not keywords).
/// Counts read <c>totalCount</c> of the first page, which includes the promoted ads repeated on
/// every page: it is only comparable at the 100-per-page maximum, so the page size is not optional.
/// </summary>
internal sealed class SeekMarketSource(
    SeekSearchClient client,
    SeekJobSource listings,
    IOptions<SeekOptions> options,
    TimeProvider time) : IMarketSource
{
    public string Source => SeekListingTranslator.SourceName;

    public async Task<int> CountAsync(string categoryCode, string city, FullTextTerm? term, CancellationToken ct = default)
    {
        if (options.Value.PageSize != 100)
            throw new InvalidOperationException("Market counts need Seek:PageSize = 100; totalCount depends on the page size.");

        await PauseAsync(ct);
        var keywords = term is null ? "" : SeekFullTextSearch.Query(term);
        var page = await client.SearchAsync(new ListingSearch(keywords, city, SourceCategory: categoryCode), 1, ct);
        return page.TotalCount;
    }

    public async Task<SearchResult> ListAsync(string categoryCode, string city, CancellationToken ct = default)
    {
        await PauseAsync(ct);
        return await listings.SearchAsync(new ListingSearch("", city, SourceCategory: categoryCode), ct);
    }

    private Task PauseAsync(CancellationToken ct)
    {
        var delay = options.Value.DelayBetweenPages;
        return delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay * (0.7 + Random.Shared.NextDouble() * 0.6), time, ct);
    }
}
