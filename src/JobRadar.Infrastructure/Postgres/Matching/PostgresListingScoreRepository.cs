using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Matching;

internal sealed class PostgresListingScoreRepository(JobRadarDbContext db) : IListingScoreRepository
{
    private const int Chunk = 1000;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default)
    {
        foreach (var chunk in scores.DistinctBy(s => (s.Id, s.Profile)).Chunk(Chunk))
        {
            foreach (var byKey in chunk.GroupBy(s => (s.Id.Source, s.Profile)))
            {
                var (source, profile) = byKey.Key;
                var ids = byKey.Select(s => s.Id.SourceId).ToList();
                var existing = await db.ListingScores.AsTracking()
                    .Where(e => e.Source == source && e.Profile == profile && ids.Contains(e.SourceId))
                    .ToDictionaryAsync(e => e.SourceId, ct);

                foreach (var score in byKey)
                {
                    if (!existing.TryGetValue(score.Id.SourceId, out var row))
                    {
                        row = new ListingScoreEntity { Source = source, SourceId = score.Id.SourceId, Profile = profile };
                        db.ListingScores.Add(row);
                    }
                    row.Score = score.Score.Final;
                    row.Explanation = JsonSerializer.Serialize(score.Score.Items, Json);
                    row.RulesHash = score.RulesHash;
                    row.ScoredAt = score.ScoredAt.ToUniversalTime();
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            finally
            {
                // Thousands of rows per pass: do not keep them all tracked.
                db.ChangeTracker.Clear();
            }
        }
    }
}
