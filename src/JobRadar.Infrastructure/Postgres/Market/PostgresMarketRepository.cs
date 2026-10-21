using JobRadar.Application.Abstractions;
using JobRadar.Domain.Market;
using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Postgres.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Market;

internal sealed class PostgresMarketRepository(JobRadarDbContext db) : IMarketRepository
{
    private const string NoTerm = "";

    public async Task SaveSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts,
        IReadOnlyCollection<MarketListing> listings, CancellationToken ct = default)
    {
        var (source, day) = (snapshot.Source, snapshot.Date);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Taking the snapshot again the same day replaces it (counts go with it, by cascade).
            await db.MarketSnapshots.Where(s => s.Source == source && s.SnapshotDate == day).ExecuteDeleteAsync(ct);

            db.MarketSnapshots.Add(new MarketSnapshotEntity
            {
                Source = source,
                SnapshotDate = day,
                Status = ToColumn(snapshot.Status),
                Expected = snapshot.Expected,
                Saved = snapshot.Saved,
                Listings = snapshot.Listings,
                Issues = snapshot.Issues.Count == 0 ? null : string.Join('\n', snapshot.Issues),
                TakenAt = DateTimeOffset.UtcNow,
            });
            db.MarketCounts.AddRange(counts.Where(c => c.Total is not null).Select(c => new MarketCountEntity
            {
                Source = source, SnapshotDate = day, Category = c.Category, City = c.City, Term = c.Term ?? NoTerm, Total = c.Total!.Value,
            }));
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            await UpsertListingsAsync(source, day, listings, ct);
            await tx.CommitAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>New listing: first and last seen today. Known: last seen moves to today, details refresh.</summary>
    private async Task UpsertListingsAsync(string source, DateOnly day, IReadOnlyCollection<MarketListing> listings, CancellationToken ct)
    {
        var batch = listings.DistinctBy(l => (l.Listing.Id.SourceId, l.Category, l.City)).ToList();
        foreach (var chunk in batch.Chunk(1000))
        {
            var ids = chunk.Select(l => l.Listing.Id.SourceId).Distinct().ToList();
            var known = await db.MarketListings.AsTracking()
                .Where(m => m.Source == source && ids.Contains(m.SourceId))
                .ToDictionaryAsync(m => (m.SourceId, m.Category, m.City), ct);

            foreach (var l in chunk)
            {
                var key = (l.Listing.Id.SourceId, l.Category, l.City);
                if (!known.TryGetValue(key, out var row))
                {
                    row = new MarketListingEntity { Source = source, SourceId = key.SourceId, Category = l.Category, City = l.City, FirstSeen = day };
                    db.MarketListings.Add(row);
                }
                var salary = SalaryParser.Parse(l.Listing.SalaryText);
                row.Title = l.Listing.Title;
                row.Company = l.Listing.Company;
                row.Location = ScoringInput.FormatLocation(l.Listing.Location);
                row.WorkType = ListingMapper.ToColumn(l.Listing.WorkType);
                row.SalaryText = l.Listing.SalaryText;
                row.SalaryPeriod = salary?.Period.ToString().ToLowerInvariant();
                row.SalaryAnnualMin = salary?.AnnualMin;
                row.SalaryAnnualMax = salary?.AnnualMax;
                row.PostedAt = l.Listing.PostedAt?.ToUniversalTime();
                if (day > row.LastSeen)
                    row.LastSeen = day;
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyDictionary<(string Category, string City), int>> GetLastGoodTotalsAsync(
        string source, DateOnly before, CancellationToken ct = default)
    {
        var ok = ToColumn(SnapshotStatus.Ok);
        var last = await db.MarketSnapshots
            .Where(s => s.Source == source && s.Status == ok && s.SnapshotDate < before)
            .MaxAsync(s => (DateOnly?)s.SnapshotDate, ct);
        if (last is not { } day)
            return new Dictionary<(string, string), int>();

        return await db.MarketCounts
            .Where(c => c.Source == source && c.SnapshotDate == day && c.Term == NoTerm)
            .ToDictionaryAsync(c => (c.Category, c.City), c => c.Total, ct);
    }

    public async Task<IReadOnlyList<DailyCount>> GetGoodCountsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var ok = ToColumn(SnapshotStatus.Ok);
        var rows = await (
                from c in db.MarketCounts
                join s in db.MarketSnapshots on new { c.Source, c.SnapshotDate } equals new { s.Source, s.SnapshotDate }
                where c.Source == source && s.Status == ok && c.SnapshotDate >= @from && c.SnapshotDate <= to
                orderby c.SnapshotDate
                select c)
            .ToListAsync(ct);
        return rows.Select(c => new DailyCount(c.SnapshotDate, c.Category, c.City, c.Term == NoTerm ? null : c.Term, c.Total)).ToList();
    }

    public async Task<IReadOnlyList<SeenListing>> GetListingsSeenAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await db.MarketListings
            .Where(m => m.Source == source && m.FirstSeen <= to && m.LastSeen >= from)
            .ToListAsync(ct);
        return rows.Select(m => new SeenListing(m.Category, m.City, m.Title, m.Company, m.SalaryAnnualMin, m.SalaryAnnualMax,
            m.PostedAt, m.FirstSeen, m.LastSeen)).ToList();
    }

    public async Task<IReadOnlyList<MarketSnapshot>> GetSnapshotsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await db.MarketSnapshots
            .Where(s => s.Source == source && s.SnapshotDate >= from && s.SnapshotDate <= to)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(ct);
        return rows.Select(s => new MarketSnapshot(s.Source, s.SnapshotDate, ToStatus(s.Status), s.Expected, s.Saved, s.Listings,
            s.Issues?.Split('\n') ?? [])).ToList();
    }

    internal static string ToColumn(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Ok => "ok",
        SnapshotStatus.Partial => "partial",
        SnapshotStatus.Suspect => "suspect",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    internal static SnapshotStatus ToStatus(string column) => column switch
    {
        "ok" => SnapshotStatus.Ok,
        "partial" => SnapshotStatus.Partial,
        "suspect" => SnapshotStatus.Suspect,
        _ => throw new InvalidOperationException($"Unknown snapshot status '{column}' in the database."),
    };
}
