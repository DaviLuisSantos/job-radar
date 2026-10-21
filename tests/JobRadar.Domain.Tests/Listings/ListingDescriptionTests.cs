using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests.Listings;

public class ListingDescriptionTests
{
    private static readonly string Ad = string.Join(" ", Enumerable.Repeat("Labourer needed for civil works.", 4));

    [Fact]
    public void Text_is_tidied_spaces_and_blank_lines_collapsed()
    {
        var text = ListingDescription.Normalize($"  {Ad}\r\n\r\n\r\n\r\n  Key   duties:\t dig  holes  \r\n");

        Assert.Equal($"{Ad}\n\nKey duties: dig holes", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Apply now")]
    public void Too_short_to_be_an_ad_is_refused(string? raw)
    {
        Assert.Null(ListingDescription.Normalize(raw));
    }

    [Fact]
    public void A_runaway_page_is_cut()
    {
        Assert.Equal(ListingDescription.MaxLength, ListingDescription.Normalize(new string('x', 50_000))!.Length);
    }
}
