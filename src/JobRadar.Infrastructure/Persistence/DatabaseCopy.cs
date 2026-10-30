using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>Rows copied from one table.</summary>
public sealed record TableCopy(string Table, int Rows);

/// <summary>The target database already has rows: copying would mix two histories.</summary>
public sealed class TargetNotEmptyException(string table)
    : Exception($"The target database is not empty ({table} has rows). Copy only into a database just migrated.");

/// <summary>
/// Copies every row of the system's database into another one, of either provider, with the same schema and no
/// rows. Table by table, parents first, a page at a time. Below the repositories on purpose: they answer business
/// questions, and a copy has to see every row as stored. Generated ids (run and event logs, referenced by no other
/// table) are generated again, in the source's order.
/// </summary>
public static class DatabaseCopy
{
    public const int PageSize = 1000;

    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    public static async Task<IReadOnlyList<TableCopy>> CopyAsync(
        JobRadarDbContext from, JobRadarDbContext to, CancellationToken ct = default)
    {
        foreach (var (table, any) in Tables(to))
            if (await any(ct))
                throw new TargetNotEmptyException(table);

        // A page of 1,000 rows is one command; the 30 s default is for the site's quick reads, and a fresh SQL Server
        // took longer than that on the real listings (2026-10-30).
        from.Database.SetCommandTimeout(CommandTimeout);
        to.Database.SetCommandTimeout(CommandTimeout);
        var copier = new Copier(from, to, ct);
        return
        [
            await copier.CopyAsync("listings", db => db.Listings.OrderBy(e => e.Source).ThenBy(e => e.SourceId)),
            await copier.CopyAsync("listing_payloads", db => db.ListingPayloads
                .OrderBy(e => e.Source).ThenBy(e => e.SourceId).ThenBy(e => e.PayloadHash)),
            await copier.CopyAsync("listing_search_hits", db => db.ListingSearchHits
                .OrderBy(e => e.Source).ThenBy(e => e.SourceId).ThenBy(e => e.SearchKey)),
            await copier.CopyAsync("listing_descriptions", db => db.ListingDescriptions.OrderBy(e => e.Source).ThenBy(e => e.SourceId)),
            await copier.CopyAsync("listing_scores", db => db.ListingScores
                .OrderBy(e => e.Source).ThenBy(e => e.SourceId).ThenBy(e => e.Profile)),
            await copier.CopyAsync("listing_terms", db => db.ListingTerms
                .OrderBy(e => e.Source).ThenBy(e => e.SourceId).ThenBy(e => e.Term)),
            await copier.CopyAsync("applications", db => db.Applications.OrderBy(e => e.Source).ThenBy(e => e.SourceId)),
            await copier.CopyAsync("application_events", db => db.ApplicationEvents.OrderBy(e => e.Id), e => e.Id = 0),
            await copier.CopyAsync("listing_labels", db => db.ListingLabels.OrderBy(e => e.Source).ThenBy(e => e.SourceId)),
            await copier.CopyAsync("market_visits", db => db.MarketVisits.OrderBy(e => e.Market)),
            await copier.CopyAsync("collection_runs", db => db.CollectionRuns.OrderBy(e => e.Id), e => e.Id = 0),
            await copier.CopyAsync("candidate_requirements", db => db.CandidateRequirements.OrderBy(e => e.ItemId)),
            await copier.CopyAsync("candidate_experience", db => db.CandidateExperience.OrderBy(e => e.Profile)),
            await copier.CopyAsync("market_snapshots", db => db.MarketSnapshots.OrderBy(e => e.Source).ThenBy(e => e.SnapshotDate)),
            await copier.CopyAsync("market_counts", db => db.MarketCounts.OrderBy(e => e.Source).ThenBy(e => e.SnapshotDate)
                .ThenBy(e => e.Category).ThenBy(e => e.City).ThenBy(e => e.Term)),
            await copier.CopyAsync("market_listings", db => db.MarketListings.OrderBy(e => e.Source).ThenBy(e => e.SourceId)
                .ThenBy(e => e.Category).ThenBy(e => e.City)),
        ];
    }

    private static IEnumerable<(string Table, Func<CancellationToken, Task<bool>> Any)> Tables(JobRadarDbContext db) =>
    [
        ("listings", db.Listings.AnyAsync), ("listing_payloads", db.ListingPayloads.AnyAsync),
        ("listing_search_hits", db.ListingSearchHits.AnyAsync), ("listing_descriptions", db.ListingDescriptions.AnyAsync),
        ("listing_scores", db.ListingScores.AnyAsync), ("listing_terms", db.ListingTerms.AnyAsync),
        ("applications", db.Applications.AnyAsync), ("application_events", db.ApplicationEvents.AnyAsync),
        ("listing_labels", db.ListingLabels.AnyAsync), ("market_visits", db.MarketVisits.AnyAsync),
        ("collection_runs", db.CollectionRuns.AnyAsync),
        ("candidate_requirements", db.CandidateRequirements.AnyAsync), ("candidate_experience", db.CandidateExperience.AnyAsync),
        ("market_snapshots", db.MarketSnapshots.AnyAsync), ("market_counts", db.MarketCounts.AnyAsync),
        ("market_listings", db.MarketListings.AnyAsync),
    ];

    private sealed class Copier(JobRadarDbContext from, JobRadarDbContext to, CancellationToken ct)
    {
        public async Task<TableCopy> CopyAsync<T>(string table, Func<JobRadarDbContext, IOrderedQueryable<T>> ordered, Action<T>? prepare = null)
            where T : class
        {
            var rows = 0;
            while (true)
            {
                var page = await ordered(from).Skip(rows).Take(PageSize).ToListAsync(ct);
                if (page.Count == 0)
                    break;
                if (prepare is not null)
                    page.ForEach(prepare);
                to.Set<T>().AddRange(page);
                await to.SaveChangesAsync(ct);
                to.ChangeTracker.Clear();
                rows += page.Count;
            }
            return new TableCopy(table, rows);
        }
    }
}
