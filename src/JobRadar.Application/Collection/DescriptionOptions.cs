using System.ComponentModel.DataAnnotations;

namespace JobRadar.Application.Collection;

/// <summary>Fetching the full text of ads in the daily pass. Section: <c>Descriptions</c>.</summary>
public sealed class DescriptionOptions
{
    public const string Section = "Descriptions";

    public bool Enabled { get; set; } = true;

    /// <summary>Only listings some profile scores at least this high: the rest would not reach triage anyway.</summary>
    [Range(1, 100)]
    public int MinScore { get; set; } = 40;

    /// <summary>Ads stay up about 30 days; an older one is gone, there is nothing to fetch.</summary>
    [Range(1, 60)]
    public int MaxAgeDays { get; set; } = 31;

    /// <summary>Requests per pass, at most (one per listing). At 5 per second, 3,000 take about 10 minutes.</summary>
    [Range(1, 20_000)]
    public int MaxPerPass { get; set; } = 3_000;
}
