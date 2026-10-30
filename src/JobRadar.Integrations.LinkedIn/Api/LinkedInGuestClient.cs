using System.Globalization;
using System.Net;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.LinkedIn.Api;

/// <summary>
/// The two endpoints LinkedIn's public job pages load without an account: the search that fills "see more jobs"
/// and the ad page. Both answer HTML fragments. Undocumented and shared by the open-source scrapers; the fields
/// read are only the ones the translator needs. Retries and timeouts come from the resilience pipeline registered in
/// <see cref="LinkedInServiceCollectionExtensions"/>; the pace from <see cref="LinkedInPacing"/>.
/// </summary>
internal sealed class LinkedInGuestClient(HttpClient http, LinkedInPacing pacing, TimeProvider time)
{
    /// <summary>How long a refusal keeps every request home: past the rest of the pass, short of the next one.</summary>
    internal static readonly TimeSpan BlockedFor = TimeSpan.FromHours(6);

    internal const string SearchPath = "jobs-guest/jobs/api/seeMoreJobPostings/search";
    internal const string PostingPath = "jobs-guest/jobs/api/jobPosting/";

    /// <summary>Cards per page: fixed by the site, not a parameter.</summary>
    internal const int PageSize = 10;

    /// <summary>What a Brazilian profile writes for "work from anywhere in the country" (Gupy's word too).</summary>
    internal const string RemoteBr = "Remoto";

    /// <exception cref="SourceBlockedException">LinkedIn turned the request away (see <see cref="Guard"/>).</exception>
    public async Task<IReadOnlyList<LinkedInJobCard>> SearchAsync(ListingSearch search, int start, CancellationToken ct)
    {
        using var response = await SendAsync(BuildUri(search, start), ct);
        response.EnsureSuccessStatusCode();
        return LinkedInCards.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>The ad; null when LinkedIn no longer has it (404).</summary>
    /// <exception cref="SourceBlockedException">LinkedIn turned the request away (see <see cref="Guard"/>).</exception>
    public async Task<LinkedInJobPosting?> GetPostingAsync(string id, CancellationToken ct)
    {
        using var response = await SendAsync(PostingPath + Uri.EscapeDataString(id), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return LinkedInJobPostings.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// The request, unless LinkedIn refused one recently. A refusal is not an error to retry: 429 past the retries,
    /// 403, 999 (LinkedIn's own "go away"), or a redirect to the login wall, which is where an address lands once
    /// LinkedIn stops trusting it. It blocks every request for <see cref="BlockedFor"/>.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(string uri, CancellationToken ct)
    {
        if (pacing.BlockedReason(time.GetUtcNow()) is { } reason)
            throw new SourceBlockedException(LinkedInListingTranslator.SourceName, $"still blocked: {reason}");

        var response = await http.GetAsync(uri, ct);
        var landed = response.RequestMessage?.RequestUri?.AbsolutePath ?? "";
        var refusal = (int)response.StatusCode is 403 or 429 or 999
            ? $"HTTP {(int)response.StatusCode}"
            : landed.Contains("/authwall", StringComparison.OrdinalIgnoreCase)
                || landed.Contains("/login", StringComparison.OrdinalIgnoreCase)
                || landed.Contains("/checkpoint", StringComparison.OrdinalIgnoreCase)
                ? $"redirected to {landed}"
                : null;
        if (refusal is null)
            return response;

        response.Dispose();
        pacing.Block(time.GetUtcNow() + BlockedFor, refusal);
        throw new SourceBlockedException(LinkedInListingTranslator.SourceName, refusal);
    }

    /// <summary>
    /// Keywords and location as free text, newest first, the date window in seconds (<c>f_TPR=r86400</c> is one day)
    /// and the remote filter (<c>f_WT=2</c>) for a Brazilian "Remoto" search. The offset is <c>start</c>.
    /// </summary>
    internal static string BuildUri(ListingSearch search, int start)
    {
        var (location, remote) = Location(search);
        var query = new List<KeyValuePair<string, string>>
        {
            new("keywords", search.Keywords),
            new("location", location),
            new("sortBy", "DD"),
            new("start", start.ToString(CultureInfo.InvariantCulture)),
        };
        if (search.PostedWithinDays is { } days)
            query.Add(new("f_TPR", $"r{(long)days * 86400}"));
        if (remote)
            query.Add(new("f_WT", "2"));
        return $"{SearchPath}?{string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"))}";
    }

    /// <summary>
    /// The location LinkedIn resolves, from the one the profile writes. It does not read Portuguese ("Brasil" gave
    /// jobs in the United States), and a bare city name can be anywhere, so Brazilian places get the country
    /// appended; "Remoto" is the whole country with the remote filter. Australian places resolve as written
    /// ("Perth WA", "Western Australia"); "All Australia" is Seek's wording for the country.
    /// </summary>
    internal static (string Location, bool Remote) Location(ListingSearch search)
    {
        var location = search.Location.Trim();
        if (search.Market == JobMarket.BR)
        {
            if (location.Equals(RemoteBr, StringComparison.OrdinalIgnoreCase) || location.Equals("Remote", StringComparison.OrdinalIgnoreCase))
                return ("Brazil", true);
            if (location.Length == 0 || location.Equals("Brasil", StringComparison.OrdinalIgnoreCase) || location.Equals("Brazil", StringComparison.OrdinalIgnoreCase))
                return ("Brazil", false);
            return ($"{location}, Brazil", false);
        }
        if (location.Length == 0 || location.Equals("All Australia", StringComparison.OrdinalIgnoreCase))
            return ("Australia", false);
        return (location, false);
    }
}
