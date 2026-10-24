using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

public class ListingPatternsTests
{
    [Theory]
    [InlineData("Minimum 3 years experience in civil construction", 3)]
    [InlineData("3+ years of civil experience", 3)]
    [InlineData("at least 2 years' hands-on experience", 2)]
    [InlineData("2-3 yrs experience operating excavators", 2)]
    [InlineData("5 years experience preferred, 2 years experience minimum", 2)]
    [InlineData("1 year of relevant industry experience", 1)]
    [InlineData("10+ years experience in software development", 10)]
    public void Years_of_experience_asked_are_read_from_the_text(string text, int years) =>
        Assert.Equal(years, ListingPatterns.YearsAsked(text));

    [Theory]
    [InlineData("No experience needed, training provided")]
    [InlineData("Proven experience in a similar role")]
    [InlineData("A 12 month contract with 2 years extension")]
    [InlineData("Our company has 30 years experience in the industry")]   // over 20: the company, not the candidate
    public void A_text_without_a_number_of_years_asks_none(string text) =>
        Assert.Null(ListingPatterns.YearsAsked(text));
}
