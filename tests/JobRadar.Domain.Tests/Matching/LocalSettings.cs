using System.Runtime.CompilerServices;
using System.Text.Json;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// The author's local configuration (<c>appsettings.Local.json</c>, outside Git), for the tests that check the real
/// rules: <c>JOBRADAR_LOCAL_SETTINGS</c>, or the file next to the worker in this checkout. A worktree has none of its
/// own: point the variable at the main checkout's.
/// </summary>
public static class LocalSettings
{
    public const string EnvironmentVariable = "JOBRADAR_LOCAL_SETTINGS";

    private static readonly JsonDocumentOptions Document = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The file, or null when there is none.</summary>
    public static string? Find()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured)
            return File.Exists(configured) ? configured : throw new FileNotFoundException($"{EnvironmentVariable} points at a missing file.", configured);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "JobRadar.slnx")))
            {
                var path = Path.Combine(dir.FullName, "src", "JobRadar.Worker", "appsettings.Local.json");
                return File.Exists(path) ? path : null;
            }
        }
        return null;
    }

    /// <summary>A market's eligibility block (<c>Matching:Markets:&lt;market&gt;:Eligibility</c>), or null when the file or the block is missing.</summary>
    public static EligibilityRules? Eligibility(JobMarket market)
    {
        if (Find() is not { } path)
            return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path), Document);
        return Child(doc.RootElement, "Matching") is { } matching
            && Child(matching, "Markets") is { } markets
            && Child(markets, market.ToString()) is { } entry
            && Child(entry, "Eligibility") is { } block
            ? block.Deserialize<EligibilityRules>(Json)
            : null;
    }

    /// <summary>The rules as one string, to compare two blocks: what scores the same writes the same.</summary>
    public static string Canonical(EligibilityRules rules) => JsonSerializer.Serialize(rules.Normalized());

    // Configuration keys ignore case.
    private static JsonElement? Child(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => (JsonElement?)p.Value).FirstOrDefault()
            : null;
}

/// <summary>Runs only where the author's local configuration is (<see cref="LocalSettings"/>).</summary>
public sealed class LocalSettingsFactAttribute : FactAttribute
{
    public LocalSettingsFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (LocalSettings.Find() is null)
            Skip = $"No local configuration: set {LocalSettings.EnvironmentVariable} to appsettings.Local.json.";
    }
}
