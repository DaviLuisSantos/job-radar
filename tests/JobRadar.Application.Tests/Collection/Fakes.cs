using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;

namespace JobRadar.Application.Tests.Collection;

internal static class Listings
{
    public static SourcedListing Of(string source, string id) => new(
        new Listing(new ListingId(source, id), $"Job {id}", null, ListingLocation.Unknown, WorkType.Unknown,
            null, null, new Uri($"https://example.test/{id}"), null),
        $$"""{"id":"{{id}}"}""");
}

/// <summary>Answers each search from a script; records what it was asked.</summary>
internal sealed class FakeSource(string name) : IJobSource
{
    public string Name => name;
    public List<ListingSearch> Received { get; } = [];
    public Func<ListingSearch, SearchResult> Respond { get; set; } = _ => new SearchResult([], Complete: true);

    public Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        Received.Add(search);
        return Task.FromResult(Respond(search));
    }
}

/// <summary>Remembers listing ids; a listing is new the first time its id is recorded.</summary>
internal sealed class InMemoryListings : IListingRepository
{
    private readonly HashSet<ListingId> _known = [];

    public Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        var added = listings.Count(l => _known.Add(l.Listing.Id));
        return Task.FromResult(new ObservationResult(added, listings.Count - added));
    }

    public Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

internal sealed class InMemoryRuns : ICollectionRunRepository
{
    public List<CollectionRun> Runs { get; } = [];

    public Task RecordAsync(CollectionRun run, CancellationToken ct = default)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLastSuccessfulStartAsync(string source, string searchKey, CancellationToken ct = default) =>
        Task.FromResult(Runs.Where(r => r.Source == source && r.SearchKey == searchKey && r.Succeeded)
            .Select(r => (DateTimeOffset?)r.StartedAt).Max());
}
