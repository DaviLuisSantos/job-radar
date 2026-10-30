using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>
/// One fictional case per rule, with the fictional market of <see cref="TestMarkets"/>. Real-data parity lives in
/// <see cref="PrototypeParityTests"/>, real eligibility words in <see cref="LocalScoringCasesTests"/>.
/// </summary>
public class FitScorerTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly MarketRules Au = TestMarkets.Au;
    private static readonly DateTimeOffset Yesterday = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private static readonly MatchingProfile Tech = new(
        "tech",
        PositiveSignals: ["C#", ".NET", "contract"],
        NegativeSignals: ["hospitality", "cert III"],
        DisqualifySignals: [],
        QualifiedTitles: [],
        EntryLevelBoost: false,
        AllowSenior: true,
        PreferredLocations: ["Perth"]);

    private static readonly MatchingProfile Trades = new(
        "trades",
        PositiveSignals: ["offsider"],
        NegativeSignals: ["cert III"],
        DisqualifySignals: ["trade qualified"],
        QualifiedTitles: ["electrician"],
        EntryLevelBoost: true,
        AllowSenior: false,
        PreferredLocations: ["WA"]);

    private static ScoringInput Job(
        string title, string? summary = null, string? location = "Sydney NSW", string? workType = "Full time",
        DateTimeOffset? posted = null, params string[] fullText) =>
        new(title, summary, location, workType, posted ?? Yesterday, fullText);

    private static FitScore Score(ScoringInput job, MatchingProfile? profile = null, ScoringPolicy? policy = null) =>
        FitScorer.Score(job, profile ?? Tech, Au, policy ?? ScoringPolicy.Default, Today);

    private static ScoreItem Item(FitScore score, ScoreRule rule) => Assert.Single(score.Items, i => i.Rule == rule);

    // ── Exclusions ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Must be a guild member")]
    [InlineData("Guild pass required")]
    [InlineData("Full guild standing - essential")]
    public void A_reserved_status_excludes(string summary)
    {
        var score = Score(Job("Developer", summary));

        Assert.True(score.Excluded);
        Assert.Equal(ScoreRule.EligibilityRestricted, Item(score, ScoreRule.EligibilityRestricted).Rule);
    }

    [Theory]
    [InlineData("Lifetime guild standing required")]
    [InlineData("You need 24 months of valid guild standing")]
    [InlineData("No seasonal members")]
    public void A_condition_the_status_cannot_meet_excludes(string summary)
    {
        Assert.True(Score(Job("Developer", summary)).Excluded);
    }

    [Fact]
    public void An_excluding_phrase_in_the_full_text_excludes()
    {
        var score = Score(Job("Developer", fullText: "lifetime guild standing"));

        Assert.True(score.Excluded);
        Assert.Equal(ScoreWhere.FullText, Item(score, ScoreRule.EligibilityIncompatible).Where);
    }

    [Fact]
    public void Reserved_roles_exclude()
    {
        Assert.Equal(ScoreRule.ReservedRole, Assert.Single(Score(Job("Guild Scholar Liaison Officer")).Items).Rule);
    }

    [Theory]
    [InlineData("Lead Developer")]
    [InlineData("Solutions Architect")]
    [InlineData("Staff Software Engineer")]
    public void Leadership_titles_exclude_even_when_senior_is_allowed(string title)
    {
        Assert.Equal(ScoreRule.LeadershipTitle, Item(Score(Job(title)), ScoreRule.LeadershipTitle).Rule);
    }

    [Fact]
    public void Senior_excludes_unless_the_profile_allows_it()
    {
        Assert.True(Score(Job("Senior Electrician"), Trades).Excluded);
        Assert.False(Score(Job("Senior Developer")).Excluded);
    }

    [Fact]
    public void A_junior_mark_in_the_title_wins_over_leadership()
    {
        Assert.False(Score(Job("Graduate / Junior Project Manager")).Excluded);
    }

    // ── Points ────────────────────────────────────────────────────────────

    [Fact]
    public void A_neutral_listing_scores_the_base()
    {
        var score = Score(Job("Developer"));

        Assert.Equal(FitScore.Base, score.Final);
        Assert.Empty(score.Items);
    }

    [Fact]
    public void Senior_subtracts_when_allowed_but_mid_to_senior_does_not()
    {
        Assert.Equal(-15, Item(Score(Job("Senior Developer")), ScoreRule.SeniorPenalty).Points);
        Assert.DoesNotContain(Score(Job("Mid to Senior Developer")).Items, i => i.Rule == ScoreRule.SeniorPenalty);
    }

    [Fact]
    public void Signals_in_the_title_are_worth_twice_the_body()
    {
        var score = Score(Job("C# Developer", "We build .NET services"));

        Assert.Equal([(ScoreWhere.Title, 16), (ScoreWhere.Body, 8)],
            score.Items.Where(i => i.Rule == ScoreRule.PositiveSignal).Select(i => (i.Where, i.Points)));
        Assert.Equal(74, score.Final);
    }

    [Fact]
    public void Symbols_match_as_whole_tokens()
    {
        Assert.Contains(Score(Job("Developer", "Experience with .NET 8")).Items, i => i.Detail == ".NET");
        // A word character right before the symbol breaks the token: ".NET" inside "ASP.NET".
        Assert.DoesNotContain(Score(Job("Developer", "ASP.NET Core")).Items, i => i.Detail == ".NET");
    }

    [Fact]
    public void A_positive_signal_right_after_a_negation_does_not_count()
    {
        Assert.Empty(Score(Job("Developer", "This is not a contract role")).Items);
    }

    [Fact]
    public void Negative_signals_subtract_without_a_negation_check()
    {
        Assert.Equal(-15, Item(Score(Job("Developer", "Not hospitality, honest")), ScoreRule.NegativeSignal).Points);
    }

    [Fact]
    public void Training_titles_skip_qualification_negatives()
    {
        Assert.DoesNotContain(Score(Job("Apprentice Electrician", "Earn your Cert III"), Trades).Items,
            i => i.Rule == ScoreRule.NegativeSignal);
    }

    [Fact]
    public void Assistance_offered_adds_the_bonus_and_denied_adds_nothing()
    {
        var offered = Item(Score(Job("Developer", "Relocation package available")), ScoreRule.AssistanceOffered);
        var word = Item(Score(Job("Developer", "Relocation considered")), ScoreRule.AssistanceOffered);
        var denied = Score(Job("Developer", "Relocation is not available"));

        Assert.Equal((10, "relocation package"), (offered.Points, offered.Detail));
        Assert.Equal((10, "relocation"), (word.Points, word.Detail));
        Assert.Equal((0, "relocation is not available"), (Item(denied, ScoreRule.AssistanceDenied).Points, Item(denied, ScoreRule.AssistanceDenied).Detail));
        Assert.DoesNotContain(denied.Items, i => i.Rule == ScoreRule.AssistanceOffered);
    }

    [Fact]
    public void Remote_beats_hybrid()
    {
        var score = Score(Job("Developer", "Remote or hybrid"));

        Assert.Equal(10, Item(score, ScoreRule.Remote).Points);
        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.Hybrid);
    }

    [Fact]
    public void Site_work_profiles_do_not_reward_remote_which_is_the_place_there()
    {
        var score = Score(Job("FIFO Social Worker", "Support remote communities | Remote"), Trades with { RewardRemote = false });

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.Remote or ScoreRule.Hybrid);
    }

    [Fact]
    public void Fifo_counts_once()
    {
        Assert.Equal(15, Item(Score(Job("Utility", "FIFO 2:1 roster")), ScoreRule.Fifo).Points);
        var fifoProfile = Tech with { PositiveSignals = ["FIFO"] };
        Assert.DoesNotContain(Score(Job("Utility", "FIFO roster"), fifoProfile).Items, i => i.Rule == ScoreRule.Fifo);
    }

    [Fact]
    public void Entry_level_phrases_count_only_for_profiles_that_boost_them()
    {
        Assert.Equal(2, Score(Job("Labourer", "Training provided. Entry level"), Trades)
            .Items.Count(i => i.Rule == ScoreRule.EntryLevel));
        Assert.DoesNotContain(Score(Job("Labourer", "Training provided")).Items, i => i.Rule == ScoreRule.EntryLevel);
    }

    [Fact]
    public void Known_quirk_the_no_of_no_experience_negates_the_next_entry_phrase()
    {
        // Kept for parity with the prototype; a candidate for the calibration step.
        var score = Score(Job("Labourer", "No experience required, training provided"), Trades);

        Assert.Equal(["no experience required"], score.Items.Where(i => i.Rule == ScoreRule.EntryLevel).Select(i => i.Detail));
    }

    [Fact]
    public void A_welcome_from_text_or_full_text()
    {
        Assert.Equal(ScoreWhere.Text, Item(Score(Job("Barista", "Newcomers welcome")), ScoreRule.EligibilityWelcome).Where);
        Assert.Equal(ScoreWhere.FullText, Item(Score(Job("Barista", fullText: "newcomers")), ScoreRule.EligibilityWelcome).Where);
    }

    [Theory]
    [InlineData("Contract/Temp", true)]
    [InlineData("Casual/Vacation", true)]
    [InlineData("Full time", false)]
    public void Contract_temp_and_casual_are_short_engagements(string workType, bool bonus)
    {
        Assert.Equal(bonus, Score(Job("Developer", workType: workType)).Items.Any(i => i.Rule == ScoreRule.FlexibleWorkType));
    }

    [Fact]
    public void Preferred_location_adds_once()
    {
        var score = Score(Job("Developer", location: "Osborne Park, Perth WA"));

        Assert.Equal(10, Item(score, ScoreRule.PreferredLocation).Points);
    }

    // ── Compression ───────────────────────────────────────────────────────

    [Fact]
    public void Above_80_is_compressed_so_the_best_listings_keep_their_order()
    {
        // 50 + 16 (C#) + 16 (.NET) + 16 (contract) + 10 remote + 5 contract type + 10 Perth = 123
        var strong = Score(Job("C# .NET Contract Developer", "Remote", "Perth WA", "Contract/Temp"));
        // Same without remote: 113
        var weaker = Score(Job("C# .NET Contract Developer", null, "Perth WA", "Contract/Temp"));

        Assert.Equal(80 + 14, strong.Final);   // 80 + round(43 / 3)
        Assert.Equal(80 + 11, weaker.Final);   // 80 + round(33 / 3)
        Assert.True(strong.Final > weaker.Final);
    }

    // ── Restrictions after compression ────────────────────────────────────

    [Fact]
    public void Full_text_restrictions_apply_after_compression()
    {
        var score = Score(Job("C# .NET Contract Developer", "Remote", "Perth WA", "Contract/Temp",
            fullText: "full guild standing"));

        Assert.Equal(94 - 10, score.Final);
        Assert.Equal(ScoreRule.FullTextEligibilityAmbiguous, score.Items[^1].Rule);
    }

    [Fact]
    public void Doubt_and_tenure_phrases_are_cancelled_by_a_welcome()
    {
        var withoutWelcome = Score(Job("Barista", fullText: ["guild member", "24 months + guild standing"]));
        var withWelcome = Score(Job("Barista", fullText: ["guild member", "24 months + guild standing", "seasonal member"]));

        Assert.Equal(1, withoutWelcome.Final);   // 50 − 55 − 40, floored
        Assert.Equal(65, withWelcome.Final);     // 50 + 15
    }

    [Fact]
    public void A_welcome_next_to_a_reserved_status_is_a_refusal_not_a_welcome()
    {
        var score = Score(Job("Barista", fullText: ["seasonal member", "seasonal member + guild pass", "24 months + guild standing"]));

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.EligibilityWelcome);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextWelcomeRetracted).Points);
        Assert.Equal(-40, Item(score, ScoreRule.FullTextEligibilityTenure).Points);
    }

    [Fact]
    public void A_retracted_welcome_with_a_doubt_is_penalised_once()
    {
        var score = Score(Job("Barista", fullText: ["guild member", "seasonal member", "seasonal member + guild member"]));

        Assert.Equal(-55, Item(score, ScoreRule.FullTextEligibilityDoubt).Points);
        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.FullTextWelcomeRetracted);
        Assert.Equal(1, score.Final);   // 50 − 55, floored
    }

    [Fact]
    public void A_welcome_in_the_listing_own_text_still_wins_over_the_doubtful_full_text()
    {
        var score = Score(Job("Barista", "Newcomers welcome", fullText: ["seasonal member", "seasonal member + guild member"]));

        Assert.Equal(ScoreWhere.Text, Item(score, ScoreRule.EligibilityWelcome).Where);
        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.FullTextWelcomeRetracted);
        Assert.Equal(65, score.Final);
    }

    private static readonly MatchingProfile FifoWithRequirements = Trades with
    {
        Requirements = new ProfileRequirements(
            Contexts: ["FIFO"],
            Experience: ["years experience", "minimum 2 years"],
            Qualification: ["cert III"],
            Licence: ["HR licence", "HRWL"]),
    };

    [Fact]
    public void Requirements_found_in_the_full_ad_count_once_per_kind_and_a_ticket_costs_less()
    {
        var score = Score(Job("Labourer", fullText: ["years experience + FIFO", "minimum 2 years + fifo", "cert III + FIFO", "HRWL + FIFO"]),
            FifoWithRequirements);

        Assert.Equal(-20, Item(score, ScoreRule.FullTextExperience).Points);
        Assert.Equal("years experience", Item(score, ScoreRule.FullTextExperience).Detail);
        Assert.Equal(-20, Item(score, ScoreRule.FullTextQualification).Points);
        Assert.Equal(-10, Item(score, ScoreRule.FullTextLicence).Points);   // a ticket is a short course away
        Assert.Equal(3, score.Items.Count(i => i.Where == ScoreWhere.FullText));   // two experience phrases, one penalty
    }

    [Fact]
    public void A_requirement_found_with_another_profile_context_word_still_counts()
    {
        var other = Score(Job("Operator Assistant", fullText: ["HRWL + labourer"]), FifoWithRequirements);
        var notOurs = Score(Job("Labourer", fullText: ["HC licence + FIFO"]), FifoWithRequirements);

        Assert.Equal("HRWL", Item(other, ScoreRule.FullTextLicence).Detail);
        Assert.DoesNotContain(notOurs.Items, i => i.Rule == ScoreRule.FullTextLicence);
    }

    private static ScoringInput Described(string title, string description, params string[] fullText) =>
        Job(title, fullText: fullText) with { Description = description };

    [Fact]
    public void A_reserved_status_in_a_captured_ad_costs_55_unless_the_ad_welcomes_the_profile()
    {
        var membersOnly = Score(Described("Labourer", "Applicants must be guild members or guild pass holders."), Trades);
        var welcome = Score(Described("Labourer", "Open to guild pass holders and seasonal members."), Trades);

        Assert.True(membersOnly.Final > 0);   // weighed, not excluded: the ad may list who else may apply
        Assert.Equal(ScoreWhere.Description, Item(membersOnly, ScoreRule.FullTextEligibilityDoubt).Where);
        Assert.Equal(-55, Item(membersOnly, ScoreRule.FullTextEligibilityDoubt).Points);
        Assert.Equal(ScoreWhere.Description, Item(welcome, ScoreRule.EligibilityWelcome).Where);
        Assert.DoesNotContain(welcome.Items, i => i.Rule == ScoreRule.FullTextEligibilityDoubt);
    }

    [Fact]
    public void A_negated_welcome_in_a_captured_ad_earns_nothing_and_beats_the_searched_phrase()
    {
        var score = Score(Described("Labourer", "Guild members only apply. We are unable to accept seasonal members.",
            "seasonal member"), Trades);

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.EligibilityWelcome);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextEligibilityDoubt).Points);
    }

    [Fact]
    public void A_condition_the_status_cannot_meet_excludes_from_a_captured_ad()
    {
        var score = Score(Described("Labourer", "You must have lifetime guild standing to apply for this role."), Trades);

        Assert.Equal(0, score.Final);
        Assert.Equal(ScoreWhere.Description, Item(score, ScoreRule.EligibilityIncompatible).Where);
    }

    [Fact]
    public void Requirements_are_read_from_a_captured_ad_unless_negated_just_before()
    {
        var asks = Score(Described("Labourer", "Essential: minimum 2 years in a similar role and a current HR licence."), FifoWithRequirements);
        var waives = Score(Described("Labourer", "No HRWL needed, and no minimum 2 years either: we train you from day one."), FifoWithRequirements);

        Assert.Equal(ScoreWhere.Description, Item(asks, ScoreRule.FullTextExperience).Where);
        Assert.Equal(-40, Item(asks, ScoreRule.TruckLicence).Points);   // "HR licence" read locally counts once
        Assert.DoesNotContain(waives.Items, i => i.Rule is ScoreRule.FullTextExperience or ScoreRule.FullTextLicence);
    }

    [Fact]
    public void A_captured_ad_is_read_instead_of_the_search_for_requirements_and_truck_licences()
    {
        // The search matched "HRWL" and "HR licence" somewhere; the ad only asks for a car licence.
        var score = Score(Described("Labourer", "Holds a current Australian driver's license. Fit for work.",
            "HRWL + labourer", "HR licence", "years experience + FIFO"), FifoWithRequirements);

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.FullTextLicence or ScoreRule.FullTextTruckLicence
            or ScoreRule.TruckLicence or ScoreRule.FullTextExperience);
    }

    [Fact]
    public void Profile_signals_do_not_read_a_captured_ad()
    {
        var plain = Score(Job("Labourer"), Trades);
        var described = Score(Described("Labourer", "You will report to the offsider lead and work under a cert III qualified supervisor."), Trades);

        Assert.Equal(plain.Final, described.Final);
    }

    [Fact]
    public void A_policy_term_found_with_a_context_word_counts_as_the_term()
    {
        var score = Score(Job("Barista", fullText: ["guild member + FIFO"]));

        Assert.Equal("guild member", Item(score, ScoreRule.FullTextEligibilityDoubt).Detail);
    }

    [Fact]
    public void A_composite_policy_term_is_never_read_as_another_term_plus_a_context_word()
    {
        // "seasonal member + guild member" is its own term (a refusal), not "seasonal member" found with a context word.
        var score = Score(Job("Barista", fullText: ["seasonal member + guild member"]));

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.EligibilityWelcome);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextWelcomeRetracted).Points);
    }

    [Fact]
    public void A_leading_hand_is_not_an_entry_title()
    {
        var leading = Score(Job("FIFO Shift Electricians / Leading Hands"), Trades);
        var kitchen = Score(Job("Kitchen Hand Electrician Camp"), Trades);

        Assert.Equal(ScoreRule.QualificationRequired, leading.Items[^1].Rule);
        Assert.DoesNotContain(kitchen.Items, i => i.Rule == ScoreRule.QualificationRequired);
    }

    [Fact]
    public void A_truck_licence_already_counted_and_a_certificate_in_a_training_title_cost_nothing_more()
    {
        var truck = Score(Job("Labourer", "HR licence required", fullText: ["HR licence + FIFO"]), FifoWithRequirements);
        var apprentice = Score(Job("Apprentice Labourer", fullText: ["cert III + FIFO"]), FifoWithRequirements);

        // One licence penalty, whichever rule carries it ("HR licence + FIFO" is also the policy's "HR licence").
        var licence = Assert.Single(truck.Items, i => i.Rule is ScoreRule.TruckLicence or ScoreRule.FullTextTruckLicence or ScoreRule.FullTextLicence);
        Assert.Equal(-40, licence.Points);
        Assert.DoesNotContain(apprentice.Items, i => i.Rule == ScoreRule.FullTextQualification);
    }

    [Fact]
    public void Commission_and_truck_licence_count_once_whether_local_or_full_text()
    {
        Assert.Equal(-30, Item(Score(Job("Sales", "Commission only role")), ScoreRule.CommissionOnly).Points);
        var both = Score(Job("Sales", "Commission only role", fullText: "commission only"));
        Assert.DoesNotContain(both.Items, i => i.Rule == ScoreRule.CommissionOnly);
        Assert.Equal(-30, Item(both, ScoreRule.FullTextCommission).Points);
        Assert.Equal(-40, Item(Score(Job("Driver", "HR licence required")), ScoreRule.TruckLicence).Points);
    }

    [Theory]
    [InlineData("Current HR truck driver's licence - mandatory", "HR truck driver's licence")]
    [InlineData("Current HR Driver’s Licence and police clearance", "HR Driver’s Licence")]
    [InlineData("HC class licence essential", "HC class licence")]
    [InlineData("Must hold an MR/HR", "MR/HR")]
    [InlineData("Heavy combination experience", "Heavy combination")]
    [InlineData("• HR-B Drivers License", "HR-B Drivers License")]
    [InlineData("Current HC (B) licence", "HC (B) licence")]
    [InlineData("MR B class licence required", "MR B class licence")]
    public void Truck_licences_are_told_however_the_ad_spells_them(string text, string found)
    {
        var item = Item(Score(Job("Offsider", text)), ScoreRule.TruckLicence);

        Assert.Equal((found.ToLowerInvariant(), -40), (item.Detail, item.Points));
    }

    [Theory]
    [InlineData("Report to our HR team")]
    [InlineData("HR manager will contact you")]
    [InlineData("Our HR team checks your car licence")]
    [InlineData("HR a driver will call you")]
    [InlineData("No HR licence needed, we train")]
    public void A_truck_licence_is_not_read_into_other_hr_or_a_waived_one(string text) =>
        Assert.DoesNotContain(Score(Job("Labourer", text)).Items, i => i.Rule == ScoreRule.TruckLicence);

    [Fact]
    public void A_truck_licence_the_ad_calls_desirable_costs_little_unless_another_is_a_condition()
    {
        var desirable = Item(Score(Job("Labourer", "MR licence highly regarded")), ScoreRule.TruckLicence);
        var both = Item(Score(Job("Labourer", "MR licence (desirable). Current HC licence")), ScoreRule.TruckLicence);

        Assert.Equal(("mr licence (desirable)", -10), (desirable.Detail, desirable.Points));
        Assert.Equal(("hc licence", -40), (both.Detail, both.Points));
    }

    [Fact]
    public void An_essential_truck_licence_is_not_made_optional_by_the_next_one()
    {
        var item = Item(Score(Job("Yard Person", "Minimum HR driver's license is essential - HC licence desirable")), ScoreRule.TruckLicence);

        Assert.Equal(("hr driver's license", -40), (item.Detail, item.Points));
    }

    [Fact]
    public void An_unrestricted_truck_licence_is_a_truck_licence() =>
        Assert.Equal(-40, Item(Score(Job("Operator Assistant", "Current HC Unrestricted licence")), ScoreRule.TruckLicence).Points);

    [Fact]
    public void Long_term_conditions_exclude_from_the_text_or_a_captured_ad()
    {
        var local = Score(Job("Cleaning Utility", "Long-term guild standing"));
        var described = Score(Described("Cleaning Utility", "Requirements: current C class licence and long term guild standing."));

        Assert.Equal(ScoreRule.EligibilityIncompatible, Assert.Single(local.Items).Rule);
        Assert.Equal(ScoreWhere.Description, Assert.Single(described.Items).Where);
    }

    [Theory]
    [InlineData("Assistant Village Manager - DIDO/ FIFO", true)]
    [InlineData("Assistant Manager", true)]
    [InlineData("Trade Assistant", false)]
    [InlineData("Assistant to the Site Manager", true)]
    public void An_assistant_manager_is_management_not_a_junior_title(string title, bool excluded) =>
        Assert.Equal(excluded, Score(Job(title)).Items.Any(i => i.Rule == ScoreRule.LeadershipTitle));

    [Fact]
    public void A_location_the_profile_rules_out_excludes_the_listing()
    {
        var farm = Trades with { ExcludedLocations = ["Perth WA"] };

        var city = Score(Job("Farm Hand", location: "Osborne Park, Perth WA"), farm);
        var town = Score(Job("Farm Hand", location: "Geraldton, Mid West WA"), farm);

        Assert.Equal((0, ScoreRule.LocationExcluded, "Perth WA"), (city.Final, Assert.Single(city.Items).Rule, city.Items[0].Detail));
        Assert.DoesNotContain(town.Items, i => i.Rule == ScoreRule.LocationExcluded);
    }

    [Fact]
    public void A_no_on_the_line_above_does_not_waive_a_truck_licence()
    {
        var ad = Described("Driller Offsider", "Requirements\n- No experience needed\n- Current HR licence\n- Police clearance");

        Assert.Equal(-40, Item(Score(ad), ScoreRule.TruckLicence).Points);
    }

    [Fact]
    public void Apprenticeships_and_long_contracts_are_penalised()
    {
        Assert.Equal(-25, Item(Score(Job("Apprentice Electrician"), Trades), ScoreRule.Apprenticeship).Points);
        Assert.Equal(-10, Item(Score(Job("Developer", "12 month contract")), ScoreRule.LongContract).Points);
    }

    [Fact]
    public void Missing_assistance_only_costs_when_the_policy_requires_it()
    {
        var required = ScoringPolicy.Default with { AssistanceRequired = true };

        Assert.DoesNotContain(Score(Job("Developer")).Items, i => i.Rule == ScoreRule.NoAssistance);
        Assert.Equal(-10, Item(Score(Job("Developer"), policy: required), ScoreRule.NoAssistance).Points);
        Assert.Equal(-15, Item(Score(Job("Developer", "No relocation"), policy: required), ScoreRule.NoAssistance).Points);
        Assert.DoesNotContain(Score(Job("Developer", "Relocation package"), policy: required).Items, i => i.Rule == ScoreRule.NoAssistance);
    }

    [Theory]
    [InlineData(14, null)]
    [InlineData(15, ScoreRule.Aging)]
    [InlineData(31, ScoreRule.Stale)]
    public void Old_listings_drop(int daysAgo, ScoreRule? expected)
    {
        var posted = new DateTimeOffset(Today.AddDays(-daysAgo).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rules = Score(Job("Developer", posted: posted)).Items.Select(i => i.Rule).ToList();

        if (expected is { } rule)
            Assert.Contains(rule, rules);
        else
            Assert.Empty(rules);
    }

    [Fact]
    public void Age_uses_the_utc_posting_date()
    {
        // 23:30 in Perth on the 6th is still the 6th in UTC... but 07:30 Perth on the 7th is the 6th in UTC.
        var posted = new DateTimeOffset(2026, 10, 7, 7, 30, 0, TimeSpan.FromHours(8));

        Assert.Equal("15", Item(Score(Job("Developer", posted: posted)), ScoreRule.Aging).Detail);
    }

    // ── Qualification cap ─────────────────────────────────────────────────

    [Fact]
    public void A_qualified_title_caps_the_score()
    {
        var score = Score(Job("Electrician", "Offsider welcome, no experience required", "Perth WA"), Trades);

        Assert.Equal(35, score.Final);
        Assert.Equal(ScoreWhere.Title, Item(score, ScoreRule.QualificationRequired).Where);
    }

    [Fact]
    public void Entry_titles_lift_the_qualified_title_cap()
    {
        Assert.DoesNotContain(Score(Job("Trade Assistant Electrician"), Trades).Items, i => i.Rule == ScoreRule.QualificationRequired);
    }

    [Fact]
    public void Disqualify_signals_cap_unless_the_title_is_training()
    {
        Assert.Equal(35, Score(Job("Offsider", "Must be trade qualified, no experience required", "Perth WA"), Trades).Final);
        Assert.DoesNotContain(Score(Job("Apprentice", "Become trade qualified"), Trades).Items,
            i => i.Rule == ScoreRule.QualificationRequired);
    }

    [Fact]
    public void The_cap_never_raises_a_low_score()
    {
        var score = Score(Job("Electrician", "Hospitality"), Trades with { NegativeSignals = ["hospitality"] });

        Assert.Equal(35, score.Final);
        Assert.DoesNotContain(Score(Job("Electrician"), Trades with { NegativeSignals = ["electrician"] }).Items,
            i => i.Rule == ScoreRule.QualificationRequired);
    }

    [Fact]
    public void Listings_that_are_not_excluded_never_score_zero()
    {
        var awful = Score(Job("Hospitality Cert III", "hospitality", fullText: ["guild member", "commission only"]));

        Assert.Equal(1, awful.Final);
        Assert.False(awful.Excluded);
    }

    // ── Weights ───────────────────────────────────────────────────────────

    [Fact]
    public void Weights_come_from_the_policy()
    {
        var policy = ScoringPolicy.Default with
        {
            PositiveSignalTitleBonus = 20,
            PositiveSignalBodyBonus = 4,
            TruckLicencePenalty = 25,
            ObtainableRequirementPenalty = 3,
            EligibilityDoubtPenalty = 30,
            CompressAbove = 90,
            CompressionDivisor = 5,
        };
        var fifo = Trades with { Requirements = new([], [], [], ["HRWL"]) };

        var signals = Score(Job("C# Developer", "We build .NET services"), policy: policy);
        var licences = Score(Job("Offsider", "HR licence required", fullText: ["HRWL", "guild member"]), fifo, policy);
        // 50 + 20 × 3 + 10 remote + 5 contract type + 10 Perth = 135, compressed above 90: 90 + 45 / 5
        var strong = Score(Job("C# .NET Contract Developer", "Remote", "Perth WA", "Contract/Temp"), policy: policy);

        Assert.Equal([(ScoreWhere.Title, 20), (ScoreWhere.Body, 4)],
            signals.Items.Where(i => i.Rule == ScoreRule.PositiveSignal).Select(i => (i.Where, i.Points)));
        Assert.Equal(-25, Item(licences, ScoreRule.TruckLicence).Points);
        Assert.Equal(-3, Item(licences, ScoreRule.FullTextLicence).Points);
        Assert.Equal(-30, Item(licences, ScoreRule.FullTextEligibilityDoubt).Points);
        Assert.Equal(99, strong.Final);
        Assert.Equal("90", Item(strong, ScoreRule.Compression).Detail);
    }

    // ── Candidate requirements ────────────────────────────────────────────

    private static readonly MatchingProfile Fifo = Trades with
    {
        Requirements = new([], ["years experience"], ["trade qualified"], ["HRWL", "verification of competency", "white card"]),
    };

    private static CandidateRequirements Candidate(params (string Id, RequirementState State)[] states) =>
        new(states.ToDictionary(s => s.Id, s => s.State));

    private static FitScore Score(ScoringInput job, CandidateRequirements candidate) =>
        FitScorer.Score(job, Fifo, Au, ScoringPolicy.Default, Today, candidate);

    [Fact]
    public void Without_states_requirements_weigh_the_default_of_their_kind()
    {
        var score = Score(Job("Offsider", fullText: ["years experience", "trade qualified", "HRWL", "MR licence"]),
            Fifo with { Requirements = Fifo.Requirements with { Licence = ["HRWL", "MR licence"] } });

        Assert.Equal(-20, Item(score, ScoreRule.FullTextExperience).Points);
        Assert.Equal(-20, Item(score, ScoreRule.FullTextQualification).Points);
        Assert.Equal(-10, Item(score, ScoreRule.FullTextLicence).Points);
        Assert.Equal(-40, Item(score, ScoreRule.FullTextTruckLicence).Points);
    }

    [Fact]
    public void The_candidate_state_sets_the_penalty_and_a_requirement_they_have_costs_nothing()
    {
        var job = Job("Offsider", fullText: ["years experience", "trade qualified", "HRWL"]);

        var score = Score(job, Candidate(("hrwl", RequirementState.Have), ("trade-qualification", RequirementState.OutOfReach)));

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.FullTextLicence);
        Assert.Equal(("HRWL", 10), (Item(score, ScoreRule.RequirementHeld).Detail, Item(score, ScoreRule.RequirementHeld).Points));
        Assert.Equal(-40, Item(score, ScoreRule.FullTextQualification).Points);
        Assert.Equal(-20, Item(score, ScoreRule.FullTextExperience).Points);
    }

    [Fact]
    public void The_worst_requirement_of_a_kind_counts_and_one_the_candidate_has_hides_no_other()
    {
        var job = Job("Offsider", fullText: ["HRWL", "verification of competency"]);

        var hasHrwl = Item(Score(job, Candidate(("hrwl", RequirementState.Have))), ScoreRule.FullTextLicence);
        var hrwlOutOfReach = Item(Score(job, Candidate(("hrwl", RequirementState.OutOfReach))), ScoreRule.FullTextLicence);

        Assert.Equal(("verification of competency", -10), (hasHrwl.Detail, hasHrwl.Points));
        Assert.Equal(("HRWL", -40), (hrwlOutOfReach.Detail, hrwlOutOfReach.Points));
    }

    [Fact]
    public void Having_what_the_ad_asks_for_is_a_fit_point_once_per_kind_before_the_compression()
    {
        // 50 + 16 offsider + 15 FIFO + 10 remote + 5 casual + 10 WA + 10 held = 116: compressed to 80 + 12.
        var both = Job("FIFO Offsider", "Remote site", "Perth WA", "Casual/Vacation", fullText: ["HRWL", "verification of competency"]);
        var candidate = Candidate(("hrwl", RequirementState.Have), ("voc", RequirementState.Have));

        var score = Score(both, candidate);
        var rules = score.Items.Select(i => i.Rule).ToList();

        Assert.Equal(10, Item(score, ScoreRule.RequirementHeld).Points);
        Assert.True(rules.IndexOf(ScoreRule.RequirementHeld) < rules.IndexOf(ScoreRule.Compression));
        Assert.Equal(92, score.Final);
        Assert.DoesNotContain(Score(both, CandidateRequirements.None).Items, i => i.Rule == ScoreRule.RequirementHeld);
    }

    [Theory]
    [InlineData("truck-hc", "HR licence required", true)]       // HC includes HR
    [InlineData("truck-hr", "HC licence required", false)]
    [InlineData("truck-mr", "MR/HR licence", true)]             // either class: the lower one counts
    [InlineData("truck-hr", "heavy rigid licence", true)]
    public void A_truck_licence_class_includes_the_lower_ones(string held, string ad, bool covered)
    {
        var candidate = Candidate((held, RequirementState.Have));
        var score = FitScorer.Score(Job("Labourer", ad), Trades, Au, ScoringPolicy.Default, Today, candidate);

        Assert.Equal(covered, candidate.CoversTruck(ad));
        Assert.Equal(covered, !score.Items.Any(i => i.Rule == ScoreRule.TruckLicence));
        Assert.Equal(covered, score.Items.Any(i => i.Rule == ScoreRule.RequirementHeld));
    }

    [Fact]
    public void A_held_truck_class_also_clears_the_searched_licence_phrases()
    {
        var job = Job("Offsider", fullText: ["HR licence", "MR licence"]);
        var profile = Fifo with { Requirements = Fifo.Requirements with { Licence = ["HR licence", "MR licence"] } };
        var candidate = Candidate(("truck-hr", RequirementState.Have));

        var score = FitScorer.Score(job, profile, Au, ScoringPolicy.Default, Today, candidate);

        Assert.DoesNotContain(score.Items, i => i.Rule is ScoreRule.FullTextTruckLicence or ScoreRule.TruckLicence);
        Assert.Equal(10, Item(score, ScoreRule.RequirementHeld).Points);
    }

    [Theory]
    [InlineData(5.0, ScoreRule.RequirementHeld, 10)]          // reaches the 3 years asked
    [InlineData(2.0, ScoreRule.FullTextExperience, -10)]      // a year short: obtainable
    [InlineData(0.5, ScoreRule.FullTextExperience, -20)]      // further: missing
    public void Years_of_experience_the_ad_asks_are_compared_with_the_candidates(double years, ScoreRule rule, int points)
    {
        var job = Job("Offsider", "Minimum 3 years experience in civil works", fullText: ["years experience"]);
        var candidate = new CandidateRequirements(new Dictionary<string, RequirementState>())
        {
            ExperienceYears = new Dictionary<string, double> { [Fifo.Name] = years },
        };

        var item = Item(Score(job, candidate), rule);

        Assert.Equal(("years experience (3 years asked)", points), (item.Detail, item.Points));
    }

    [Fact]
    public void Without_the_candidates_years_or_a_number_in_the_ad_experience_weighs_as_before()
    {
        var withYears = new CandidateRequirements(new Dictionary<string, RequirementState>())
        {
            ExperienceYears = new Dictionary<string, double> { [Fifo.Name] = 10 },
        };
        var noNumber = Job("Offsider", "Proven experience in civil works", fullText: ["years experience"]);
        var number = Job("Offsider", "Minimum 3 years experience in civil works", fullText: ["years experience"]);

        Assert.Equal(("years experience", -20), Pair(Item(Score(noNumber, withYears), ScoreRule.FullTextExperience)));
        Assert.Equal(("years experience", -20), Pair(Item(Score(number, CandidateRequirements.None), ScoreRule.FullTextExperience)));
        // Another profile's years say nothing about this one.
        var otherProfile = withYears with { ExperienceYears = new Dictionary<string, double> { ["tech"] = 10 } };
        Assert.Equal(-20, Item(Score(number, otherProfile), ScoreRule.FullTextExperience).Points);

        static (string, int) Pair(ScoreItem i) => (i.Detail, i.Points);
    }

    [Fact]
    public void Years_in_the_ad_count_for_a_profile_that_searches_no_experience_phrase_once_its_years_are_known()
    {
        var job = Job("C# Developer", "5+ years of commercial experience with .NET");
        static CandidateRequirements Years(double years) => new(new Dictionary<string, RequirementState>())
        {
            ExperienceYears = new Dictionary<string, double> { [Tech.Name] = years },
        };

        var senior = Score(job, Tech, null);
        var held = Item(FitScorer.Score(job, Tech, Au, ScoringPolicy.Default, Today, Years(8)), ScoreRule.RequirementHeld);
        var missing = Item(FitScorer.Score(job, Tech, Au, ScoringPolicy.Default, Today, Years(2)), ScoreRule.FullTextExperience);

        Assert.DoesNotContain(senior.Items, i => i.Rule is ScoreRule.RequirementHeld or ScoreRule.FullTextExperience);
        Assert.Equal(("experience (5 years asked)", ScoreWhere.Text, 10), (held.Detail, held.Where, held.Points));
        Assert.Equal(-20, missing.Points);
    }

    [Fact]
    public void A_phrase_outside_the_catalog_keeps_the_default_of_its_kind()
    {
        var score = Score(Job("Offsider", fullText: ["white card"]), Candidate(("hrwl", RequirementState.Have)));

        Assert.Equal(("white card", -10), (Item(score, ScoreRule.FullTextLicence).Detail, Item(score, ScoreRule.FullTextLicence).Points));
    }

    [Fact]
    public void Only_states_that_change_what_a_profile_searches_are_relevant_to_it()
    {
        var candidate = Candidate(
            ("hrwl", RequirementState.Obtainable),             // the default of a ticket
            ("trade-qualification", RequirementState.OutOfReach),
            ("degree", RequirementState.Have));                // the profile does not look for a degree

        Assert.Equal([KeyValuePair.Create("trade-qualification", RequirementState.OutOfReach)],
            candidate.Relevant(Fifo, ScoringPolicy.Default));
        Assert.Empty(CandidateRequirements.None.Relevant(Fifo, ScoringPolicy.Default));
    }
}
