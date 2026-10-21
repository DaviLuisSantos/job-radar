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

/// <summary>Totals of one collection pass over every configured search and source.</summary>
public sealed record CollectionSummary(int Runs, int Failed, int Incomplete, int Found, int New);
