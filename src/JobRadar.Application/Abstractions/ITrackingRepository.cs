using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;

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

    /// <summary>
    /// Status changes after <paramref name="since"/> of listings any of <paramref name="profiles"/> scored, newest
    /// first: what moved in the applications of one market.
    /// </summary>
    Task<IReadOnlyList<RecentStatusChange>> ListStatusChangesAsync(
        IReadOnlyCollection<string> profiles, DateTimeOffset since, CancellationToken ct = default);

    Task<ListingLabel?> FindLabelAsync(ListingId id, CancellationToken ct = default);

    /// <summary>Sets the label, or removes it when <paramref name="label"/> is null.</summary>
    Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default);
}
