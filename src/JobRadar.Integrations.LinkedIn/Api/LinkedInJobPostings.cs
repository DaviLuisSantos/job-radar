using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.LinkedIn.Api;

/// <summary>The ad as the guest posting page (<c>jobPosting/{id}</c>) gives it.</summary>
/// <param name="DescriptionHtml">The ad body, as the employer wrote it.</param>
/// <param name="Criteria">"Seniority level", "Employment type", "Job function", "Industries", when the page has them.</param>
/// <param name="Salary">The pay range LinkedIn shows, when the employer gave one.</param>
internal sealed record LinkedInJobPosting(string? DescriptionHtml, IReadOnlyList<KeyValuePair<string, string>> Criteria, string? Salary)
{
    /// <summary>
    /// The ad as plain text for the scorer and the page, with the criteria and the pay appended as lines: the body alone
    /// often leaves the employment type and the seniority unsaid. Null when the page has no ad body.
    /// </summary>
    public string? ToText()
    {
        var body = HtmlText.ToPlain(DescriptionHtml);
        if (body is null)
            return null;
        var lines = Criteria.Select(c => $"{c.Key}: {c.Value}").ToList();
        if (Salary is not null)
            lines.Insert(0, $"Pay: {Salary}");
        return lines.Count == 0 ? body : $"{body}\n\n{string.Join('\n', lines)}";
    }
}

internal static partial class LinkedInJobPostings
{
    /// <exception cref="InvalidOperationException">The page has no ad body: not an ad page as this reader knows it.</exception>
    public static LinkedInJobPosting Parse(string html)
    {
        var description = LinkedInHtml.Element(html, DescriptionOpen(), "div")
            ?? throw new InvalidOperationException("The LinkedIn posting page has no ad body (show-more-less-html__markup).");
        var criteria = Criteria().Matches(html)
            .Select(m => (Name: LinkedInHtml.Text(m.Groups[1].Value), Value: LinkedInHtml.Text(m.Groups[2].Value)))
            .Where(c => c.Name is not null && c.Value is not null)
            .Select(c => KeyValuePair.Create(c.Name!, c.Value!))
            .ToList();
        return new LinkedInJobPosting(description, criteria, LinkedInHtml.First(html, Salary()));
    }

    [GeneratedRegex(@"<div[^>]*" + LinkedInHtml.ClassOpen + "show-more-less-html__markup" + LinkedInHtml.ClassClose + @"[^>]*>", RegexOptions.Singleline)]
    private static partial Regex DescriptionOpen();

    [GeneratedRegex(@"description__job-criteria-subheader[^>]*>(.*?)</h3>\s*<span[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex Criteria();

    [GeneratedRegex(LinkedInHtml.ClassOpen + "compensation__salary" + LinkedInHtml.ClassClose + @"[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex Salary();
}
