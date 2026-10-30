using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.LinkedIn.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.LinkedIn;

/// <summary>
/// Listings from LinkedIn's public job search, for the two countries: the Australian searches as the profiles write
/// them, the Brazilian ones with the country appended (see <see cref="LinkedInGuestClient.Location"/>). Pages of 10,
/// newest first, inside the date window, until a short page or the page cap. The card does not carry the ad: the
/// description source fetches it later for the listings that matter.
/// </summary>
internal sealed class LinkedInJobSource(
    LinkedInGuestClient client,
    IOptions<LinkedInOptions> options,
    ILogger<LinkedInJobSource> log) : IJobSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => LinkedInListingTranslator.SourceName;

    /// <summary>The configured ones that are countries: a remote market has no place to search LinkedIn by.</summary>
    public IReadOnlySet<JobMarket> Markets { get; } =
        options.Value.Markets.Where(m => m is JobMarket.AU or JobMarket.BR).ToHashSet();

    /// <summary>
    /// Only the profiles that name it: ~1,000 results a search at one request a second, and a login wall after a burst.
    /// </summary>
    public bool OptIn => true;

    public async Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        var o = options.Value;
        var (_, remote) = LinkedInGuestClient.Location(search);
        var seen = new HashSet<string>();
        var listings = new List<SourcedListing>();
        var elsewhere = 0;

        for (var page = 0; page < o.MaxPages; page++)
        {
            var cards = await client.SearchAsync(search, page * LinkedInGuestClient.PageSize, ct);
            foreach (var card in cards)
            {
                if (LinkedInListingTranslator.ToListing(card, search.Market, remote) is not { } listing)
                {
                    elsewhere++;
                    continue;
                }
                if (seen.Add(card.Id))
                    listings.Add(new SourcedListing(listing, JsonSerializer.Serialize(card, Json)));
            }

            // A location LinkedIn cannot resolve answers jobs from another country: a configuration problem, said as one.
            if (page == 0 && cards.Count > 0 && listings.Count == 0)
                throw new InvalidOperationException(
                    $"LinkedIn did not resolve the location '{search.Location}': {cards.Count} listings, none in "
                    + $"{LinkedInLocation.CountryName(LinkedInListingTranslator.Country(search.Market))}.");

            if (cards.Count < LinkedInGuestClient.PageSize)
            {
                if (elsewhere > 0)
                    log.LogDebug("LinkedIn search {SearchKey}: {Elsewhere} listings outside the market's country left out", search.Key, elsewhere);
                return new SearchResult(listings, Complete: true);
            }
        }

        log.LogWarning("LinkedIn search {SearchKey} stopped at the {MaxPages}-page cap; more listings exist", search.Key, o.MaxPages);
        return new SearchResult(listings, Complete: false);
    }
}
