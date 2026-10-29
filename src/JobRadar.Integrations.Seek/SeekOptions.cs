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
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>
    /// 100 is the API maximum. Market counts must use it: totalCount includes promoted ads
    /// repeated on every page, so smaller pages inflate it.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 100;

    /// <summary>The API returns nothing past 500 results (5 pages of 100).</summary>
    [Range(1, 5)]
    public int MaxPages { get; set; } = 5;

    /// <summary>
    /// Requests in flight at once, across every stage (collection, terms, market). Searches run in
    /// parallel; this is what keeps the total polite.
    /// </summary>
    [Range(1, 16)]
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>Requests started per second, across every stage, evenly spread (no bursts).</summary>
    [Range(0.1, 20)]
    public double RequestsPerSecond { get; set; } = 5;
}
