using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Listings;

internal sealed class PostgresListingDescriptionRepository(JobRadarDbContext db) : IListingDescriptionRepository
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

    internal static string ToColumn(DescriptionOrigin origin) => origin switch
    {
        DescriptionOrigin.Browser => "browser",
        DescriptionOrigin.Pasted => "pasted",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    internal static DescriptionOrigin ToOrigin(string column) => column switch
    {
        "browser" => DescriptionOrigin.Browser,
        "pasted" => DescriptionOrigin.Pasted,
        _ => throw new InvalidOperationException($"Unknown description origin '{column}'."),
    };
}
