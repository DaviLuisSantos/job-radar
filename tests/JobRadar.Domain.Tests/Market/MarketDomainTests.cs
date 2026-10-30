using JobRadar.Domain.Market;

namespace JobRadar.Domain.Tests.Market;

public class SalaryParserTests
{
    [Theory]
    [InlineData("$50 - $65 per hour", 50, 65, SalaryPeriod.Hour, 104_000, 135_200)]
    [InlineData("$100,000 – $120,000 pa", 100_000, 120_000, SalaryPeriod.Year, 100_000, 120_000)]
    [InlineData("$950 per day + super", 950, 950, SalaryPeriod.Day, 247_000, 247_000)]
    [InlineData("$165k - $175k + Super", 165_000, 175_000, SalaryPeriod.Year, 165_000, 175_000)]
    [InlineData("Up to $1,050 per day", 1050, 1050, SalaryPeriod.Day, 273_000, 273_000)]
    [InlineData("$36.50 hourly + penalty rates", 36.5, 36.5, SalaryPeriod.Hour, 75_920, 75_920)]
    public void Reads_the_range_and_its_annual_equivalent(string text, double min, double max, SalaryPeriod period, int annualMin, int annualMax)
    {
        var salary = SalaryParser.Parse(text);

        Assert.NotNull(salary);
        Assert.Equal(((decimal)min, (decimal)max, period, annualMin, annualMax), (salary.Min, salary.Max, salary.Period, salary.AnnualMin, salary.AnnualMax));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("—")]
    [InlineData("Excellent salary")]
    [InlineData("$5 coffee voucher per hour")]
    [InlineData("$2m project, competitive package")]
    public void Not_a_salary_is_null(string? text)
    {
        Assert.Null(SalaryParser.Parse(text));
    }
}

public class SnapshotQualityTests
{
    private static readonly Dictionary<(string, string), int> Previous = new() { [("HVAC-R", "Perth WA")] = 100 };

    [Fact]
    public void Plausible_complete_counts_are_ok()
    {
        var result = SnapshotQuality.Assess([new("HVAC-R", "Perth WA", null, 120), new("HVAC-R", "Perth WA", "fifo", 3)], Previous, []);

        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void A_failed_count_or_a_cut_listing_makes_it_partial()
    {
        Assert.Equal(SnapshotStatus.Partial, SnapshotQuality.Assess([new("HVAC-R", "Perth WA", "fifo", null)], Previous, []).Status);
        Assert.Equal(SnapshotStatus.Partial, SnapshotQuality.Assess([new("HVAC-R", "Perth WA", null, 100)], Previous, [("HVAC-R", "Perth WA")]).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    [InlineData(49)]
    public void Zeroed_or_swinging_more_than_half_is_suspect(int total)
    {
        var result = SnapshotQuality.Assess([new("HVAC-R", "Perth WA", null, total)], Previous, [("HVAC-R", "Perth WA")]);

        Assert.Equal(SnapshotStatus.Suspect, result.Status);
        Assert.Equal(2, result.Issues.Count);   // suspect first, then the partial reason
    }

    [Fact]
    public void Small_bases_may_swing_and_terms_are_not_compared()
    {
        var small = new Dictionary<(string, string), int> { [("HVAC-R", "Perth WA")] = 10 };

        Assert.Equal(SnapshotStatus.Ok, SnapshotQuality.Assess([new("HVAC-R", "Perth WA", null, 30)], small, []).Status);
        Assert.Equal(SnapshotStatus.Ok, SnapshotQuality.Assess([new("HVAC-R", "Perth WA", "fifo", 0)], Previous, []).Status);
    }
}

public class MonthlyReportTests
{
    private static readonly DateOnly D1 = new(2026, 10, 1), D2 = new(2026, 10, 2);

    private static SeenListing L(string title, string? company, int? min, int? max, string posted = "2026-10-05", string city = "Perth WA") =>
        new("HVAC-R", city, title, company, min, max, DateTimeOffset.Parse(posted, System.Globalization.CultureInfo.InvariantCulture), D1, D2);

    private static MarketReport Build(IReadOnlyCollection<DailyCount> current, IReadOnlyCollection<DailyCount>? previous = null, IReadOnlyCollection<SeenListing>? listings = null) =>
        MonthlyReport.Build("2026-10", ["HVAC-R", "Elétrica"], ["Perth WA", "All Australia"], ["fifo"], current, previous ?? [], listings ?? [], [])!;

    [Fact]
    public void No_good_snapshot_no_report()
    {
        Assert.Null(MonthlyReport.Build("2026-10", ["HVAC-R"], [], [], [], [], [], []));
    }

    [Fact]
    public void Cities_follow_the_configured_order()
    {
        var report = Build([new(D1, "HVAC-R", "Zeehan TAS", null, 1), new(D1, "HVAC-R", "All Australia", null, 9), new(D1, "HVAC-R", "Perth WA", null, 5)]);

        Assert.Equal(["Perth WA", "All Australia", "Zeehan TAS"], report.Categories[0].Cities.Select(c => c.City));
    }

    [Fact]
    public void Daily_averages_change_and_mention_share()
    {
        var report = Build(
            [new(D1, "HVAC-R", "Perth WA", null, 100), new(D2, "HVAC-R", "Perth WA", null, 120), new(D1, "HVAC-R", "Perth WA", "fifo", 11)],
            previous: [new(new DateOnly(2026, 9, 10), "HVAC-R", "Perth WA", null, 88)]);

        var city = Assert.Single(Assert.Single(report.Categories).Cities);
        Assert.Equal(2, report.DaysCovered);
        Assert.Equal(110, city.AverageOpen);
        Assert.Equal(0.25, city.ChangeVsPreviousMonth!.Value, 3);
        Assert.Equal(("fifo", 11.0, 0.1), (city.Mentions[0].Term, city.Mentions[0].Average, city.Mentions[0].Share!.Value));
    }

    [Fact]
    public void Reposts_collapse_and_salary_is_the_middle_of_each_range()
    {
        var report = Build([new(D1, "HVAC-R", "Perth WA", null, 4)], listings:
        [
            L("HVAC Tech", "Cool Co", 80_000, 100_000),
            L("hvac  tech!", "COOL CO.", 80_000, 100_000),         // repost
            L("Installer", "Air Ltd", 60_000, null),
            L("Apprentice", "Air Ltd", null, null, posted: "2026-09-20"),
        ]);

        var stats = Assert.Single(report.Categories[0].Cities).Listings!;
        Assert.Equal((4, 3, 2), (stats.Seen, stats.Unique, stats.PostedInMonth));
        Assert.Equal(2, stats.Salary!.WithSalary);
        Assert.Equal(75_000, stats.Salary.Median);   // mids 60k and 90k
    }

    [Fact]
    public void Percentiles_interpolate_like_pandas()
    {
        // Position (n − 1) × p, interpolating between neighbours.
        Assert.Equal(3.0, MonthlyReport.Percentile([1, 2, 4, 8], .5));
        Assert.Equal(5.0, MonthlyReport.Percentile([1, 2, 4, 8], .75));
        Assert.Equal(1.75, MonthlyReport.Percentile([1, 2, 4, 8], .25));
    }
}
