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
/// The runs of one daily pass, searches and terms together. Runs carry no pass id: a pass is the runs
/// that started one after another with no pause longer than <see cref="Gap"/>.
/// </summary>
public sealed record CollectionPass(IReadOnlyList<CollectionRun> Runs)
{
    /// <summary>
    /// Inside a pass runs start seconds apart, in parallel; between passes there are hours. The steps that
    /// record no run (descriptions, scores, snapshot) come after the last one, so they open no gap.
    /// </summary>
    public static readonly TimeSpan Gap = TimeSpan.FromHours(1);

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
        var newestFirst = runs.OrderByDescending(r => r.StartedAt).ToList();
        if (newestFirst.Count == 0)
            return null;
        var count = 1;
        while (count < newestFirst.Count && newestFirst[count - 1].StartedAt - newestFirst[count].StartedAt <= Gap)
            count++;
        return new CollectionPass(newestFirst.Take(count).Reverse().ToList());
    }
}
