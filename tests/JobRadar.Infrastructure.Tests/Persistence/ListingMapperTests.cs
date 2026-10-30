using JobRadar.Domain.Listings;
using JobRadar.Infrastructure.Persistence.Listings;

namespace JobRadar.Infrastructure.Tests.Persistence;

public class ListingMapperTests
{
    private static readonly DateTimeOffset Perth = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(8));

    internal static Listing Sample(string sourceId = "1", string title = "Backend Developer (.NET)") => new(
        Id: new ListingId("seek", sourceId),
        Title: title,
        Company: "Acme",
        Location: new ListingLocation(Country.AU, "Osborne Park", "Perth", "WA", "6017"),
        WorkType: WorkType.Contract,
        SalaryText: "$90k - $110k",
        PostedAt: Perth.AddDays(-2),
        Url: new Uri("https://www.seek.com.au/job/1"),
        SourceCategory: "Developers/Programmers",
        Summary: "Fictional teaser | Hybrid");

    [Fact]
    public void New_entity_round_trips_to_the_same_listing()
    {
        var listing = Sample();

        var record = ListingMapper.ToRecord(ListingMapper.ToNewEntity(listing, Perth));

        Assert.Equal(listing, record.Listing);
        Assert.Equal(Perth, record.FirstSeenAt);
        Assert.Equal(Perth, record.LastSeenAt);
    }

    [Fact]
    public void Timestamps_are_stored_in_utc()
    {
        var e = ListingMapper.ToNewEntity(Sample(), Perth);

        Assert.Equal(TimeSpan.Zero, e.FirstSeenAt.Offset);
        Assert.Equal(TimeSpan.Zero, e.LastSeenAt.Offset);
        Assert.Equal(TimeSpan.Zero, e.PostedAt!.Value.Offset);
    }

    [Fact]
    public void Refresh_updates_details_and_last_seen_but_not_first_seen()
    {
        var e = ListingMapper.ToNewEntity(Sample(), Perth);
        var later = Perth.AddDays(1);

        ListingMapper.Refresh(e, Sample(title: "Senior Backend Developer"), later);

        Assert.Equal("Senior Backend Developer", e.Title);
        Assert.Equal(Perth, e.FirstSeenAt);
        Assert.Equal(later, e.LastSeenAt);
    }

    [Fact]
    public void Unknown_values_are_stored_as_null_and_read_back_as_unknown()
    {
        var listing = Sample() with { Location = ListingLocation.Unknown(Country.AU), WorkType = WorkType.Unknown };

        var e = ListingMapper.ToNewEntity(listing, Perth);

        Assert.Null(e.State);
        Assert.Null(e.WorkType);
        Assert.Equal(listing, ListingMapper.ToRecord(e).Listing);
    }

    [Theory]
    [InlineData(Country.AU, "AU")]
    [InlineData(Country.BR, "BR")]
    public void Country_is_stored_by_its_iso_code(Country country, string column)
    {
        Assert.Equal(column, ListingMapper.ToColumn(country));
        Assert.Equal(country, ListingMapper.ToCountry(column));
    }

    [Fact]
    public void An_unknown_country_in_the_database_fails_loudly()
    {
        // Every row says which market it is: guessing one would mix two markets in a score or a report.
        Assert.Throws<InvalidOperationException>(() => ListingMapper.ToCountry("NZ"));
    }

    [Fact]
    public void A_Brazilian_location_round_trips_with_its_state_and_cep()
    {
        var listing = Sample() with { Location = new ListingLocation(Country.BR, "Centro", "Rio de Janeiro", "RJ", "20040-002") };

        var e = ListingMapper.ToNewEntity(listing, Perth);

        Assert.Equal("BR", e.Country);
        Assert.Equal(listing, ListingMapper.ToRecord(e).Listing);
    }

    [Theory]
    [InlineData(WorkType.FullTime, "full_time")]
    [InlineData(WorkType.PartTime, "part_time")]
    [InlineData(WorkType.Casual, "casual")]
    [InlineData(WorkType.Contract, "contract")]
    public void Work_type_round_trips_through_its_column_spelling(WorkType workType, string column)
    {
        Assert.Equal(column, ListingMapper.ToColumn(workType));
        Assert.Equal(workType, ListingMapper.ToWorkType(column));
    }

    [Fact]
    public void Every_work_type_has_a_column_spelling()
    {
        foreach (var w in Enum.GetValues<WorkType>().Where(w => w != WorkType.Unknown))
            Assert.NotNull(ListingMapper.ToColumn(w));
    }
}
