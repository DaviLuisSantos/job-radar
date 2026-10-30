using Bunit;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;
using JobRadar.Web.Components.Shared;
using JobRadar.Web.Text;
using MudBlazor.Services;

namespace JobRadar.Web.Tests;

public class ComponentTests : BunitContext
{
    public ComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void Explanation_walks_from_the_base_to_the_final_score()
    {
        var score = new FitScore(74,
        [
            new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 16),
            new ScoreItem(ScoreRule.PositiveSignal, ".NET", ScoreWhere.Body, 8),
        ]);

        var cut = Render<Explanation>(p => p.Add(x => x.Score, score));

        var lines = cut.FindAll("li").Select(li => li.TextContent.Trim()).ToList();
        Assert.Equal(4, lines.Count);
        Assert.StartsWith("50", lines[0]);
        Assert.Contains("+16", lines[1]);
        Assert.Contains("Sinal do perfil “C#”", lines[1]);
        Assert.Contains("título", lines[1]);
        Assert.Contains("Nota final", lines[3]);
        Assert.Equal(2, cut.FindAll("li.plus").Count);
    }

    [Fact]
    public void An_excluded_listing_has_no_base_line_and_says_why()
    {
        var score = new FitScore(0, [new ScoreItem(ScoreRule.CitizenshipRequired, "australian citizen", ScoreWhere.Text, -50)]);

        var cut = Render<Explanation>(p => p.Add(x => x.Score, score));

        Assert.Empty(cut.FindAll("li.base"));
        Assert.Single(cut.FindAll("li.exclusion"));
        Assert.Contains("Excluída", cut.Find("li.total").TextContent);
    }

    [Fact]
    public void Facts_show_as_labels_and_nothing_shows_without_them()
    {
        var cut = Render<FactChips>(p => p.Add(x => x.Facts,
            [new ListingFact(FactKind.Pay, "$48/hr"), new ListingFact(FactKind.Roster, "2:1 roster")]));

        Assert.Equal(["$48/hr", "2:1 roster"], cut.FindAll(".chip.fact").Select(c => c.TextContent));
        Assert.Contains("fact-pay", cut.Find(".chip.fact").ClassList);
        Assert.Empty(Render<FactChips>(p => p.Add(x => x.Facts, [])).Markup.Trim());
        Assert.All(Enum.GetValues<FactKind>(), k => Assert.EndsWith("(lido do anúncio)", Wording.FactTitle(k), StringComparison.Ordinal));
    }

    private static TriageRow Row(int score) =>
        new(new ListingId("gupy", "7"), "Desenvolvedor .NET", "Acme", new ListingLocation(Country.BR, null, "Rio de Janeiro", "RJ", null),
            WorkType.FullTime, null, null, DateTimeOffset.UnixEpoch, score, null, null);

    private IRenderedComponent<TriageRowView> RenderRow(TriageRow row) =>
        Render<TriageRowView>(p => p.Add(x => x.Row, row).Add(x => x.Href, "listing/gupy/7"));

    [Fact]
    public void A_row_shows_the_arrangement_and_contract_next_to_the_facts()
    {
        var cut = RenderRow(Row(72) with
        {
            Conditions = new ListingConditions(WorkArrangement.Remote, "PJ"),
            Facts = [new ListingFact(FactKind.Start, "Immediate start")],
        });

        Assert.Equal(["Remota", "PJ", "Immediate start"], cut.FindAll(".tags .tag, .tags .chip.fact").Select(c => c.TextContent.Trim()));
        Assert.Single(cut.FindAll(".tag-remote"));
        Assert.Empty(cut.FindAll(".excluded-by"));
    }

    [Fact]
    public void A_remote_row_shows_who_may_apply_the_time_zones_and_how_it_hires()
    {
        var cut = RenderRow(Row(72) with
        {
            Location = ListingLocation.Unknown(Country.Remote),
            Eligibility = "Brazil, Colombia, Mexico",
            Conditions = new ListingConditions(WorkArrangement.Remote, null) { TimeZones = new TimeZoneWindow(-5, -2) },
            Engagement = Engagement.Contractor,
        });

        Assert.Equal(["Aceita: Brazil, Colombia, Mexico", "Remota", "fuso UTC-5 a UTC-2", "contractor (PJ)"],
            cut.FindAll(".tags .tag").Select(c => c.TextContent.Trim()));
        Assert.Contains("Remoto", cut.Find(".meta").TextContent);
        Assert.Equal("Aberta ao mundo", Wording.Eligibility(RemoteEligibility.Worldwide));
    }

    [Fact]
    public void A_row_without_conditions_shows_no_tags()
    {
        var cut = RenderRow(Row(72));

        Assert.Empty(cut.FindAll(".tag"));
    }

    [Fact]
    public void An_excluded_row_says_in_one_line_why()
    {
        var cut = RenderRow(Row(0) with { ExcludedBy = new ScoreItem(ScoreRule.TitleExcluded, "júnior", ScoreWhere.Title, -50) });

        Assert.Contains("excluded", cut.Find("li.row").ClassList);
        Assert.Equal("Palavra do título que este perfil descarta: júnior", cut.Find(".excluded-by").TextContent);
        Assert.Equal(Wording.ExcludedReasonUnknown, RenderRow(Row(0)).Find(".excluded-by").TextContent);
    }

    [Fact]
    public void Row_buttons_report_the_label_and_the_status()
    {
        Verdict? label = null;
        ApplicationStatus? status = null;
        var cut = Render<TriageRowView>(p => p.Add(x => x.Row, Row(72)).Add(x => x.Href, "x")
            .Add(x => x.OnLabel, v => label = v).Add(x => x.OnStatus, s => status = s));

        cut.FindAll(".actions button").Single(b => b.TextContent.Trim() == "Ruim").Click();
        cut.FindAll(".actions button").Single(b => b.TextContent.Trim() == "Candidatei").Click();

        Assert.Equal((Verdict.Bad, ApplicationStatus.Applied), (label, status));
    }

    [Theory]
    [InlineData(0, "score-excluded", "×")]
    [InlineData(42, "score-low", "42")]
    [InlineData(60, "score-fair", "60")]
    [InlineData(70, "score-good", "70")]
    [InlineData(91, "score-strong", "91")]
    public void Score_badge_colour_follows_the_band(int value, string css, string text)
    {
        var cut = Render<ScoreBadge>(p => p.Add(x => x.Value, value));

        Assert.Contains(css, cut.Find("span").ClassList);
        Assert.Equal(text, cut.Find("span").TextContent);
    }
}

public class BookmarkletTests
{
    [Fact]
    public void Sends_to_this_site_only_and_fits_one_line()
    {
        var code = Bookmarklet.For("http://100.67.84.28:5380/");

        Assert.StartsWith("javascript:(()=>{const o=\"http://100.67.84.28:5380\";", code, StringComparison.Ordinal);
        Assert.Contains("w.postMessage({type:'jobradar-capture'", code, StringComparison.Ordinal);
        Assert.Contains("},o);", code, StringComparison.Ordinal);   // target origin: this site, never '*'
        Assert.DoesNotContain('\n', code);
        Assert.DoesNotContain('%', code);   // a '%' in a bookmark URL is read as an escape
    }
}

public class WordingTests
{
    [Fact]
    public void Every_rule_has_its_own_wording()
    {
        foreach (var rule in Enum.GetValues<ScoreRule>())
        {
            var text = Wording.Describe(new ScoreItem(rule, "x", ScoreWhere.Text, 0));
            Assert.False(text.StartsWith($"{rule}:", StringComparison.Ordinal), $"{rule} falls back to its enum name.");
        }
    }

    [Theory]
    [InlineData(0, "hoje")]
    [InlineData(3, "3d")]
    [InlineData(20, "2sem")]
    public void Age_is_short(int days, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, Wording.Age(now.AddDays(-days), now));
    }

    [Fact]
    public void Errors_from_the_application_are_worded_by_type_never_by_their_English_message()
    {
        Assert.Equal("A vaga seek:1 nunca foi coletada.", Wording.Error(new JobRadar.Application.Tracking.ListingNotFoundException(new ListingId("seek", "1"))));
        Assert.StartsWith("O texto é curto demais", Wording.Error(new JobRadar.Application.Tracking.DescriptionTooShortException()), StringComparison.Ordinal);
        Assert.Equal("Erro inesperado (InvalidOperationException).",
            Wording.Error(new InvalidOperationException("secret English detail")));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("HttpRequestException: 503", "HttpRequestException: 503")]
    [InlineData("TaskCanceledException: timeout\n   at Polly...", "TaskCanceledException: timeout")]
    [InlineData("SourceBlockedException: Seek answered 403 with a challenge", "SourceBlockedException: Seek…")]
    public void Errors_are_shortened_to_their_first_line(string? error, string expected) =>
        Assert.Equal(expected, Wording.Shorten(error, 30));
}

public class RequirementWordingTests
{
    [Theory]
    [InlineData(1, "1 vaga")]
    [InlineData(0, "0 vagas")]
    [InlineData(1406, "1.406 vagas")]
    public void Listing_counts_read_naturally(int count, string text) => Assert.Equal(text, Wording.Listings(count));

    [Fact]
    public void Every_catalog_requirement_has_a_plain_title_and_hint()
    {
        Assert.All(RequirementCatalog.Defaults, item =>
        {
            var (title, hint) = Wording.Requirement(item.Id);
            Assert.NotEqual(item.Id, title);
            Assert.False(string.IsNullOrWhiteSpace(hint));
        });
        Assert.Equal(("forklift", ""), Wording.Requirement("forklift"));
    }

    [Theory]
    [InlineData(1, "1 ano", "1")]
    [InlineData(0.5, "0,5 anos", "0,5")]
    [InlineData(10, "10 anos", "10+")]
    public void Years_read_naturally(double years, string text, string button)
    {
        Assert.Equal(text, Wording.Years(years));
        Assert.Equal(button, Wording.YearsShort(years));
    }

    [Fact]
    public void Every_requirement_state_and_kind_has_words()
    {
        Assert.All(Enum.GetValues<RequirementState>(), s => Assert.NotEqual(s.ToString(), Wording.State(s)));
        Assert.All(Enum.GetValues<RequirementKind>(), k => Assert.False(string.IsNullOrWhiteSpace(Wording.Kind(k))));
        Assert.DoesNotContain(nameof(ScoreRule.RequirementHeld),
            Wording.Describe(new ScoreItem(ScoreRule.RequirementHeld, "HRWL", ScoreWhere.FullText, 10)), StringComparison.Ordinal);
    }
}

public class DeploymentTests
{
    [Fact]
    public void Reads_the_deploy_record_next_to_the_site_folder()
    {
        var root = Directory.CreateTempSubdirectory("jobradar-deploy-");
        try
        {
            var web = root.CreateSubdirectory("web");
            File.WriteAllText(Path.Combine(root.FullName, Deployment.FileName),
                "dab938e deployed 2026-10-23 19:20:00 -03:00\r\n", System.Text.Encoding.UTF8);   // with a BOM, like Set-Content -Encoding utf8

            Assert.Equal("dab938e deployed 2026-10-23 19:20:00 -03:00", Deployment.Read(web.FullName + Path.DirectorySeparatorChar).Deployed);
            Assert.Null(Deployment.Read(root.FullName).Deployed);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_build_commit_comes_from_the_informational_version()
    {
        var commit = Deployment.BuildCommit(typeof(Deployment).Assembly);

        Assert.Matches("^[0-9a-f]{7}$", commit);
    }
}
