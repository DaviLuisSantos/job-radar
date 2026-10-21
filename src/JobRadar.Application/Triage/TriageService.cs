using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Triage;

/// <summary>The reads behind the triage screens. Writes go through <c>TrackingService</c>.</summary>
public sealed class TriageService(
    IListingRepository listings,
    IListingScoreRepository scores,
    IListingTermRepository terms,
    ITrackingRepository tracking)
{
    public Task<IReadOnlyList<string>> ProfilesAsync(CancellationToken ct = default) => scores.ListProfilesAsync(ct);

    /// <summary>One page of the profile listings, the same job posted several times shown once.</summary>
    public async Task<TriagePage> ListAsync(TriageFilter filter, CancellationToken ct = default)
    {
        var f = filter with
        {
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 1, 200),
            MinScore = Math.Clamp(filter.MinScore, 0, 100),
            Text = string.IsNullOrWhiteSpace(filter.Text) ? null : filter.Text.Trim(),
        };
        var rows = Duplicates.Collapse(await scores.ListForTriageAsync(f, ct));
        return new TriagePage(rows.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList(), rows.Count);
    }

    /// <summary>Null when the listing was never collected.</summary>
    public async Task<ListingDetail?> DetailAsync(ListingId id, CancellationToken ct = default)
    {
        if (await listings.FindAsync(id, ct) is not { } record)
            return null;

        var byId = await terms.GetTermsAsync([id], ct);
        return new ListingDetail(
            record,
            await scores.GetScoresAsync(id, ct),
            byId.GetValueOrDefault(id) ?? [],
            await tracking.FindApplicationAsync(id, ct),
            await tracking.FindLabelAsync(id, ct));
    }
}
