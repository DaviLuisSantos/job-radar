using JobRadar.Domain.Collection;

namespace JobRadar.Application.Abstractions;

/// <summary>Log of every search run (today in Postgres, table <c>collection_runs</c>).</summary>
public interface ICollectionRunRepository
{
    Task RecordAsync(CollectionRun run, CancellationToken ct = default);

    /// <summary>When the last successful run of this search on this source started; null if never.</summary>
    Task<DateTimeOffset?> GetLastSuccessfulStartAsync(string source, string searchKey, CancellationToken ct = default);
}
