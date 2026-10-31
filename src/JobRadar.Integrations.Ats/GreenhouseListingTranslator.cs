using System.Globalization;
using JobRadar.Domain.Listings;
using JobRadar.Integrations.Ats.Api;

namespace JobRadar.Integrations.Ats;

/// <summary>A Greenhouse job → neutral <see cref="Listing"/>. Everything Greenhouse-specific stops here.</summary>
internal static class GreenhouseListingTranslator
{
    public const string SourceName = "greenhouse";

    /// <summary>Listing ids are at most 64 characters (<c>listings.source_id</c>).</summary>
    private const int MaxIdLength = 64;

    /// <summary>"slug/id": the description stage rebuilds the job's address from it, so it is never shortened.</summary>
    internal static string? IdFor(string slug, long jobId)
    {
        var id = $"{slug}/{jobId.ToString(CultureInfo.InvariantCulture)}";
        return id.Length <= MaxIdLength ? id : null;
    }

    /// <summary>
    /// The job as a listing and the market it belongs to; null when it is in none of our markets or its id does not fit.
    /// The board is asked without the ad, so the summary is what the board does say: departments, offices and place.
    /// </summary>
    internal static (Listing Listing, JobMarket Market)? ToListing(string slug, GreenhouseJob job)
    {
        if (IdFor(slug, job.Id) is not { } id)
            return null;

        var offices = (job.Offices ?? []).SelectMany(o => new[] { o.Name, o.Location }).ToList();
        var place = AtsPlace.Parse(new[] { job.Location?.Name }.Concat(offices));
        if (place is null)
            return null;

        var departments = (job.Departments ?? []).Select(d => Blank(d.Name)).OfType<string>().Distinct().ToList();
        var listing = new Listing(
            Id: new ListingId(SourceName, id),
            Title: Blank(job.Title) ?? "(untitled)",
            Company: Blank(job.CompanyName) ?? slug,
            Location: place.Location,
            WorkType: WorkType.Unknown,
            SalaryText: null,
            PostedAt: Date(job.FirstPublished) ?? Date(job.UpdatedAt),
            Url: ListingUrl.Choose(job.AbsoluteUrl, $"https://boards.greenhouse.io/{Uri.EscapeDataString(slug)}/jobs/{job.Id}"),
            SourceCategory: departments.FirstOrDefault(),
            Summary: Summary(departments, offices, job.Location?.Name),
            Eligibility: place.Eligibility);
        return (listing, place.Market);
    }

    /// <summary>Departments, offices and the place, joined with " | "; null when the board gave none.</summary>
    internal static string? Summary(IEnumerable<string> departments, IEnumerable<string?> offices, string? location)
    {
        var parts = departments.Concat(offices.Select(Blank).OfType<string>()).Append(Blank(location) ?? "")
            .Where(p => p.Length > 0).Distinct();
        var text = string.Join(" | ", parts);
        return text.Length == 0 ? null : text;
    }

    private static DateTimeOffset? Date(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
