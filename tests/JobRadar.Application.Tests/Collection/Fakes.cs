using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Tests.Collection;

internal static class Listings
{
    public static SourcedListing Of(string source, string id, string? title = null, DateTimeOffset? posted = null) => new(
        new Listing(new ListingId(source, id), title ?? $"Job {id}", null, ListingLocation.Unknown, WorkType.Unknown,
            null, posted, new Uri($"https://example.test/{id}"), null),
        $$"""{"id":"{{id}}"}""");
}

/// <summary>Answers each search from a script; records what it was asked. Safe to call in parallel.</summary>
internal sealed class FakeSource(string name) : IJobSource
{
    private readonly List<ListingSearch> _received = [];

    public string Name => name;
    public List<ListingSearch> Received { get { lock (_received) return [.. _received]; } }
    public Func<ListingSearch, SearchResult> Respond { get; set; } = _ => new SearchResult([], Complete: true);
    public Func<ListingSearch, Task<SearchResult>>? RespondAsync { get; set; }

    public Task<SearchResult> SearchAsync(ListingSearch search, CancellationToken ct = default)
    {
        lock (_received)
            _received.Add(search);
        return RespondAsync?.Invoke(search) ?? Task.FromResult(Respond(search));
    }
}

/// <summary>Remembers listings, when they were first seen, and which searches found them.</summary>
internal sealed class InMemoryListings : IListingRepository
{
    private readonly Dictionary<ListingId, (Listing Listing, DateTimeOffset FirstSeen)> _known = [];
    private readonly HashSet<(ListingId, string)> _hits = [];

    public Task<ObservationResult> RecordObservationsAsync(
        IReadOnlyCollection<SourcedListing> listings, string searchKey, DateTimeOffset observedAt, CancellationToken ct = default)
    {
        var added = 0;
        foreach (var l in listings)
        {
            if (_known.TryAdd(l.Listing.Id, (l.Listing, observedAt)))
                added++;
            _hits.Add((l.Listing.Id, searchKey));
        }
        return Task.FromResult(new ObservationResult(added, listings.Count - added));
    }

    public Task<ListingRecord?> FindAsync(ListingId id, CancellationToken ct = default) =>
        Task.FromResult(_known.TryGetValue(id, out var k) ? new ListingRecord(k.Listing, k.FirstSeen, k.FirstSeen) : null);

    public List<ImportedListing> Imported { get; } = [];

    public Task<ObservationResult> ImportAsync(IReadOnlyCollection<ImportedListing> listings, CancellationToken ct = default)
    {
        Imported.AddRange(listings);
        var added = 0;
        foreach (var l in listings)
        {
            if (_known.TryAdd(l.Listing.Listing.Id, (l.Listing.Listing, l.FirstSeenAt)))
                added++;
            foreach (var key in l.HitKeys)
                _hits.Add((l.Listing.Listing.Id, key));
        }
        return Task.FromResult(new ObservationResult(added, listings.Count - added));
    }

    public Task<IReadOnlyList<Listing>> ListFoundByAsync(IReadOnlyCollection<string> searchKeys, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Listing>>(
            _hits.Where(h => searchKeys.Contains(h.Item2)).Select(h => h.Item1).Distinct().Select(id => _known[id].Listing).ToList());

    public Task<DateTimeOffset?> GetOldestPostedAmongFirstSeenSinceAsync(string source, DateTimeOffset since, CancellationToken ct = default) =>
        Task.FromResult(_known.Values
            .Where(k => k.Listing.Id.Source == source && k.FirstSeen > since)
            .Select(k => k.Listing.PostedAt)
            .Min());

    public Task<IReadOnlyCollection<string>> GetSearchKeysAsync(ListingId id, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyCollection<string>>(_hits.Where(h => h.Item1 == id).Select(h => h.Item2).ToList());
}

internal sealed class InMemoryDescriptions : IListingDescriptionRepository
{
    public Dictionary<ListingId, ListingDescription> Saved { get; } = [];

    public Task SaveAsync(ListingDescription description, CancellationToken ct = default)
    {
        Saved[description.Id] = description;
        return Task.CompletedTask;
    }

    public Task<ListingDescription?> FindAsync(ListingId id, CancellationToken ct = default) =>
        Task.FromResult(Saved.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<ListingId, string>> GetTextsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<ListingId, string>>(
            Saved.Where(s => ids.Contains(s.Key)).ToDictionary(s => s.Key, s => s.Value.Text));
}

internal sealed class InMemoryRuns : ICollectionRunRepository
{
    public List<CollectionRun> Runs { get; } = [];

    public Task RecordAsync(CollectionRun run, CancellationToken ct = default)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLastStartAsync(CancellationToken ct = default) =>
        Task.FromResult(Runs.Select(r => (DateTimeOffset?)r.StartedAt).Max());

    public Task<IReadOnlyList<DailyRunSummary>> SummariseRecentDaysAsync(int days, TimeZoneInfo zone, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<DateTimeOffset?> GetLastCompleteStartAsync(string source, string searchKey, CancellationToken ct = default) =>
        Task.FromResult(Runs.Where(r => r.Source == source && r.SearchKey == searchKey && r.Succeeded && r.Complete)
            .Select(r => (DateTimeOffset?)r.StartedAt).Max());
}

/// <summary>Marks only what was recorded; like the real one, it counts new marks.</summary>
internal sealed class InMemoryTerms : IListingTermRepository
{
    public HashSet<(ListingId Id, string Term)> Marks { get; } = [];

    public Task<int> RecordAsync(string source, string term, IReadOnlyCollection<string> sourceIds, DateTimeOffset foundAt, CancellationToken ct = default) =>
        Task.FromResult(sourceIds.Count(id => Marks.Add((new ListingId(source, id), term))));

    public Task<IReadOnlyDictionary<ListingId, IReadOnlyList<string>>> GetTermsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<ListingId, IReadOnlyList<string>>>(Marks
            .Where(m => ids.Contains(m.Id))
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(m => m.Term).ToList()));
}

/// <summary>Answers each term from a script; records what it was asked.</summary>
internal sealed class FakeFullTextSearch(string source) : IFullTextSearch
{
    public string Source => source;
    private readonly List<(string Term, int Days)> _received = [];
    public List<(string Term, int Days)> Received { get { lock (_received) return [.. _received]; } }
    public Func<FullTextTerm, FullTextResult> Respond { get; set; } = _ => new FullTextResult(new HashSet<string>(), true);

    public Task<FullTextResult> FindAsync(FullTextTerm term, int postedWithinDays, CancellationToken ct = default)
    {
        lock (_received)
            _received.Add((term.Text, postedWithinDays));
        return Task.FromResult(Respond(term));
    }
}
