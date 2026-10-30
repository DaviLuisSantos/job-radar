using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests.Listings;

public class ListingUrlTests
{
    [Fact]
    public void The_first_usable_address_wins()
    {
        Assert.Equal(new Uri("https://example.com/a"), ListingUrl.Choose("https://example.com/a", "https://example.com/b"));
        Assert.Equal(new Uri("https://example.com/b"), ListingUrl.Choose(null, "  ", "https://example.com/b"));
    }

    [Theory]
    [InlineData("https://acme gupy.io/job x")]      // does not parse
    [InlineData("/jobs/123")]                       // relative
    [InlineData("mailto:jobs@example.com")]         // not a page
    public void An_unusable_address_is_skipped(string bad) =>
        Assert.Equal(new Uri("https://board.example/job/1"), ListingUrl.Choose(bad, "https://board.example/job/1"));

    [Fact]
    public void An_address_longer_than_the_column_is_skipped() =>
        Assert.Equal(new Uri("https://board.example/job/1"),
            ListingUrl.Choose($"https://example.com/{new string('a', ListingUrl.MaxLength)}", "https://board.example/job/1"));
}
