using JobRadar.Application.Abstractions;
using JobRadar.Application.Import;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Import;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Tracking;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Import;

public class PrototypeImportServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset June = new(2026, 6, 1, 12, 54, 35, TimeSpan.Zero);

    private sealed class FixedArchive(PrototypeData data) : IPrototypeArchive
    {
        public Task<PrototypeData> ReadAsync(string path, CancellationToken ct = default) => Task.FromResult(data);
    }

    private sealed class Tracking : ITrackingRepository
    {
        public Dictionary<ListingId, JobApplication> Applications { get; } = [];
        public Dictionary<ListingId, ListingLabel> Labels { get; } = [];
        public Task<JobApplication?> FindApplicationAsync(ListingId id, CancellationToken ct = default) => Task.FromResult(Applications.GetValueOrDefault(id));
        public Task SaveApplicationAsync(JobApplication application, CancellationToken ct = default) { Applications[application.ListingId] = application; return Task.CompletedTask; }
        public Task<ListingLabel?> FindLabelAsync(ListingId id, CancellationToken ct = default) => Task.FromResult(Labels.GetValueOrDefault(id));
        public Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default) { if (label is null) Labels.Remove(id); else Labels[id] = label; return Task.CompletedTask; }
    }

    private sealed class Market : IMarketRepository
    {
        public HashSet<DateOnly> Days { get; } = [];
        public List<ImportedMarketListing> Listings { get; } = [];
        public Task<bool> ImportSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts, CancellationToken ct = default) => Task.FromResult(Days.Add(snapshot.Date));
        public Task<int> ImportListingHistoryAsync(string source, IReadOnlyCollection<ImportedMarketListing> listings, CancellationToken ct = default) { Listings.AddRange(listings); return Task.FromResult(listings.Count); }
        public Task SaveSnapshotAsync(MarketSnapshot snapshot, IReadOnlyCollection<MarketCount> counts, IReadOnlyCollection<MarketListing> listings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<(string Category, string City), int>> GetLastGoodTotalsAsync(string source, DateOnly before, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DailyCount>> GetGoodCountsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeenListing>> GetListingsSeenAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MarketSnapshot>> GetSnapshotsAsync(string source, DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static PrototypeJob Job(string id, string? status = "new", bool applied = false, Verdict? label = null, string? notes = null,
        string[]? profiles = null, string[]? terms = null) =>
        new(Listings.Of("seek", id), June, profiles ?? ["tech"], terms ?? [], status, applied, applied ? June.AddDays(1) : null, notes, label, null, null);

    private readonly InMemoryListings _listings = new();
    private readonly InMemoryTerms _terms = new();
    private readonly Tracking _tracking = new();
    private readonly Market _market = new();

    private PrototypeImportService Service(params PrototypeJob[] jobs) => Service(new PrototypeData(jobs, [], []));

    private PrototypeImportService Service(PrototypeData data) =>
        new(new FixedArchive(data), _listings, _terms, _tracking, _market, new FakeTimeProvider(June.AddMonths(4)));

    [Fact]
    public async Task Listings_keep_the_prototype_first_sighting_and_attach_to_their_profiles()
    {
        await Service(Job("1", profiles: ["trades", "fifo"])).ImportAsync("jobs.db", dryRun: false, Ct);

        var imported = Assert.Single(_listings.Imported);
        Assert.Equal(June, imported.FirstSeenAt);
        Assert.Equal(["profile: trades", "profile: fifo"], imported.HitKeys);
    }

    [Fact]
    public async Task Statuses_map_with_rejected_split_by_whether_the_candidate_applied()
    {
        var report = await Service(
            Job("1"), Job("2", "reviewed"), Job("3", "applied", applied: true), Job("4", "interview", applied: true),
            Job("5", "rejected"), Job("6", "rejected", applied: true), Job("7", notes: "call Ana")).ImportAsync("jobs.db", false, Ct);

        ApplicationStatus Status(string id) => _tracking.Applications[new ListingId("seek", id)].Status;
        Assert.False(_tracking.Applications.ContainsKey(new ListingId("seek", "1")));
        Assert.Equal(ApplicationStatus.Reviewed, Status("2"));
        Assert.Equal(ApplicationStatus.Applied, Status("3"));
        Assert.Equal(ApplicationStatus.Interview, Status("4"));
        Assert.Equal(ApplicationStatus.Dismissed, Status("5"));
        Assert.Equal(ApplicationStatus.Declined, Status("6"));
        Assert.Equal((ApplicationStatus.Reviewed, "call Ana"), (Status("7"), _tracking.Applications[new ListingId("seek", "7")].Notes));
        Assert.Equal(June.AddDays(1), _tracking.Applications[new ListingId("seek", "3")].AppliedAt);
        Assert.Equal(6, report.ApplicationsAdded);
    }

    [Fact]
    public async Task What_this_system_already_holds_wins()
    {
        var id = new ListingId("seek", "1");
        _tracking.Applications[id] = JobApplication.Start(id, ApplicationStatus.Interview, June.AddMonths(4));
        _tracking.Labels[id] = new ListingLabel(id, Verdict.Good, "new system", June.AddMonths(4));

        var report = await Service(Job("1", "rejected", label: Verdict.Bad)).ImportAsync("jobs.db", false, Ct);

        Assert.Equal(ApplicationStatus.Interview, _tracking.Applications[id].Status);
        Assert.Equal(Verdict.Good, _tracking.Labels[id].Verdict);
        Assert.Equal((1, 1), (report.ApplicationsSkipped, report.LabelsSkipped));
    }

    [Fact]
    public async Task Terms_and_market_history_come_along()
    {
        var snapshot = new MarketSnapshot("seek", new DateOnly(2026, 10, 1), SnapshotStatus.Ok, 30, 30, 400, []);
        _market.Days.Add(new DateOnly(2026, 10, 2));   // this system already took the 2nd
        var data = new PrototypeData(
            [Job("1", terms: ["australian citizen", "working holiday"])],
            [new PrototypeSnapshot(snapshot, []), new PrototypeSnapshot(snapshot with { Date = new DateOnly(2026, 10, 2) }, [])],
            [new ImportedMarketListing(new MarketListing("HVAC-R", "Perth WA", Listings.Of("seek", "9").Listing), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20))]);

        var report = await Service(data).ImportAsync("jobs.db", false, Ct);

        Assert.Equal(2, _terms.Marks.Count);
        Assert.Equal((1, 1), (report.SnapshotsAdded, report.SnapshotsSkipped));
        Assert.Single(_market.Listings);
    }

    [Fact]
    public async Task A_dry_run_reads_and_counts_but_writes_nothing()
    {
        var report = await Service(Job("1", "applied", applied: true, label: Verdict.Good, terms: ["88 days"])).ImportAsync("jobs.db", dryRun: true, Ct);

        Assert.Equal((1, 1, 1, 1), (report.JobsInFile, report.TermsInFile, report.LabelsInFile, report.ApplicationsInFile));
        Assert.Empty(_listings.Imported);
        Assert.Empty(_tracking.Applications);
        Assert.Empty(_terms.Marks);
    }
}
