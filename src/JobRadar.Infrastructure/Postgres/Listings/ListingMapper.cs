using JobRadar.Domain.Listings;

namespace JobRadar.Infrastructure.Postgres.Listings;

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
        e.City = listing.Location.City;
        e.State = ToColumn(listing.Location.State);
        e.Postcode = listing.Location.Postcode;
        e.WorkType = ToColumn(listing.WorkType);
        e.SalaryText = listing.SalaryText;
        // Npgsql only writes timestamptz from offset 0.
        e.PostedAt = listing.PostedAt?.ToUniversalTime();
        e.Url = listing.Url.ToString();
        e.SourceCategory = listing.SourceCategory;
        e.LastSeenAt = observedAt.ToUniversalTime();
    }

    internal static ListingRecord ToRecord(ListingEntity e) => new(
        new Listing(
            Id: new ListingId(e.Source, e.SourceId),
            Title: e.Title,
            Company: e.Company,
            Location: new ListingLocation(e.City, ToState(e.State), e.Postcode),
            WorkType: ToWorkType(e.WorkType),
            SalaryText: e.SalaryText,
            PostedAt: e.PostedAt,
            Url: new Uri(e.Url),
            SourceCategory: e.SourceCategory),
        FirstSeenAt: e.FirstSeenAt,
        LastSeenAt: e.LastSeenAt);

    // Unknown is stored as NULL: "the source didn't say" is not a value.
    internal static string? ToColumn(AustralianState state) =>
        state == AustralianState.Unknown ? null : state.ToString();

    internal static AustralianState ToState(string? column) =>
        Enum.TryParse<AustralianState>(column?.Trim(), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : AustralianState.Unknown;

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
