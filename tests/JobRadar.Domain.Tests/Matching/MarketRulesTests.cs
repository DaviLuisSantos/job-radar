using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>Fictional ads. The Brazilian rules are new, not ported: these pin what each one is for.</summary>
public class MarketRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static readonly MatchingProfile Tech = new(
        "tech", PositiveSignals: ["C#"], NegativeSignals: [], DisqualifySignals: [], QualifiedTitles: [],
        EntryLevelBoost: false, AllowSenior: true, PreferredLocations: []);

    private static readonly MatchingProfile Dotnet = new(
        "dotnet-br", PositiveSignals: [".NET", "C#"], NegativeSignals: [], DisqualifySignals: [], QualifiedTitles: [],
        EntryLevelBoost: false, AllowSenior: true, PreferredLocations: [])
    {
        ExcludedTitles = ["júnior", "estágio", "java"],
        OnSiteLocations = ["Rio de Janeiro"],
        Market = JobMarket.BR,
    };

    private static ScoringInput Br(string title, string? summary = null, string location = "Rio de Janeiro RJ", string? description = null) =>
        new(title, summary, location, null, null, []) { Description = description };

    private static MarketRules Rules(JobMarket market) => market == JobMarket.AU ? TestMarkets.Au : MarketRules.For(market);

    private static FitScore Score(ScoringInput job, MatchingProfile? profile = null) =>
        FitScorer.Score(job, profile ?? Dotnet, Rules((profile ?? Dotnet).Market), ScoringPolicy.Default, Today);

    private static bool Has(FitScore score, ScoreRule rule) => score.Items.Any(i => i.Rule == rule);

    [Fact]
    public void The_profile_market_picks_the_rules_whatever_the_text()
    {
        // The same ad, read by an Australian profile and by a Brazilian one.
        var ad = Br("Desenvolvedor C# Sênior", "Guild members only");

        Assert.Equal(ScoreRule.EligibilityRestricted, Assert.Single(Score(ad, Tech).Items).Rule);
        Assert.True(Has(Score(ad), ScoreRule.SeniorPenalty));
        Assert.Same(MarketRules.Brazil, MarketRules.For(JobMarket.BR));
    }

    [Theory]
    [InlineData(JobMarket.LatamRemote)]
    [InlineData(JobMarket.GlobalRemote)]
    public void Every_market_has_its_own_rules(JobMarket market) => Assert.Equal(market, MarketRules.For(market).Market);

    [Fact]
    public void Australia_reads_who_may_apply_from_its_eligibility_block()
    {
        var input = new ScoringInput("Senior C# Developer", "Guild members only. Remote.", "Perth WA", "Contract", null, []);

        var withBlock = FitScorer.Score(input, Tech, TestMarkets.Au, ScoringPolicy.Default, Today);
        var withNone = FitScorer.Score(input, Tech, MarketRules.Australia(EligibilityRules.None), ScoringPolicy.Default, Today);

        Assert.Equal(0, withBlock.Final);
        Assert.Equal(ScoreRule.EligibilityRestricted, Assert.Single(withBlock.Items).Rule);
        Assert.DoesNotContain(withNone.Items, i => i.Rule == ScoreRule.EligibilityRestricted);
    }

    [Fact]
    public void Rules_of_another_market_are_refused()
    {
        var input = new ScoringInput("C# Developer", null, "Perth WA", null, null, []);

        Assert.Throws<ArgumentException>(() => FitScorer.Score(input, Tech, MarketRules.Brazil, ScoringPolicy.Default, Today));
    }

    [Fact]
    public void Australian_rules_never_fire_on_a_Brazilian_ad()
    {
        var score = Score(Br("Desenvolvedor .NET", "Contrato CLT. Seasonal members bem-vindos. Guild pass não exigido."));

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.EligibilityRestricted or ScoreRule.EligibilityWelcome
            or ScoreRule.FlexibleWorkType or ScoreRule.LongContract);
    }

    [Fact]
    public void English_as_a_condition_costs_points_only_in_Brazil()
    {
        var br = Score(Br("Desenvolvedor .NET Pleno", description: "Requisitos: C#, SQL e inglês avançado."));
        var au = FitScorer.Score(new ScoringInput("C# Developer", "Fluent English", "Perth WA", null, null, []), Tech, TestMarkets.Au, ScoringPolicy.Default, Today);

        Assert.Equal(-ScoringPolicy.Default.EnglishRequiredPenalty, Assert.Single(br.Items, i => i.Rule == ScoreRule.EnglishRequired).Points);
        Assert.False(Has(au, ScoreRule.EnglishRequired));
    }

    [Theory]
    [InlineData("Desenvolvedor .NET Júnior", "júnior")]
    [InlineData("Estágio em Desenvolvimento .NET", "estágio")]
    [InlineData("Desenvolvedor Java e .NET", "java")]
    public void A_title_word_the_profile_rules_out_excludes(string title, string word)
    {
        var score = Score(Br(title));

        Assert.Equal(0, score.Final);
        Assert.Equal(word, Assert.Single(score.Items, i => i.Rule == ScoreRule.TitleExcluded).Detail);
    }

    [Fact]
    public void Senior_costs_points_and_mid_to_senior_does_not()
    {
        Assert.True(Has(Score(Br("Desenvolvedor .NET Sênior")), ScoreRule.SeniorPenalty));
        Assert.False(Has(Score(Br("Desenvolvedor .NET Pleno/Sênior")), ScoreRule.SeniorPenalty));
    }

    [Fact]
    public void Leadership_titles_are_ruled_out()
    {
        Assert.True(Has(Score(Br("Tech Lead .NET")), ScoreRule.LeadershipTitle));
        Assert.True(Has(Score(Br("Coordenador de Desenvolvimento")), ScoreRule.LeadershipTitle));
    }

    [Fact]
    public void Remote_is_rewarded_in_Portuguese()
    {
        Assert.True(Has(Score(Br("Desenvolvedor .NET", "Remoto · CLT")), ScoreRule.Remote));
        Assert.True(Has(Score(Br("Desenvolvedor .NET", "Híbrido · CLT")), ScoreRule.Hybrid));
    }

    [Fact]
    public void On_site_work_counts_only_where_the_profile_accepts_it()
    {
        var elsewhere = Score(Br("Desenvolvedor .NET", "Presencial · CLT", location: "São Paulo SP"));
        var home = Score(Br("Desenvolvedor .NET", "Presencial · CLT", location: "Centro, Rio de Janeiro RJ"));
        var anywhere = Score(Br("Desenvolvedor .NET", "Presencial · CLT", location: "São Paulo SP"), Dotnet with { OnSiteLocations = [] });

        Assert.Equal(0, elsewhere.Final);
        Assert.True(Has(elsewhere, ScoreRule.OnSiteElsewhere));
        Assert.False(Has(home, ScoreRule.OnSiteElsewhere));
        Assert.False(Has(anywhere, ScoreRule.OnSiteElsewhere));
    }

    [Theory]
    [InlineData("Necessário 5+ anos de experiência com C#", 5)]
    [InlineData("Experiência mínima de 3 anos em .NET", 3)]
    [InlineData("2 anos de atuação com APIs; 5 anos de experiência desejável", 2)]
    [InlineData("Experiência com APIs REST", null)]
    public void Years_asked_are_read_in_Portuguese(string text, int? years)
    {
        Assert.Equal(years, MarketRules.Brazil.YearsAsked(text.ToLowerInvariant()));
    }
}
