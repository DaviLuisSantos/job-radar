namespace JobRadar.Application.Abstractions;

/// <summary>
/// A source that is read by sweeping whole boards instead of searching by keyword (the ATS job boards of single
/// companies). The sweep is a stage of the daily pass, before the collection: it is minutes long, and a search that
/// waited for it would hold one of the collector's parallel slots. It leaves what it found for the source's searches
/// to filter and records itself as one row in <c>collection_runs</c>.
/// </summary>
public interface IBoardSweep
{
    /// <summary>The source whose listings the sweep finds (<see cref="IJobSource.Name"/>).</summary>
    string Source { get; }

    /// <param name="ignoreETags">Read every board whole, even the ones that say nothing changed.</param>
    Task SweepAsync(bool ignoreETags, CancellationToken ct = default);
}
