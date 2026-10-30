using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Matching;

/// <summary>A profile ready to use: its scoring rules, its searches and the hash of its rules.</summary>
public sealed record ProfileDefinition(MatchingProfile Profile, IReadOnlyList<ListingSearch> Searches, string RulesHash)
{
    public string Name => Profile.Name;

    /// <summary>The market the profile searches and scores.</summary>
    public JobMarket Market => Profile.Market;

    /// <summary>
    /// Keys whose listings this profile scores: its searches, plus <see cref="ProfileCatalog.DirectKey"/> for
    /// listings attached to the profile without a search (imported from the prototype).
    /// </summary>
    public IReadOnlyCollection<string> SearchKeys { get; } = Searches.Select(s => s.Key)
        .Append(ProfileCatalog.DirectKey(Profile.Name))
        .ToHashSet(StringComparer.Ordinal);
}

/// <summary>The configured profiles, built once. Singleton: options are read at startup.</summary>
public sealed class ProfileCatalog
{
    public ProfileCatalog(IOptions<MatchingOptions> options, ScoringPolicy policy)
    {
        Policy = policy;
        Profiles = options.Value.Profiles
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => Build(p.Key, p.Value, policy))
            .ToList();
        Searches = Profiles.SelectMany(p => p.Searches).DistinctBy(s => s.Key).ToList();
    }

    public ScoringPolicy Policy { get; }

    /// <summary>Hit key for a listing attached to a profile directly, not found by one of its searches.</summary>
    public static string DirectKey(string profile) => $"profile: {profile.Trim().ToLowerInvariant()}";

    public IReadOnlyList<ProfileDefinition> Profiles { get; }

    /// <summary>Every search of every profile, once: a search shared by two profiles runs once.</summary>
    public IReadOnlyList<ListingSearch> Searches { get; }

    internal static ProfileDefinition Build(string name, ProfileOptions o, ScoringPolicy policy)
    {
        var written = string.IsNullOrWhiteSpace(o.Market) ? o.Country : o.Market;
        var market = string.IsNullOrWhiteSpace(written) ? JobMarket.AU
            : JobMarkets.Parse(written) ?? throw new InvalidOperationException(
                $"Profile '{name}': unknown market '{written}' (Matching:Profiles:{name}:Market). Use one of: {string.Join(", ", Enum.GetNames<JobMarket>())}.");
        var profile = new MatchingProfile(
            name, o.PositiveSignals, o.NegativeSignals, o.DisqualifySignals, o.QualifiedTitles,
            o.EntryLevelBoost, o.AllowSenior, o.PreferredLocations)
        {
            Requirements = Requirements(name, o.Requirements, policy),
            RewardRemote = o.RewardRemote,
            ExcludedLocations = o.ExcludedLocations,
            ExcludedTitles = o.ExcludedTitles,
            OnSiteLocations = o.OnSiteLocations,
            Market = market,
        };
        var searches = o.Keywords
            .SelectMany(k => o.Locations.Select(l => new ListingSearch(k, l, Market: market)))
            .DistinctBy(s => s.Key)
            .ToList();
        return new ProfileDefinition(profile, searches, RulesHash(profile, policy));
    }

    /// <summary>
    /// The phrases of the catalog items the profile picks, then its own phrases, each once. An unknown id
    /// stops the start: a typo would silently drop a penalty.
    /// </summary>
    private static ProfileRequirements Requirements(string profile, RequirementOptions o, ScoringPolicy policy)
    {
        var items = o.Items
            .Select(id => policy.Requirements.FirstOrDefault(i => string.Equals(i.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Profile '{profile}' asks for requirement '{id}', which is not in the catalog. " +
                    $"Known: {string.Join(", ", policy.Requirements.Select(i => i.Id))}."))
            .ToList();

        List<string> Phrases(RequirementKind kind, List<string> own) => items
            .Where(i => i.Kind == kind)
            .SelectMany(i => i.Phrases)
            .Concat(own)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProfileRequirements(
            o.Contexts,
            Phrases(RequirementKind.Experience, o.Experience),
            Phrases(RequirementKind.Qualification, o.Qualification),
            Phrases(RequirementKind.Licence, o.Licence));
    }

    /// <summary>
    /// Changes when the scorer version, a profile signal or a policy weight changes. Searches are
    /// left out: they change what is collected, not how it is scored.
    /// </summary>
    internal static string RulesHash(MatchingProfile profile, ScoringPolicy policy) =>
        Hash(new { FitScorer.RulesVersion, profile, policy });

    /// <summary>
    /// The profile's rules hash with the candidate's states on its requirements: the same hash while every
    /// state the profile searches is at its default, so setting nothing changes nothing.
    /// </summary>
    internal static string RulesHash(ProfileDefinition profile, CandidateRequirements candidate, ScoringPolicy policy)
    {
        var states = candidate.Relevant(profile.Profile, policy);
        double? years = candidate.ExperienceYears.TryGetValue(profile.Name, out var y) ? y : null;
        return states.Count > 0 || years is not null ? Hash(new { profile.RulesHash, states, years }) : profile.RulesHash;
    }

    private static string Hash(object rules) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rules))))[..16];
}
