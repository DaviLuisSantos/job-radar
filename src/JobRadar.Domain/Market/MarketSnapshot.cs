using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Market;

/// <summary>
/// One count of the day: open listings of a category in a city, or (with <see cref="Term"/>) how
/// many of them mention a term anywhere in the ad.
/// </summary>
/// <param name="Term">Null for the total of open listings.</param>
/// <param name="Total">Null when the count failed.</param>
public sealed record MarketCount(string Category, string City, string? Term, int? Total);

/// <summary>A listing seen in a category and city on a snapshot day; aggregated, never published.</summary>
public sealed record MarketListing(string Category, string City, Listing Listing);

public enum SnapshotStatus
{
    /// <summary>Complete and plausible: used by the monthly report.</summary>
    Ok,
    /// <summary>Something failed or was cut: left out of the report.</summary>
    Partial,
    /// <summary>A number that looks like a collection error (zeroed, swung too much): left out.</summary>
    Suspect,
}

public sealed record SnapshotAssessment(SnapshotStatus Status, IReadOnlyList<string> Issues);

/// <summary>A day's market snapshot as it was recorded.</summary>
public sealed record MarketSnapshot(
    string Source,
    DateOnly Date,
    SnapshotStatus Status,
    int Expected,
    int Saved,
    int Listings,
    IReadOnlyList<string> Issues);

/// <summary>Decides whether a day's numbers can go into the monthly report. Ported from the prototype.</summary>
public static class SnapshotQuality
{
    /// <summary>A daily change bigger than this (on a base of at least <see cref="MinBase"/>) is a collection error, not the market.</summary>
    public const double MaxDailySwing = 0.5;

    public const int MinBase = 20;

    /// <param name="previousTotals">Totals of the last <see cref="SnapshotStatus.Ok"/> snapshot, by (category, city).</param>
    /// <param name="incomplete">(category, city) whose listing did not come in full.</param>
    public static SnapshotAssessment Assess(
        IEnumerable<MarketCount> counts,
        IReadOnlyDictionary<(string Category, string City), int> previousTotals,
        IEnumerable<(string Category, string City)> incomplete)
    {
        var suspect = new List<string>();
        var partial = new List<string>();

        foreach (var c in counts)
        {
            var label = c.Term is null ? $"{c.Category} / {c.City}" : $"{c.Category} / {c.City} / {c.Term}";
            if (c.Total is not { } total)
            {
                partial.Add($"contagem falhou: {label}");
                continue;
            }
            if (c.Term is not null || !previousTotals.TryGetValue((c.Category, c.City), out var previous))
                continue;
            if (total == 0 && previous > 0)
                suspect.Add($"zerou: {label} (antes {previous})");
            else if (previous >= MinBase && Math.Abs(total - previous) / (double)previous > MaxDailySwing)
                suspect.Add($"variação brusca: {label} {previous} → {total}");
        }

        partial.AddRange(incomplete.Select(i => $"listagem incompleta: {i.Category} / {i.City}"));

        if (suspect.Count > 0)
            return new SnapshotAssessment(SnapshotStatus.Suspect, [.. suspect, .. partial]);
        return partial.Count > 0
            ? new SnapshotAssessment(SnapshotStatus.Partial, partial)
            : new SnapshotAssessment(SnapshotStatus.Ok, []);
    }
}
