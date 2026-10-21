using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Listings;

internal sealed class PostgresListingRepository(JobRadarDbContext db) : IListingRepository
{
    public async Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<Listing> listings, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        if (listings.Count == 0)
            return new ObservationResult(0, 0);

        // A source can repeat an ad within one run (promoted ads); the last copy wins.
        var batch = listings.GroupBy(l => l.Id).Select(g => g.Last()).ToList();

        var known = new Dictionary<(string, string), ListingEntity>();
        foreach (var bySource in batch.GroupBy(l => l.Id.Source))
        {
            var source = bySource.Key;
            var ids = bySource.Select(l => l.Id.SourceId).ToList();
            var rows = await db.Listings.AsTracking()
                .Where(e => e.Source == source && ids.Contains(e.SourceId))
                .ToListAsync(ct);
            foreach (var row in rows)
                known[(row.Source, row.SourceId)] = row;
        }

        var added = 0;
        foreach (var listing in batch)
        {
            if (known.TryGetValue((listing.Id.Source, listing.Id.SourceId), out var row))
            {
                ListingMapper.Refresh(row, listing, observedAt);
            }
            else
            {
                db.Listings.Add(ListingMapper.ToNewEntity(listing, observedAt));
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new ObservationResult(added, batch.Count - added);
    }

    public async Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var row = await db.Listings
            .FirstOrDefaultAsync(e => e.Source == source && e.SourceId == sourceId, ct);
        return row is null ? null : ListingMapper.ToRecord(row);
    }
}
