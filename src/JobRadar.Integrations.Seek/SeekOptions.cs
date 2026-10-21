using System.ComponentModel.DataAnnotations;

namespace JobRadar.Integrations.Seek;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>Seek</c>.
/// </summary>
public sealed class SeekOptions
{
    public const string Section = "Seek";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://www.seek.com.au/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadarAU/0.1 (+https://github.com/DaviLuisSantos/job-radar-au)";

    /// <summary>
    /// 100 is the API maximum. Market counts must use it: totalCount includes promoted ads
    /// repeated on every page, so smaller pages inflate it.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 100;

    /// <summary>The API returns nothing past 500 results (5 pages of 100).</summary>
    [Range(1, 5)]
    public int MaxPages { get; set; } = 5;

    /// <summary>Pause between pages of one search, with ±30% jitter, to stay polite.</summary>
    public TimeSpan DelayBetweenPages { get; set; } = TimeSpan.FromSeconds(1);
}
