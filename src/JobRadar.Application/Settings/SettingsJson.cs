using System.Text.Json;
using System.Text.Json.Nodes;

namespace JobRadar.Application.Settings;

/// <summary>Pure helpers between a stored section (key + JSON) and the nested configuration shape.</summary>
public static class SettingsJson
{
    /// <summary>
    /// <c>Matching:Profiles</c> + <c>{...}</c> becomes <c>{"Matching":{"Profiles":{...}}}</c>.
    /// Throws <see cref="JsonException"/> when <paramref name="json"/> is not JSON.
    /// </summary>
    public static string Nest(string key, string json)
    {
        var node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = false })
            ?? JsonValue.Create((string?)null);
        var segments = key.Split(':');
        for (var i = segments.Length - 1; i >= 0; i--)
            node = new JsonObject { [segments[i]] = node };
        return node!.ToJsonString();
    }

    /// <summary>The configuration keys (<c>A:B:0</c>) and string values a JSON document stands for.</summary>
    public static Dictionary<string, string?> Flatten(string json)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, "", result);
        return result;
    }

    private static void Walk(JsonElement element, string path, Dictionary<string, string?> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in element.EnumerateObject())
                    Walk(p.Value, path.Length == 0 ? p.Name : $"{path}:{p.Name}", into);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                    Walk(item, $"{path}:{i++}", into);
                break;
            case JsonValueKind.Null:
                into[path] = null;
                break;
            case JsonValueKind.True:
                into[path] = "True";
                break;
            case JsonValueKind.False:
                into[path] = "False";
                break;
            default:
                into[path] = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
                break;
        }
    }
}
