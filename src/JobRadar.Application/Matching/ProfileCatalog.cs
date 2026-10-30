using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Matching;

/// <summary>A profile ready to use: its scoring rules, its market's rules, its searches and the hash of its rules.</summary>
public sealed record ProfileDefinition(MatchingProfile Profile, MarketRules Rules, IReadOnlyList<ListingSearch> Searches, string RulesHash)
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
        Markets = BuildMarkets(options.Value);
        Profiles = options.Value.Profiles
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => Build(p.Key, p.Value, policy, options.Value.Compensation, Markets[MarketOf(p.Key, p.Value)]))
            .ToList();
        Searches = Profiles.SelectMany(p => p.Searches).DistinctBy(s => s.Key).ToList();
    }

    public ScoringPolicy Policy { get; }

    /// <summary>The rules of every market a profile uses or the configuration describes, built once.</summary>
    public IReadOnlyDictionary<JobMarket, MarketRules> Markets { get; }

    /// <summary>The rules a market's profiles are scored with: <see cref="Markets"/>, or the market's own without configuration.</summary>
    public MarketRules RulesOf(JobMarket market) => Markets.TryGetValue(market, out var rules) ? rules : MarketRules.For(market);

    /// <summary>
    /// Each market's rules, from its configured eligibility block: every market a profile uses, and every market with a
    /// block, so a block with a typo stops the start even before a profile uses it.
    /// </summary>
    internal static IReadOnlyDictionary<JobMarket, MarketRules> BuildMarkets(MatchingOptions o)
    {
        var configured = new Dictionary<JobMarket, EligibilityRules>();
        foreach (var (key, value) in o.Markets)
        {
            var market = JobMarkets.Parse(key) ?? throw new InvalidOperationException(
                $"Matching:Markets:{key}: unknown market. Use one of: {string.Join(", ", Enum.GetNames<JobMarket>())}.");
            if (value.Eligibility is { } eligibility)
                configured[market] = eligibility.ToRules();
        }
        var unready = o.Profiles
            .Where(p => MarketRules.RequiresEligibility(MarketOf(p.Key, p.Value)))
            .GroupBy(p => MarketOf(p.Key, p.Value))
            .FirstOrDefault(g => !configured.TryGetValue(g.Key, out var block) || block.IsEmpty());
        if (unready is not null)
            throw new InvalidOperationException(
                $"Profiles {string.Join(", ", unready.Select(p => $"'{p.Key}'").Order(StringComparer.Ordinal))} are in market {unready.Key}, "
                + $"which has no eligibility rules. Set Matching:Markets:{unready.Key}:Eligibility in appsettings.Local.json "
                + "(shape in appsettings.Local.example.json).");
        return o.Profiles.Select(p => MarketOf(p.Key, p.Value))
            .Concat(configured.Keys)
            .Distinct()
            .ToDictionary(m => m, m => MarketRules.For(m, configured.GetValueOrDefault(m)));
    }

    /// <summary>Hit key for a listing attached to a profile directly, not found by one of its searches.</summary>
    public static string DirectKey(string profile) => $"profile: {profile.Trim().ToLowerInvariant()}";

    public IReadOnlyList<ProfileDefinition> Profiles { get; }

    /// <summary>Every search of every profile, once: a search shared by two profiles runs once.</summary>
    public IReadOnlyList<ListingSearch> Searches { get; }

    private static JobMarket MarketOf(string name, ProfileOptions o)
    {
        var written = string.IsNullOrWhiteSpace(o.Market) ? o.Country : o.Market;
        return string.IsNullOrWhiteSpace(written) ? JobMarket.AU
            : JobMarkets.Parse(written) ?? throw new InvalidOperationException(
                $"Profile '{name}': unknown market '{written}' (Matching:Profiles:{name}:Market). Use one of: {string.Join(", ", Enum.GetNames<JobMarket>())}.");
    }

    /// <param name="rules">The rules of the profile's market; none: the market's own, without configuration.</param>
    internal static ProfileDefinition Build(
        string name, ProfileOptions o, ScoringPolicy policy, CompensationOptions? compensation = null, MarketRules? rules = null)
    {
        var market = MarketOf(name, o);
        rules ??= MarketRules.For(market);
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
            Compensation = Compensation(o.Compensation, compensation ?? new CompensationOptions()),
        };
        var searches = o.Keywords
            .SelectMany(k => o.Locations.Select(l => new ListingSearch(k, l, Market: market)))
            .DistinctBy(s => s.Key)
            .ToList();
        return new ProfileDefinition(profile, rules, searches, RulesHash(profile, policy, rules.Eligibility));
    }

    private static Compensation? Compensation(ProfileCompensationOptions? o, CompensationOptions numbers)
    {
        static PayBand? Band(PayBandOptions? b) =>
            b is null ? null : new PayBand(b.Currency.Trim().ToUpperInvariant(), b.MonthlyFloor, b.MonthlyTarget);
        return o is null || (o.Contractor is null && o.Eor is null)
            ? null
            : new Compensation(Band(o.Contractor), Band(o.Eor), numbers.UsdToBrl, numbers.HoursPerMonth);
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
    /// Changes when the scorer version, a profile signal, a policy weight or the market's eligibility block changes.
    /// Searches are left out: they change what is collected, not how it is scored.
    /// </summary>
    internal static string RulesHash(MatchingProfile profile, ScoringPolicy policy, EligibilityRules eligibility) =>
        Hash(new { FitScorer.RulesVersion, profile, policy, eligibility });

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
