using System.Text.Json;

namespace JobRadar.Application.Matching;

/// <summary>An education entry of the candidate facts: its level and whether Australia recognises it.</summary>
public sealed record CandidateEducation(string Level, bool RecognisedInAustralia);

/// <summary>
/// What <c>candidate/candidate.json</c> (format in <c>candidate/candidate.schema.json</c>) says about the catalog
/// requirements. Only these fields are read: the rest of the file is for the candidate and for profile drafting.
/// </summary>
public sealed record CandidateFacts(
    IReadOnlyList<string> TicketsHeld,
    IReadOnlyList<string> TicketsObtainable,
    IReadOnlyList<string> TicketsOutOfReach,
    IReadOnlyList<string> HeavyLicences,
    IReadOnlyList<CandidateEducation> Education)
{
    public const int SchemaVersion = 1;

    /// <summary>Reads the file's text; a wrong schema version or a broken file is a <see cref="FormatException"/>.</summary>
    public static CandidateFacts Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != SchemaVersion)
                throw new FormatException($"Candidate facts must have \"schemaVersion\": {SchemaVersion}.");

            var tickets = Property(root, "tickets");
            return new CandidateFacts(
                Strings(tickets, "held"),
                Strings(tickets, "obtainable"),
                tickets is { } t && t.TryGetProperty("outOfReach", out var outOfReach)
                    ? outOfReach.EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToList()
                    : [],
                Strings(Property(root, "licences"), "heavy"),
                root.TryGetProperty("education", out var education)
                    ? education.EnumerateArray()
                        .Select(e => new CandidateEducation(e.GetProperty("level").GetString()!, e.GetProperty("recognisedInAustralia").GetBoolean()))
                        .ToList()
                    : []);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new FormatException($"Not a candidate facts file: {ex.Message}", ex);
        }
    }

    private static JsonElement? Property(JsonElement e, string name) => e.TryGetProperty(name, out var p) ? p : null;

    private static List<string> Strings(JsonElement? e, string name) =>
        e is { } o && o.TryGetProperty(name, out var array) ? array.EnumerateArray().Select(x => x.GetString()!).ToList() : [];
}
