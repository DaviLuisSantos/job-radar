using Bunit;
using JobRadar.Domain.Matching;
using JobRadar.Web.Components.Shared;
using JobRadar.Web.Text;

namespace JobRadar.Web.Tests;

public class ComponentTests : BunitContext
{
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
        Assert.Contains("Profile signal “C#”", lines[1]);
        Assert.Contains("title", lines[1]);
        Assert.Contains("Final score", lines[3]);
        Assert.Equal(2, cut.FindAll("li.plus").Count);
    }

    [Fact]
    public void An_excluded_listing_has_no_base_line_and_says_why()
    {
        var score = new FitScore(0, [new ScoreItem(ScoreRule.CitizenshipRequired, "australian citizen", ScoreWhere.Text, -50)]);

        var cut = Render<Explanation>(p => p.Add(x => x.Score, score));

        Assert.Empty(cut.FindAll("li.base"));
        Assert.Single(cut.FindAll("li.exclusion"));
        Assert.Contains("Excluded", cut.Find("li.total").TextContent);
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
    [InlineData(0, "today")]
    [InlineData(3, "3d")]
    [InlineData(20, "2w")]
    public void Age_is_short(int days, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, Wording.Age(now.AddDays(-days), now));
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
    [InlineData(1, "1 listing")]
    [InlineData(0, "0 listings")]
    [InlineData(1406, "1,406 listings")]
    public void Listing_counts_read_naturally(int count, string text) => Assert.Equal(text, Wording.Listings(count));

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
