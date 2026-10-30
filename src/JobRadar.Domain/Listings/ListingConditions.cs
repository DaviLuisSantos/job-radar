namespace JobRadar.Domain.Listings;

/// <summary>Where the work is done, as the source states it.</summary>
public enum WorkArrangement
{
    Unknown,
    OnSite,
    Hybrid,
    Remote,
}

/// <summary>
/// The conditions a source gives as fields and the translators put in the summary: the arrangement (Seek's
/// "Hybrid", Gupy's "Remoto"), the contract (Gupy's "CLT", "PJ") and the time zones (Himalayas' "UTC-5 to UTC-2"). For showing, not scoring: the scorer reads
/// arrangements in the text by its own rules.
/// </summary>
/// <param name="Contract">The contract as the ad names it ("CLT", "PJ"); null when the source does not say.</param>
public sealed record ListingConditions(WorkArrangement Arrangement, string? Contract)
{
    public static readonly ListingConditions None = new(WorkArrangement.Unknown, null);

    /// <summary>The time zones the employer accepts, when a remote board says (<see cref="TimeZoneWindow"/>).</summary>
    public TimeZoneWindow? TimeZones { get; init; }

    private static readonly Dictionary<string, WorkArrangement> Arrangements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Remote"] = WorkArrangement.Remote,
        ["Remoto"] = WorkArrangement.Remote,
        ["Hybrid"] = WorkArrangement.Hybrid,
        ["Híbrido"] = WorkArrangement.Hybrid,
        ["On-site"] = WorkArrangement.OnSite,
        ["Presencial"] = WorkArrangement.OnSite,
    };

    private static readonly HashSet<string> Contracts = new(StringComparer.OrdinalIgnoreCase) { "CLT", "PJ", "Associado" };

    /// <summary>
    /// Reads whole parts of the summary only (" | " for Seek, " · " for Gupy): a teaser that says "remote" in passing
    /// is not the source's field.
    /// </summary>
    public static ListingConditions Read(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return None;
        var parts = summary.Split(['|', '·'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var arrangement = parts.Select(p => Arrangements.GetValueOrDefault(p)).FirstOrDefault(a => a != WorkArrangement.Unknown);
        var contract = parts.FirstOrDefault(Contracts.Contains);
        var zones = parts.Select(TimeZoneWindow.Parse).FirstOrDefault(z => z is not null);
        return arrangement == WorkArrangement.Unknown && contract is null && zones is null
            ? None
            : new(arrangement, contract) { TimeZones = zones };
    }
}
