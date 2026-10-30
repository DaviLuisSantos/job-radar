using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Triage;

/// <summary>
/// The "Today" page of a market: the listings of the last week still to triage, the best of them per profile, what
/// arrived since the last visit, and what moved in the applications. A listing to triage is what the triage list
/// would show (untriaged, not excluded, copies collapsed) and was first seen in the window, so the counts match the
/// triage, and only triaging takes a listing away.
/// </summary>
public sealed class TodayService(IListingScoreRepository scores, ITrackingRepository tracking, IMarketVisitRepository visits)
{
    public const int BestPerProfile = 5;

    /// <summary>The page. Does not record this visit: see <see cref="RecordVisitAsync"/>.</summary>
    public async Task<MarketToday> TodayAsync(JobMarket market, IReadOnlyList<string> profiles, DateTimeOffset now, CancellationToken ct = default)
    {
        var last = (await visits.GetLastVisitsAsync(ct)).TryGetValue(market, out var at) ? at : (DateTimeOffset?)null;
        var since = now - MarketToday.Window;

        var perProfile = new List<ProfileToday>();
        var ids = new HashSet<ListingId>();
        var fresh = new HashSet<ListingId>();
        foreach (var profile in profiles)
        {
            var rows = await ToTriageAsync(profile, since, ct);
            var arrived = rows.Where(r => last is not { } visit || r.FirstSeenAt > visit).ToList();
            perProfile.Add(new ProfileToday(profile, rows.Count, rows.Take(BestPerProfile).ToList()) { Fresh = arrived.Count });
            ids.UnionWith(rows.Select(r => r.Id));
            fresh.UnionWith(arrived.Select(r => r.Id));
        }

        var changes = await tracking.ListStatusChangesAsync(profiles.ToList(), since, ct);
        return new MarketToday(market, since, last, perProfile, changes) { New = ids.Count, Fresh = fresh.Count };
    }

    /// <summary>Listings to triage per market, for the market selector: the same number its "Today" page shows.</summary>
    public async Task<IReadOnlyDictionary<JobMarket, int>> NewCountsAsync(
        IReadOnlyDictionary<JobMarket, IReadOnlyList<string>> profilesByMarket, DateTimeOffset now, CancellationToken ct = default)
    {
        var since = now - MarketToday.Window;
        var counts = new Dictionary<JobMarket, int>();
        foreach (var (market, profiles) in profilesByMarket)
        {
            var ids = new HashSet<ListingId>();
            foreach (var profile in profiles)
                ids.UnionWith((await ToTriageAsync(profile, since, ct)).Select(r => r.Id));
            counts[market] = ids.Count;
        }
        return counts;
    }

    public Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default) =>
        visits.RecordVisitAsync(market, at, ct);

    private async Task<IReadOnlyList<TriageRow>> ToTriageAsync(string profile, DateTimeOffset since, CancellationToken ct) =>
        Duplicates.Collapse(await scores.ListForTriageAsync(new TriageFilter(profile, SeenSince: since), ct));
}
