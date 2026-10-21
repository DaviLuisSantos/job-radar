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

/// <summary>Remembers listings and which searches found them.</summary>
internal sealed class InMemoryListings : IListingRepository
{
    private readonly Dictionary<ListingId, Listing> _known = [];
    private readonly HashSet<(ListingId, string)> _hits = [];

    public Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, string searchKey, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        var added = 0;
        foreach (var l in listings)
        {
            if (_known.TryAdd(l.Listing.Id, l.Listing))
                added++;
            _hits.Add((l.Listing.Id, searchKey));
        }
        return Task.FromResult(new ObservationResult(added, listings.Count - added));
    }

    public Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Listing>>(
            _hits.Where(h => searchKeys.Contains(h.Item2)).Select(h => h.Item1).Distinct().Select(id => _known[id]).ToList());
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
