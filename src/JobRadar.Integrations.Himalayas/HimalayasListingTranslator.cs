using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Himalayas.Api;

namespace JobRadar.Integrations.Himalayas;

/// <summary>Himalayas' job → neutral <see cref="Listing"/>. Everything Himalayas-specific stops here.</summary>
internal static class HimalayasListingTranslator
{
    public const string SourceName = "himalayas";

    /// <summary>Listings ids are at most 64 characters (<c>listings.source_id</c>).</summary>
    private const int MaxIdLength = 64;

    public static Listing ToListing(HimalayasJob job) => new(
        Id: new ListingId(SourceName, Id(job)),
        Title: Blank(job.Title) ?? "(untitled)",
        Company: Blank(job.CompanyName),
        Location: ListingLocation.Unknown(Country.Remote),
        WorkType: WorkType(job.EmploymentType),
        SalaryText: Salary(job),
        PostedAt: job.PubDate is { } unix ? DateTimeOffset.FromUnixTimeSeconds(unix) : null,
        Url: new Uri(Blank(job.ApplicationLink) ?? Blank(job.Guid) ?? "https://himalayas.app/jobs"),
        SourceCategory: null,
        Summary: Summary(job),
        Eligibility: Eligibility(job.LocationRestrictions));

    /// <summary>
    /// No numeric id: the guid is the job's URL, ".../companies/{company}/jobs/{slug}". The id is "company/slug",
    /// shortened with a hash of the guid when too long, so it stays the same on every pass.
    /// </summary>
    internal static string Id(HimalayasJob job)
    {
        var guid = Blank(job.Guid) ?? Blank(job.ApplicationLink)
            ?? throw new InvalidOperationException($"Himalayas job '{job.Title}' has no guid.");
        var path = new Uri(guid).AbsolutePath.Trim('/').Split('/');
        var id = path is [.., "companies", var company, "jobs", var slug] ? $"{company}/{slug}" : string.Join('/', path);
        if (id.Length <= MaxIdLength)
            return id;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(guid)))[..16];
        return $"{id[..(MaxIdLength - hash.Length - 1)]}~{hash}";
    }

    /// <summary>An empty restriction list means anyone may apply.</summary>
    internal static string Eligibility(IReadOnlyList<string>? restrictions) =>
        restrictions is { Count: > 0 } places ? string.Join(", ", places.Select(p => p.Trim())) : RemoteEligibility.Worldwide;

    /// <summary>Teaser, "Remote" (the board lists only remote jobs) and the time zones it accepts, joined with " | ".</summary>
    internal static string Summary(HimalayasJob job)
    {
        var parts = new List<string?> { HtmlText.ToPlain(job.Excerpt), "Remote" };
        if (job.TimezoneRestrictions is { Count: > 0 } zones && TimeZoneWindow.Of(zones) is { } window)
            parts.Add(window.ToString());
        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>"USD 4,000–6,000 per month", in the board's own currency and period; null without an amount.</summary>
    internal static string? Salary(HimalayasJob job)
    {
        if (job.MinSalary is null && job.MaxSalary is null)
            return null;
        static string N(decimal? v) => v is { } x ? x.ToString("#,0.##", CultureInfo.InvariantCulture) : "";
        var range = (job.MinSalary, job.MaxSalary) switch
        {
            ({ } min, { } max) when min != max => $"{N(min)}–{N(max)}",
            ({ } min, _) => N(min),
            (_, var max) => N(max),
        };
        var period = job.SalaryPeriod?.ToLowerInvariant() switch
        {
            "annual" or "yearly" or "year" => " per year",
            "monthly" or "month" => " per month",
            "weekly" or "week" => " per week",
            "daily" or "day" => " per day",
            "hourly" or "hour" => " per hour",
            _ => "",
        };
        return $"{Blank(job.Currency) ?? ""} {range}{period}".Trim();
    }

    internal static WorkType WorkType(string? employmentType) => employmentType?.Trim().ToLowerInvariant() switch
    {
        "full time" or "full-time" => Domain.Listings.WorkType.FullTime,
        "part time" or "part-time" => Domain.Listings.WorkType.PartTime,
        "contractor" or "contract" or "freelance" => Domain.Listings.WorkType.Contract,
        "temporary" or "intern" or "internship" => Domain.Listings.WorkType.Casual,
        _ => Domain.Listings.WorkType.Unknown,
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
