using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Matching;

/// <summary>A catalog requirement, the candidate's state on it and the profiles that search it.</summary>
/// <param name="IsDefault">No state set: the default of its kind.</param>
public sealed record RequirementStatus(RequirementItem Item, RequirementState State, bool IsDefault, IReadOnlyList<string> Profiles);

/// <summary>A state the candidate facts set, and the fact that set it.</summary>
/// <param name="From">The state set before, null when the item was at its default.</param>
public sealed record RequirementChange(string ItemId, RequirementState? From, RequirementState To, string Fact);

/// <summary>What an import of the candidate facts set (or would set), and the facts no catalog item matches.</summary>
public sealed record CandidateImportReport(IReadOnlyList<RequirementChange> Changes, IReadOnlyList<string> NotInCatalog);

/// <summary>What holding one catalog item would change in one profile's listings (see <c>ListingScoringService.ImpactAsync</c>).</summary>
/// <param name="Affected">Listings whose score moves.</param>
/// <param name="VisibleWithout">Of those, at or above the visible threshold without the item; <paramref name="VisibleWith"/>, with it.</param>
/// <param name="AlertWithout">Of those, at or above the alert threshold without the item; <paramref name="AlertWith"/>, with it.</param>
/// <param name="Good">Of those, the ones the candidate judged good; <paramref name="Bad"/>, bad.</param>
public sealed record RequirementImpact(
    string ItemId, string Profile, int Affected, int VisibleWithout, int VisibleWith, int AlertWithout, int AlertWith, int Good, int Bad);

public sealed class UnknownRequirementException(string id, IEnumerable<string> known)
    : Exception($"'{id}' is not in the requirement catalog. Known: {string.Join(", ", known)}.");

/// <summary>
/// The candidate's state on each catalog requirement: listed, set one by one or imported from the candidate facts,
/// with what holding each would change. A change rescores at once the listings where the item may be asked; the
/// rest of each profile follows in the next daily pass, which changes nothing else in them.
/// </summary>
public sealed class CandidateRequirementService(
    ProfileCatalog catalog,
    ICandidateRequirementRepository repository,
    ListingScoringService scoring,
    IOptions<CollectionOptions> collection,
    TimeProvider time)
{
    private TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(collection.Value.TimeZone);

    /// <summary>What holding each item would change, per profile (<see cref="ListingScoringService.ImpactAsync"/>).</summary>
    public Task<IReadOnlyList<RequirementImpact>> ImpactAsync(CancellationToken ct = default) => scoring.ImpactAsync(Zone, ct);

    public async Task<IReadOnlyList<RequirementStatus>> ListAsync(CancellationToken ct = default)
    {
        var candidate = await repository.GetAsync(ct);
        return catalog.Policy.Requirements
            .Select(i => new RequirementStatus(
                i,
                candidate.States.TryGetValue(i.Id, out var state) ? state : CandidateRequirements.Default(i.Kind, i.Phrases[0]),
                !candidate.States.ContainsKey(i.Id),
                catalog.Profiles
                    .Where(p => p.Profile.Requirements.Terms().Any(t => i.Phrases.Contains(t.Phrase, StringComparer.OrdinalIgnoreCase)))
                    .Select(p => p.Name)
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Sets the state of an item (null clears it back to the default of its kind) and rescores the listings where it
    /// may be asked. Returns the item and how many scores were saved.
    /// </summary>
    public async Task<(RequirementItem Item, int Rescored)> SetAsync(string id, RequirementState? state, CancellationToken ct = default)
    {
        var item = Find(id) ?? throw new UnknownRequirementException(id, catalog.Policy.Requirements.Select(i => i.Id));
        await repository.SetAsync(item.Id, state, time.GetUtcNow(), ct);
        return (item, await scoring.RescoreRequirementAsync(item.Id, Zone, ct));
    }

    /// <summary>
    /// Sets the states the candidate facts imply: tickets as held, obtainable or out of reach, heavy vehicle
    /// classes held, and a trade or degree recognised in Australia. Additive: a state the facts say nothing
    /// about is kept. Experience is not read here: years against what an ad asks are a rule of their own.
    /// </summary>
    public async Task<CandidateImportReport> ImportAsync(CandidateFacts facts, bool dryRun, CancellationToken ct = default)
    {
        var implied = new List<(string Name, RequirementState State, string Fact)>();
        // Most cautious first, so a ticket listed twice ends with what the candidate holds.
        implied.AddRange(facts.TicketsOutOfReach.Select(n => (n, RequirementState.OutOfReach, $"ticket out of reach: {n}")));
        implied.AddRange(facts.TicketsObtainable.Select(n => (n, RequirementState.Obtainable, $"ticket obtainable: {n}")));
        implied.AddRange(facts.TicketsHeld.Select(n => (n, RequirementState.Have, $"ticket held: {n}")));
        implied.AddRange(facts.HeavyLicences.Select(c => ($"truck-{c.ToLowerInvariant()}", RequirementState.Have, $"heavy licence held: {c}")));
        implied.AddRange(facts.Education.Where(e => e.RecognisedInAustralia).SelectMany(e => e.Level switch
        {
            "trade" => [("trade-qualification", RequirementState.Have, "trade recognised in Australia")],
            "bachelor" or "postgraduate" => [("degree", RequirementState.Have, $"{e.Level} recognised in Australia")],
            _ => Array.Empty<(string, RequirementState, string)>(),
        }));

        var current = await repository.GetAsync(ct);
        var wanted = new Dictionary<string, (RequirementItem Item, RequirementState State, string Fact)>(StringComparer.Ordinal);
        var notInCatalog = new List<string>();
        foreach (var (name, state, fact) in implied)
        {
            if (Find(name) is { } item)
                wanted[item.Id] = (item, state, fact);
            else
                notInCatalog.Add(fact);
        }

        // A fact that only repeats the current state (set, or the default of an item never set) changes nothing.
        var changes = wanted
            .Where(w => (current.States.TryGetValue(w.Key, out var s) ? s : CandidateRequirements.Default(w.Value.Item.Kind, w.Value.Item.Phrases[0]))
                        != w.Value.State)
            .Select(w => new RequirementChange(w.Key, current.States.TryGetValue(w.Key, out var s) ? s : null, w.Value.State, w.Value.Fact))
            .OrderBy(c => c.ItemId, StringComparer.Ordinal)
            .ToList();
        if (!dryRun)
        {
            var now = time.GetUtcNow();
            foreach (var change in changes)
                await repository.SetAsync(change.ItemId, change.To, now, ct);
            foreach (var change in changes)
                await scoring.RescoreRequirementAsync(change.ItemId, Zone, ct);
        }
        return new CandidateImportReport(changes, notInCatalog);
    }

    /// <summary>A catalog item by its id or by one of its phrases ("HRWL", "ARCtick"), ignoring case.</summary>
    private RequirementItem? Find(string name)
    {
        var key = name.Trim();
        return catalog.Policy.Requirements.FirstOrDefault(i => string.Equals(i.Id, key, StringComparison.OrdinalIgnoreCase))
               ?? catalog.Policy.Requirements.FirstOrDefault(i => i.Phrases.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
