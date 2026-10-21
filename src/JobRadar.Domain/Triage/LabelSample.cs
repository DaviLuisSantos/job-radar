namespace JobRadar.Domain.Triage;

/// <summary>
/// An order for labeling without selection bias. Best-first only ever shows the top, so labels would
/// only teach "good" against "almost good"; this deals the rows out across score bands instead, so a
/// page holds a few listings of every band.
/// </summary>
public static class LabelSample
{
    /// <summary>Lower bound of each band, highest first; 0 is the excluded band.</summary>
    private static readonly int[] BandFloors = [80, 65, 50, 35, 1, 0];

    /// <summary>
    /// Round-robin over the bands, best band first. Inside a band the order is a stable shuffle of the
    /// ids: not the top of each band, and the same on every reload, so paging stays consistent.
    /// </summary>
    public static IReadOnlyList<TriageRow> Spread(IReadOnlyList<TriageRow> rows)
    {
        var bands = rows
            .GroupBy(r => Band(r.Score))
            .OrderBy(g => g.Key)
            .Select(g => new Queue<TriageRow>(g.OrderBy(r => Shuffle(r.Id.ToString())).ThenBy(r => r.Id.ToString(), StringComparer.Ordinal)))
            .ToList();

        var spread = new List<TriageRow>(rows.Count);
        while (spread.Count < rows.Count)
        {
            foreach (var band in bands)
            {
                if (band.TryDequeue(out var row))
                    spread.Add(row);
            }
        }
        return spread;
    }

    private static int Band(int score) => Array.FindIndex(BandFloors, floor => score >= floor);

    /// <summary>FNV-1a: <see cref="string.GetHashCode()"/> changes with every process.</summary>
    private static uint Shuffle(string id)
    {
        var hash = 2166136261u;
        foreach (var c in id)
            hash = (hash ^ c) * 16777619u;
        return hash;
    }
}
