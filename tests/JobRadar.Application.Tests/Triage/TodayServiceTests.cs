using JobRadar.Application.Abstractions;
using JobRadar.Application.Triage;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Tests.Triage;

public class TodayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Rows per profile, filtered by first sighting the way the repository does it.</summary>
    private sealed class Scores(Dictionary<string, TriageRow[]> byProfile) : IListingScoreRepository
    {
        public Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TriageRow>>(byProfile.GetValueOrDefault(filter.Profile, [])
                .Where(r => filter.SeenSince is not { } since || r.FirstSeenAt > since).ToList());

        public Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Visits : IMarketVisitRepository
    {
        public Dictionary<JobMarket, DateTimeOffset> Last { get; } = [];

        public Task<IReadOnlyDictionary<JobMarket, DateTimeOffset>> GetLastVisitsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<JobMarket, DateTimeOffset>>(new Dictionary<JobMarket, DateTimeOffset>(Last));

        public Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default)
        {
            Last[market] = at;
            return Task.CompletedTask;
        }
    }

    private sealed class Changes : ITrackingRepository
    {
        public DateTimeOffset? AskedSince { get; private set; }

        public Task<IReadOnlyList<RecentStatusChange>> ListStatusChangesAsync(
            IReadOnlyCollection<string> profiles, DateTimeOffset since, CancellationToken ct = default)
        {
            AskedSince = since;
            return Task.FromResult<IReadOnlyList<RecentStatusChange>>(
                [new RecentStatusChange(new ListingId("gupy", "1"), "Dev", "Acme", ApplicationStatus.Applied, ApplicationStatus.Interview, Now)]);
        }

        public Task<JobApplication?> FindApplicationAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveApplicationAsync(JobApplication application, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ListingLabel?> FindLabelAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static TriageRow Row(string id, string company, int score, int hoursAgo) =>
        new(new ListingId("gupy", id), "Dev .NET", company, ListingLocation.Unknown(Country.BR), WorkType.FullTime, null, null,
            Now.AddHours(-hoursAgo), score, null, null);

    // Two profiles share listing "a"; "old" arrived before the last visit; "b2" is a copy of "b".
    private static Scores Rows() => new(new()
    {
        ["dotnet-br"] = [Row("a", "A", 90, 2), Row("b", "B", 80, 3), Row("b2", "B", 70, 4), Row("old", "C", 85, 48)],
        ["remote"] = [Row("a", "A", 88, 2), Row("d", "D", 60, 1)],
    });

    [Fact]
    public async Task New_is_what_arrived_since_the_last_visit_copies_collapsed_and_counted_once_per_market()
    {
        var visits = new Visits();
        visits.Last[JobMarket.BR] = Now.AddHours(-24);
        var tracking = new Changes();

        var today = await new TodayService(Rows(), tracking, visits).TodayAsync(JobMarket.BR, ["dotnet-br", "remote"], Now, Ct);

        Assert.False(today.FirstVisit);
        Assert.Equal(Now.AddHours(-24), today.Since);
        Assert.Equal([("dotnet-br", 2), ("remote", 2)], today.Profiles.Select(p => (p.Profile, p.New)));
        Assert.Equal(["a", "b"], today.Profiles[0].Best.Select(r => r.Id.SourceId));
        Assert.Equal(3, today.New);   // a, b, d
        Assert.Equal(Now.AddHours(-24), tracking.AskedSince);
        Assert.Single(today.Changes);
        Assert.DoesNotContain(visits.Last, v => v.Value == Now);   // reading the page does not record the visit
    }

    [Fact]
    public async Task The_first_visit_looks_a_week_back_and_counts_match_the_page()
    {
        var visits = new Visits();
        var service = new TodayService(Rows(), new Changes(), visits);

        var today = await service.TodayAsync(JobMarket.BR, ["dotnet-br", "remote"], Now, Ct);
        var counts = await service.NewCountsAsync(new Dictionary<JobMarket, IReadOnlyList<string>> { [JobMarket.BR] = ["dotnet-br", "remote"] }, Now, Ct);

        Assert.True(today.FirstVisit);
        Assert.Equal(Now - MarketToday.FirstVisitWindow, today.Since);
        Assert.Equal(4, today.New);   // "old" too: two days back is inside the week
        Assert.Equal(today.New, counts[JobMarket.BR]);

        await service.RecordVisitAsync(JobMarket.BR, Now, Ct);
        Assert.Equal(0, (await service.NewCountsAsync(new Dictionary<JobMarket, IReadOnlyList<string>> { [JobMarket.BR] = ["dotnet-br"] }, Now, Ct))[JobMarket.BR]);
    }
}
