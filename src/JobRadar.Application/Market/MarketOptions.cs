using Microsoft.Extensions.Options;

namespace JobRadar.Application.Market;

/// <summary>
/// What the Raio-X measures. Section: <c>Market</c>. Each list, when present in configuration,
/// replaces the default (the prototype's choice) instead of being merged into it. Terms have no default: the
/// phrases a snapshot counts are the publisher's choice, kept in the local configuration.
/// </summary>
public sealed class MarketOptions
{
    public const string Section = "Market";

    /// <summary>Areas measured; each maps to the category code of every source that has one.</summary>
    public List<MarketCategoryOptions> Categories { get; set; } = [];

    /// <summary>Cities with counts and every listing (salaries, reposts).</summary>
    public List<string> FullCities { get; set; } = [];

    /// <summary>Cities with counts only: one request per term, cheap.</summary>
    public List<string> CountOnlyCities { get; set; } = [];

    /// <summary>
    /// Phrases counted in the full ad. A mention, not an offer: the phrase negated counts too. <c>Name</c> is the key
    /// stored with each count, so renaming one splits its history; none configured: totals only.
    /// </summary>
    public List<MarketTermOptions> Terms { get; set; } = [];

    /// <summary>Wait before retrying the counts that failed (a passing rate limit, usually).</summary>
    public TimeSpan RetryPause { get; set; } = TimeSpan.FromSeconds(15);

    public IEnumerable<string> AllCities => FullCities.Concat(CountOnlyCities.Except(FullCities));

    internal static readonly MarketCategoryOptions[] DefaultCategories =
    [
        new() { Name = "HVAC-R", Codes = new() { ["seek"] = "1313" } },          // Air Conditioning & Refrigeration
        new() { Name = "Elétrica", Codes = new() { ["seek"] = "6230" } },        // Electricians
        new() { Name = "Desenvolvimento", Codes = new() { ["seek"] = "6287,6290" } }, // Developers/Programmers + Engineering - Software
    ];
}

public sealed class MarketCategoryOptions
{
    public string Name { get; set; } = "";

    /// <summary>Source name → that source's category code (Seek: subclassification ids, comma-separated).</summary>
    public Dictionary<string, string> Codes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MarketTermOptions
{
    public string Name { get; set; } = "";
    public string Phrase { get; set; } = "";
}

/// <summary>Fills each list the configuration left empty with the default (terms have none).</summary>
internal sealed class MarketDefaults : IPostConfigureOptions<MarketOptions>
{
    public void PostConfigure(string? name, MarketOptions o)
    {
        if (o.Categories.Count == 0)
            o.Categories = [.. MarketOptions.DefaultCategories];
        if (o.FullCities.Count == 0)
            o.FullCities = ["Perth WA"];
        if (o.CountOnlyCities.Count == 0)
            o.CountOnlyCities = ["All Australia"];
    }
}
