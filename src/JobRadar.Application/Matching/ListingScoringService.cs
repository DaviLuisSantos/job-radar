using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Matching;

/// <summary>
/// Scores, for each profile, every listing its searches found. Runs every day after collection:
/// a listing's age is part of its score, so yesterday's scores are stale even if nothing else changed.
/// Scores the pass did not write are deleted: a listing whose searches left the profile (or a profile
/// that left the configuration) would otherwise sit in triage with a frozen score.
/// </summary>
public sealed class ListingScoringService(
    ProfileCatalog catalog,
    IListingRepository listings,
    IListingTermRepository terms,
    IListingDescriptionRepository descriptions,
    IListingScoreRepository scores,
    TimeProvider time,
    ILogger<ListingScoringService> log)
{
    public async Task ScoreAllAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = Today(now, zone);

        foreach (var profile in catalog.Profiles)
        {
            var found = await listings.ListFoundByAsync(profile.SearchKeys, ct);
            var ids = found.Select(l => l.Id).ToList();
            var termsById = await terms.GetTermsAsync(ids, ct);
            var textsById = await descriptions.GetTextsAsync(ids, ct);
            // Pure and thread-safe: score on every core.
            var scored = found
                .AsParallel()
                .Select(l => Score(l, termsById.GetValueOrDefault(l.Id), textsById.GetValueOrDefault(l.Id), profile, today, now))
                .ToList();
            await scores.SaveAsync(scored, ct);

            log.LogInformation(
                "Scored {Count} listings for {Profile}: {Excluded} excluded, {Strong} at 70 or more",
                scored.Count, profile.Name, scored.Count(s => s.Score.Excluded), scored.Count(s => s.Score.Final >= 70));
        }

        // Every score this pass wrote carries `now`; anything older is an orphan.
        var orphans = await scores.DeleteScoredBeforeAsync(now, ct);
        if (orphans > 0)
            log.LogInformation("Deleted {Count} scores no profile search backs any more", orphans);
    }

    /// <summary>
    /// Scores one listing now, for every profile whose searches found it (after its text changed,
    /// without waiting for the daily pass). Empty when the listing was never collected.
    /// </summary>
    public async Task<IReadOnlyList<ListingScore>> ScoreOneAsync(ListingId id, TimeZoneInfo zone, CancellationToken ct)
    {
        if (await listings.FindAsync(id, ct) is not { } record)
            return [];

        var keys = await listings.GetSearchKeysAsync(id, ct);
        var now = time.GetUtcNow();
        var listingTerms = (await terms.GetTermsAsync([id], ct)).GetValueOrDefault(id);
        var text = (await descriptions.GetTextsAsync([id], ct)).GetValueOrDefault(id);
        var scored = catalog.Profiles
            .Where(p => keys.Any(p.SearchKeys.Contains))
            .Select(p => Score(record.Listing, listingTerms, text, p, Today(now, zone), now))
            .ToList();
        await scores.SaveAsync(scored, ct);
        return scored;
    }

    /// <summary>
    /// The judged listings scored again with the rules of this build, nothing saved: measures a rule
    /// change against the labels before it ships (<c>calibrate --rescore</c>). A listing whose profile left
    /// the configuration, or that was never collected, is left out.
    /// </summary>
    public async Task<IReadOnlyList<JudgedScore>> RescoreAsync(IReadOnlyList<JudgedScore> judged, TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = Today(now, zone);
        var ids = judged.Select(j => j.Id).Distinct().ToList();
        var termsById = await terms.GetTermsAsync(ids, ct);
        var textsById = await descriptions.GetTextsAsync(ids, ct);
        var profiles = catalog.Profiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var rescored = new List<JudgedScore>();
        foreach (var j in judged)
        {
            if (!profiles.TryGetValue(j.Profile, out var profile) || await listings.FindAsync(j.Id, ct) is not { } record)
                continue;
            var input = ScoringInput.From(record.Listing, termsById.GetValueOrDefault(j.Id), textsById.GetValueOrDefault(j.Id));
            rescored.Add(j with { Score = FitScorer.Score(input, profile.Profile, catalog.Policy, today) });
        }
        return rescored;
    }

    private ListingScore Score(
        Listing listing, IReadOnlyCollection<string>? listingTerms, string? text, ProfileDefinition profile, DateOnly today, DateTimeOffset now) =>
        new(listing.Id, profile.Name,
            FitScorer.Score(ScoringInput.From(listing, listingTerms, text), profile.Profile, catalog.Policy, today),
            profile.RulesHash, now);

    private static DateOnly Today(DateTimeOffset now, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
}
