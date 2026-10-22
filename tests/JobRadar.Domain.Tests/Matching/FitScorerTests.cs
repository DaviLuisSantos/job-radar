using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

/// <summary>One fictional case per rule. Real-data parity lives in <see cref="PrototypeParityTests"/>.</summary>
public class FitScorerTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
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
        FitScorer.Score(job, profile ?? Tech, policy ?? ScoringPolicy.Default, Today);

    private static ScoreItem Item(FitScore score, ScoreRule rule) => Assert.Single(score.Items, i => i.Rule == rule);

    // ── Exclusions ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Must be an Australian citizen")]
    [InlineData("NV1 security clearance")]
    [InlineData("Full Australian working rights - essential")]
    public void Citizenship_or_PR_requirements_exclude(string summary)
    {
        var score = Score(Job("Developer", summary));

        Assert.True(score.Excluded);
        Assert.Equal(ScoreRule.CitizenshipRequired, Item(score, ScoreRule.CitizenshipRequired).Rule);
    }

    [Theory]
    [InlineData("Unlimited working rights required")]
    [InlineData("You need 12 months of valid working rights")]
    [InlineData("No working holiday visas")]
    public void Work_rights_a_working_holiday_visa_cannot_give_exclude(string summary)
    {
        Assert.True(Score(Job("Developer", summary)).Excluded);
    }

    [Fact]
    public void An_excluding_phrase_in_the_full_text_excludes()
    {
        var score = Score(Job("Developer", fullText: "unrestricted working rights"));

        Assert.True(score.Excluded);
        Assert.Equal(ScoreWhere.FullText, Item(score, ScoreRule.WorkRightsIncompatible).Where);
    }

    [Fact]
    public void Identified_roles_exclude()
    {
        Assert.True(Score(Job("Indigenous Liaison Officer")).Excluded);
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
    public void Sponsorship_mentioned_adds_the_bonus_and_denied_adds_nothing()
    {
        Assert.Equal(10, Item(Score(Job("Developer", "482 visa sponsorship available")), ScoreRule.SponsorshipMentioned).Points);
        Assert.Equal(0, Item(Score(Job("Developer", "Sponsorship is not available")), ScoreRule.SponsorshipDenied).Points);
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
    public void Working_holiday_friendly_from_text_or_full_text()
    {
        Assert.Equal(ScoreWhere.Text, Item(Score(Job("Barista", "Backpackers welcome")), ScoreRule.WorkingHolidayFriendly).Where);
        Assert.Equal(ScoreWhere.FullText, Item(Score(Job("Barista", fullText: "88 days")), ScoreRule.WorkingHolidayFriendly).Where);
    }

    [Theory]
    [InlineData("Contract/Temp", true)]
    [InlineData("Casual/Vacation", true)]
    [InlineData("Full time", false)]
    public void Contract_temp_and_casual_fit_six_months_per_employer(string workType, bool bonus)
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
            fullText: "full working rights"));

        Assert.Equal(94 - 10, score.Final);
        Assert.Equal(ScoreRule.FullTextAmbiguousRights, score.Items[^1].Rule);
    }

    [Fact]
    public void Citizen_and_twelve_month_phrases_are_cancelled_by_a_working_holiday_mention()
    {
        var withoutWhv = Score(Job("Barista", fullText: ["australian citizen", "12 months + working rights"]));
        var withWhv = Score(Job("Barista", fullText: ["australian citizen", "12 months + working rights", "working holiday"]));

        Assert.Equal(1, withoutWhv.Final);   // 50 − 55 − 40, floored
        Assert.Equal(65, withWhv.Final);     // 50 + 15
    }

    [Fact]
    public void Working_holiday_next_to_citizenship_or_residency_is_a_refusal_not_a_welcome()
    {
        var score = Score(Job("Barista", fullText: ["working holiday", "working holiday + residency", "12 months + working rights"]));

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.WorkingHolidayFriendly);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextWhvWithResidency).Points);
        Assert.Equal(-40, Item(score, ScoreRule.FullTextTwelveMonthRights).Points);
    }

    [Fact]
    public void Working_holiday_with_citizenship_is_penalised_once()
    {
        var score = Score(Job("Barista", fullText: ["australian citizen", "working holiday", "working holiday + citizen"]));

        Assert.Equal(-55, Item(score, ScoreRule.FullTextCitizenship).Points);
        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.FullTextWhvWithResidency);
        Assert.Equal(1, score.Final);   // 50 − 55, floored
    }

    [Fact]
    public void A_welcome_in_the_listing_own_text_still_wins_over_the_doubtful_full_text()
    {
        var score = Score(Job("Barista", "Backpackers welcome", fullText: ["working holiday", "working holiday + citizen"]));

        Assert.Equal(ScoreWhere.Text, Item(score, ScoreRule.WorkingHolidayFriendly).Where);
        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.FullTextWhvWithResidency);
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
    public void Requirements_found_in_the_full_ad_cost_twenty_once_per_kind()
    {
        var score = Score(Job("Labourer", fullText: ["years experience + FIFO", "minimum 2 years + fifo", "cert III + FIFO", "HRWL + FIFO"]),
            FifoWithRequirements);

        Assert.Equal(-20, Item(score, ScoreRule.FullTextExperience).Points);
        Assert.Equal("years experience", Item(score, ScoreRule.FullTextExperience).Detail);
        Assert.Equal(-20, Item(score, ScoreRule.FullTextQualification).Points);
        Assert.Equal(-20, Item(score, ScoreRule.FullTextLicence).Points);
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
    public void Citizenship_in_a_captured_ad_costs_55_unless_the_ad_welcomes_working_holiday_makers()
    {
        var citizensOnly = Score(Described("Labourer", "Applicants must be Australian citizens or permanent residents."), Trades);
        var welcome = Score(Described("Labourer", "Open to Australian citizens, PR and working holiday visa holders."), Trades);

        Assert.True(citizensOnly.Final > 0);   // weighed, not excluded: the ad may welcome other visas
        Assert.Equal(ScoreWhere.Description, Item(citizensOnly, ScoreRule.FullTextCitizenship).Where);
        Assert.Equal(-55, Item(citizensOnly, ScoreRule.FullTextCitizenship).Points);
        Assert.Equal(ScoreWhere.Description, Item(welcome, ScoreRule.WorkingHolidayFriendly).Where);
        Assert.DoesNotContain(welcome.Items, i => i.Rule == ScoreRule.FullTextCitizenship);
    }

    [Fact]
    public void A_negated_working_holiday_mention_in_a_captured_ad_earns_nothing_and_beats_the_searched_phrase()
    {
        var score = Score(Described("Labourer", "Australian citizens only apply. We are unable to accept working holiday visa holders.",
            "working holiday"), Trades);

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.WorkingHolidayFriendly);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextCitizenship).Points);
    }

    [Fact]
    public void Work_rights_a_working_holiday_cannot_give_exclude_from_a_captured_ad()
    {
        var score = Score(Described("Labourer", "You must have unrestricted work rights in Australia to apply for this role."), Trades);

        Assert.Equal(0, score.Final);
        Assert.Equal(ScoreWhere.Description, Item(score, ScoreRule.WorkRightsIncompatible).Where);
    }

    [Fact]
    public void Requirements_are_read_from_a_captured_ad_unless_negated_just_before()
    {
        var asks = Score(Described("Labourer", "Essential: minimum 2 years in a similar role and a current HR licence."), FifoWithRequirements);
        var waives = Score(Described("Labourer", "No HRWL needed, and no minimum 2 years either: we train you from day one."), FifoWithRequirements);

        Assert.Equal(ScoreWhere.Description, Item(asks, ScoreRule.FullTextExperience).Where);
        Assert.Equal(-20, Item(asks, ScoreRule.TruckLicence).Points);   // "HR licence" read locally counts once
        Assert.DoesNotContain(waives.Items, i => i.Rule is ScoreRule.FullTextExperience or ScoreRule.FullTextLicence);
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
        var score = Score(Job("Barista", fullText: ["australian citizen + FIFO"]));

        Assert.Equal("australian citizen", Item(score, ScoreRule.FullTextCitizenship).Detail);
    }

    [Fact]
    public void A_composite_policy_term_is_never_read_as_another_term_plus_a_context_word()
    {
        // "working holiday + citizen" is its own term (a refusal), not "working holiday" found with "citizen".
        var score = Score(Job("Barista", fullText: ["working holiday + citizen"]));

        Assert.DoesNotContain(score.Items, i => i.Rule == ScoreRule.WorkingHolidayFriendly);
        Assert.Equal(-55, Item(score, ScoreRule.FullTextWhvWithResidency).Points);
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
        Assert.Equal(-20, licence.Points);
        Assert.DoesNotContain(apprentice.Items, i => i.Rule == ScoreRule.FullTextQualification);
    }

    [Fact]
    public void Commission_and_truck_licence_count_once_whether_local_or_full_text()
    {
        Assert.Equal(-30, Item(Score(Job("Sales", "Commission only role")), ScoreRule.CommissionOnly).Points);
        var both = Score(Job("Sales", "Commission only role", fullText: "commission only"));
        Assert.DoesNotContain(both.Items, i => i.Rule == ScoreRule.CommissionOnly);
        Assert.Equal(-30, Item(both, ScoreRule.FullTextCommission).Points);
        Assert.Equal(-20, Item(Score(Job("Driver", "HR licence required")), ScoreRule.TruckLicence).Points);
    }

    [Fact]
    public void Apprenticeships_and_long_contracts_are_penalised()
    {
        Assert.Equal(-25, Item(Score(Job("Apprentice Electrician"), Trades), ScoreRule.Apprenticeship).Points);
        Assert.Equal(-10, Item(Score(Job("Developer", "12 month contract")), ScoreRule.LongContract).Points);
    }

    [Fact]
    public void Missing_sponsorship_only_costs_when_the_visa_requires_it()
    {
        var required = ScoringPolicy.Default with { VisaRequired = true };

        Assert.DoesNotContain(Score(Job("Developer")).Items, i => i.Rule == ScoreRule.NoSponsorship);
        Assert.Equal(-10, Item(Score(Job("Developer"), policy: required), ScoreRule.NoSponsorship).Points);
        Assert.Equal(-15, Item(Score(Job("Developer", "No sponsorship"), policy: required), ScoreRule.NoSponsorship).Points);
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
        var awful = Score(Job("Hospitality Cert III", "hospitality", fullText: ["australian citizen", "commission only"]));

        Assert.Equal(1, awful.Final);
        Assert.False(awful.Excluded);
    }
}
