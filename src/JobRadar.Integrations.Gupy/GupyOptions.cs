using System.ComponentModel.DataAnnotations;

namespace JobRadar.Integrations.Gupy;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>Gupy</c>. The terms forbid aggregating the platform: personal use, a low pace, nothing published
/// (docs/fontes.md).
/// </summary>
public sealed class GupyOptions
{
    public const string Section = "Gupy";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://portal.gupy.io/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>100 is the most the search takes; more answers 400.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 100;

    /// <summary>A whole-country search for "desenvolvedor" had 422 listings in October 2026.</summary>
    [Range(1, 20)]
    public int MaxPages { get; set; } = 10;

    /// <summary>Requests in flight at once. Its own budget, apart from Seek's, and lower.</summary>
    [Range(1, 8)]
    public int MaxConcurrentRequests { get; set; } = 2;

    /// <summary>Requests started per second, evenly spread.</summary>
    [Range(0.1, 5)]
    public double RequestsPerSecond { get; set; } = 1;
}
