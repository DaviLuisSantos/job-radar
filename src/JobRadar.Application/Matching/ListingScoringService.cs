using JobRadar.Application.Abstractions;
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
    IListingScoreRepository scores,
    TimeProvider time,
    ILogger<ListingScoringService> log)
{
    public async Task ScoreAllAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        foreach (var profile in catalog.Profiles)
        {
            var found = await listings.ListFoundByAsync(profile.SearchKeys, ct);
            var termsById = await terms.GetTermsAsync(found.Select(l => l.Id).ToList(), ct);
            // Pure and thread-safe: score on every core.
            var scored = found
                .AsParallel()
                .Select(l => new ListingScore(
                    l.Id, profile.Name,
                    FitScorer.Score(ScoringInput.From(l, termsById.GetValueOrDefault(l.Id)), profile.Profile, catalog.Policy, today),
                    profile.RulesHash, now))
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
}
