using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Tracking;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Matching;

internal sealed class EfListingScoreRepository(JobRadarDbContext db) : IListingScoreRepository
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

    public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        // Postgres keeps microseconds and .NET ticks are 100 ns: a score saved at exactly `before` is
        // stored at or after the cutoff truncated to microseconds, never before it.
        var ticks = before.UtcTicks;
        var cutoff = new DateTimeOffset(ticks - ticks % 10, TimeSpan.Zero);
        return db.ListingScores.Where(s => s.ScoredAt < cutoff).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default)
    {
        var rows = await (
                from s in db.ListingScores
                join l in db.Listings on new { s.Source, s.SourceId } equals new { l.Source, l.SourceId }
                join lab in db.ListingLabels on new { s.Source, s.SourceId } equals new { lab.Source, lab.SourceId } into labels
                from lab in labels.DefaultIfEmpty()
                join app in db.Applications on new { s.Source, s.SourceId } equals new { app.Source, app.SourceId } into apps
                from app in apps.DefaultIfEmpty()
                where lab != null || app != null
                select new { s.Source, s.SourceId, s.Profile, s.Score, s.Explanation, l.Title, Verdict = lab.Verdict, Status = app.Status })
            .ToListAsync(ct);

        var judged = new List<JudgedScore>();
        foreach (var r in rows)
        {
            var verdict = Judgement.Effective(
                r.Verdict is null ? null : TrackingMapper.ToVerdict(r.Verdict),
                r.Status is null ? null : TrackingMapper.ToStatus(r.Status));
            if (verdict is not { } v)
                continue;   // reviewed only: no verdict yet
            var items = JsonSerializer.Deserialize<List<ScoreItem>>(r.Explanation, Json) ?? [];
            judged.Add(new JudgedScore(new ListingId(r.Source, r.SourceId), r.Title, r.Profile, new FitScore(r.Score, items), v));
        }
        return judged;
    }

    public async Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) =>
        await db.ListingScores.Select(s => s.Profile).Distinct().OrderBy(p => p).ToListAsync(ct);

    public async Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default)
    {
        var query =
            from s in db.ListingScores
            where s.Profile == filter.Profile
            join l in db.Listings on new { s.Source, s.SourceId } equals new { l.Source, l.SourceId }
            join app in db.Applications on new { s.Source, s.SourceId } equals new { app.Source, app.SourceId } into apps
            from app in apps.DefaultIfEmpty()
            join lab in db.ListingLabels on new { s.Source, s.SourceId } equals new { lab.Source, lab.SourceId } into labels
            from lab in labels.DefaultIfEmpty()
            select new { s.Score, l, Status = app.Status, Verdict = lab.Verdict };

        if (!filter.IncludeExcluded)
            query = query.Where(r => r.Score > 0);
        if (filter.MinScore > 0)
            query = query.Where(r => r.Score >= filter.MinScore);
        if (filter.OnlyUntriaged)
            query = query.Where(r => r.Status == null && r.Verdict == null);
        if (filter.Text is { } text)
        {
            // Contains keeps the text literal (EF escapes LIKE wildcards on every provider); lowering both sides
            // ignores case on both databases, whose collations are case-sensitive.
            var needle = text.ToLowerInvariant();
            query = query.Where(r => r.l.Title.ToLower().Contains(needle) || (r.l.Company ?? "").ToLower().Contains(needle));
        }

        var rows = await query
            .OrderByDescending(r => r.Score).ThenByDescending(r => r.l.PostedAt).ThenBy(r => r.l.SourceId)
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var listing = ListingMapper.ToRecord(r.l);
            return new TriageRow(
                listing.Listing.Id, listing.Listing.Title, listing.Listing.Company, listing.Listing.Location,
                listing.Listing.WorkType, listing.Listing.SalaryText, listing.Listing.PostedAt, listing.FirstSeenAt,
                r.Score,
                r.Status is null ? null : TrackingMapper.ToStatus(r.Status),
                r.Verdict is null ? null : TrackingMapper.ToVerdict(r.Verdict));
        }).ToList();
    }

    public async Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var rows = await db.ListingScores
            .Where(s => s.Source == source && s.SourceId == sourceId)
            .OrderByDescending(s => s.Score)
            .ToListAsync(ct);
        return rows.Select(r => new ProfileScore(
            r.Profile, new FitScore(r.Score, JsonSerializer.Deserialize<List<ScoreItem>>(r.Explanation, Json) ?? []), r.ScoredAt)).ToList();
    }
}
