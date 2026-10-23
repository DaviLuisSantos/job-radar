using JobRadar.Domain.Import;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Tests.Import;

public class PrototypeMappingTests
{
    [Theory]
    [InlineData("new", false, null)]
    [InlineData(null, false, null)]
    [InlineData("reviewed", false, ApplicationStatus.Reviewed)]
    [InlineData("applied", true, ApplicationStatus.Applied)]
    [InlineData("interview", true, ApplicationStatus.Interview)]
    [InlineData("rejected", false, ApplicationStatus.Dismissed)]
    [InlineData("rejected", true, ApplicationStatus.Declined)]
    [InlineData("new", true, ApplicationStatus.Applied)]
    [InlineData(" Applied ", true, ApplicationStatus.Applied)]
    public void Prototype_status_becomes_an_application(string? status, bool applied, ApplicationStatus? expected)
    {
        Assert.Equal(expected, PrototypeMapping.Status(status, applied));
    }

    [Fact]
    public void Market_days_move_to_the_zone_date_they_were_taken_and_listing_ranges_follow()
    {
        var perth = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
        var snapshot = new MarketSnapshot("seek", new DateOnly(2026, 10, 12), SnapshotStatus.Ok, 30, 30, 446, []);
        var data = new PrototypeData([],
            [
                new PrototypeSnapshot(snapshot, [], new DateTimeOffset(2026, 10, 12, 22, 32, 32, TimeSpan.Zero)),        // 13 Oct in Perth
                new PrototypeSnapshot(snapshot with { Date = new DateOnly(2026, 10, 14) }, [],
                    new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero)),                                         // 08:00 in Brazil: same date
                new PrototypeSnapshot(snapshot with { Date = new DateOnly(2026, 10, 16) }, []),                         // no time: kept
            ],
            [new ImportedMarketListing(new MarketListing("HVAC-R", "Perth WA", Listing()), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 16))]);

        var moved = PrototypeMapping.InScheduleDays(data, perth);

        Assert.Equal([new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16)], moved.Snapshots.Select(s => s.Snapshot.Date));
        var listing = Assert.Single(moved.MarketListings);
        Assert.Equal((new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 16)), (listing.FirstSeen, listing.LastSeen));
    }

    private static Listing Listing() =>
        new(new ListingId("seek", "9"), "Installer", null, new ListingLocation(null, "Perth", AustralianState.WA, null), WorkType.FullTime,
            null, null, new Uri("https://example.com/9"), null);
}
