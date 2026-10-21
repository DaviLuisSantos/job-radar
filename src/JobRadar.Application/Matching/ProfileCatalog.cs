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

    public IReadOnlyCollection<string> SearchKeys { get; } = Searches.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
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

    public IReadOnlyList<ProfileDefinition> Profiles { get; }

    /// <summary>Every search of every profile, once: a search shared by two profiles runs once.</summary>
    public IReadOnlyList<ListingSearch> Searches { get; }

    internal static ProfileDefinition Build(string name, ProfileOptions o, ScoringPolicy policy)
    {
        var profile = new MatchingProfile(
            name, o.PositiveSignals, o.NegativeSignals, o.DisqualifySignals, o.QualifiedTitles,
            o.EntryLevelBoost, o.AllowSenior, o.PreferredLocations);
        var searches = o.Keywords
            .SelectMany(k => o.Locations.Select(l => new ListingSearch(k, l)))
            .DistinctBy(s => s.Key)
            .ToList();
        return new ProfileDefinition(profile, searches, RulesHash(profile, policy));
    }

    /// <summary>
    /// Changes when the scorer version, a profile signal or a policy weight changes. Searches are
    /// left out: they change what is collected, not how it is scored.
    /// </summary>
    internal static string RulesHash(MatchingProfile profile, ScoringPolicy policy)
    {
        var json = JsonSerializer.Serialize(new { FitScorer.RulesVersion, profile, policy });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
    }
}
