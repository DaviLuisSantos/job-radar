using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The words of one job market that <see cref="FitScorer"/> reads: its language, its visas, its seniority words,
/// its contracts. The scorer is one, with one order of phases for every country; what a phrase means is the
/// country's. Weights stay in <see cref="ScoringPolicy"/>.
/// </summary>
public sealed record MarketRules
{
    public required JobMarket Market { get; init; }

    // Exclusions.
    public required Regex CitizenshipRequired { get; init; }
    public required Regex WorkRightsIncompatible { get; init; }
    public required Regex IdentifiedRole { get; init; }
    public required Regex LeadershipTitle { get; init; }
    public required Regex SeniorRank { get; init; }
    public required Regex MidTitle { get; init; }
    public required Regex JuniorTitle { get; init; }

    // Points.
    public required Regex SponsorshipDenied { get; init; }
    public required IReadOnlyList<string> SponsorshipOffered { get; init; }
    public required string Remote { get; init; }
    public required string Hybrid { get; init; }
    public required IReadOnlyList<string> Fifo { get; init; }
    public required IReadOnlyList<string> NoExperience { get; init; }
    public required Regex WorkingHolidayFriendly { get; init; }
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
    /// <summary>Residence or work authorisation elsewhere as a condition ("US only"): excludes.</summary>
    public Regex ResidencyRestricted { get; init; } = BrazilPatterns.Never;
    public IReadOnlyList<string> ContractorPhrases { get; init; } = [];
    public IReadOnlyList<string> EorPhrases { get; init; } = [];
    public IReadOnlyList<string> DirectHirePhrases { get; init; } = [];
    public IReadOnlyList<string> HiringPlatforms { get; init; } = [];

    /// <summary>The rules ported from the prototype: English, a working holiday visa, Australian licences.</summary>
    public static MarketRules Australia { get; } = new()
    {
        Market = JobMarket.AU,
        CitizenshipRequired = ListingPatterns.CitizenshipRequired,
        WorkRightsIncompatible = ListingPatterns.WorkRightsIncompatible,
        IdentifiedRole = ListingPatterns.IdentifiedRole,
        LeadershipTitle = ListingPatterns.LeadershipTitle,
        SeniorRank = ListingPatterns.SeniorRank,
        MidTitle = ListingPatterns.MidTitle,
        JuniorTitle = ListingPatterns.JuniorTitle,
        SponsorshipDenied = ListingPatterns.SponsorshipDenied,
        SponsorshipOffered = ListingPatterns.SponsorshipOffered,
        Remote = "remote",
        Hybrid = "hybrid",
        Fifo = ["fifo", "fly-in fly-out"],
        NoExperience = ListingPatterns.NoExperience,
        WorkingHolidayFriendly = ListingPatterns.WorkingHolidayFriendly,
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

    /// <summary>
    /// Portuguese, CLT or PJ, no visa: what is Australian (working holiday, citizenship, truck licences, FIFO,
    /// six months per employer) never matches. Written for this project and not yet calibrated.
    /// </summary>
    public static MarketRules Brazil { get; } = new()
    {
        Market = JobMarket.BR,
        CitizenshipRequired = BrazilPatterns.Never,
        WorkRightsIncompatible = BrazilPatterns.Never,
        IdentifiedRole = BrazilPatterns.Never,
        LeadershipTitle = BrazilPatterns.LeadershipTitle,
        SeniorRank = BrazilPatterns.SeniorRank,
        MidTitle = BrazilPatterns.MidTitle,
        JuniorTitle = BrazilPatterns.JuniorTitle,
        SponsorshipDenied = BrazilPatterns.Never,
        SponsorshipOffered = [],
        Remote = "remoto",
        Hybrid = "híbrido",
        Fifo = [],
        NoExperience = BrazilPatterns.NoExperience,
        WorkingHolidayFriendly = BrazilPatterns.Never,
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
    /// junior and internships), no visa, and English expected rather than penalised. Who may apply decides first
    /// (<see cref="EligibleScope"/>). Written for this project and not yet calibrated.
    /// </summary>
    public static MarketRules LatamRemote { get; } = RemoteRules(JobMarket.LatamRemote, EligibilityScope.IncludesBrazil);

    /// <summary>As <see cref="LatamRemote"/>, for jobs open worldwide: a job restricted to a region belongs to LATAM.</summary>
    public static MarketRules GlobalRemote { get; } = RemoteRules(JobMarket.GlobalRemote, EligibilityScope.Worldwide);

    private static MarketRules RemoteRules(JobMarket market, EligibilityScope scope) => new()
    {
        Market = market,
        CitizenshipRequired = BrazilPatterns.Never,
        WorkRightsIncompatible = BrazilPatterns.Never,
        IdentifiedRole = BrazilPatterns.Never,
        LeadershipTitle = ListingPatterns.LeadershipTitle,
        SeniorRank = ListingPatterns.SeniorRank,
        MidTitle = ListingPatterns.MidTitle,
        JuniorTitle = ListingPatterns.JuniorTitle,
        SponsorshipDenied = BrazilPatterns.Never,
        SponsorshipOffered = [],
        Remote = "remote",
        Hybrid = "hybrid",
        Fifo = [],
        NoExperience = ListingPatterns.NoExperience,
        WorkingHolidayFriendly = BrazilPatterns.Never,
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
        ResidencyRestricted = RemotePatterns.ResidencyRestricted,
        ContractorPhrases = RemotePatterns.Contractor,
        EorPhrases = RemotePatterns.Eor,
        DirectHirePhrases = RemotePatterns.DirectHire,
        HiringPlatforms = RemotePatterns.HiringPlatforms,
    };

    /// <summary>The rules of a profile's market. A market without rules is refused, never read as another.</summary>
    public static MarketRules For(JobMarket market) => market switch
    {
        JobMarket.AU => Australia,
        JobMarket.BR => Brazil,
        JobMarket.LatamRemote => LatamRemote,
        JobMarket.GlobalRemote => GlobalRemote,
        _ => throw new NotSupportedException($"No scoring rules for {market} yet."),
    };
}
