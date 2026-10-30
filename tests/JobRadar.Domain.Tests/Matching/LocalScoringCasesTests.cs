using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// Cases written from real ads, checked against the real eligibility block: both live outside Git, next to each other
/// (<c>scoring-cases.Local.json</c> beside <c>appsettings.Local.json</c>, or <c>JOBRADAR_LOCAL_CASES</c>). The public
/// tests check the mechanism with fictional words; these check that the author's words still do what they did.
/// </summary>
public class LocalScoringCasesTests
{
    public const string EnvironmentVariable = "JOBRADAR_LOCAL_CASES";

    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Yesterday = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    // The two shapes the public scorer tests use: an office profile and a site one.
    private static readonly Dictionary<string, MatchingProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tech"] = new("tech", ["C#", ".NET", "contract"], ["hospitality", "cert III"], [], [], false, true, ["Perth"]),
        ["trades"] = new("trades", ["offsider"], ["cert III"], ["trade qualified"], ["electrician"], true, false, ["WA"]),
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string? Find()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured)
            return File.Exists(configured) ? configured : throw new FileNotFoundException($"{EnvironmentVariable} points at a missing file.", configured);
        return LocalSettings.Find() is { } settings && Path.Combine(Path.GetDirectoryName(settings)!, "scoring-cases.Local.json") is var path
            && File.Exists(path) ? path : null;
    }

    [LocalCasesFact]
    public void Every_local_case_scores_as_written()
    {
        var file = JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(Find()!), Json)!;
        var rules = MarketRules.For(JobMarket.AU, LocalSettings.Eligibility(JobMarket.AU)
            ?? throw new InvalidOperationException($"No Matching:Markets:AU:Eligibility in {LocalSettings.Find() ?? "the local configuration"}."));

        var failures = new List<string>();
        foreach (var c in file.Cases)
        {
            var input = new ScoringInput(c.Title, c.Summary, c.Location ?? "Sydney NSW", c.WorkType ?? "Full time", Yesterday, c.FullText ?? [])
            {
                Description = c.Description,
            };
            var policy = ScoringPolicy.Default with { AssistanceRequired = c.AssistanceRequired };
            var score = FitScorer.Score(input, Profiles[c.Profile ?? "tech"], rules, policy, Today);
            var got = string.Join(", ", score.Items.Select(i => $"{i.Rule}|{i.Detail}|{i.Where}|{i.Points}"));

            var e = c.Expect;
            var wrong = new List<string>();
            if (e.Final is { } final && score.Final != final)
                wrong.Add($"final {score.Final}, expected {final}");
            if (e.Excluded is { } excluded && score.Excluded != excluded)
                wrong.Add(excluded ? "not excluded" : "excluded");
            foreach (var item in e.Items ?? [])
            {
                var found = score.Items.Where(i => i.Rule == item.Rule).ToList();
                if (found.Count != 1)
                    wrong.Add($"{found.Count} × {item.Rule}");
                else if ((item.Points is { } points && found[0].Points != points)
                    || (item.Where is { } where && found[0].Where != where)
                    || (item.Detail is { } detail && found[0].Detail != detail))
                    wrong.Add($"{item.Rule} is {found[0].Where} {found[0].Points} '{found[0].Detail}'");
            }
            foreach (var absent in e.Absent ?? [])
            {
                if (score.Items.Any(i => i.Rule == absent))
                    wrong.Add($"{absent} present");
            }
            if (wrong.Count > 0)
                failures.Add($"{c.Name}: {string.Join("; ", wrong)} ({got})");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {file.Cases.Count} local cases differ:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private sealed record CaseFile(List<ScoringCase> Cases);

    private sealed record ScoringCase(
        string Name, string? Profile, string Title, string? Summary, string? Location, string? WorkType,
        string? Description, string[]? FullText, bool AssistanceRequired, Expectation Expect);

    private sealed record Expectation(int? Final, bool? Excluded, List<ExpectedItem>? Items, List<ScoreRule>? Absent);

    private sealed record ExpectedItem(ScoreRule Rule, int? Points, ScoreWhere? Where, string? Detail);
}

/// <summary>Runs only where the author's local cases are (<see cref="LocalScoringCasesTests"/>).</summary>
public sealed class LocalCasesFactAttribute : FactAttribute
{
    public LocalCasesFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (LocalScoringCasesTests.Find() is null)
            Skip = $"No local scoring cases: set {LocalScoringCasesTests.EnvironmentVariable} to scoring-cases.Local.json.";
    }
}
