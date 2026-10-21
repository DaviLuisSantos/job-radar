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

    public static ScoringInput From(Listing listing, IReadOnlyCollection<string>? fullTextTerms = null, string? description = null) => new(
        listing.Title,
        listing.Summary,
        FormatLocation(listing.Location),
        FormatWorkType(listing.WorkType),
        listing.PostedAt,
        fullTextTerms ?? [])
    {
        Description = description,
    };

    /// <summary>"Suburb, Region STATE", leaving out what is unknown.</summary>
    public static string? FormatLocation(ListingLocation l)
    {
        var place = (l.Suburb, l.Region) switch
        {
            ({ } s, { } r) => $"{s}, {r}",
            (null, { } r) => r,
            ({ } s, null) => s,
            _ => null,
        };
        var state = l.State == AustralianState.Unknown ? null : l.State.ToString();
        return (place, state) switch
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
