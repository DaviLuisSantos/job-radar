using JobRadar.Application.Abstractions;
using JobRadar.Application.Tests.Collection;
using JobRadar.Application.Tracking;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Tracking;

public class TrackingServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly ListingId Known = new("seek", "1");

    private sealed class InMemoryTracking : ITrackingRepository
    {
        public Dictionary<ListingId, JobApplication> Applications { get; } = [];
        public Dictionary<ListingId, ListingLabel> Labels { get; } = [];
        public int Saves { get; private set; }

        public Task<JobApplication?> FindApplicationAsync(ListingId id, CancellationToken ct = default) =>
            Task.FromResult(Applications.GetValueOrDefault(id));

        public Task SaveApplicationAsync(JobApplication application, CancellationToken ct = default)
        {
            Applications[application.ListingId] = application;
            Saves++;
            return Task.CompletedTask;
        }

        public Task<ListingLabel?> FindLabelAsync(ListingId id, CancellationToken ct = default) =>
            Task.FromResult(Labels.GetValueOrDefault(id));

        public Task SetLabelAsync(ListingId id, ListingLabel? label, CancellationToken ct = default)
        {
            if (label is null)
                Labels.Remove(id);
            else
                Labels[id] = label;
            return Task.CompletedTask;
        }
    }

    /// <summary>Only <see cref="Known"/> exists.</summary>
    private sealed class OneListing : IListingRepository
    {
        public Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default) =>
            Task.FromResult(id == Known
                ? new ListingRecord(Listings.Of("seek", "1").Listing, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)
                : null);

        public Task<ObservationResult> RecordObservationsAsync(IReadOnlyCollection<SourcedListing> listings, string searchKey, DateTimeOffset observedAt, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ObservationResult> ImportAsync(IReadOnlyCollection<ImportedListing> listings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateTimeOffset?> GetOldestPostedAmongFirstSeenSinceAsync(string source, DateTimeOffset since, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<string>> GetSearchKeysAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 1, 0, 0, TimeSpan.Zero));
    private readonly InMemoryTracking _tracking = new();
    private TrackingService Service => new(new OneListing(), _tracking, _time);

    [Fact]
    public async Task First_status_starts_the_application_and_later_ones_move_it()
    {
        await Service.SetStatusAsync(Known, ApplicationStatus.Applied, Ct);
        _time.Advance(TimeSpan.FromDays(3));
        var app = await Service.SetStatusAsync(Known, ApplicationStatus.Interview, Ct);

        Assert.Equal(ApplicationStatus.Interview, app.Status);
        Assert.Equal(2, app.History.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 1, 0, 0, TimeSpan.Zero), app.AppliedAt);
    }

    [Fact]
    public async Task Repeating_the_status_does_not_save()
    {
        await Service.SetStatusAsync(Known, ApplicationStatus.Applied, Ct);
        await Service.SetStatusAsync(Known, ApplicationStatus.Applied, Ct);

        Assert.Equal(1, _tracking.Saves);
    }

    [Fact]
    public async Task Notes_on_an_untouched_listing_mark_it_reviewed()
    {
        var app = await Service.SetNotesAsync(Known, "Recruiter call Friday", Ct);

        Assert.Equal((ApplicationStatus.Reviewed, "Recruiter call Friday"), (app.Status, app.Notes));
    }

    [Fact]
    public async Task Labels_are_set_and_cleared()
    {
        await Service.LabelAsync(Known, Verdict.Good, " exactly my stack ", Ct);
        Assert.Equal((Verdict.Good, "exactly my stack"), (_tracking.Labels[Known].Verdict, _tracking.Labels[Known].Note));

        await Service.LabelAsync(Known, null, ct: Ct);
        Assert.Empty(_tracking.Labels);
    }

    [Fact]
    public async Task A_listing_never_collected_is_refused()
    {
        var unknown = new ListingId("seek", "404");

        await Assert.ThrowsAsync<ListingNotFoundException>(() => Service.SetStatusAsync(unknown, ApplicationStatus.Applied, Ct));
        await Assert.ThrowsAsync<ListingNotFoundException>(() => Service.LabelAsync(unknown, Verdict.Bad, ct: Ct));
        Assert.Empty(_tracking.Applications);
    }
}
