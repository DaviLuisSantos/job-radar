namespace JobRadar.Domain.Collection;

/// <summary>One search against one source, as it happened.</summary>
/// <param name="SearchKey">Identity of the search without its date window (<c>ListingSearch.Key</c>).</param>
/// <param name="PostedWithinDays">Date window actually sent; null means the full history.</param>
/// <param name="Found">Distinct listings the source returned.</param>
/// <param name="New">Of those, listings this system had never seen.</param>
/// <param name="Complete">False when the source stopped at its result cap.</param>
/// <param name="Error">Why the run failed; null when it succeeded.</param>
public sealed record CollectionRun(
    string Source,
    string SearchKey,
    int? PostedWithinDays,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Found,
    int New,
    bool Complete,
    string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>Runs of one local day, for the status page: searches and full-text terms together.</summary>
public sealed record DailyRunSummary(DateOnly Day, int Runs, int Failed, int Incomplete, int Found, int New, DateTimeOffset LastFinished);

/// <summary>Totals of one collection pass over every configured search and source.</summary>
public sealed record CollectionSummary(int Runs, int Failed, int Incomplete, int Found, int New);

/// <summary>
/// The runs of one daily pass, searches and terms together. Runs carry no pass id: a pass ends when no
/// run starts within <see cref="Gap"/> of the end of every run before it.
/// </summary>
public sealed record CollectionPass(IReadOnlyList<CollectionRun> Runs)
{
    /// <summary>
    /// Inside a pass a run starts as soon as a slot frees, and the terms right after the last search ends.
    /// Measured from the <b>end</b> of the runs before: the start of the last one is no guide, since the steps
    /// that record no run (descriptions, scores, snapshot) follow it for minutes. A pass recovered after a
    /// restart can be under an hour from the scheduled one (seen 2026-10-23: 18:04 and 19:01).
    /// </summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMinutes(15);

    public DateTimeOffset StartedAt => Runs.Min(r => r.StartedAt);

    public DateTimeOffset FinishedAt => Runs.Max(r => r.FinishedAt);

    /// <summary>Failed runs first, then the ones cut at the source cap; by search within each.</summary>
    public IReadOnlyList<CollectionRun> Problems => Runs
        .Where(r => !r.Succeeded || !r.Complete)
        .OrderBy(r => r.Succeeded)
        .ThenBy(r => r.SearchKey, StringComparer.Ordinal)
        .ThenBy(r => r.Source, StringComparer.Ordinal)
        .ToList();

    /// <summary>The most recent pass among <paramref name="runs"/>; null when there are none.</summary>
    public static CollectionPass? Last(IEnumerable<CollectionRun> runs)
    {
        var ordered = runs.OrderBy(r => r.StartedAt).ToList();
        if (ordered.Count == 0)
            return null;
        var first = 0;
        var finished = ordered[0].FinishedAt;
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].StartedAt - finished > Gap)
                first = i;
            if (ordered[i].FinishedAt > finished)
                finished = ordered[i].FinishedAt;
        }
        return new CollectionPass(ordered.Skip(first).ToList());
    }
}
