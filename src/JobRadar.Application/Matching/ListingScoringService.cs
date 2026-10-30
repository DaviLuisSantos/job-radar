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
    ICandidateRequirementRepository requirements,
    TimeProvider time,
    ILogger<ListingScoringService> log)
{
    public async Task ScoreAllAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = Today(now, zone);
        var candidate = await requirements.GetAsync(ct);

        foreach (var profile in catalog.Profiles)
        {
            var found = await listings.ListFoundByAsync(profile.SearchKeys, ct);
            var ids = found.Select(l => l.Id).ToList();
            var termsById = await terms.GetTermsAsync(ids, ct);
            var textsById = await descriptions.GetTextsAsync(ids, ct);
            // Pure and thread-safe: score on every core.
            var scored = found
                .AsParallel()
                .Select(l => Score(l, termsById.GetValueOrDefault(l.Id), textsById.GetValueOrDefault(l.Id), profile, candidate, today, now))
                .ToList();
            await scores.SaveAsync(scored, ct);

            log.LogInformation(
                "Scored {Count} listings for {Profile}: {Excluded} excluded, {Strong} at 70 or more",
                scored.Count, profile.Name, scored.Count(s => s.Score.Excluded), scored.Count(s => s.Score.Final >= 70));
            // The scorer is pure: what it could not read about pay is said here, once per pass.
            var unreadable = scored.Where(s => s.Score.Items.Any(i => i.Rule == ScoreRule.PayUnreadable)).Select(s => s.Id.ToString()).ToList();
            if (unreadable.Count > 0)
                log.LogInformation("Pay unreadable in {Count} listings for {Profile}: {Listings}", unreadable.Count, profile.Name, string.Join(", ", unreadable));
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
        var candidate = await requirements.GetAsync(ct);
        var scored = catalog.Profiles
            .Where(p => keys.Any(p.SearchKeys.Contains))
            .Select(p => Score(record.Listing, listingTerms, text, p, candidate, Today(now, zone), now))
            .ToList();
        await scores.SaveAsync(scored, ct);
        return scored;
    }

    /// <summary>
    /// The judged listings scored again with the rules of this build, nothing saved: measures a rule
    /// change against the labels before it ships (<c>calibrate --rescore</c>). A listing whose profile left
    /// the configuration, or that was never collected, is left out. <paramref name="assume"/> replaces the
    /// candidate's stored state on those catalog items (null: the default), to measure a state before setting it.
    /// </summary>
    public async Task<IReadOnlyList<JudgedScore>> RescoreAsync(
        IReadOnlyList<JudgedScore> judged, TimeZoneInfo zone, CancellationToken ct,
        CandidateAssumption? assume = null)
    {
        var now = time.GetUtcNow();
        var today = Today(now, zone);
        var ids = judged.Select(j => j.Id).Distinct().ToList();
        var termsById = await terms.GetTermsAsync(ids, ct);
        var textsById = await descriptions.GetTextsAsync(ids, ct);
        var profiles = catalog.Profiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var candidate = Assume(await requirements.GetAsync(ct), assume ?? CandidateAssumption.None);

        var rescored = new List<JudgedScore>();
        foreach (var j in judged)
        {
            if (!profiles.TryGetValue(j.Profile, out var profile) || await listings.FindAsync(j.Id, ct) is not { } record)
                continue;
            var input = ScoringInput.From(record.Listing, termsById.GetValueOrDefault(j.Id), textsById.GetValueOrDefault(j.Id));
            rescored.Add(j with { Score = FitScorer.Score(input, profile.Profile, profile.Rules, catalog.Policy, today, candidate) });
        }
        return rescored;
    }

    /// <summary>
    /// What holding each catalog item would change, per profile, scored in memory and saved nowhere: the listings
    /// whose score moves, and how many of them reach the visible and alert thresholds without and with it. Without
    /// is the item at the candidate's state, or at its default when they already hold it, so a held item shows
    /// what it gives. An item counts for the profiles that search it; a truck class, for every profile, since the
    /// ad's own text is read for truck licences whatever the profile searches.
    /// </summary>
    public async Task<IReadOnlyList<RequirementImpact>> ImpactAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var today = Today(time.GetUtcNow(), zone);
        var candidate = await requirements.GetAsync(ct);
        var verdicts = (await scores.ListJudgedAsync(ct))
            .GroupBy(j => (j.Id, j.Profile))
            .ToDictionary(g => g.Key, g => g.First().Verdict);

        var impacts = new List<RequirementImpact>();
        foreach (var profile in catalog.Profiles)
        {
            var items = catalog.Policy.Requirements.Where(i => Concerns(i, profile)).ToList();
            if (items.Count == 0)
                continue;
            var inputs = await InputsAsync(profile, ct);
            foreach (var item in items)
            {
                var stored = candidate.States.TryGetValue(item.Id, out var s) ? s : (RequirementState?)null;
                var without = With(candidate, item.Id, stored is RequirementState.Have ? null : stored);
                var with = With(candidate, item.Id, RequirementState.Have);
                var moved = inputs
                    .Where(i => item.MayBeAskedIn(i.Input))
                    .AsParallel()
                    .Select(i => (i.Id,
                        Without: FitScorer.Score(i.Input, profile.Profile, profile.Rules, catalog.Policy, today, without).Final,
                        With: FitScorer.Score(i.Input, profile.Profile, profile.Rules, catalog.Policy, today, with).Final))
                    .Where(r => r.Without != r.With)
                    .ToList();
                var judged = moved.Select(m => verdicts.TryGetValue((m.Id, profile.Name), out var v) ? v : (Domain.Tracking.Verdict?)null).ToList();
                impacts.Add(new RequirementImpact(
                    item.Id, profile.Name, moved.Count,
                    moved.Count(m => m.Without >= Calibration.VisibleThreshold), moved.Count(m => m.With >= Calibration.VisibleThreshold),
                    moved.Count(m => m.Without >= Calibration.AlertThreshold), moved.Count(m => m.With >= Calibration.AlertThreshold),
                    judged.Count(v => v == Domain.Tracking.Verdict.Good), judged.Count(v => v == Domain.Tracking.Verdict.Bad)));
            }
        }
        return impacts;
    }

    /// <summary>
    /// Scores again, with the candidate's current states and years, every listing of one profile: after the
    /// candidate changed their years in it, which any ad stating years may weigh. Returns how many were saved.
    /// </summary>
    public async Task<int> RescoreProfileAsync(string profileName, TimeZoneInfo zone, CancellationToken ct)
    {
        var profile = catalog.Profiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnknownProfileException(profileName, catalog.Profiles.Select(p => p.Name));
        var now = time.GetUtcNow();
        var today = Today(now, zone);
        var candidate = await requirements.GetAsync(ct);
        var hash = ProfileCatalog.RulesHash(profile, candidate, catalog.Policy);
        var scored = (await InputsAsync(profile, ct))
            .AsParallel()
            .Select(i => new ListingScore(i.Id, profile.Name, FitScorer.Score(i.Input, profile.Profile, profile.Rules, catalog.Policy, today, candidate), hash, now))
            .ToList();
        await scores.SaveAsync(scored, ct);
        return scored.Count;
    }

    /// <summary>
    /// Scores again, with the candidate's current states, the listings where <paramref name="itemId"/> may be
    /// asked: after the candidate changed their state on it, so triage shows it now instead of after the next
    /// daily pass. Returns how many scores were saved.
    /// </summary>
    public async Task<int> RescoreRequirementAsync(string itemId, TimeZoneInfo zone, CancellationToken ct)
    {
        var item = catalog.Policy.Requirements.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnknownRequirementException(itemId, catalog.Policy.Requirements.Select(i => i.Id));
        var now = time.GetUtcNow();
        var today = Today(now, zone);
        var candidate = await requirements.GetAsync(ct);
        var saved = 0;
        foreach (var profile in catalog.Profiles.Where(p => Concerns(item, p)))
        {
            var hash = ProfileCatalog.RulesHash(profile, candidate, catalog.Policy);
            var scored = (await InputsAsync(profile, ct))
                .Where(i => item.MayBeAskedIn(i.Input))
                .Select(i => new ListingScore(i.Id, profile.Name, FitScorer.Score(i.Input, profile.Profile, profile.Rules, catalog.Policy, today, candidate), hash, now))
                .ToList();
            await scores.SaveAsync(scored, ct);
            saved += scored.Count;
        }
        return saved;
    }

    private static bool Concerns(RequirementItem item, ProfileDefinition profile) =>
        item.IsTruckLicence
        || profile.Profile.Requirements.Terms().Any(t => item.Phrases.Contains(t.Phrase, StringComparer.OrdinalIgnoreCase));

    private static CandidateRequirements With(CandidateRequirements candidate, string itemId, RequirementState? state)
    {
        var states = new Dictionary<string, RequirementState>(candidate.States, StringComparer.OrdinalIgnoreCase);
        if (state is { } s)
            states[itemId] = s;
        else
            states.Remove(itemId);
        return candidate with { States = states };
    }

    private async Task<IReadOnlyList<(ListingId Id, ScoringInput Input)>> InputsAsync(ProfileDefinition profile, CancellationToken ct)
    {
        var found = await listings.ListFoundByAsync(profile.SearchKeys, ct);
        var ids = found.Select(l => l.Id).ToList();
        var termsById = await terms.GetTermsAsync(ids, ct);
        var textsById = await descriptions.GetTextsAsync(ids, ct);
        return found.Select(l => (l.Id, ScoringInput.From(l, termsById.GetValueOrDefault(l.Id), textsById.GetValueOrDefault(l.Id)))).ToList();
    }

    private CandidateRequirements Assume(CandidateRequirements stored, CandidateAssumption assume)
    {
        if (assume.States.Count == 0 && assume.Years.Count == 0)
            return stored;
        var years = new Dictionary<string, double>(stored.ExperienceYears, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in assume.Years)
        {
            var profile = catalog.Profiles.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new UnknownProfileException(name, catalog.Profiles.Select(p => p.Name));
            if (value is { } v)
                years[profile.Name] = v;
            else
                years.Remove(profile.Name);
        }
        var states = new Dictionary<string, RequirementState>(stored.States, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, state) in assume.States)
        {
            var item = catalog.Policy.Requirements.FirstOrDefault(i => string.Equals(i.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new UnknownRequirementException(id, catalog.Policy.Requirements.Select(i => i.Id));
            if (state is { } s)
                states[item.Id] = s;
            else
                states.Remove(item.Id);
        }
        return new CandidateRequirements(states) { ExperienceYears = years };
    }

    private ListingScore Score(
        Listing listing, IReadOnlyCollection<string>? listingTerms, string? text, ProfileDefinition profile,
        CandidateRequirements candidate, DateOnly today, DateTimeOffset now) =>
        new(listing.Id, profile.Name,
            FitScorer.Score(ScoringInput.From(listing, listingTerms, text), profile.Profile, profile.Rules, catalog.Policy, today, candidate),
            ProfileCatalog.RulesHash(profile, candidate, catalog.Policy), now);

    private static DateOnly Today(DateTimeOffset now, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
}
