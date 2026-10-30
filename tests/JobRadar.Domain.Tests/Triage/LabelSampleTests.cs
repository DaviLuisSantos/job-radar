using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Domain.Tests.Triage;

public class LabelSampleTests
{
    private static TriageRow Row(string id, int score) =>
        new(new ListingId("seek", id), $"Job {id}", "Acme", ListingLocation.Unknown(Country.AU), WorkType.Unknown,
            null, null, DateTimeOffset.UnixEpoch, score, null, null);

    [Fact]
    public void Bands_take_turns_best_band_first()
    {
        var rows = new[] { Row("a", 95), Row("b", 90), Row("c", 85), Row("d", 70), Row("e", 40), Row("f", 20), Row("g", 0) };

        var spread = LabelSample.Spread(rows);

        // One of each band (80+, 65–79, 35–49, 1–34, excluded), then the rest of the 80+ band.
        Assert.Equal([true, false, false, false, false], spread.Take(5).Select(r => r.Score >= 80));
        Assert.Equal([70, 40, 20, 0], spread.Skip(1).Take(4).Select(r => r.Score));
        Assert.All(spread.Skip(5), r => Assert.True(r.Score >= 80));
    }

    [Fact]
    public void Every_row_comes_out_once()
    {
        var rows = Enumerable.Range(0, 200).Select(i => Row(i.ToString(System.Globalization.CultureInfo.InvariantCulture), i % 101)).ToList();

        var spread = LabelSample.Spread(rows);

        Assert.Equal(rows.Select(r => r.Id.ToString()).Order(StringComparer.Ordinal), spread.Select(r => r.Id.ToString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Inside_a_band_it_is_not_just_the_top_and_it_is_the_same_every_time()
    {
        var rows = Enumerable.Range(0, 30).Select(i => Row(i.ToString(System.Globalization.CultureInfo.InvariantCulture), 99 - (i % 15))).ToList();

        var first = LabelSample.Spread(rows);
        var again = LabelSample.Spread(rows.AsEnumerable().Reverse().ToList());

        Assert.NotEqual(rows.Select(r => r.Id), first.Select(r => r.Id));
        Assert.Equal(first.Select(r => r.Id), again.Select(r => r.Id));
    }

    [Fact]
    public void Nothing_in_nothing_out()
    {
        Assert.Empty(LabelSample.Spread([]));
    }
}
