using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Integrations.LinkedIn.Api;

/// <summary>
/// One result card of the guest search, as the page shows it. Stored as the listing's payload (as JSON: the source
/// sends HTML), so it holds only what is stable from one pass to the next: no "be an early applicant" badges.
/// </summary>
/// <param name="PostedOn">The day the card's <c>&lt;time&gt;</c> names; the hour is not given.</param>
/// <param name="Link">The ad's page, without the tracking query string.</param>
internal sealed record LinkedInJobCard(
    string Id,
    string Title,
    string? Company,
    string? Location,
    string? Salary,
    DateOnly? PostedOn,
    string? Link);

/// <summary>The cards of one page of the guest search (<c>seeMoreJobPostings/search</c>): a list of <c>&lt;li&gt;</c>.</summary>
internal static partial class LinkedInCards
{
    public static IReadOnlyList<LinkedInJobCard> Parse(string html)
    {
        var urns = Urn().Matches(html);
        var cards = new List<LinkedInJobCard>(urns.Count);
        for (var i = 0; i < urns.Count; i++)
        {
            // Everything from this card's id to the next card's is this card's.
            var end = i + 1 < urns.Count ? urns[i + 1].Index : html.Length;
            var card = html[urns[i].Index..end];
            cards.Add(new LinkedInJobCard(
                Id: urns[i].Groups[1].Value,
                Title: LinkedInHtml.First(card, Title()) ?? "(untitled)",
                Company: LinkedInHtml.First(card, Company()),
                Location: LinkedInHtml.First(card, Location()),
                Salary: LinkedInHtml.First(card, Salary()),
                PostedOn: Posted().Match(card) is { Success: true } posted
                    && DateOnly.TryParseExact(posted.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    ? day
                    : null,
                Link: Link().Match(card) is { Success: true } link ? WithoutQuery(WebUtility.HtmlDecode(link.Groups[1].Value)) : null));
        }
        return cards;
    }

    private static string WithoutQuery(string url) => url.IndexOf('?') is var i and >= 0 ? url[..i] : url;

    [GeneratedRegex(@"data-entity-urn=""urn:li:jobPosting:(\d+)""")]
    private static partial Regex Urn();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "base-search-card__title" + LinkedInHtml.ClassClose + @"[^>]*>(.*?)</h3>", RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "base-search-card__subtitle" + LinkedInHtml.ClassClose + @"[^>]*>(.*?)</h4>", RegexOptions.Singleline)]
    private static partial Regex Company();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "job-search-card__location" + LinkedInHtml.ClassClose + @"[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex Location();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "job-search-card__salary-info" + LinkedInHtml.ClassClose + @"[^>]*>(.*?)</(?:span|div)>", RegexOptions.Singleline)]
    private static partial Regex Salary();

    [GeneratedRegex(@"<time[^>]*datetime=""(\d{4}-\d{2}-\d{2})""")]
    private static partial Regex Posted();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "base-card__full-link" + LinkedInHtml.ClassClose + @"[^>]*href=""([^""]*)""", RegexOptions.Singleline)]
    private static partial Regex Link();
}
