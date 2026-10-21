using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests;

public class ListingIdTests
{
    [Fact]
    public void ToString_and_Parse_round_trip()
    {
        var id = new ListingId("seek", "81234567");

        Assert.Equal("seek:81234567", id.ToString());
        Assert.Equal(id, ListingId.Parse(id.ToString()));
    }

    [Fact]
    public void Source_is_normalised_so_the_same_site_never_splits_in_two()
    {
        Assert.Equal(new ListingId("seek", "1"), new ListingId(" SEEK ", "1"));
    }

    [Fact]
    public void Parse_keeps_separators_inside_the_source_id()
    {
        var id = ListingId.Parse("adzuna:abc:123");

        Assert.Equal("adzuna", id.Source);
        Assert.Equal("abc:123", id.SourceId);
    }

    [Theory]
    [InlineData("seek")]
    [InlineData(":123")]
    [InlineData("seek:")]
    public void Parse_rejects_malformed_values(string value)
    {
        Assert.Throws<FormatException>(() => ListingId.Parse(value));
    }

    [Fact]
    public void Source_with_separator_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ListingId("se:ek", "1"));
    }
}
