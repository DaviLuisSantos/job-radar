using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests.Listings;

public class RemoteEligibilityTests
{
    [Theory]
    [InlineData(null, EligibilityScope.Unknown)]
    [InlineData("Worldwide", EligibilityScope.Worldwide)]
    [InlineData("Anywhere", EligibilityScope.Worldwide)]
    [InlineData("LATAM", EligibilityScope.IncludesBrazil)]
    [InlineData("Brazil", EligibilityScope.IncludesBrazil)]
    [InlineData("Brazil, Colombia, Mexico", EligibilityScope.IncludesBrazil)]
    [InlineData("EMEA, LATAM, Canada, USA", EligibilityScope.IncludesBrazil)]
    [InlineData("Americas", EligibilityScope.IncludesBrazil)]
    [InlineData("North America", EligibilityScope.ExcludesBrazil)]
    [InlineData("USA", EligibilityScope.ExcludesBrazil)]
    [InlineData("Europe, Canada", EligibilityScope.ExcludesBrazil)]
    public void Classifies_who_may_apply(string? eligibility, EligibilityScope scope) =>
        Assert.Equal(scope, RemoteEligibility.Classify(eligibility));

    [Theory]
    [InlineData("<p>One &amp; two</p><p>Three<br/>four</p>", "One & two\n\nThree\nfour")]
    [InlineData("<ul><li>C#</li><li> Azure </li></ul>", "• C#\n• Azure")]
    [InlineData("<script>x()</script><b>Bold</b>&nbsp;text", "Bold text")]
    [InlineData("  ", null)]
    public void Html_becomes_readable_text(string html, string? text) => Assert.Equal(text, HtmlText.ToPlain(html));

    [Theory]
    [InlineData(new double[] { -5, -4, -3 }, "UTC-5 to UTC-3")]
    [InlineData(new double[] { 0 }, "UTC+0")]
    [InlineData(new double[] { 1, 5.5 }, "UTC+1 to UTC+5.5")]
    public void Time_zone_windows_round_trip_through_their_text(double[] offsets, string text)
    {
        var window = TimeZoneWindow.Of(offsets)!;

        Assert.Equal(text, window.ToString());
        Assert.Equal(window, TimeZoneWindow.Parse(text));
    }

    [Fact]
    public void A_window_across_the_whole_world_restricts_nothing() =>
        Assert.Null(TimeZoneWindow.Of(Enumerable.Range(-11, 24).Select(i => (double)i)));
}
