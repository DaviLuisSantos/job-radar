using System.ComponentModel.DataAnnotations;

namespace JobRadar.Integrations.Ats;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>Ats</c>. The boards are the public job-board API each company's careers page calls: no key, no account,
/// no terms accepted (docs/fontes.md). A private tool, like the other sources.
/// </summary>
public sealed class AtsOptions
{
    public const string Section = "Ats";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://boards-api.greenhouse.io/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>Requests in flight at once. Twenty at eight in flight took 1.1 s, with no 429 (2026-10-30).</summary>
    [Range(1, 16)]
    public int MaxConcurrentRequests { get; set; } = 8;

    /// <summary>Requests started per second, evenly spread. Not raised on the strength of a short test: a burst is what gets blocked.</summary>
    [Range(0.5, 20)]
    public double RequestsPerSecond { get; set; } = 10;

    /// <summary>
    /// Boards asked per pass: the ones swept longest ago. 8,300 boards at ten a second are fourteen minutes before any
    /// retry, past the thirty the pipeline is meant to stay under, so the list is cut into slices.
    /// </summary>
    [Range(1, 20000)]
    public int MaxBoardsPerSweep { get; set; } = 1500;

    /// <summary>A board found gone (404) this many sweeps in a row is not asked again.</summary>
    [Range(1, 10)]
    public int MaxFailures { get; set; } = 3;

    /// <summary>
    /// A board that never had a listing in our markets is asked again only after this many days: with about one listing
    /// in a hundred in Australia, most boards are never of use.
    /// </summary>
    [Range(1, 365)]
    public int IdleRecheckDays { get; set; } = 30;
}
