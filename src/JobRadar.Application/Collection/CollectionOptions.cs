using System.ComponentModel.DataAnnotations;

namespace JobRadar.Application.Collection;

/// <summary>
/// What to collect and when. Section: <c>Collection</c>. The searches are an operator decision
/// (they live in configuration); the schedule defaults live here.
/// </summary>
public sealed class CollectionOptions
{
    public const string Section = "Collection";

    public List<SearchOptions> Searches { get; set; } = [];

    /// <summary>Local time of the daily run, in <see cref="TimeZone"/>.</summary>
    public TimeOnly DailyAt { get; set; } = new(6, 0);

    /// <summary>IANA id. Perth by default: the market is read in the morning there.</summary>
    [Required]
    public string TimeZone { get; set; } = "Australia/Perth";

    /// <summary>Run once as soon as the worker starts, then follow the schedule. For development.</summary>
    public bool RunOnStartup { get; set; }
}

public sealed class SearchOptions
{
    public string Keywords { get; set; } = "";

    [Required]
    public string Location { get; set; } = "";
}
