using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekListingTranslatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Dictionary<string, Listing>> Page1Async()
    {
        var result = await new FakeSeekApi().Page(1, "search-page1.json").Source(maxPages: 1)
            .SearchAsync(new ListingSearch("", "All Australia"), Ct);
        return result.Listings.ToDictionary(l => l.Listing.Id.SourceId, l => l.Listing);
    }

    [Fact]
    public async Task Translates_a_full_ad()
    {
        var l = (await Page1Async())["90000002"];

        Assert.Equal(new ListingId("seek", "90000002"), l.Id);
        Assert.Equal("Backend Engineer", l.Title);
        Assert.Equal("Sample Recruitment", l.Company);
        Assert.Equal(new ListingLocation("Osborne Park", "Perth", AustralianState.WA, null), l.Location);
        Assert.Equal(WorkType.Contract, l.WorkType);
        Assert.Equal("$90 per hour plus super", l.SalaryText);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 3, 15, 54, TimeSpan.Zero), l.PostedAt);
        Assert.Equal(new Uri("https://www.seek.com.au/job/90000002"), l.Url);
        Assert.Equal("Developers/Programmers", l.SourceCategory);
    }

    [Fact]
    public async Task Hidden_salary_is_null_and_title_is_trimmed()
    {
        var l = (await Page1Async())["90000001"];

        Assert.Null(l.SalaryText);
        Assert.Equal("Software Developer (.NET)", l.Title);
    }

    [Fact]
    public async Task Missing_company_falls_back_to_the_advertiser()
    {
        Assert.Equal("Private Advertiser", (await Page1Async())["90000003"].Company);
    }

    [Theory]
    [InlineData("Perth WA", null, "Perth", AustralianState.WA)]
    [InlineData("Osborne Park, Perth WA", "Osborne Park", "Perth", AustralianState.WA)]
    [InlineData("Austinmer, Wollongong, Illawarra & South Coast NSW", "Austinmer", "Wollongong, Illawarra & South Coast", AustralianState.NSW)]
    [InlineData("Cape Schanck, Mornington Peninsula & Bass Coast VIC", "Cape Schanck", "Mornington Peninsula & Bass Coast", AustralianState.VIC)]
    [InlineData("Hobart TAS", null, "Hobart", AustralianState.TAS)]
    [InlineData("Darwin NT", null, "Darwin", AustralianState.NT)]
    [InlineData("Canberra ACT", null, "Canberra", AustralianState.ACT)]
    [InlineData("Auckland", null, "Auckland", AustralianState.Unknown)]
    public void Parses_location_labels(string label, string? suburb, string? region, AustralianState state)
    {
        Assert.Equal(new ListingLocation(suburb, region, state, null), SeekListingTranslator.ParseLocation(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_location_is_unknown(string? label)
    {
        Assert.Equal(ListingLocation.Unknown, SeekListingTranslator.ParseLocation(label));
    }

    [Theory]
    [InlineData("Full time", WorkType.FullTime)]
    [InlineData("Part time", WorkType.PartTime)]
    [InlineData("Casual/Vacation", WorkType.Casual)]
    [InlineData("Contract/Temp", WorkType.Contract)]
    [InlineData("Internship", WorkType.Unknown)]
    [InlineData(null, WorkType.Unknown)]
    public void Maps_work_types(string? seek, WorkType expected)
    {
        Assert.Equal(expected, SeekListingTranslator.ParseWorkType(seek));
    }
}
