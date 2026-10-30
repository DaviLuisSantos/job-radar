using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Matching;

/// <summary>A market's eligibility block as the local configuration writes it, ready to paste into <c>appsettings.Local.json</c>.</summary>
public static class EligibilityJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Section(JobMarket market, EligibilityRules rules) => JsonSerializer.Serialize(
        new { Matching = new { Markets = new Dictionary<string, object> { [market.ToString()] = new { Eligibility = rules.Normalized() } } } },
        Options);
}
