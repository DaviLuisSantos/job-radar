using System.ComponentModel.DataAnnotations;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.LinkedIn;

/// <summary>
/// Defaults live here, not in appsettings.json: the JSON only carries operator decisions.
/// Section: <c>LinkedIn</c>. The guest endpoints answer without an account, so no terms were accepted; the pace
/// stays low and nothing is published (docs/fontes.md).
/// </summary>
public sealed class LinkedInOptions
{
    public const string Section = "LinkedIn";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://www.linkedin.com/");

    /// <summary>The bot says who it is instead of pretending to be a browser.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobRadar/0.1 (+https://github.com/DaviLuisSantos/job-radar)";

    /// <summary>
    /// The markets whose searches run here: AU and BR (the remote markets are the remote boards'). A market left out
    /// keeps its searches on its other sources only.
    /// </summary>
    public List<JobMarket> Markets { get; set; } = [JobMarket.AU, JobMarket.BR];

    /// <summary>
    /// Pages of 10 (the guest search gives no choice). The search stops answering around 1,000 results and refuses
    /// an offset of 1,000 or more, so 100 pages is the most that can be asked. 40 covers a day's window with room.
    /// </summary>
    [Range(1, 100)]
    public int MaxPages { get; set; } = 40;

    /// <summary>Requests in flight at once. Its own budget, apart from the other sources', and low.</summary>
    [Range(1, 4)]
    public int MaxConcurrentRequests { get; set; } = 2;

    /// <summary>Requests started per second, evenly spread. Bursts are what gets an address sent to the login wall.</summary>
    [Range(0.1, 5)]
    public double RequestsPerSecond { get; set; } = 1;
}
