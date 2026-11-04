using System.Text.Json;
using System.Text.Json.Nodes;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using Microsoft.Extensions.Configuration;

namespace JobRadar.Worker.Cli;

/// <summary>Moves configuration between a JSON file and the settings table: one row per second-level section.</summary>
public static class ConfigTransfer
{
    /// <summary>
    /// <c>{"Matching":{"Profiles":{...},"Markets":{...}}}</c> becomes the rows <c>Matching:Profiles</c> and <c>Matching:Markets</c>.
    /// Sections outside the allowed prefixes, and top-level values that are not objects, are left out.
    /// </summary>
    public static (List<(string Key, string Json)> Rows, List<string> Skipped) Split(string json)
    {
        var rows = new List<(string, string)>();
        var skipped = new List<string>();
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
            as JsonObject ?? throw new FormatException("The file must hold a JSON object.");
        foreach (var (name, node) in root)
        {
            if (!SettingsService.AllowedPrefixes.Contains(name, StringComparer.OrdinalIgnoreCase) || node is not JsonObject section)
            {
                skipped.Add(name);
                continue;
            }
            foreach (var (child, value) in section)
                rows.Add(($"{name}:{child}", value?.ToJsonString() ?? "null"));
        }
        // Markets (eligibility) first: saving a profile checks that its market has rules.
        return (rows.OrderBy(r => r.Item1.EndsWith(":Markets", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList(), skipped);
    }

    /// <summary>
    /// Each row is validated by a service whose configuration already includes the rows saved before it, because the
    /// running configuration does not change until the next restart (profiles need the markets imported with them).
    /// </summary>
    public static async Task<int> ImportAsync(string path, ISettingsRepository repository, IConfiguration configuration,
        TimeProvider time, TextWriter output, CancellationToken ct)
    {
        var (rows, skipped) = Split(await File.ReadAllTextAsync(path, ct));
        var imported = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, json) in rows)
        {
            var layered = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(imported).Build();
            await new SettingsService(repository, layered, time).SaveAsync(key, json, ct);
            foreach (var (k, v) in SettingsJson.Flatten(SettingsJson.Nest(key, json)))
                imported[k] = v;
            await output.WriteLineAsync($"  {key}");
        }
        if (skipped.Count > 0)
            await output.WriteLineAsync("Left out (not an allowed object section): " + string.Join(", ", skipped));
        await output.WriteLineAsync($"{rows.Count} sections saved. Empty the host's appsettings.Local.json (to {{}}) so old list items do not linger.");
        return 0;
    }

    /// <summary>The rows merged back into one nested document.</summary>
    public static string Export(IReadOnlyList<Setting> rows)
    {
        var root = new JsonObject();
        foreach (var row in rows.OrderBy(r => r.Key, StringComparer.Ordinal))
            Merge(root, (JsonObject)JsonNode.Parse(SettingsJson.Nest(row.Key, row.Json))!);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void Merge(JsonObject into, JsonObject from)
    {
        foreach (var (name, value) in from.ToList())
        {
            from.Remove(name);
            if (value is JsonObject obj && into[name] is JsonObject existing)
                Merge(existing, obj);
            else
                into[name] = value;
        }
    }
}
