using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Application.Tracking;

/// <summary>A listing id that this system never collected.</summary>
public sealed class ListingNotFoundException(ListingId id) : Exception($"Listing {id} was never collected.")
{
    public ListingId Id { get; } = id;
}

/// <summary>What the candidate does with a listing: move it through the application stages, take notes, label it.</summary>
public sealed class TrackingService(IListingRepository listings, ITrackingRepository tracking, TimeProvider time)
{
    public async Task<JobApplication> SetStatusAsync(ListingId id, ApplicationStatus status, CancellationToken ct = default)
    {
        var application = await LoadAsync(id, ct);
        var now = time.GetUtcNow();
        if (application is null)
            application = JobApplication.Start(id, status, now);
        else if (!application.ChangeStatus(status, now))
            return application;

        await tracking.SaveApplicationAsync(application, ct);
        return application;
    }

    /// <summary>Notes on a listing never touched start its application as <see cref="ApplicationStatus.Reviewed"/>.</summary>
    public async Task<JobApplication> SetNotesAsync(ListingId id, string? notes, CancellationToken ct = default)
    {
        var application = await LoadAsync(id, ct) ?? JobApplication.Start(id, ApplicationStatus.Reviewed, time.GetUtcNow());
        application.SetNotes(notes);
        await tracking.SaveApplicationAsync(application, ct);
        return application;
    }

    /// <summary>Labels the listing; a null verdict removes the label.</summary>
    public async Task LabelAsync(ListingId id, Verdict? verdict, string? note = null, CancellationToken ct = default)
    {
        await EnsureCollectedAsync(id, ct);
        var label = verdict is { } v ? new ListingLabel(id, v, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), time.GetUtcNow()) : null;
        await tracking.SetLabelAsync(id, label, ct);
    }

    private async Task<JobApplication?> LoadAsync(ListingId id, CancellationToken ct)
    {
        await EnsureCollectedAsync(id, ct);
        return await tracking.FindApplicationAsync(id, ct);
    }

    private async Task EnsureCollectedAsync(ListingId id, CancellationToken ct)
    {
        if (await listings.FindAsync(id, ct) is null)
            throw new ListingNotFoundException(id);
    }
}
