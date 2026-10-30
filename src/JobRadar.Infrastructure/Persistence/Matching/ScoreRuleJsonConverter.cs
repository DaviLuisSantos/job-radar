using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Matching;

namespace JobRadar.Infrastructure.Persistence.Matching;

/// <summary>
/// A <see cref="ScoreRule"/> by name, as the explanation stores it. A name this build does not know (a rule renamed
/// since the score was written) reads as <see cref="ScoreRule.Unknown"/> with its points and detail intact, instead of
/// failing the whole page until the next pass rewrites the score.
/// </summary>
internal sealed class ScoreRuleJsonConverter : JsonConverter<ScoreRule>
{
    private static readonly Dictionary<string, ScoreRule> ByName =
        Enum.GetValues<ScoreRule>().ToDictionary(r => r.ToString(), StringComparer.Ordinal);

    public override ScoreRule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && ByName.TryGetValue(reader.GetString()!, out var rule) ? rule : ScoreRule.Unknown;

    public override void Write(Utf8JsonWriter writer, ScoreRule value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
