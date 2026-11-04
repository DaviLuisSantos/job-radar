using System.Text.Json;
using System.Text.Json.Nodes;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Settings;

/// <summary>
/// Reads and writes the configuration sections kept in the database. A section is saved only when the configuration it
/// would produce still builds (profiles, markets and their eligibility).
/// </summary>
public sealed class SettingsService(ISettingsRepository repository, IConfiguration configuration, TimeProvider time)
{
    /// <summary>First segments of a key that may be kept in the database; connection, provider and telemetry never are.</summary>
    public static IReadOnlyList<string> AllowedPrefixes { get; } =
        ["Matching", "Market", "Collection", "Description", "Seek", "Gupy", "LinkedIn", "Himalayas", "Jobicy", "Ats"];

    /// <summary>The configuration in use, nested, restricted to the allowed prefixes.</summary>
    public string GetEffective()
    {
        var root = new JsonObject();
        foreach (var section in configuration.GetChildren())
            if (IsAllowedPrefix(section.Key))
                root[section.Key] = ToNode(section);
        return root.ToJsonString();
    }

    public Task<Setting?> GetAsync(string key, CancellationToken ct) =>
        IsAllowedKey(key) ? repository.GetAsync(key, ct) : Task.FromResult<Setting?>(null);

    public async Task SaveAsync(string key, string json, CancellationToken ct)
    {
        EnsureAllowed(key);
        string nested;
        try
        {
            nested = SettingsJson.Nest(key, json);
        }
        catch (JsonException ex)
        {
            throw new SettingsValidationException($"Invalid JSON: {ex.Message}");
        }
        if (key.StartsWith("Matching", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Market", StringComparison.OrdinalIgnoreCase))
            Validate(key, nested);
        await repository.SaveAsync(key, json, time.GetUtcNow(), ct);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct)
    {
        EnsureAllowed(key);
        if (key.StartsWith("Matching", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Market", StringComparison.OrdinalIgnoreCase))
            Validate(key, null);
        return await repository.DeleteAsync(key, ct);
    }

    private static bool IsAllowedPrefix(string segment) =>
        AllowedPrefixes.Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static bool IsAllowedKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 200
        && key.Split(':') is var segments && segments.All(s => s.Trim().Length > 0)
        && IsAllowedPrefix(segments[0]);

    private static void EnsureAllowed(string key)
    {
        if (!IsAllowedKey(key))
            throw new SettingsValidationException(
                $"Key '{key}' is not allowed. Use a key starting with: {string.Join(", ", AllowedPrefixes)}.");
    }

    /// <summary>
    /// Builds the rules from the configuration as it will be after the change: the section under <paramref name="key"/>
    /// is replaced (not merged) by the candidate, or dropped when it is null, as it is at the next start.
    /// Strict on purpose: a file that still holds that section does not count, since the file is meant to be emptied.
    /// </summary>
    private void Validate(string key, string? nested)
    {
        var kept = configuration.AsEnumerable()
            .Where(p => p.Value is not null
                && !p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                && !p.Key.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
            .Select(p => new KeyValuePair<string, string?>(p.Key, p.Value));
        var builder = new ConfigurationBuilder().AddInMemoryCollection(kept);
        if (nested is not null)
            builder.AddInMemoryCollection(SettingsJson.Flatten(nested));
        var candidate = builder.Build();
        try
        {
            var options = new MatchingOptions();
            candidate.GetSection(MatchingOptions.Section).Bind(options, o => o.ErrorOnUnknownConfiguration = false);
            var result = new MatchingOptionsValidation().Validate(null, options);
            if (result.Failed)
                throw new SettingsValidationException(result.FailureMessage);
            _ = new ProfileCatalog(Options.Create(options), ScoringPolicy.Default);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            throw new SettingsValidationException(ex.Message);
        }
    }

    private static JsonNode? ToNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
            return section.Value is null ? null : JsonValue.Create(section.Value);
        if (children.Select((c, i) => c.Key == i.ToString()).All(x => x))
        {
            var array = new JsonArray();
            foreach (var c in children)
                array.Add(ToNode(c));
            return array;
        }
        var obj = new JsonObject();
        foreach (var c in children)
            obj[c.Key] = ToNode(c);
        return obj;
    }
}
