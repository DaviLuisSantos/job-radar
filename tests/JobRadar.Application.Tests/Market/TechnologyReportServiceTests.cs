using JobRadar.Application.Market;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Listings;

namespace JobRadar.Application.Tests.Market;

public class TechnologyReportServiceTests
{
    [Fact]
    public async Task Reads_the_month_of_the_country_asked_and_counts_its_ads()
    {
        var descriptions = new InMemoryDescriptions();
        descriptions.Ads.Add(("Desenvolvedor .NET", "C#, .NET e Azure"));

        var report = await new TechnologyReportService(descriptions).BuildAsync(Country.BR, "2026-10", TestContext.Current.CancellationToken);

        Assert.Equal((Country.BR, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)),
            descriptions.AdsAsked);
        Assert.Equal("2026-10", report.Month);
        Assert.Contains(report.Technologies, t => t.Name == "Azure");
        Assert.Empty(report.Engagements);   // only remote ads say how they hire
    }

    [Fact]
    public async Task Remote_ads_are_also_counted_by_how_they_hire()
    {
        var descriptions = new InMemoryDescriptions();
        descriptions.Ads.Add(("Backend Developer", "C# and .NET. Independent contractor."));
        descriptions.Ads.Add(("Backend Engineer", ".NET. Hired through an employer of record."));
        descriptions.Ads.Add(("Platform Engineer", "Go and Kubernetes. Independent contractor."));

        var report = await new TechnologyReportService(descriptions).BuildAsync(Country.Remote, "2026-10", TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Engagements[JobRadar.Domain.Matching.Engagement.Contractor]);
        Assert.Equal(1, report.Engagements[JobRadar.Domain.Matching.Engagement.Eor]);
    }
}
