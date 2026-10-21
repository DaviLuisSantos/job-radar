using JobRadar.Domain.Collection;

namespace JobRadar.Application.Abstractions;

/// <summary>Log of every search run (today in Postgres, table <c>collection_runs</c>).</summary>
public interface ICollectionRunRepository
{
    Task RecordAsync(CollectionRun run, CancellationToken ct = default);

    /// <summary>When the most recent run of anything started; null if nothing ever ran.</summary>
    Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default);

    /// <summary>Totals per local day of the most recent <paramref name="days"/> days with runs, newest first.</summary>
    Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default);

    /// <summary>
    /// When the last run of this search on this source that succeeded <b>and saw its whole window</b>
    /// started; null if never. A run cut at the source cap does not count: the next one must cover
    /// that period again, or the listings past the cap are never collected.
    /// </summary>
    Task<DateTimeOffset?> GetLastCompleteStartAsync(string source, string searchKey, CancellationToken ct = default);
}
