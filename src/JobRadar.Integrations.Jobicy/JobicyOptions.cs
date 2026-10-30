using System.ComponentModel.DataAnnotations;

namespace JobRadar.Integrations.Jobicy;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>Jobicy</c>. Public API, no key; the terms ask to credit Jobicy and keep its links (docs/fontes.md).
/// </summary>
public sealed class JobicyOptions
{
    public const string Section = "Jobicy";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://jobicy.com/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>The region of the feed: "latam" also brings the jobs open to anywhere.</summary>
    [Required]
    public string Geo { get; set; } = "latam";

    /// <summary>The industry of the feed; keywords are matched here, since the API's own tag reads titles only.</summary>
    [Required]
    public string Industry { get; set; } = "engineering";

    /// <summary>100 is the most a page takes.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 100;

    /// <summary>The feed covers the last 7 days: a few hundred jobs.</summary>
    [Range(1, 20)]
    public int MaxPages { get; set; } = 5;

    [Range(1, 4)]
    public int MaxConcurrentRequests { get; set; } = 1;

    [Range(0.1, 2)]
    public double RequestsPerSecond { get; set; } = 0.5;
}
