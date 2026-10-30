using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests.Listings;

public class ListingConditionsTests
{
    [Theory]
    [InlineData("Fictional teaser | Great team | Hybrid", WorkArrangement.Hybrid, null)]
    [InlineData("Fictional teaser | On-site", WorkArrangement.OnSite, null)]
    [InlineData("Remoto · CLT", WorkArrangement.Remote, "CLT")]
    [InlineData("Presencial · PJ", WorkArrangement.OnSite, "PJ")]
    [InlineData("Híbrido", WorkArrangement.Hybrid, null)]
    [InlineData("PJ", WorkArrangement.Unknown, "PJ")]
    public void Reads_the_arrangement_and_contract_the_source_gives(string summary, WorkArrangement arrangement, string? contract) =>
        Assert.Equal(new ListingConditions(arrangement, contract), ListingConditions.Read(summary));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Work remote two days a week | Great team")]   // the teaser says it in passing: not the source's field
    public void Nothing_is_read_from_text_in_passing(string? summary) =>
        Assert.Same(ListingConditions.None, ListingConditions.Read(summary));
}
