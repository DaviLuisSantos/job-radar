using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres.Tracking;

internal sealed class PostgresTrackingRepository(JobRadarDbContext db) : ITrackingRepository
{
    public async Task<JobApplication?> FindApplicationAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var row = await db.Applications.FirstOrDefaultAsync(a => a.Source == source && a.SourceId == sourceId, ct);
        if (row is null)
            return null;

        var events = await db.ApplicationEvents
            .Where(e => e.Source == source && e.SourceId == sourceId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return JobApplication.Restore(
            id, TrackingMapper.ToStatus(row.Status), row.AppliedAt, row.Notes,
            events.Select(e => new StatusChange(
                e.FromStatus is null ? null : TrackingMapper.ToStatus(e.FromStatus), TrackingMapper.ToStatus(e.ToStatus), e.At)));
    }

    public async Task SaveApplicationAsync(JobApplication application, CancellationToken ct = default)
    {
        var (source, sourceId) = (application.ListingId.Source, application.ListingId.SourceId);
        var row = await db.Applications.AsTracking().FirstOrDefaultAsync(a => a.Source == source && a.SourceId == sourceId, ct);
        if (row is null)
        {
            row = new ApplicationEntity { Source = source, SourceId = sourceId };
            db.Applications.Add(row);
        }
        row.Status = TrackingMapper.ToColumn(application.Status);
        row.AppliedAt = application.AppliedAt?.ToUniversalTime();
        row.Notes = application.Notes;
        row.UpdatedAt = (application.History.Count > 0 ? application.History[^1].At : DateTimeOffset.UtcNow).ToUniversalTime();

        // History is append-only: store the entries past the ones already saved.
        var stored = await db.ApplicationEvents.CountAsync(e => e.Source == source && e.SourceId == sourceId, ct);
        foreach (var change in application.History.Skip(stored))
        {
            db.ApplicationEvents.Add(new ApplicationEventEntity
            {
                Source = source,
                SourceId = sourceId,
                FromStatus = change.From is { } from ? TrackingMapper.ToColumn(from) : null,
                ToStatus = TrackingMapper.ToColumn(change.To),
                At = change.At.ToUniversalTime(),
            });
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

    public async Task<ListingLabel?> FindLabelAsync(ListingId id, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var row = await db.ListingLabels.FirstOrDefaultAsync(l => l.Source == source && l.SourceId == sourceId, ct);
        return row is null ? null : new ListingLabel(id, TrackingMapper.ToVerdict(row.Verdict), row.Note, row.LabeledAt);
    }

    public async Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default)
    {
        var (source, sourceId) = (id.Source, id.SourceId);
        var row = await db.ListingLabels.AsTracking().FirstOrDefaultAsync(l => l.Source == source && l.SourceId == sourceId, ct);
        if (label is null)
        {
            if (row is not null)
                db.ListingLabels.Remove(row);
        }
        else
        {
            if (row is null)
            {
                row = new ListingLabelEntity { Source = source, SourceId = sourceId };
                db.ListingLabels.Add(row);
            }
            row.Verdict = TrackingMapper.ToColumn(label.Verdict);
            // A new verdict without a note keeps the earlier note, as in the prototype.
            row.Note = label.Note ?? row.Note;
            row.LabeledAt = label.LabeledAt.ToUniversalTime();
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
}
