using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>Fictional ads. The remote rules are new, not ported: these pin what each one is for.</summary>
public class RemoteRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    private static readonly Compensation Pay = new(
        Contractor: new PayBand("USD", MonthlyFloor: 3000, MonthlyTarget: 5000),
        Eor: new PayBand("BRL", MonthlyFloor: 12000, MonthlyTarget: 18000),
        UsdToBrl: 5, HoursPerMonth: 160);

    private static MatchingProfile Profile(JobMarket market, Compensation? pay = null) => new(
        "dotnet-remote", PositiveSignals: [".NET", "C#"], NegativeSignals: [], DisqualifySignals: [], QualifiedTitles: [],
        EntryLevelBoost: false, AllowSenior: true, PreferredLocations: [])
    {
        ExcludedTitles = ["junior", "intern", "java"],
        Market = market,
        Compensation = pay,
    };

    private static ScoringInput Ad(string title, string? eligibility, string summary = "Fictional remote team | Remote",
        string? description = null, string? salary = null) =>
        new(title, summary, "Remote", null, null, []) { Eligibility = eligibility, Description = description, SalaryText = salary };

    private static FitScore Score(ScoringInput ad, JobMarket market = JobMarket.LatamRemote, Compensation? pay = null) =>
        FitScorer.Score(ad, Profile(market, pay), ScoringPolicy.Default, Today);

    private static ScoreItem? Item(FitScore score, ScoreRule rule) => score.Items.FirstOrDefault(i => i.Rule == rule);

    [Theory]
    [InlineData("USA", JobMarket.LatamRemote)]
    [InlineData("Europe, Canada", JobMarket.GlobalRemote)]
    public void A_job_Brazil_cannot_apply_to_is_excluded_in_either_market(string eligibility, JobMarket market)
    {
        var score = Score(Ad("Backend Developer (.NET)", eligibility), market);

        Assert.True(score.Excluded);
        Assert.Equal(eligibility, Item(score, ScoreRule.RegionRestricted)!.Detail);
    }

    [Fact]
    public void A_job_falls_in_one_remote_market_only()
    {
        var worldwide = Ad("Backend Developer (.NET)", RemoteEligibility.Worldwide);
        var latam = Ad("Backend Developer (.NET)", "Brazil, Colombia, Mexico");

        Assert.False(Score(worldwide, JobMarket.GlobalRemote).Excluded);
        Assert.False(Score(latam, JobMarket.LatamRemote).Excluded);
        Assert.NotNull(Item(Score(worldwide, JobMarket.LatamRemote), ScoreRule.OtherRemoteMarket));
        Assert.NotNull(Item(Score(latam, JobMarket.GlobalRemote), ScoreRule.OtherRemoteMarket));
    }

    [Theory]
    [InlineData("This role is US only.")]
    [InlineData("Candidates must reside in the United States.")]
    [InlineData("You must be legally authorized to work in the UK.")]
    [InlineData("Must be based in Europe.")]
    public void The_ads_words_exclude_when_the_board_does_not_say(string text)
    {
        var score = Score(Ad("Backend Developer (.NET)", eligibility: null, description: text));

        Assert.Equal(ScoreRule.RegionRestricted, Assert.Single(score.Items).Rule);
    }

    [Fact]
    public void A_team_somewhere_else_is_not_a_restriction() =>
        Assert.False(Score(Ad("Backend Developer (.NET)", null, description: "Our US-based team works with people from LATAM.")).Excluded);

    [Fact]
    public void English_is_expected_and_seniority_reads_like_the_Brazilian_profile()
    {
        var english = Score(Ad("Backend Developer (.NET)", "LATAM", description: "Fluent English required."));
        var senior = Score(Ad("Senior .NET Engineer", "LATAM"));
        var junior = Score(Ad("Junior .NET Developer", "LATAM"));

        Assert.Null(Item(english, ScoreRule.EnglishRequired));
        Assert.NotNull(Item(senior, ScoreRule.SeniorPenalty));
        Assert.NotNull(Item(junior, ScoreRule.TitleExcluded));
        Assert.NotNull(Item(Score(Ad("Engineering Manager (.NET)", "LATAM")), ScoreRule.LeadershipTitle));
    }

    [Fact]
    public void Time_zones_that_leave_Brazil_out_cost_points_and_a_nearby_window_does_not()
    {
        var asia = Score(Ad("Backend Developer (.NET)", "Worldwide", "Team | Remote | UTC+5 to UTC+9"), JobMarket.GlobalRemote);
        var americas = Score(Ad("Backend Developer (.NET)", "Worldwide", "Team | Remote | UTC-5 to UTC-4"), JobMarket.GlobalRemote);

        Assert.Equal(-ScoringPolicy.Default.TimeZonePenalty, Item(asia, ScoreRule.TimeZoneOutOfReach)!.Points);
        Assert.Null(Item(americas, ScoreRule.TimeZoneOutOfReach));   // -4 is an hour from Brazil
    }

    [Fact]
    public void Without_compensation_neither_engagement_nor_pay_is_read()
    {
        var score = Score(Ad("Backend Developer (.NET)", "LATAM", description: "Independent contractor via Deel.", salary: "USD 1,000 per month"));

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.Engagement or ScoreRule.PayBelowFloor or ScoreRule.PayAtTarget);
    }

    [Theory]
    [InlineData("You will join as an independent contractor.", Engagement.Contractor)]
    [InlineData("Paid through Deel as a contractor.", Engagement.Contractor)]
    [InlineData("Hired through Deel as a full employee with benefits.", Engagement.Eor)]
    [InlineData("We use an employer of record; contractors also welcome.", Engagement.Eor)]
    [InlineData("This is a full-time employee position.", Engagement.Direct)]
    [InlineData("Great team, great product.", Engagement.Unknown)]
    public void The_engagement_is_a_label_read_from_the_ad(string description, Engagement engagement)
    {
        var score = Score(Ad("Backend Developer (.NET)", "LATAM", description: description), pay: Pay);

        Assert.Equal(engagement, EngagementDetector.Detect(description, MarketRules.LatamRemote));
        if (engagement == Engagement.Unknown)
            Assert.Null(Item(score, ScoreRule.Engagement));
        else
            Assert.Equal((engagement.ToString(), 0), (Item(score, ScoreRule.Engagement)!.Detail, Item(score, ScoreRule.Engagement)!.Points));
    }

    [Fact]
    public void Pay_at_the_target_earns_points_and_below_the_floor_costs_them()
    {
        const string contractor = "Independent contractor.";
        var high = Score(Ad("Backend Developer (.NET)", "LATAM", description: contractor, salary: "USD 5,500–7,000 per month"), pay: Pay);
        var low = Score(Ad("Backend Developer (.NET)", "LATAM", description: contractor, salary: "USD 15/hr"), pay: Pay);
        var middle = Score(Ad("Backend Developer (.NET)", "LATAM", description: contractor, salary: "USD 48,000 per year"), pay: Pay);

        Assert.Equal(ScoringPolicy.Default.PayAtTargetBonus, Item(high, ScoreRule.PayAtTarget)!.Points);
        Assert.Equal(-ScoringPolicy.Default.PayBelowFloorPenalty, Item(low, ScoreRule.PayBelowFloor)!.Points);   // 15 × 160 = 2,400
        Assert.DoesNotContain(middle.Items, i => i.Rule is ScoreRule.PayAtTarget or ScoreRule.PayBelowFloor);  // 4,000 a month
    }

    [Fact]
    public void Reais_use_the_EOR_band_and_convert_only_when_the_currencies_differ()
    {
        // EOR band in BRL: R$ 20,000 reaches the R$ 18,000 target as it is.
        var reais = Score(Ad("Desenvolvedor .NET", "Brazil", salary: "R$ 20.000 por mês"), pay: Pay);
        // A dollar pay in an EOR job: USD 4,000 × 5 = R$ 20,000.
        var eorDollars = Score(Ad("Backend Developer (.NET)", "LATAM", description: "Employer of record in Brazil.", salary: "USD 4,000 per month"), pay: Pay);

        Assert.NotNull(Item(reais, ScoreRule.PayAtTarget));
        Assert.NotNull(Item(eorDollars, ScoreRule.PayAtTarget));
    }

    [Fact]
    public void A_currency_without_a_rate_is_said_and_costs_nothing()
    {
        var score = Score(Ad("Backend Developer (.NET)", "Worldwide", salary: "EUR 3,000 per month"), JobMarket.GlobalRemote, Pay);

        Assert.Equal(0, Item(score, ScoreRule.PayUnreadable)!.Points);
    }

    [Theory]
    [InlineData("USD 4,000–6,000 per month", 4000, 6000, "USD", PayPeriod.Month)]
    [InlineData("$45/hr", 45, 45, "USD", PayPeriod.Hour)]
    [InlineData("US$ 60k - 80k annually", 60000, 80000, "USD", PayPeriod.Year)]
    [InlineData("$40-60k", 40000, 60000, "USD", PayPeriod.Year)]
    [InlineData("R$ 12.000 a 15.000 por mês", 12000, 15000, "BRL", PayPeriod.Month)]
    [InlineData("USD 38.50 hourly", 38.5, 38.5, "USD", PayPeriod.Hour)]
    [InlineData("$5,000", 5000, 5000, "USD", PayPeriod.Month)]
    public void Pay_is_read_with_its_currency_and_period(string text, double min, double max, string currency, PayPeriod period) =>
        Assert.Equal(new Pay((decimal)min, (decimal)max, currency, period), PayParser.Parse(text));

    [Theory]
    [InlineData("Benefícios: • Vale alimentação de R$800,00 • Plano de saúde")]
    [InlineData("Vale-refeição de R$ 37,00 por dia útil")]
    [InlineData("We manage an annual ad spend exceeding $2 billion.")]
    [InlineData("Healthcare stipend: $50/month to support your insurance costs")]
    [InlineData("Our Series D brought our total investment to over $320 million.")]
    public void Amounts_in_an_ad_that_are_not_the_pay_are_not_read(string text) => Assert.Null(PayParser.Find(text));

    [Theory]
    [InlineData("Remuneração: A partir de R$1.800,00 - Contratação CLT", 1800, 1800)]
    [InlineData("Salário - R$ 1,8 a 2,5k", 1800, 2500)]
    [InlineData("The base salary range is $75,000 - $175,000 depending on experience.", 75000, 175000)]
    public void The_pay_in_an_ad_is_read_next_to_its_word(string text, double min, double max)
    {
        var pay = PayParser.Find(text)!;

        Assert.Equal(((decimal)min, (decimal)max), (pay.Min, pay.Max));
    }

    [Theory]
    [InlineData("Competitive salary")]
    [InlineData("5 years of experience")]
    public void No_amount_no_pay(string text) => Assert.Null(PayParser.Parse(text));
}
