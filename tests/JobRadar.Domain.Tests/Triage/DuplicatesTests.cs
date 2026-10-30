using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Domain.Tests.Triage;

public class DuplicatesTests
{
    private static TriageRow Row(string id, string title, string? company, int score, string region = "Perth", string? state = "WA") =>
        new(new ListingId("seek", id), title, company, new ListingLocation(Country.AU, null, region, state, null), WorkType.Contract,
            null, null, DateTimeOffset.UnixEpoch, score, null, null);

    [Fact]
    public void The_same_title_and_company_collapse_into_the_first_row()
    {
        var rows = Duplicates.Collapse(
        [
            Row("1", "Driller's Offsider | FIFO", "LH Partners", 95),
            Row("2", "Plumber", "Acme", 80),
            Row("3", "driller's  offsider | fifo ", "lh partners", 95),
            Row("4", "Driller's Offsider | FIFO", "LH Partners", 90),
        ]);

        Assert.Equal(["1", "2"], rows.Select(r => r.Id.SourceId));
        Assert.Equal(["3", "4"], rows[0].Copies.Select(c => c.SourceId));
        Assert.Equal(["1", "3", "4"], rows[0].AllIds.Select(c => c.SourceId));
    }

    [Fact]
    public void Other_cities_are_listed_once_and_the_row_own_city_is_not_repeated()
    {
        var rows = Duplicates.Collapse(
        [
            Row("1", "Kitchen Hand", "Cafe Co", 70),
            Row("2", "Kitchen Hand", "Cafe Co", 70, "Sydney", "NSW"),
            Row("3", "Kitchen Hand", "Cafe Co", 70, "Sydney", "NSW"),
            Row("4", "Kitchen Hand", "Cafe Co", 70),
        ]);

        var row = Assert.Single(rows);
        Assert.Equal(["Sydney NSW"], row.OtherLocations);
        Assert.Equal(3, row.Copies.Count);
    }

    [Fact]
    public void Listings_without_a_company_are_never_grouped()
    {
        var rows = Duplicates.Collapse([Row("1", "Labourer", null, 60), Row("2", "Labourer", "  ", 60), Row("3", "Labourer", null, 60)]);

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void The_order_given_is_kept()
    {
        var rows = Duplicates.Collapse([Row("1", "A", "X", 90), Row("2", "B", "Y", 80), Row("3", "A", "X", 99)]);

        Assert.Equal(["1", "2"], rows.Select(r => r.Id.SourceId));
    }
}
