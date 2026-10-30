using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Triage;

/// <summary>
/// The "Today" page of a market: what arrived since the last visit, the best of it per profile, and what moved in the
/// applications. "New" is what the triage list would show (untriaged, not excluded, copies collapsed) and was first
/// seen after the visit, so the counts match the triage.
/// </summary>
public sealed class TodayService(IListingScoreRepository scores, ITrackingRepository tracking, IMarketVisitRepository visits)
{
    public const int BestPerProfile = 5;

    /// <summary>What happened since the last visit. Does not record this one: see <see cref="RecordVisitAsync"/>.</summary>
    public async Task<MarketToday> TodayAsync(JobMarket market, IReadOnlyList<string> profiles, DateTimeOffset now, CancellationToken ct = default)
    {
        var last = (await visits.GetLastVisitsAsync(ct)).TryGetValue(market, out var at) ? at : (DateTimeOffset?)null;
        var since = last ?? now - MarketToday.FirstVisitWindow;

        var perProfile = new List<ProfileToday>();
        var ids = new HashSet<ListingId>();
        foreach (var profile in profiles)
        {
            var rows = await NewRowsAsync(profile, since, ct);
            perProfile.Add(new ProfileToday(profile, rows.Count, rows.Take(BestPerProfile).ToList()));
            ids.UnionWith(rows.Select(r => r.Id));
        }

        var changes = await tracking.ListStatusChangesAsync(profiles.ToList(), since, ct);
        return new MarketToday(market, since, last is null, perProfile, changes) { New = ids.Count };
    }

    /// <summary>New listings per market since its last visit, for the market selector.</summary>
    public async Task<IReadOnlyDictionary<JobMarket, int>> NewCountsAsync(
        IReadOnlyDictionary<JobMarket, IReadOnlyList<string>> profilesByMarket, DateTimeOffset now, CancellationToken ct = default)
    {
        var last = await visits.GetLastVisitsAsync(ct);
        var counts = new Dictionary<JobMarket, int>();
        foreach (var (market, profiles) in profilesByMarket)
        {
            var since = last.TryGetValue(market, out var at) ? at : now - MarketToday.FirstVisitWindow;
            var ids = new HashSet<ListingId>();
            foreach (var profile in profiles)
                ids.UnionWith((await NewRowsAsync(profile, since, ct)).Select(r => r.Id));
            counts[market] = ids.Count;
        }
        return counts;
    }

    public Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default) =>
        visits.RecordVisitAsync(market, at, ct);

    private async Task<IReadOnlyList<TriageRow>> NewRowsAsync(string profile, DateTimeOffset since, CancellationToken ct) =>
        Duplicates.Collapse(await scores.ListForTriageAsync(new TriageFilter(profile, SeenSince: since), ct));
}
