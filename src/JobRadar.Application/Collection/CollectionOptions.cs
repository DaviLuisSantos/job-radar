using System.ComponentModel.DataAnnotations;

namespace JobRadar.Application.Collection;

/// <summary>
/// When to collect. Section: <c>Collection</c>. What to collect comes from the profiles
/// (<c>Matching:Profiles</c>), each searching its keywords in its locations.
/// </summary>
public sealed class CollectionOptions
{
    public const string Section = "Collection";

    /// <summary>Local time of the daily run, in <see cref="TimeZone"/>.</summary>
    public TimeOnly DailyAt { get; set; } = new(6, 0);

    /// <summary>IANA id. Perth by default: the market is read in the morning there.</summary>
    [Required]
    public string TimeZone { get; set; } = "Australia/Perth";

    /// <summary>
    /// Searches (and full-text terms) in flight at once. The source paces its own requests, so this
    /// only decides how many are queued for its budget.
    /// </summary>
    [Range(1, 32)]
    public int MaxParallelSearches { get; set; } = 6;

    /// <summary>Run once as soon as the worker starts, then follow the schedule. For development.</summary>
    public bool RunOnStartup { get; set; }
}
