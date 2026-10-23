using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Collection;

public class DescriptionFetchServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string Ad = "Labourer wanted for civil works. Minimum 2 years experience and own car required. " + new string('.', 40);

    private sealed class FakeDescriptions(string source, Func<string, string?> answer) : IDescriptionSource
    {
        private readonly List<string> _asked = [];
        public List<string> Asked { get { lock (_asked) return [.. _asked]; } }
        public string Source => source;

        public Task<string?> FetchAsync(string sourceId, CancellationToken ct = default)
        {
            lock (_asked)
                _asked.Add(sourceId);
            return Task.FromResult(answer(sourceId));
        }
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 1, 0, 0, TimeSpan.Zero));
    private readonly InMemoryDescriptions _descriptions = new();

    private DescriptionFetchService Service(IDescriptionSource source, DescriptionOptions? options = null) =>
        new([source], _descriptions, Options.Create(options ?? new DescriptionOptions()),
            Options.Create(new CollectionOptions { MaxParallelSearches = 1 }), _time, NullLogger<DescriptionFetchService>.Instance);

    private void Want(params string[] ids) => _descriptions.ToFetch.AddRange(ids.Select(id => new ListingId("seek", id)));

    [Fact]
    public async Task Texts_are_saved_as_fetched_from_the_source_and_gone_ads_are_skipped()
    {
        Want("1", "2");
        var seek = new FakeDescriptions("seek", id => id == "1" ? Ad : null);

        var summary = Assert.Single(await Service(seek).FetchAsync(Ct));

        Assert.Equal((2, 1, 1, 0, false), (summary.Asked, summary.Saved, summary.Gone, summary.Failed, summary.Blocked));
        var saved = Assert.Single(_descriptions.Saved).Value;
        Assert.Equal((DescriptionOrigin.Source, _time.GetUtcNow()), (saved.Origin, saved.CapturedAt));
    }

    [Fact]
    public async Task The_selection_follows_the_options()
    {
        var seek = new FakeDescriptions("seek", _ => Ad);

        await Service(seek, new DescriptionOptions { MinScore = 55, MaxAgeDays = 10, MaxPerPass = 7 }).FetchAsync(Ct);

        Assert.Equal(("seek", 55, _time.GetUtcNow().AddDays(-10), 7), _descriptions.Asked);
    }

    [Fact]
    public async Task The_first_refusal_stops_the_stage_and_keeps_what_was_saved()
    {
        Want("1", "2", "3", "4");
        var seek = new FakeDescriptions("seek", id => id == "2" ? throw new SourceBlockedException("seek", "HTTP 403") : Ad);

        var summary = Assert.Single(await Service(seek).FetchAsync(Ct));

        Assert.True(summary.Blocked);
        Assert.Equal(["1", "2"], seek.Asked);   // one at a time here: nothing asked after the refusal
        Assert.Equal([new ListingId("seek", "1")], _descriptions.Saved.Keys);
    }

    [Fact]
    public async Task One_failing_ad_does_not_stop_the_others()
    {
        Want("1", "2");
        var seek = new FakeDescriptions("seek", id => id == "1" ? throw new HttpRequestException("timeout") : Ad);

        var summary = Assert.Single(await Service(seek).FetchAsync(Ct));

        Assert.Equal((1, 1, false), (summary.Failed, summary.Saved, summary.Blocked));
    }

    [Fact]
    public async Task Turned_off_asks_nothing()
    {
        Want("1");
        var seek = new FakeDescriptions("seek", _ => Ad);

        Assert.Empty(await Service(seek, new DescriptionOptions { Enabled = false }).FetchAsync(Ct));
        Assert.Empty(seek.Asked);
    }
}
