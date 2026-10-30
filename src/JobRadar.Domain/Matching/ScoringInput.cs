using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Matching;

/// <summary>The text the scorer reads, as labels a person would see on the ad.</summary>
/// <param name="Summary">Short text the source gives (teaser, bullets); null when none.</param>
/// <param name="LocationLabel">e.g. "Osborne Park, Perth WA".</param>
/// <param name="WorkTypeLabel">e.g. "Contract/Temp".</param>
/// <param name="FullTextTerms">Phrases found in the full ad text by phrase searches.</param>
public sealed record ScoringInput(
    string Title,
    string? Summary,
    string? LocationLabel,
    string? WorkTypeLabel,
    DateTimeOffset? PostedAt,
    IReadOnlyCollection<string> FullTextTerms)
{
    /// <summary>The full ad text, when the candidate captured it; null for most listings.</summary>
    public string? Description { get; init; }

    /// <summary>Who may apply, from remote boards (<see cref="Listing.Eligibility"/>).</summary>
    public string? Eligibility { get; init; }

    /// <summary>The source's pay field, read only by profiles with a <see cref="MatchingProfile.Compensation"/>.</summary>
    public string? SalaryText { get; init; }

    public static ScoringInput From(Listing listing, IReadOnlyCollection<string>? fullTextTerms = null, string? description = null) => new(
        listing.Title,
        listing.Summary,
        FormatLocation(listing.Location),
        FormatWorkType(listing.WorkType),
        listing.PostedAt,
        fullTextTerms ?? [])
    {
        Description = description,
        Eligibility = listing.Eligibility,
        SalaryText = listing.SalaryText,
    };

    /// <summary>"Suburb, Region STATE", leaving out what is unknown; "Remote" for a job in no country.</summary>
    public static string? FormatLocation(ListingLocation l)
    {
        if (l.Country == Country.Remote && l is { Suburb: null, Region: null, State: null })
            return "Remote";
        var place = (l.Suburb, l.Region) switch
        {
            ({ } s, { } r) => $"{s}, {r}",
            (null, { } r) => r,
            ({ } s, null) => s,
            _ => null,
        };
        return (place, l.State) switch
        {
            (null, null) => null,
            (null, { } st) => st,
            ({ } p, null) => p,
            ({ } p, { } st) => $"{p} {st}",
        };
    }

    /// <summary>
    /// The usual Australian wording. "Temp" and "casual" matter: they mark work that fits a
    /// Working Holiday visa's six months per employer.
    /// </summary>
    public static string? FormatWorkType(WorkType w) => w switch
    {
        WorkType.FullTime => "Full time",
        WorkType.PartTime => "Part time",
        WorkType.Casual => "Casual/Vacation",
        WorkType.Contract => "Contract/Temp",
        _ => null,
    };
}
