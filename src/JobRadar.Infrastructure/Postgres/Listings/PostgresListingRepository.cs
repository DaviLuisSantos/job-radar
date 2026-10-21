using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Listings;

internal sealed class PostgresListingRepository(JobRadarDbContext db) : IListingRepository
{
    public async Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        if (listings.Count == 0)
            return new ObservationResult(0, 0);

        // A source can repeat an ad within one batch; the last copy wins.
        var batch = listings.GroupBy(l => l.Listing.Id).Select(g => g.Last()).ToList();

        var known = new Dictionary<(string, string), ListingEntity>();
        var knownPayloads = new HashSet<(string, string, string)>();
        foreach (var bySource in batch.GroupBy(l => l.Listing.Id.Source))
        {
            var source = bySource.Key;
            var ids = bySource.Select(l => l.Listing.Id.SourceId).ToList();

            foreach (var row in await db.Listings.AsTracking()
                         .Where(e => e.Source == source && ids.Contains(e.SourceId))
                         .ToListAsync(ct))
                known[(row.Source, row.SourceId)] = row;

            foreach (var p in await db.ListingPayloads
                         .Where(e => e.Source == source && ids.Contains(e.SourceId))
                         .Select(e => new { e.SourceId, e.PayloadHash })
                         .ToListAsync(ct))
                knownPayloads.Add((source, p.SourceId, p.PayloadHash));
        }

        var added = 0;
        foreach (var (listing, raw) in batch)
        {
            var key = (listing.Id.Source, listing.Id.SourceId);
            if (known.TryGetValue(key, out var row))
            {
                ListingMapper.Refresh(row, listing, observedAt);
            }
            else
            {
                db.Listings.Add(ListingMapper.ToNewEntity(listing, observedAt));
                added++;
            }

            var hash = ListingPayloadEntity.Hash(raw);
            if (knownPayloads.Add((key.Source, key.SourceId, hash)))
            {
                db.ListingPayloads.Add(new ListingPayloadEntity
                {
                    Source = key.Source,
                    SourceId = key.SourceId,
                    PayloadHash = hash,
                    Payload = raw,
                    FirstSeenAt = observedAt.ToUniversalTime(),
                });
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The context is scoped: left dirty, the next save in this scope (the run log)
            // would retry these rows and fail the same way.
            db.ChangeTracker.Clear();
            throw;
        }
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
