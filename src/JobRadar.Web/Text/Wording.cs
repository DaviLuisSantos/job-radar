using System.Globalization;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Web.Text;

/// <summary>
/// Every user-facing word about scores and statuses. The domain explains a score with typed rules;
/// this is the only place that turns them into prose, so a translation starts and ends here.
/// </summary>
public static class Wording
{
    public static string Describe(ScoreItem item) => item.Rule switch
    {
        ScoreRule.CitizenshipRequired => $"Citizenship or PR required: “{item.Detail}”",
        ScoreRule.WorkRightsIncompatible => $"Needs work rights a working holiday visa cannot give: “{item.Detail}”",
        ScoreRule.IdentifiedRole => "Identified position for Aboriginal and Torres Strait Islander applicants",
        ScoreRule.LeadershipTitle => $"Leadership title: {item.Detail}",
        ScoreRule.SeniorTitle => $"Senior title: {item.Detail}",
        ScoreRule.LocationExcluded => $"Location this profile rules out: {item.Detail}",
        ScoreRule.SeniorPenalty => "Senior title",
        ScoreRule.PositiveSignal => $"Profile signal “{item.Detail}”",
        ScoreRule.NegativeSignal => $"Negative signal “{item.Detail}”",
        ScoreRule.SponsorshipMentioned => "Mentions visa sponsorship",
        ScoreRule.SponsorshipDenied => "Says there is no visa sponsorship",
        ScoreRule.Remote => "Remote",
        ScoreRule.Hybrid => "Hybrid",
        ScoreRule.Fifo => "FIFO",
        ScoreRule.EntryLevel => $"Entry level: “{item.Detail}”",
        ScoreRule.WorkingHolidayFriendly => $"Welcomes working holiday makers: “{item.Detail}”",
        ScoreRule.FlexibleWorkType => $"{item.Detail} fits six months per employer",
        ScoreRule.PreferredLocation => $"Preferred location: {item.Detail}",
        ScoreRule.RequirementHeld => $"Asks for something you have: “{item.Detail}”",
        ScoreRule.Compression => "Compressed above 80 to keep the best in order",
        ScoreRule.FullTextCitizenship => $"Full ad mentions “{item.Detail}”",
        ScoreRule.FullTextWhvWithResidency => $"Full ad mentions “{item.Detail.Replace(" + ", "” and “", StringComparison.Ordinal)}”: usually turning working holiday visas down",
        ScoreRule.FullTextAmbiguousRights => $"Full ad asks for “{item.Detail}” (may exclude visas)",
        ScoreRule.FullTextTwelveMonthRights => $"Full ad asks for “{item.Detail}”",
        ScoreRule.FullTextCommission => $"Full ad mentions “{item.Detail}”",
        ScoreRule.FullTextTruckLicence => $"Full ad asks for a truck licence: “{item.Detail}”",
        ScoreRule.FullTextExperience => $"Full ad asks for experience: “{item.Detail}”",
        ScoreRule.FullTextQualification => $"Full ad asks for a qualification: “{item.Detail}”",
        ScoreRule.FullTextLicence => $"Full ad asks for a licence or ticket: “{item.Detail}”",
        ScoreRule.Apprenticeship => "Formal apprenticeship: often needs PR and lasts years",
        ScoreRule.LongContract => $"Longer than six months per employer: “{item.Detail}”",
        ScoreRule.CommissionOnly => $"Commission only: “{item.Detail}”",
        ScoreRule.TruckLicence => $"Truck licence: “{item.Detail}”",
        ScoreRule.NoSponsorship => "No confirmed sponsorship",
        ScoreRule.Aging => $"Posted {item.Detail} days ago",
        ScoreRule.Stale => $"Posted {item.Detail} days ago, probably closed",
        ScoreRule.QualificationRequired => $"Needs a qualification: {item.Detail}",
        _ => $"{item.Rule}: {item.Detail}",
    };

    public static string Where(ScoreWhere where) => where switch
    {
        ScoreWhere.Title => "title",
        ScoreWhere.Body => "summary",
        ScoreWhere.Text => "text",
        ScoreWhere.FullText => "full ad",
        ScoreWhere.WorkType => "work type",
        ScoreWhere.Location => "location",
        ScoreWhere.PostedDate => "date",
        ScoreWhere.Description => "captured ad",
        _ => "",
    };

    public static string Points(int points) => points switch
    {
        > 0 => $"+{points}",
        _ => points.ToString(CultureInfo.InvariantCulture),
    };

    public static string Status(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Reviewed => "Reviewed",
        ApplicationStatus.Dismissed => "Dismissed",
        ApplicationStatus.Applied => "Applied",
        ApplicationStatus.Interview => "Interview",
        ApplicationStatus.Offer => "Offer",
        ApplicationStatus.Declined => "Declined by employer",
        _ => status.ToString(),
    };

    public static string Origin(DescriptionOrigin origin) => origin switch
    {
        DescriptionOrigin.Browser => "from the ad page",
        DescriptionOrigin.Pasted => "pasted",
        DescriptionOrigin.Source => "fetched from the source",
        _ => origin.ToString(),
    };

    public static string State(RequirementState state) => state switch
    {
        RequirementState.Have => "I have it",
        RequirementState.Obtainable => "I would get it",
        RequirementState.Missing => "I don't have it",
        RequirementState.OutOfReach => "Out of reach",
        _ => state.ToString(),
    };

    /// <summary>A catalog requirement in plain words: what it is and how one gets it. Unknown ids show as they are.</summary>
    public static (string Title, string Hint) Requirement(string id) => id switch
    {
        "hrwl" => ("High Risk Work Licence (HRWL)", "Forklift, elevated work platform, dogging and the like: a course of a few days."),
        "voc" => ("Verification of competency (VOC)", "Proof you can run a given machine, usually assessed on the job."),
        "truck-mr" => ("Truck licence MR (medium rigid)", "Needs a year or more of a full car licence first."),
        "truck-hr" => ("Truck licence HR (heavy rigid)", "Needs a year or more of a full car licence first; includes MR."),
        "truck-hc" => ("Truck licence HC (heavy combination)", "Needs a year or more of HR first; includes HR and MR."),
        "truck-mc" => ("Truck licence MC (multi combination)", "Road trains; needs HC first; includes every lower class."),
        "trade-qualification" => ("Trade certificate (Cert III, trade qualified)", "An Australian trade qualification; an overseas course counts only once assessed."),
        "degree" => ("University degree", "Asked as “degree in”, “tertiary qualification”."),
        "refrigerant-licence" => ("Refrigerant handling licence (ARCtick)", "Needs a refrigeration trade plus supervised time."),
        "electrical-licence" => ("Electrical licence", "Needs an electrical trade plus supervised time."),
        "experience-years" => ("Years of experience, when the ad gives no number", "With a number, your years above decide."),
        "experience-proven" => ("Proven experience in a similar role", "“Proven” or “demonstrated experience”."),
        "experience-operating" => ("Experience operating machinery", "“Experience operating” excavators, rollers and the like."),
        _ => (id, ""),
    };

    /// <summary>The tooltip of a fact label: what kind of fact it is and where it came from.</summary>
    public static string FactTitle(FactKind kind) => kind switch
    {
        FactKind.Pay => "Pay, as the ad states it",
        FactKind.Travel => "How you get to site",
        FactKind.Roster => "Roster: days or weeks on and off",
        FactKind.Accommodation => "Accommodation provided",
        FactKind.Meals => "Meals provided",
        FactKind.Flights => "Flights paid by the employer",
        FactKind.Start => "Start date",
        FactKind.Shift => "Shifts",
        FactKind.Duration => "Length of the contract",
        _ => kind.ToString(),
    } + " (read from the ad)";

    /// <summary>Short labels for the four states, as buttons.</summary>
    public static string StateButton(RequirementState state) => state switch
    {
        RequirementState.Have => "I have it",
        RequirementState.Obtainable => "I'd get it",
        RequirementState.Missing => "I don't have it",
        RequirementState.OutOfReach => "Out of reach",
        _ => state.ToString(),
    };

    public static string Years(double years) =>
        years == 1 ? "1 year" : $"{years.ToString("0.#", CultureInfo.InvariantCulture)} years";

    /// <summary>A quick-pick button: the top option reads "10+".</summary>
    public static string YearsShort(double years) => years >= 10 ? "10+" : years.ToString("0.#", CultureInfo.InvariantCulture);

    public static string Listings(int count) =>
        count == 1 ? "1 listing" : $"{count.ToString("N0", CultureInfo.InvariantCulture)} listings";

    public static string Kind(RequirementKind kind) => kind switch
    {
        RequirementKind.Experience => "Experience",
        RequirementKind.Qualification => "Qualification",
        RequirementKind.Licence => "Licence or ticket",
        _ => kind.ToString(),
    };

    public static string Verdict(Verdict verdict) => verdict == Domain.Tracking.Verdict.Good ? "Good match" : "Bad match";

    public static string WorkType(WorkType workType) => ScoringInput.FormatWorkType(workType) ?? "";

    public static string Location(ListingLocation location) => ScoringInput.FormatLocation(location) ?? "Location not given";

    /// <summary>A stored instant (UTC) in the local time of the machine running the site.</summary>
    public static string When(DateTimeOffset at) => at.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>First line of <paramref name="text"/>, cut at <paramref name="max"/> characters; a stack trace stays out of a table cell.</summary>
    public static string Shorten(string? text, int max)
    {
        var line = text?.Split('\n', 2)[0].TrimEnd() ?? "";
        return line.Length <= max ? line : string.Concat(line.AsSpan(0, max - 1).TrimEnd(), "…");
    }

    /// <summary>"today", "3d", "5w": short enough for a table column.</summary>
    public static string Age(DateTimeOffset? posted, DateTimeOffset now)
    {
        if (posted is not { } p)
            return "";
        var days = (int)(now - p).TotalDays;
        return days switch
        {
            < 1 => "today",
            < 14 => $"{days}d",
            _ => $"{days / 7}w",
        };
    }

    /// <summary>Score band used for colour: excluded, low, fair, good, strong.</summary>
    public static string Band(int score) => score switch
    {
        0 => "excluded",
        < 50 => "low",
        < 65 => "fair",
        < 80 => "good",
        _ => "strong",
    };
}
