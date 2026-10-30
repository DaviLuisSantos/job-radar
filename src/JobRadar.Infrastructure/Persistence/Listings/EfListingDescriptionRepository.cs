using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Listings;

internal sealed class EfListingDescriptionRepository(JobRadarDbContext db) : IListingDescriptionRepository
{
    private const int Chunk = 5000;

    public async Task SaveAsync(ListingDescription description, CancellationToken ct = default)
    {
        var (source, sourceId) = (description.Id.Source, description.Id.SourceId);
        var row = await db.ListingDescriptions.AsTracking()
            .SingleOrDefaultAsync(d => d.Source == source && d.SourceId == sourceId, ct);
        if (row is null)
        {
            row = new ListingDescriptionEntity { Source = source, SourceId = sourceId };
            db.ListingDescriptions.Add(row);
        }
        row.Text = description.Text;
        row.Origin = ToColumn(description.Origin);
        row.CapturedAt = description.CapturedAt.ToUniversalTime();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<ListingDescription?> FindAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var row = await db.ListingDescriptions.SingleOrDefaultAsync(d => d.Source == source && d.SourceId == sourceId, ct);
        return row is null ? null : new ListingDescription(id, row.Text, ToOrigin(row.Origin), row.CapturedAt);
    }

    public async Task<IReadOnlyList<(string Title, string? Description)>> ListAdsFirstSeenAsync(
        Country country, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var code = ListingMapper.ToColumn(country);
        var (start, end) = (from.ToUniversalTime(), to.ToUniversalTime());
        var rows = await (
                from l in db.Listings
                where l.Country == code && l.FirstSeenAt >= start && l.FirstSeenAt < end
                join d in db.ListingDescriptions on new { l.Source, l.SourceId } equals new { d.Source, d.SourceId } into ds
                from d in ds.DefaultIfEmpty()
                select new { l.Title, Text = (string?)d.Text })
            .ToListAsync(ct);
        return rows.Select(r => (r.Title, (string?)r.Text)).ToList();
    }

    public async Task<IReadOnlyDictionary<ListingId, string>> GetTextsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<ListingId, string>();
        foreach (var bySource in ids.Distinct().GroupBy(i => i.Source))
        {
            var source = bySource.Key;
            foreach (var chunk in bySource.Select(i => i.SourceId).Chunk(Chunk))
            {
                var sourceIds = chunk.ToList();
                var rows = await db.ListingDescriptions
                    .Where(d => d.Source == source && sourceIds.Contains(d.SourceId))
                    .Select(d => new { d.SourceId, d.Text })
                    .ToListAsync(ct);
                foreach (var r in rows)
                    result[new ListingId(source, r.SourceId)] = r.Text;
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<ListingId>> ListToFetchAsync(
        string source, int minScore, DateTimeOffset postedSince, int limit, CancellationToken ct = default)
    {
        var since = postedSince.ToUniversalTime();
        var rows = await (
                from l in db.Listings
                where l.Source == source
                where !db.ListingDescriptions.Any(d => d.Source == l.Source && d.SourceId == l.SourceId)
                let handled = db.ListingLabels.Any(x => x.Source == l.Source && x.SourceId == l.SourceId)
                              || db.Applications.Any(x => x.Source == l.Source && x.SourceId == l.SourceId)
                let best = db.ListingScores.Where(s => s.Source == l.Source && s.SourceId == l.SourceId).Max(s => (int?)s.Score)
                where handled || ((l.PostedAt ?? l.FirstSeenAt) >= since && best >= minScore)
                orderby handled descending, best descending, l.PostedAt descending, l.SourceId
                select l.SourceId)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(id => new ListingId(source, id)).ToList();
    }

    internal static string ToColumn(DescriptionOrigin origin) => origin switch
    {
        DescriptionOrigin.Browser => "browser",
        DescriptionOrigin.Pasted => "pasted",
        DescriptionOrigin.Source => "source",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    internal static DescriptionOrigin ToOrigin(string column) => column switch
    {
        "browser" => DescriptionOrigin.Browser,
        "pasted" => DescriptionOrigin.Pasted,
        "source" => DescriptionOrigin.Source,
        _ => throw new InvalidOperationException($"Unknown description origin '{column}'."),
    };
}
