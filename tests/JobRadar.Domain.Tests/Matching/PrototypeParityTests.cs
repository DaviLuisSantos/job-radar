using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// Runs only with JOBRADAR_PARITY_FILE pointing at cases exported from the Python prototype.
/// The file holds real ads, so it never enters this public repository.
/// </summary>
public sealed class ParityFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "JOBRADAR_PARITY_FILE";

    public ParityFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariable)))
            Skip = $"Set {EnvironmentVariable} to the prototype's scoring-parity.json.";
    }
}

public class PrototypeParityTests
{
    [ParityFact]
    public void Every_prototype_case_scores_the_same_with_the_same_steps()
    {
        var file = JsonSerializer.Deserialize<ParityFile>(
            File.ReadAllText(Environment.GetEnvironmentVariable(ParityFactAttribute.EnvironmentVariable)!))!;
        var today = DateOnly.Parse(file.Today, System.Globalization.CultureInfo.InvariantCulture);
        var policy = file.Policy.ToPolicy();
        var profiles = file.Profiles.ToDictionary(p => p.Key, p => p.Value.ToProfile(p.Key));
        // Who may apply is the market's words: the local block, else (until it is pasted) the rules that were in code.
        var rules = MarketRules.For(JobMarket.AU, LocalSettings.Eligibility(JobMarket.AU) ?? LegacyEligibility.Australia);
        var searched = rules.FullTextGroups.Values.SelectMany(p => p).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unsearched = file.Policy.FullTextGroups.Values.SelectMany(p => p).Where(p => !searched.Contains(p)).ToList();
        Assert.True(unsearched.Count == 0, $"The prototype searched phrases the AU rules do not: {string.Join(", ", unsearched)}.");

        var mismatches = new List<string>();
        var byDesign = 0;
        foreach (var c in file.Cases)
        {
            var input = new ScoringInput(c.Title, c.Description, c.Location, c.WorkType,
                c.ListedAt is null ? null : DateTimeOffset.Parse(c.ListedAt, System.Globalization.CultureInfo.InvariantCulture),
                c.FullTextTerms);
            var score = FitScorer.Score(input, profiles[c.Profile], rules, policy, today);
            var points = score.Items.Select(i => i.Points).ToList();

            if (score.Final != c.Final || !points.SequenceEqual(c.Points))
            {
                // Differ on purpose; everything else must match. Since RulesVersion 8 a truck licence weighs 40
                // (10 when desirable), not the prototype's 20, and more spellings of it are read. Since 9 an
                // "Assistant ... Manager" title is management, excluded as leadership.
                if (score.Items.Any(i => i.Rule is ScoreRule.TruckLicence or ScoreRule.FullTextTruckLicence)
                    || (score.Items.Any(i => i.Rule == ScoreRule.LeadershipTitle) && c.Title.Contains("assistant", StringComparison.OrdinalIgnoreCase)))
                {
                    byDesign++;
                    continue;
                }
                mismatches.Add(
                    $"{c.Id} [{c.Profile}] '{c.Title}': python {c.Final} ({string.Join(", ", c.Items)}) "
                    + $"vs .NET {score.Final} ({string.Join(", ", score.Items.Select(i => $"{i.Rule}|{i.Detail}|{i.Points}"))})");
            }
        }

        var report = new StringBuilder()
            .AppendLine($"{mismatches.Count} of {file.Cases.Count} cases differ ({byDesign} more by design: truck licence, assistant manager). First ones:")
            .AppendJoin(Environment.NewLine, mismatches.Take(15))
            .ToString();
        Assert.True(mismatches.Count == 0, report);
    }

    private sealed record ParityFile(
        [property: JsonPropertyName("today")] string Today,
        [property: JsonPropertyName("policy")] ParityPolicy Policy,
        [property: JsonPropertyName("profiles")] Dictionary<string, ParityProfile> Profiles,
        [property: JsonPropertyName("cases")] List<ParityCase> Cases);

    // The file's employer-assistance keys hold the policy defaults (required: no, 10 and 10): not read. Its phrase
    // groups are only checked against the market's: the market's own block decides what is searched.
    private sealed record ParityPolicy(
        [property: JsonPropertyName("disqualify_max_score")] int DisqualifyMaxScore,
        [property: JsonPropertyName("exclude_senior")] bool ExcludeSenior,
        [property: JsonPropertyName("apprenticeship_penalty")] int ApprenticeshipPenalty,
        [property: JsonPropertyName("long_contract_penalty")] int LongContractPenalty,
        [property: JsonPropertyName("fulltext_groups")] Dictionary<string, List<string>> FullTextGroups,
        [property: JsonPropertyName("aging_after_days")] int AgingAfterDays,
        [property: JsonPropertyName("aging_penalty")] int AgingPenalty,
        [property: JsonPropertyName("stale_after_days")] int StaleAfterDays,
        [property: JsonPropertyName("stale_penalty")] int StalePenalty)
    {
        public ScoringPolicy ToPolicy() => new()
        {
            DisqualifyMaxScore = DisqualifyMaxScore,
            ExcludeSeniorTitles = ExcludeSenior,
            ApprenticeshipPenalty = ApprenticeshipPenalty,
            LongContractPenalty = LongContractPenalty,
            AgingAfterDays = AgingAfterDays,
            AgingPenalty = AgingPenalty,
            StaleAfterDays = StaleAfterDays,
            StalePenalty = StalePenalty,
        };
    }

    private sealed record ParityProfile(
        [property: JsonPropertyName("positive_signals")] List<string> Positive,
        [property: JsonPropertyName("negative_signals")] List<string> Negative,
        [property: JsonPropertyName("disqualify_signals")] List<string> Disqualify,
        [property: JsonPropertyName("qualified_titles")] List<string> QualifiedTitles,
        [property: JsonPropertyName("entry_level_boost")] bool EntryLevelBoost,
        [property: JsonPropertyName("allow_senior")] bool AllowSenior,
        [property: JsonPropertyName("preferred_locations")] List<string> PreferredLocations)
    {
        public MatchingProfile ToProfile(string name) =>
            new(name, Positive, Negative, Disqualify, QualifiedTitles, EntryLevelBoost, AllowSenior, PreferredLocations);
    }

    private sealed record ParityCase(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("profile")] string Profile,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("work_type")] string? WorkType,
        [property: JsonPropertyName("listed_at")] string? ListedAt,
        [property: JsonPropertyName("fulltext_terms")] List<string> FullTextTerms,
        [property: JsonPropertyName("final")] int Final,
        [property: JsonPropertyName("points")] List<int> Points,
        [property: JsonPropertyName("items")] List<string> Items);
}
