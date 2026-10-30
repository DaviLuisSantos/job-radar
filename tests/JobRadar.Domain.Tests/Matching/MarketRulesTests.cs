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

    private static FitScore Score(ScoringInput job, MatchingProfile? profile = null) =>
        FitScorer.Score(job, profile ?? Dotnet, ScoringPolicy.Default, Today);

    private static bool Has(FitScore score, ScoreRule rule) => score.Items.Any(i => i.Rule == rule);

    [Fact]
    public void The_profile_market_picks_the_rules_whatever_the_text()
    {
        // The same ad, read by an Australian profile and by a Brazilian one.
        var ad = Br("Desenvolvedor C# Sênior", "Australian citizens only");

        Assert.Equal(ScoreRule.CitizenshipRequired, Assert.Single(Score(ad, Tech).Items).Rule);
        Assert.True(Has(Score(ad), ScoreRule.SeniorPenalty));
        Assert.Same(MarketRules.Australia, MarketRules.For(JobMarket.AU));
        Assert.Same(MarketRules.Brazil, MarketRules.For(JobMarket.BR));
    }

    [Theory]
    [InlineData(JobMarket.LatamRemote)]
    [InlineData(JobMarket.GlobalRemote)]
    public void A_market_without_rules_yet_is_refused_instead_of_read_as_another(JobMarket market)
    {
        Assert.Throws<NotSupportedException>(() => Score(Br("Backend Developer"), Tech with { Market = market }));
    }

    [Fact]
    public void Australia_keeps_the_prototype_phrases()
    {
        // The extraction moved where the phrases live, not what they say: parity depends on it.
        var input = new ScoringInput("Senior C# Developer", "Australian citizens only. Remote.", "Perth WA", "Contract", null, []);

        var score = FitScorer.Score(input, Tech, ScoringPolicy.Default, Today);

        Assert.Equal(0, score.Final);
        Assert.Equal(ScoreRule.CitizenshipRequired, Assert.Single(score.Items).Rule);
    }

    [Fact]
    public void Australian_rules_never_fire_on_a_Brazilian_ad()
    {
        var score = Score(Br("Desenvolvedor .NET", "Contrato CLT. Working holiday não se aplica. Citizenship não exigida."));

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.CitizenshipRequired or ScoreRule.WorkingHolidayFriendly
            or ScoreRule.FlexibleWorkType or ScoreRule.LongContract);
    }

    [Fact]
    public void English_as_a_condition_costs_points_only_in_Brazil()
    {
        var br = Score(Br("Desenvolvedor .NET Pleno", description: "Requisitos: C#, SQL e inglês avançado."));
        var au = FitScorer.Score(new ScoringInput("C# Developer", "Fluent English", "Perth WA", null, null, []), Tech, ScoringPolicy.Default, Today);

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
