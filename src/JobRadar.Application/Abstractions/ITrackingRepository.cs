using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// The candidate's applications and labels (today in Postgres, tables <c>applications</c>,
/// <c>application_events</c> and <c>listing_labels</c>).
/// </summary>
public interface ITrackingRepository
{
    Task<JobApplication?> FindApplicationAsync(ListingId id, CancellationToken ct = default);

    /// <summary>Inserts or updates the application; history entries are only ever appended.</summary>
    Task SaveApplicationAsync(JobApplication application, CancellationToken ct = default);

    /// <summary>Sets the label, or removes it when <paramref name="label"/> is null.</summary>
    Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default);
}
