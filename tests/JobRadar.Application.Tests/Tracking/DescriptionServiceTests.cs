using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Application.Matching;
using JobRadar.Application.Tests.Collection;
using JobRadar.Application.Tests.Matching;
using JobRadar.Application.Tracking;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Triage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Tracking;

public class DescriptionServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly ListingId Id = new("seek", "1");
    private static readonly string Ad = "We are hiring a C# developer to build integrations. " + new string('.', 60);

    private sealed class SavedScores : IListingScoreRepository
    {
        public List<ListingScore> Saved { get; } = [];

        public Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default)
        {
            Saved.AddRange(scores);
            return Task.CompletedTask;
        }

        public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListings _listings = new();
    private readonly InMemoryDescriptions _descriptions = new();
    private readonly SavedScores _scores = new();

    private DescriptionService Service()
    {
        var catalog = ProfileCatalogTests.Catalog(("tech", ProfileCatalogTests.Dotnet()));
        var scoring = new ListingScoringService(catalog, _listings, new InMemoryTerms(), _descriptions, _scores, _time,
            NullLogger<ListingScoringService>.Instance);
        return new DescriptionService(_listings, _descriptions, scoring, Options.Create(new CollectionOptions()), _time);
    }

    [Fact]
    public async Task The_text_is_stored_tidied_and_the_listing_scored_again_at_once()
    {
        await _listings.RecordObservationsAsync([Listings.Of("seek", "1", "C# Developer")], "c# developer @ perth wa", _time.GetUtcNow(), Ct);

        var result = await Service().CaptureAsync(Id, $"  {Ad}\r\n\r\n\r\n", DescriptionOrigin.Browser, Ct);

        var saved = _descriptions.Saved[Id];
        Assert.Equal((Ad.Trim(), DescriptionOrigin.Browser, _time.GetUtcNow()), (saved.Text, saved.Origin, saved.CapturedAt));
        Assert.Equal("tech", Assert.Single(result.Scores).Profile);
        Assert.Equal(result.Scores, _scores.Saved);
    }

    [Fact]
    public async Task A_listing_never_collected_or_a_text_too_short_is_refused()
    {
        await Assert.ThrowsAsync<ListingNotFoundException>(() => Service().CaptureAsync(Id, Ad, DescriptionOrigin.Pasted, Ct));
        await Assert.ThrowsAsync<DescriptionTooShortException>(() => Service().CaptureAsync(Id, "Apply now", DescriptionOrigin.Pasted, Ct));
        Assert.Empty(_descriptions.Saved);
    }
}
