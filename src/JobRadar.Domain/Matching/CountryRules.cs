using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The words of one job market that <see cref="FitScorer"/> reads: its language, its visas, its seniority words,
/// its contracts. The scorer is one, with one order of phases for every country; what a phrase means is the
/// country's. Weights stay in <see cref="ScoringPolicy"/>.
/// </summary>
public sealed record CountryRules
{
    public required Country Country { get; init; }

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

    /// <summary>The rules ported from the prototype: English, a working holiday visa, Australian licences.</summary>
    public static CountryRules Australia { get; } = new()
    {
        Country = Country.AU,
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
    };

    /// <summary>The rules of a listing's market. Brazil has none yet: its listings cannot be scored.</summary>
    public static CountryRules For(Country country) => country switch
    {
        Country.AU => Australia,
        _ => throw new NotSupportedException($"No scoring rules for {country} yet."),
    };
}
