using System.Globalization;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Jobicy.Api;

namespace JobRadar.Integrations.Jobicy;

/// <summary>Jobicy's job → neutral <see cref="Listing"/>. Everything Jobicy-specific stops here.</summary>
internal static class JobicyListingTranslator
{
    public const string SourceName = "jobicy";

    public static Listing ToListing(JobicyJob job) => new(
        Id: new ListingId(SourceName, job.Id.ToString(CultureInfo.InvariantCulture)),
        Title: Blank(job.JobTitle) ?? "(untitled)",
        Company: Blank(job.CompanyName),
        Location: ListingLocation.Unknown(Country.Remote),
        WorkType: WorkType(job.JobType),
        SalaryText: Salary(job),
        PostedAt: job.PubDate,
        Url: new Uri(Blank(job.Url) ?? $"https://jobicy.com/jobs/{job.Id}"),
        SourceCategory: null,
        Summary: string.Join(" | ", new[] { HtmlText.ToPlain(job.JobExcerpt), "Remote" }.Where(p => !string.IsNullOrWhiteSpace(p))),
        Eligibility: Eligibility(job.JobGeo));

    /// <summary>"Anywhere" is written as the other boards' "Worldwide"; the rest as it came ("LATAM, Canada, USA").</summary>
    internal static string? Eligibility(string? geo) =>
        Blank(geo) is not { } g ? null
        : g.Equals("Anywhere", StringComparison.OrdinalIgnoreCase) ? RemoteEligibility.Worldwide
        : g;

    internal static string? Salary(JobicyJob job)
    {
        if (job.SalaryMin is null && job.SalaryMax is null)
            return null;
        static string N(decimal? v) => v is { } x ? x.ToString("#,0.##", CultureInfo.InvariantCulture) : "";
        var range = (job.SalaryMin, job.SalaryMax) switch
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
        return $"{Blank(job.SalaryCurrency) ?? ""} {range}{period}".Trim();
    }

    internal static WorkType WorkType(IReadOnlyList<string>? types) => types?.FirstOrDefault()?.Trim().ToLowerInvariant() switch
    {
        "full-time" or "full time" => Domain.Listings.WorkType.FullTime,
        "part-time" or "part time" => Domain.Listings.WorkType.PartTime,
        "contract" or "freelance" => Domain.Listings.WorkType.Contract,
        "internship" or "temporary" => Domain.Listings.WorkType.Casual,
        _ => Domain.Listings.WorkType.Unknown,
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
