using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The words of one job market that <see cref="FitScorer"/> reads: its language, who may apply, its seniority words,
/// its contracts. The scorer is one, with one order of phases for every country; what a phrase means is the
/// market's. Weights stay in <see cref="ScoringPolicy"/>. Who may apply is data (<see cref="EligibilityRules"/>, from
/// the local configuration); the rest is code.
/// </summary>
public sealed record MarketRules
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    public required JobMarket Market { get; init; }

    /// <summary>The eligibility block these rules were built from, normalised: in the rules hash, and what <c>eligibility</c> prints.</summary>
    public EligibilityRules Eligibility { get; init; } = EligibilityRules.None;

    /// <summary>Phrases searched in the full ad, by <see cref="FullTextGroup"/>: the code's groups and the market's eligibility phrases.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FullTextGroups { get; init; } = FullTextGroup.Code;

    // Exclusions.
    public required Regex Restricted { get; init; }
    public required Regex Incompatible { get; init; }
    public required Regex ReservedTitle { get; init; }
    public required Regex LeadershipTitle { get; init; }
    public required Regex SeniorRank { get; init; }
    public required Regex MidTitle { get; init; }
    public required Regex JuniorTitle { get; init; }

    // Points.
    public required Regex AssistanceDenied { get; init; }
    public required IReadOnlyList<string> AssistanceOffered { get; init; }
    /// <summary>A substring that alone earns the assistance bonus (<see cref="EligibilityRules.AssistanceWord"/>).</summary>
    public string? AssistanceWord { get; init; }
    public required string Remote { get; init; }
    public required string Hybrid { get; init; }
    public required IReadOnlyList<string> Fifo { get; init; }
    public required IReadOnlyList<string> NoExperience { get; init; }
    public required Regex Welcome { get; init; }
    public required Regex FlexibleWorkType { get; init; }

    // Restrictions and the qualification cap.
    public required Regex TrainingTitle { get; init; }
    public required Regex EntryTitle { get; init; }
    public required IReadOnlyList<string> QualificationWords { get; init; }
    public required Regex LongContract { get; init; }
    public required Regex CommissionOnly { get; init; }
    public required Regex TruckLicence { get; init; }
    public required Func<string, (string Value, bool Desirable)?> TruckLicenceAsked { get; init; }
    public required Func<string, int?> YearsAsked { get; init; }
    /// <summary>English as a condition of the job, where the ads are not in English.</summary>
    public required Regex EnglishRequired { get; init; }
    /// <summary>Work at the employer's site, for <see cref="MatchingProfile.OnSiteLocations"/>.</summary>
    public required Regex OnSite { get; init; }

    // Remote markets: who may apply, and how they are hired. Empty everywhere else.
    /// <summary>
    /// The eligibility this market takes (<see cref="RemoteEligibility"/>): worldwide for the global market, a region
    /// with Brazil for LATAM. Null: not a remote market, eligibility is not read.
    /// </summary>
    public EligibilityScope? EligibleScope { get; init; }
    /// <summary>
    /// Residence or work authorisation elsewhere as a condition ("US only"): excludes. The code's phrases
    /// (<see cref="RemotePatterns.ResidencyRestricted"/>) and the market's <see cref="EligibilityRules.RestrictedPattern"/>.
    /// </summary>
    public Regex ResidencyRestricted { get; init; } = BrazilPatterns.Never;
    public IReadOnlyList<string> ContractorPhrases { get; init; } = [];
    public IReadOnlyList<string> EorPhrases { get; init; } = [];
    public IReadOnlyList<string> DirectHirePhrases { get; init; } = [];
    public IReadOnlyList<string> HiringPlatforms { get; init; } = [];

    /// <summary>
    /// The rules ported from the prototype: English, Australian licences, and who may apply as the market's
    /// <paramref name="eligibility"/> says.
    /// </summary>
    public static MarketRules Australia(EligibilityRules eligibility)
    {
        var e = Validated(eligibility, JobMarket.AU);
        return new()
        {
            Market = JobMarket.AU,
            Eligibility = e,
            FullTextGroups = Groups(e),
            Restricted = Compile(e.RestrictedPattern, JobMarket.AU, nameof(EligibilityRules.RestrictedPattern)),
            Incompatible = Compile(e.IncompatiblePattern, JobMarket.AU, nameof(EligibilityRules.IncompatiblePattern)),
            ReservedTitle = Compile(e.ReservedTitlePattern, JobMarket.AU, nameof(EligibilityRules.ReservedTitlePattern)),
            LeadershipTitle = ListingPatterns.LeadershipTitle,
            SeniorRank = ListingPatterns.SeniorRank,
            MidTitle = ListingPatterns.MidTitle,
            JuniorTitle = ListingPatterns.JuniorTitle,
            AssistanceDenied = Compile(e.AssistanceDeniedPattern, JobMarket.AU, nameof(EligibilityRules.AssistanceDeniedPattern)),
            AssistanceOffered = e.AssistanceOfferedPhrases,
            AssistanceWord = e.AssistanceWord,
            Remote = "remote",
            Hybrid = "hybrid",
            Fifo = ["fifo", "fly-in fly-out"],
            NoExperience = ListingPatterns.NoExperience,
            Welcome = Compile(e.WelcomePattern, JobMarket.AU, nameof(EligibilityRules.WelcomePattern)),
            FlexibleWorkType = ListingPatterns.FlexibleWorkType,
            TrainingTitle = ListingPatterns.TrainingTitle,
            EntryTitle = ListingPatterns.EntryTitle,
            QualificationWords = ListingPatterns.QualificationWords,
            LongContract = ListingPatterns.LongContract,
            CommissionOnly = ListingPatterns.CommissionOnly,
            TruckLicence = ListingPatterns.TruckLicence,
            TruckLicenceAsked = ListingPatterns.TruckLicenceAsked,
            YearsAsked = ListingPatterns.YearsAsked,
            EnglishRequired = BrazilPatterns.Never,
            OnSite = BrazilPatterns.Never,
        };
    }

    /// <summary>
    /// Portuguese, CLT or PJ, no eligibility to read: what is Australian (licences, FIFO, short engagements) never
    /// matches. Written for this project and not yet calibrated.
    /// </summary>
    public static MarketRules Brazil { get; } = new()
    {
        Market = JobMarket.BR,
        Restricted = BrazilPatterns.Never,
        Incompatible = BrazilPatterns.Never,
        ReservedTitle = BrazilPatterns.Never,
        LeadershipTitle = BrazilPatterns.LeadershipTitle,
        SeniorRank = BrazilPatterns.SeniorRank,
        MidTitle = BrazilPatterns.MidTitle,
        JuniorTitle = BrazilPatterns.JuniorTitle,
        AssistanceDenied = BrazilPatterns.Never,
        AssistanceOffered = [],
        Remote = "remoto",
        Hybrid = "híbrido",
        Fifo = [],
        NoExperience = BrazilPatterns.NoExperience,
        Welcome = BrazilPatterns.Never,
        FlexibleWorkType = BrazilPatterns.Never,
        TrainingTitle = BrazilPatterns.TrainingTitle,
        EntryTitle = BrazilPatterns.TrainingTitle,
        QualificationWords = ["certifica"],
        LongContract = BrazilPatterns.Never,
        CommissionOnly = BrazilPatterns.CommissionOnly,
        TruckLicence = BrazilPatterns.Never,
        TruckLicenceAsked = _ => null,
        YearsAsked = BrazilPatterns.YearsAsked,
        EnglishRequired = BrazilPatterns.EnglishRequired,
        OnSite = BrazilPatterns.OnSite,
    };

    /// <summary>
    /// Remote jobs read from Brazil, in English: seniority like the Brazilian profile (the profile's lists exclude
    /// junior and internships), and English expected rather than penalised. Who may apply decides first
    /// (<see cref="EligibleScope"/>). Written for this project and not yet calibrated.
    /// </summary>
    public static MarketRules LatamRemote { get; } = RemoteMarket(JobMarket.LatamRemote, EligibilityRules.None);

    /// <summary>As <see cref="LatamRemote"/>, for jobs open worldwide: a job restricted to a region belongs to LATAM.</summary>
    public static MarketRules GlobalRemote { get; } = RemoteMarket(JobMarket.GlobalRemote, EligibilityRules.None);

    /// <summary>
    /// A remote market. Its eligibility block may only add a status elsewhere as a condition
    /// (<see cref="EligibilityRules.RestrictedPattern"/>) to the residency phrases: remote ads are not searched in full.
    /// </summary>
    public static MarketRules RemoteMarket(JobMarket market, EligibilityRules eligibility)
    {
        var scope = market switch
        {
            JobMarket.LatamRemote => EligibilityScope.IncludesBrazil,
            JobMarket.GlobalRemote => EligibilityScope.Worldwide,
            _ => throw new ArgumentOutOfRangeException(nameof(market), market, "Not a remote market."),
        };
        var e = Validated(eligibility, market);
        if (!(e with { RestrictedPattern = null }).IsEmpty())
            throw new InvalidOperationException(
                $"Matching:Markets:{market}:Eligibility may only set {nameof(EligibilityRules.RestrictedPattern)}: "
                + "remote ads are not searched in full and nothing else is read there.");
        var residency = RemotePatterns.ResidencyRestricted + (e.RestrictedPattern is { } restricted ? "|" + restricted : "");
        return new()
        {
            Market = market,
            Eligibility = e,
            Restricted = BrazilPatterns.Never,
            Incompatible = BrazilPatterns.Never,
            ReservedTitle = BrazilPatterns.Never,
            LeadershipTitle = ListingPatterns.LeadershipTitle,
            SeniorRank = ListingPatterns.SeniorRank,
            MidTitle = ListingPatterns.MidTitle,
            JuniorTitle = ListingPatterns.JuniorTitle,
            AssistanceDenied = BrazilPatterns.Never,
            AssistanceOffered = [],
            Remote = "remote",
            Hybrid = "hybrid",
            Fifo = [],
            NoExperience = ListingPatterns.NoExperience,
            Welcome = BrazilPatterns.Never,
            FlexibleWorkType = BrazilPatterns.Never,
            TrainingTitle = ListingPatterns.TrainingTitle,
            EntryTitle = ListingPatterns.EntryTitle,
            QualificationWords = ListingPatterns.QualificationWords,
            LongContract = BrazilPatterns.Never,
            CommissionOnly = ListingPatterns.CommissionOnly,
            TruckLicence = BrazilPatterns.Never,
            TruckLicenceAsked = _ => null,
            YearsAsked = ListingPatterns.YearsAsked,
            EnglishRequired = BrazilPatterns.Never,
            OnSite = BrazilPatterns.Never,
            EligibleScope = scope,
            ResidencyRestricted = Compile(residency, market, nameof(EligibilityRules.RestrictedPattern)),
            ContractorPhrases = RemotePatterns.Contractor,
            EorPhrases = RemotePatterns.Eor,
            DirectHirePhrases = RemotePatterns.DirectHire,
            HiringPlatforms = RemotePatterns.HiringPlatforms,
        };
    }

    /// <summary>
    /// The rules of a profile's market, with the market's eligibility block. A market without rules is refused, never
    /// read as another; so is a block for a market that reads none (Brazil). Australia scores only with its block:
    /// without one, no rule would say who may apply, and every listing would look open to the profile.
    /// </summary>
    public static MarketRules For(JobMarket market, EligibilityRules? eligibility = null)
    {
        var configured = eligibility is not null && !eligibility.IsEmpty();
        return market switch
        {
            JobMarket.AU when configured => Australia(eligibility!),
            JobMarket.AU => throw new InvalidOperationException(
                "Market AU scores only with its eligibility rules: set Matching:Markets:AU:Eligibility in appsettings.Local.json "
                + "(shape in appsettings.Local.example.json)."),
            JobMarket.BR when configured => throw new InvalidOperationException(
                "Market BR reads no eligibility rules: remove Matching:Markets:BR:Eligibility."),
            JobMarket.BR => Brazil,
            JobMarket.LatamRemote when !configured => LatamRemote,
            JobMarket.GlobalRemote when !configured => GlobalRemote,
            JobMarket.LatamRemote or JobMarket.GlobalRemote => RemoteMarket(market, eligibility!),
            _ => throw new NotSupportedException($"No scoring rules for {market} yet."),
        };
    }

    /// <summary>Whether the market scores only with a configured eligibility block (<see cref="For"/> refuses it without one).</summary>
    public static bool RequiresEligibility(JobMarket market) => market == JobMarket.AU;

    /// <summary>The code's groups, then the market's eligibility phrases.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Groups(EligibilityRules e) =>
        FullTextGroup.Code.Concat(e.Phrases).ToDictionary(g => g.Key, g => g.Value, StringComparer.Ordinal);

    /// <summary>Normalised, with only the groups eligibility may fill: a typo would silently drop a rule.</summary>
    private static EligibilityRules Validated(EligibilityRules eligibility, JobMarket market)
    {
        var e = eligibility.Normalized();
        var unknown = e.Phrases.Keys.Where(k => !FullTextGroup.Eligibility.Contains(k)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Matching:Markets:{market}:Eligibility:Phrases has unknown groups: {string.Join(", ", unknown)}. "
                + $"Known: {string.Join(", ", FullTextGroup.Eligibility)}.");
        return e;
    }

    /// <summary>A configured pattern, compiled once; none never matches. One that does not parse names its place.</summary>
    private static Regex Compile(string? pattern, JobMarket market, string slot)
    {
        if (pattern is null)
            return BrazilPatterns.Never;
        try
        {
            return new Regex(pattern, Options);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Matching:Markets:{market}:Eligibility:{slot} is not a valid regular expression: {ex.Message}", ex);
        }
    }
}
