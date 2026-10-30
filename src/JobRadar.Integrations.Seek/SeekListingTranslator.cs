using JobRadar.Domain.Listings;
using JobRadar.Integrations.Seek.Api;

namespace JobRadar.Integrations.Seek;

/// <summary>Seek's ad → neutral <see cref="Listing"/>. Every Seek quirk is absorbed here.</summary>
/// <remarks>
/// <see cref="ParseLocation"/> and <see cref="ParseWorkType"/> are public on purpose: the prototype stored
/// Seek labels verbatim and its importer (Integrations.Prototype) reads them the same way.
/// </remarks>
public static class SeekListingTranslator
{
    public const string SourceName = "seek";

    private static readonly Uri JobBase = new("https://www.seek.com.au/job/");

    internal static Listing ToListing(SeekJob job) => new(
        Id: new ListingId(SourceName, job.Id),
        Title: job.Title?.Trim() ?? "",
        // ~9% of ads have no companyName (private advertisers, some agencies).
        Company: Blank(job.CompanyName) ?? Blank(job.Advertiser?.Description),
        Location: ParseLocation(job.Locations?.FirstOrDefault()?.Label),
        WorkType: ParseWorkType(job.WorkTypes?.FirstOrDefault()),
        // The API sends "" when the advertiser hides the salary.
        SalaryText: Blank(job.SalaryLabel),
        PostedAt: job.ListingDate,
        Url: new Uri(JobBase, job.Id),
        SourceCategory: Blank(job.Classifications?.FirstOrDefault()?.Subclassification?.Description),
        Summary: Summary(job));

    /// <summary>Teaser, bullet points and work arrangement ("Hybrid"), joined with " | ".</summary>
    internal static string? Summary(SeekJob job)
    {
        var parts = new List<string?> { job.Teaser };
        parts.AddRange(job.BulletPoints ?? []);
        parts.Add(job.WorkArrangements?.DisplayText);
        var text = string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return text.Length == 0 ? null : text;
    }

    /// <summary>Seek is Australian: its labels end in one of these, upper case.</summary>
    private static readonly HashSet<string> States = new(StringComparer.Ordinal) { "NSW", "VIC", "QLD", "WA", "SA", "TAS", "ACT", "NT" };

    /// <summary>
    /// Labels are "Region STATE" or "Suburb, Region STATE", and some regions contain a comma
    /// themselves ("Austinmer, Wollongong, Illawarra &amp; South Coast NSW"): split on the first
    /// comma only.
    /// </summary>
    public static ListingLocation ParseLocation(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return ListingLocation.Unknown(Country.AU);

        var text = label.Trim();
        string? state = null;
        var lastSpace = text.LastIndexOf(' ');
        if (lastSpace > 0 && States.Contains(text[(lastSpace + 1)..]))
        {
            state = text[(lastSpace + 1)..];
            text = text[..lastSpace].TrimEnd();
        }

        var comma = text.IndexOf(", ", StringComparison.Ordinal);
        return comma < 0
            ? new ListingLocation(Country.AU, null, Blank(text), state, null)
            : new ListingLocation(Country.AU, Blank(text[..comma]), Blank(text[(comma + 2)..]), state, null);
    }

    public static WorkType ParseWorkType(string? seekWorkType) => seekWorkType?.Trim() switch
    {
        "Full time" => WorkType.FullTime,
        "Part time" => WorkType.PartTime,
        "Casual/Vacation" => WorkType.Casual,
        "Contract/Temp" => WorkType.Contract,
        _ => WorkType.Unknown,
    };

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
