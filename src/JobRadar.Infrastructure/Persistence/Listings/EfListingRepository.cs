using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Listings;

internal sealed class EfListingRepository(JobRadarDbContext db) : IListingRepository
{
    public async Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, string searchKey, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        if (listings.Count == 0)
            return new ObservationResult(0, 0);

        // A source can repeat an ad within one batch; the last copy wins.
        var batch = listings.GroupBy(l => l.Listing.Id).Select(g => g.Last()).ToList();
        var at = observedAt.ToUniversalTime();

        var known = new Dictionary<(string, string), ListingEntity>();
        var knownPayloads = new HashSet<(string, string, string)>();
        var knownHits = new HashSet<(string, string)>();
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

            foreach (var id in await db.ListingSearchHits
                         .Where(e => e.Source == source && e.SearchKey == searchKey && ids.Contains(e.SourceId))
                         .Select(e => e.SourceId)
                         .ToListAsync(ct))
                knownHits.Add((source, id));
        }

        var added = 0;
        foreach (var (listing, raw, _) in batch)
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
                    Source = key.Source, SourceId = key.SourceId, PayloadHash = hash, Payload = raw, FirstSeenAt = at,
                });
            }

            if (knownHits.Add(key))
            {
                db.ListingSearchHits.Add(new ListingSearchHitEntity
                {
                    Source = key.Source, SourceId = key.SourceId, SearchKey = searchKey, FirstSeenAt = at,
                });
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            // The context is scoped to a whole collection pass. Left dirty after a failure, the next
            // save (the run log) would retry these rows; left tracked after success, hundreds of
            // searches would pile up in memory.
            db.ChangeTracker.Clear();
        }
        return new ObservationResult(added, batch.Count - added);
    }

    public async Task<ObservationResult> ImportAsync(IReadOnlyCollection<ImportedListing> listings, CancellationToken ct = default)
    {
        if (listings.Count == 0)
            return new ObservationResult(0, 0);

        var batch = listings.GroupBy(l => l.Listing.Listing.Id).Select(g => g.MinBy(l => l.FirstSeenAt)!).ToList();
        var known = new Dictionary<(string, string), ListingEntity>();
        var knownPayloads = new HashSet<(string, string, string)>();
        var knownHits = new HashSet<(string, string, string)>();
        foreach (var bySource in batch.GroupBy(l => l.Listing.Listing.Id.Source))
        {
            var source = bySource.Key;
            var ids = bySource.Select(l => l.Listing.Listing.Id.SourceId).ToList();
            foreach (var row in await db.Listings.AsTracking().Where(e => e.Source == source && ids.Contains(e.SourceId)).ToListAsync(ct))
                known[(row.Source, row.SourceId)] = row;
            foreach (var p in await db.ListingPayloads.Where(e => e.Source == source && ids.Contains(e.SourceId))
                         .Select(e => new { e.SourceId, e.PayloadHash }).ToListAsync(ct))
                knownPayloads.Add((source, p.SourceId, p.PayloadHash));
            foreach (var h in await db.ListingSearchHits.Where(e => e.Source == source && ids.Contains(e.SourceId))
                         .Select(e => new { e.SourceId, e.SearchKey }).ToListAsync(ct))
                knownHits.Add((source, h.SourceId, h.SearchKey));
        }

        var added = 0;
        foreach (var imported in batch)
        {
            var (listing, raw, _) = imported.Listing;
            var key = (listing.Id.Source, listing.Id.SourceId);
            var first = imported.FirstSeenAt.ToUniversalTime();
            if (known.TryGetValue(key, out var row))
            {
                // This system's details are newer; only an earlier sighting moves first-seen back.
                if (first < row.FirstSeenAt)
                    row.FirstSeenAt = first;
            }
            else
            {
                var entity = ListingMapper.ToNewEntity(listing, imported.LastSeenAt);
                entity.FirstSeenAt = first;
                db.Listings.Add(entity);
                added++;
            }

            var hash = ListingPayloadEntity.Hash(raw);
            if (knownPayloads.Add((key.Source, key.SourceId, hash)))
                db.ListingPayloads.Add(new ListingPayloadEntity { Source = key.Source, SourceId = key.SourceId, PayloadHash = hash, Payload = raw, FirstSeenAt = first });
            foreach (var hit in imported.HitKeys.Where(h => knownHits.Add((key.Source, key.SourceId, h))))
                db.ListingSearchHits.Add(new ListingSearchHitEntity { Source = key.Source, SourceId = key.SourceId, SearchKey = hit, FirstSeenAt = first });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
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

    public async Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default)
    {
        if (searchKeys.Count == 0)
            return [];

        var keys = searchKeys.ToList();
        var rows = await db.Listings
            .Where(l => db.ListingSearchHits.Any(h =>
                h.Source == l.Source && h.SourceId == l.SourceId && keys.Contains(h.SearchKey)))
            .ToListAsync(ct);
        return rows.Select(r => ListingMapper.ToRecord(r).Listing).ToList();
    }

    public async Task<IReadOnlyCollection<string>> GetSearchKeysAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        return await db.ListingSearchHits
            .Where(h => h.Source == source && h.SourceId == sourceId)
            .Select(h => h.SearchKey)
            .ToListAsync(ct);
    }

    public Task<DateTimeOffset?> GetOldestPostedAmongFirstSeenSinceAsync(string source, DateTimeOffset since, CancellationToken ct = default)
    {
        var after = since.ToUniversalTime();
        return db.Listings
            .Where(l => l.Source == source && l.FirstSeenAt > after && l.PostedAt != null)
            .MinAsync(l => l.PostedAt, ct);
    }
}
