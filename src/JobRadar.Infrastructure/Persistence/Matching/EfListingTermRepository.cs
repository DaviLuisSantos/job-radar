using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Matching;

internal sealed class EfListingTermRepository(JobRadarDbContext db) : IListingTermRepository
{
    private const int Chunk = 5000;

    public async Task<int> RecordAsync(
        string source, string term, IReadOnlyCollection<string> sourceIds, DateTimeOffset foundAt, CancellationToken ct = default)
    {
        var added = 0;
        var at = foundAt.ToUniversalTime();
        foreach (var chunk in sourceIds.Distinct(StringComparer.Ordinal).Chunk(Chunk))
        {
            var ids = chunk.ToList();
            // Only listings this system collected: the search answers for the whole of Seek.
            var stored = await db.Listings
                .Where(l => l.Source == source && ids.Contains(l.SourceId))
                .Select(l => l.SourceId)
                .ToListAsync(ct);
            var marked = (await db.ListingTerms
                    .Where(t => t.Source == source && t.Term == term && ids.Contains(t.SourceId))
                    .Select(t => t.SourceId)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var id in stored.Where(id => !marked.Contains(id)))
            {
                db.ListingTerms.Add(new ListingTermEntity { Source = source, SourceId = id, Term = term, FoundAt = at });
                added++;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
        return added;
    }

    public async Task<IReadOnlyDictionary<ListingId, IReadOnlyList<string>>> GetTermsAsync(
        IReadOnlyCollection<ListingId> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<ListingId, IReadOnlyList<string>>();
        foreach (var bySource in ids.Distinct().GroupBy(i => i.Source))
        {
            var source = bySource.Key;
            foreach (var chunk in bySource.Select(i => i.SourceId).Chunk(Chunk))
            {
                var sourceIds = chunk.ToList();
                var rows = await db.ListingTerms
                    .Where(t => t.Source == source && sourceIds.Contains(t.SourceId))
                    .Select(t => new { t.SourceId, t.Term })
                    .ToListAsync(ct);
                foreach (var g in rows.GroupBy(r => r.SourceId))
                    result[new ListingId(source, g.Key)] = g.Select(r => r.Term).Order(StringComparer.Ordinal).ToList();
            }
        }
        return result;
    }
}
