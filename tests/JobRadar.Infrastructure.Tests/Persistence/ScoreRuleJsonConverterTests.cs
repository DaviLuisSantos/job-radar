using System.Text.Json;
using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Persistence.Matching;

namespace JobRadar.Infrastructure.Tests.Persistence;

public class ScoreRuleJsonConverterTests
{
    [Fact]
    public void Rules_are_stored_by_name_and_read_back()
    {
        var items = new[] { new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 16) };

        var json = JsonSerializer.Serialize(items, EfListingScoreRepository.Json);

        Assert.Contains("\"rule\":\"PositiveSignal\"", json);
        Assert.Contains("\"where\":\"Title\"", json);
        Assert.Equal(items, JsonSerializer.Deserialize<ScoreItem[]>(json, EfListingScoreRepository.Json));
    }

    [Theory]
    [InlineData("\"ARuleRenamedSince\"")]
    [InlineData("\"5\"")]
    [InlineData("\"PositiveSignal, Remote\"")]
    [InlineData("\"positivesignal\"")]
    public void A_rule_this_build_does_not_know_reads_as_unknown_with_its_points(string rule)
    {
        var json = $$"""[{"rule":{{rule}},"detail":"some phrase","where":"FullText","points":-55}]""";

        var item = Assert.Single(JsonSerializer.Deserialize<ScoreItem[]>(json, EfListingScoreRepository.Json)!);

        Assert.Equal(new ScoreItem(ScoreRule.Unknown, "some phrase", ScoreWhere.FullText, -55), item);
    }
}
