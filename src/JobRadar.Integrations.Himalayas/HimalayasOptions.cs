using System.ComponentModel.DataAnnotations;

namespace JobRadar.Integrations.Himalayas;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>Himalayas</c>. Public API, no key; the terms ask for the source link and the application link as given,
/// and for showing the jobs only on sites the user runs (docs/fontes.md): a private tool.
/// </summary>
public sealed class HimalayasOptions
{
    public const string Section = "Himalayas";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://himalayas.app/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>The search always answers 20 per page.</summary>
    public const int PageSize = 20;

    /// <summary>".net" from Brazil had 153 listings in October 2026: eight pages.</summary>
    [Range(1, 30)]
    public int MaxPages { get; set; } = 15;

    /// <summary>Requests in flight at once. Keyless callers share one allowance: be slow.</summary>
    [Range(1, 4)]
    public int MaxConcurrentRequests { get; set; } = 1;

    /// <summary>Requests started per second, evenly spread.</summary>
    [Range(0.1, 2)]
    public double RequestsPerSecond { get; set; } = 0.5;
}
