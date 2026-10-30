using JobRadar.Domain.Listings;

namespace JobRadar.Infrastructure.Persistence.Listings;

/// <summary>
/// Domain ↔ row. Text values of enums are the schema's spelling, so they live here,
/// not on the enums.
/// </summary>
internal static class ListingMapper
{
    internal static ListingEntity ToNewEntity(Listing listing, DateTimeOffset observedAt)
    {
        var e = new ListingEntity
        {
            Source = listing.Id.Source,
            SourceId = listing.Id.SourceId,
            FirstSeenAt = observedAt.ToUniversalTime(),
        };
        Refresh(e, listing, observedAt);
        return e;
    }

    /// <summary>Copies the latest details from the source; never touches <c>FirstSeenAt</c>.</summary>
    internal static void Refresh(ListingEntity e, Listing listing, DateTimeOffset observedAt)
    {
        e.Title = listing.Title;
        e.Company = listing.Company;
        e.Country = ToColumn(listing.Location.Country);
        e.Suburb = listing.Location.Suburb;
        e.Region = listing.Location.Region;
        e.State = listing.Location.State;
        e.Postcode = listing.Location.Postcode;
        e.WorkType = ToColumn(listing.WorkType);
        e.SalaryText = listing.SalaryText;
        // Npgsql only writes timestamptz from offset 0.
        e.PostedAt = listing.PostedAt?.ToUniversalTime();
        e.Url = listing.Url.ToString();
        e.SourceCategory = listing.SourceCategory;
        e.Summary = listing.Summary;
        e.Eligibility = listing.Eligibility;
        e.LastSeenAt = observedAt.ToUniversalTime();
    }

    internal static ListingRecord ToRecord(ListingEntity e) => new(
        new Listing(
            Id: new ListingId(e.Source, e.SourceId),
            Title: e.Title,
            Company: e.Company,
            Location: new ListingLocation(ToCountry(e.Country), e.Suburb, e.Region, e.State, e.Postcode),
            WorkType: ToWorkType(e.WorkType),
            SalaryText: e.SalaryText,
            PostedAt: e.PostedAt,
            Url: new Uri(e.Url),
            SourceCategory: e.SourceCategory,
            Summary: e.Summary,
            Eligibility: e.Eligibility),
        FirstSeenAt: e.FirstSeenAt,
        LastSeenAt: e.LastSeenAt);

    internal static string ToColumn(Country country) => country switch
    {
        Country.AU => "AU",
        Country.BR => "BR",
        Country.Remote => "ZZ",
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, null),
    };

    internal static Country ToCountry(string column) => column switch
    {
        "AU" => Country.AU,
        "BR" => Country.BR,
        "ZZ" => Country.Remote,
        _ => throw new InvalidOperationException($"Unknown country '{column}' in listings.country."),
    };

    internal static string? ToColumn(WorkType workType) => workType switch
    {
        WorkType.FullTime => "full_time",
        WorkType.PartTime => "part_time",
        WorkType.Casual => "casual",
        WorkType.Contract => "contract",
        _ => null,
    };

    internal static WorkType ToWorkType(string? column) => column?.Trim() switch
    {
        "full_time" => WorkType.FullTime,
        "part_time" => WorkType.PartTime,
        "casual" => WorkType.Casual,
        "contract" => WorkType.Contract,
        _ => WorkType.Unknown,
    };
}
