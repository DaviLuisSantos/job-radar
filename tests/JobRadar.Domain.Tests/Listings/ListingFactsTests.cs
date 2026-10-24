using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tests.Listings;

public class ListingFactsTests
{
    private static string? Fact(string text, FactKind kind, bool hasPayField = false) =>
        ListingFacts.Read(text, hasPayField).SingleOrDefault(f => f.Kind == kind)?.Text;

    [Theory]
    [InlineData("Pay rate $45 per hour plus overtime", "$45 per hour")]
    [InlineData("Earn $38.50/hr + super", "$38.50/hr")]
    [InlineData("$120k + super package", "$120k + super")]
    [InlineData("Salary $90,000 - $105,000 per annum", "$90,000 - $105,000 per annum")]
    [InlineData("Up to $450 per day", "$450 per day")]
    public void Pay_is_read_with_the_period_it_is_paid_in(string text, string pay) =>
        Assert.Equal(pay, Fact(text, FactKind.Pay));

    [Theory]
    [InlineData("Work on a $5 million project")]                 // no period: not pay
    [InlineData("A $500 sign-on bonus")]
    public void A_dollar_figure_without_a_period_is_not_pay(string text) => Assert.Null(Fact(text, FactKind.Pay));

    [Fact]
    public void Pay_is_not_read_when_the_source_already_gives_it() =>
        Assert.Null(Fact("$45 per hour", FactKind.Pay, hasPayField: true));

    [Theory]
    [InlineData("Working a 2:1 roster out of Perth", "2:1 roster")]
    [InlineData("8/6 swing on site", "8:6 roster")]
    [InlineData("on a roster of 14:7", "14:7 roster")]
    [InlineData("2 weeks on, 1 week off", "2wk on / 1wk off")]
    [InlineData("Even time roster", "Even-time roster")]
    public void Rosters_are_read_in_their_usual_forms(string text, string roster) =>
        Assert.Equal(roster, Fact(text, FactKind.Roster));

    [Fact]
    public void A_time_of_day_is_not_a_roster() => Assert.Null(Fact("Start at 7:30 sharp each morning", FactKind.Roster));

    [Theory]
    [InlineData("FIFO from Perth", FactKind.Travel, "FIFO")]
    [InlineData("Fly-in fly-out position", FactKind.Travel, "FIFO")]
    [InlineData("DIDO from Kalgoorlie", FactKind.Travel, "DIDO")]
    [InlineData("Accommodation provided on site", FactKind.Accommodation, "Accommodation")]
    [InlineData("All meals and accommodation included", FactKind.Meals, "Meals")]
    [InlineData("Flights paid from Perth", FactKind.Flights, "Flights paid")]
    [InlineData("Immediate start available", FactKind.Start, "Immediate start")]
    [InlineData("Must be able to start ASAP", FactKind.Start, "Immediate start")]
    [InlineData("Day and night shifts", FactKind.Shift, "Days and nights")]
    [InlineData("Night shift, 4 nights a week", FactKind.Shift, "Night shift")]
    [InlineData("Monday to Friday, 7am-3pm", FactKind.Shift, "Day shift")]
    [InlineData("3 month contract with view to extend", FactKind.Duration, "3 months")]
    [InlineData("a 1-month shutdown", FactKind.Duration, "1 month")]
    public void Each_fact_is_read_as_a_short_label(string text, FactKind kind, string label) =>
        Assert.Equal(label, Fact(text, kind));

    [Theory]
    [InlineData("Accommodation is not provided", FactKind.Accommodation)]
    [InlineData("Please note there are no flights paid", FactKind.Flights)]
    [InlineData("This is not a FIFO role", FactKind.Travel)]
    [InlineData("Unfortunately we cannot offer free accommodation", FactKind.Accommodation)]
    public void A_fact_stated_as_not_offered_is_not_read(string text, FactKind kind) => Assert.Null(Fact(text, kind));

    [Fact]
    public void Facts_come_in_kind_order_one_of_each()
    {
        var facts = ListingFacts.Read(
            "Immediate start! FIFO 2:1 roster. $48/hr. Accommodation and meals provided. FIFO from Perth.");

        Assert.Equal([FactKind.Pay, FactKind.Travel, FactKind.Roster, FactKind.Accommodation, FactKind.Meals, FactKind.Start],
            facts.Select(f => f.Kind));
        Assert.Empty(ListingFacts.Read(null));
        Assert.Empty(ListingFacts.Read("   "));
    }
}
