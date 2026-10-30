namespace JobRadar.Domain.Matching;

/// <summary>
/// Fit score of a listing for a profile. Pure: same input, same score.
///
/// Order matters and is part of the rules:
/// 1. Exclusions (score 0): citizenship/PR, work rights a WHV cannot give, identified roles, a location
///    or a title word the profile rules out, on-site work outside where the profile accepts it, leadership
///    and (for most profiles) senior titles.
/// 2. Points from base 50: profile signals (title worth twice the body), sponsorship, remote,
///    FIFO, entry level, WHV-friendly, flexible work type, preferred location.
/// 3. Compression above a threshold, so the best listings keep their order instead of tying at 100.
/// 4. Restrictions and penalties after compression, so a listing with points to spare cannot
///    absorb them: full-text phrases, apprenticeships, long contracts, commission only, English as a
///    condition, truck licence, no sponsorship (when required), age.
/// 5. Qualification cap: a listing that needs a qualification the candidate lacks stays
///    visible below the ones they can apply to.
///
/// The phases are the same in every country; the phrases each one reads are the listing's market's
/// (<see cref="CountryRules"/>), and the weights the policy's.
/// </summary>
public static class FitScorer
{
    /// <summary>Bump when a rule changes, so stored scores are recomputed.</summary>
    public const int RulesVersion = 14;

    /// <summary>Shown in the explanation of an excluded listing, whose score is 0 whatever the points.</summary>
    private const int ExclusionPoints = -50;

    /// <param name="candidate">The candidate's state on each requirement; none means every requirement at its default.</param>
    public static FitScore Score(
        ScoringInput job, MatchingProfile profile, ScoringPolicy policy, DateOnly today, CandidateRequirements? candidate = null)
    {
        candidate ??= CandidateRequirements.None;
        var rules = CountryRules.For(job.Country);
        var title = job.Title.ToLowerInvariant();
        var body = string.Join(" ", job.Summary ?? "", job.LocationLabel ?? "", job.WorkTypeLabel ?? "").ToLowerInvariant();
        var full = $"{title} {body}";
        // The captured full ad, when there is one. Read only by the rules about requirements and work
        // rights: profile signals stay on title and summary, or a listing with a description would
        // score differently just for having more text ("report to the site manager").
        var desc = job.Description?.ToLowerInvariant() ?? "";
        var described = desc.Length > 0;
        var fullAndDesc = described ? $"{full} {desc}" : full;
        var items = new List<ScoreItem>();

        FitScore Excluded(ScoreRule rule, string detail, ScoreWhere where)
        {
            items.Add(new ScoreItem(rule, detail, where, ExclusionPoints));
            return new FitScore(0, items);
        }

        var groups = FullTextHits(job.FullTextTerms, policy.FullTextGroups);

        // ── 1. Exclusions ─────────────────────────────────────────────────
        if (rules.CitizenshipRequired.Match(full) is { Success: true } citizen)
            return Excluded(ScoreRule.CitizenshipRequired, citizen.Value, ScoreWhere.Text);
        if (rules.WorkRightsIncompatible.Match(full) is { Success: true } rights)
            return Excluded(ScoreRule.WorkRightsIncompatible, rights.Value, ScoreWhere.Text);
        // Specific enough to exclude on the full ad too. Citizenship is not: "Australian or New Zealand
        // citizens, PR or valid working visa holders" welcomes a WHV; it is weighed below.
        if (described && rules.WorkRightsIncompatible.Match(desc) is { Success: true } descRights)
            return Excluded(ScoreRule.WorkRightsIncompatible, descRights.Value, ScoreWhere.Description);
        if (groups.TryGetValue(FullTextGroup.Exclude, out var excludeHits))
            return Excluded(ScoreRule.WorkRightsIncompatible, excludeHits[0], ScoreWhere.FullText);
        if (rules.IdentifiedRole.Match(title) is { Success: true } identified)
            return Excluded(ScoreRule.IdentifiedRole, identified.Value, ScoreWhere.Title);
        if (profile.ExcludedLocations.FirstOrDefault(l => ListingPatterns.Signal(l).IsMatch(job.LocationLabel ?? "")) is { } excludedAt)
            return Excluded(ScoreRule.LocationExcluded, excludedAt, ScoreWhere.Location);
        if (profile.ExcludedTitles.FirstOrDefault(t => ListingPatterns.Signal(t).IsMatch(title)) is { } excludedTitle)
            return Excluded(ScoreRule.TitleExcluded, excludedTitle, ScoreWhere.Title);
        if (profile.OnSiteLocations.Count > 0 && rules.OnSite.IsMatch(full)
            && !profile.OnSiteLocations.Any(l => ListingPatterns.Signal(l).IsMatch(job.LocationLabel ?? "")))
            return Excluded(ScoreRule.OnSiteElsewhere, job.LocationLabel ?? "", ScoreWhere.Location);

        var juniorTitle = rules.JuniorTitle.IsMatch(title);
        if (policy.ExcludeSeniorTitles && !juniorTitle)
        {
            if (rules.LeadershipTitle.Match(title) is { Success: true } lead)
                return Excluded(ScoreRule.LeadershipTitle, lead.Value, ScoreWhere.Title);
            if (!profile.AllowSenior && rules.SeniorRank.Match(title) is { Success: true } senior)
                return Excluded(ScoreRule.SeniorTitle, senior.Value, ScoreWhere.Title);
        }

        var trainingTitle = rules.TrainingTitle.IsMatch(title);
        var score = FitScore.Base;

        void Add(ScoreRule rule, string detail, ScoreWhere where, int points)
        {
            items.Add(new ScoreItem(rule, detail, where, points));
            score += points;
        }

        // "Senior" subtracts for profiles that allow it; "Mid to Senior" does not.
        if (profile.AllowSenior && !juniorTitle && rules.SeniorRank.IsMatch(title) && !rules.MidTitle.IsMatch(title))
            Add(ScoreRule.SeniorPenalty, "senior", ScoreWhere.Title, -policy.SeniorPenalty);

        // ── 2. Points ─────────────────────────────────────────────────────
        foreach (var signal in profile.PositiveSignals)
        {
            if (ListingPatterns.MatchesUnnegated(signal, title))
                Add(ScoreRule.PositiveSignal, signal, ScoreWhere.Title, policy.PositiveSignalTitleBonus);
            else if (ListingPatterns.MatchesUnnegated(signal, body))
                Add(ScoreRule.PositiveSignal, signal, ScoreWhere.Body, policy.PositiveSignalBodyBonus);
        }

        foreach (var signal in profile.NegativeSignals)
        {
            if (trainingTitle && rules.QualificationWords.Any(w => signal.Contains(w, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (ListingPatterns.Signal(signal).IsMatch(title))
                Add(ScoreRule.NegativeSignal, signal, ScoreWhere.Title, -policy.NegativeSignalTitlePenalty);
            else if (ListingPatterns.Signal(signal).IsMatch(body))
                Add(ScoreRule.NegativeSignal, signal, ScoreWhere.Body, -policy.NegativeSignalBodyPenalty);
        }

        // "sponsor" as a substring also covers "sponsorship", "sponsored"; the offered phrases
        // are substrings too ("no sponsorship available" contains one), so both are gated by
        // the denial pattern.
        var sponsorDenied = rules.SponsorshipDenied.IsMatch(full);
        var sponsorConfirmed = !sponsorDenied && rules.SponsorshipOffered.Any(full.Contains);
        if (sponsorConfirmed || (full.Contains("sponsor", StringComparison.Ordinal) && !sponsorDenied))
            Add(ScoreRule.SponsorshipMentioned, "sponsor", ScoreWhere.Text, policy.SponsorBonus);
        else if (sponsorDenied)
            Add(ScoreRule.SponsorshipDenied, "sponsor", ScoreWhere.Text, 0);

        if (profile.RewardRemote && ListingPatterns.Signal(rules.Remote).IsMatch(full))
            Add(ScoreRule.Remote, "remote", ScoreWhere.Text, policy.RemoteBonus);
        else if (profile.RewardRemote && ListingPatterns.Signal(rules.Hybrid).IsMatch(full))
            Add(ScoreRule.Hybrid, "hybrid", ScoreWhere.Text, policy.HybridBonus);

        // Not twice when the profile already rewards FIFO as a signal.
        var fifoInProfile = profile.PositiveSignals.Any(s =>
            s.Contains("fifo", StringComparison.OrdinalIgnoreCase) || s.Contains("fly-in", StringComparison.OrdinalIgnoreCase));
        if (!fifoInProfile && rules.Fifo.Any(f => ListingPatterns.Signal(f).IsMatch(full)))
            Add(ScoreRule.Fifo, "fifo", ScoreWhere.Text, policy.FifoBonus);

        if (profile.EntryLevelBoost)
        {
            foreach (var phrase in rules.NoExperience)
            {
                if (ListingPatterns.MatchesUnnegated(phrase, full))
                    Add(ScoreRule.EntryLevel, phrase, ScoreWhere.Text, policy.EntryLevelBonus);
            }
        }

        // The full-text search gives no context: next to citizenship or residency, a working holiday
        // mention is mostly a refusal. The listing's own text still counts: it was read, not searched.
        // A captured ad has the context the search lacks: it replaces the searched WHV phrases, and its
        // mention counts unless negated just before ("unable to accept working holiday visas"); a
        // refusal after it ("working holiday visa holders will not") was excluded above.
        var whvLocal = rules.WorkingHolidayFriendly.Match(full);
        var whvDescribed = described && rules.WorkingHolidayFriendly.Match(desc) is { Success: true } inDesc && ListingPatterns.MatchesUnnegated(inDesc.Value, desc)
            ? inDesc.Value
            : null;
        var whvDoubtful = groups.ContainsKey(FullTextGroup.WhvWithResidency);
        var whvHits = described || whvDoubtful ? null : groups.GetValueOrDefault(FullTextGroup.Whv);
        var whv = whvLocal.Success || whvDescribed is not null || whvHits is not null;
        if (whvLocal.Success)
            Add(ScoreRule.WorkingHolidayFriendly, whvLocal.Value, ScoreWhere.Text, policy.WorkingHolidayBonus);
        else if (whvDescribed is not null)
            Add(ScoreRule.WorkingHolidayFriendly, whvDescribed, ScoreWhere.Description, policy.WorkingHolidayBonus);
        else if (whvHits is not null)
            Add(ScoreRule.WorkingHolidayFriendly, whvHits[0], ScoreWhere.FullText, policy.WorkingHolidayBonus);

        // Contract, temp and casual fit the six months per employer.
        if (job.WorkTypeLabel is { } workType && rules.FlexibleWorkType.IsMatch(workType))
            Add(ScoreRule.FlexibleWorkType, workType, ScoreWhere.WorkType, policy.FlexibleWorkTypeBonus);

        var location = job.LocationLabel ?? "";
        if (profile.PreferredLocations.FirstOrDefault(l => ListingPatterns.Signal(l).IsMatch(location)) is { } preferred)
            Add(ScoreRule.PreferredLocation, preferred, ScoreWhere.Location, policy.PreferredLocationBonus);

        // The profile's requirements the ad asks for, with the candidate's state on each. The context word only
        // narrows the search: a phrase found with any context ("HC licence + FIFO", searched for another
        // profile) is in the ad. A captured ad is read instead of the search, which also matches what the ad does
        // not say; there the phrase counts when not negated just before it ("no HR licence needed").
        var found = job.FullTextTerms.Select(t => new FullTextTerm(t).Phrases[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asked = profile.Requirements.Terms()
            .Select(r => (r.Kind, Truck: r.Kind == RequirementKind.Licence && rules.TruckLicence.IsMatch(r.Phrase), r.Phrase,
                Where: described ? (ListingPatterns.MatchesUnnegated(r.Phrase, desc) ? ScoreWhere.Description : ScoreWhere.None)
                    : found.Contains(r.Phrase) ? ScoreWhere.FullText : ScoreWhere.None))
            .Where(r => r.Where != ScoreWhere.None)
            .Select(r => (r.Kind, r.Truck, r.Phrase, r.Where, State: candidate.Of(r.Kind, r.Phrase, policy)))
            .ToList();
        // Experience the ad counts in years, read in its own text, against the candidate's years in this line of
        // work: the number decides instead of the item's state. Neither known, nothing changes. The number is a
        // requirement on its own: a profile that searches no experience phrase (tech) still weighs it once the
        // candidate gave their years for it.
        if (rules.YearsAsked(fullAndDesc) is { } yearsAsked
            && candidate.ExperienceState(profile.Name, yearsAsked) is { } experience)
        {
            var years = $"{yearsAsked} {(yearsAsked == 1 ? "year" : "years")} asked";
            if (asked.Any(r => r.Kind == RequirementKind.Experience))
                asked = asked
                    .Select(r => r.Kind == RequirementKind.Experience ? r with { Phrase = $"{r.Phrase} ({years})", State = experience } : r)
                    .ToList();
            else
                asked.Add((RequirementKind.Experience, false, $"experience ({years})",
                    described && rules.YearsAsked(desc) is not null ? ScoreWhere.Description : ScoreWhere.Text, experience));
        }
        var truckAsked = rules.TruckLicenceAsked(fullAndDesc);
        var truckHeld = truckAsked is { } asksTruck && candidate.CoversTruck(asksTruck.Value);
        var truckGroupHit = !described && groups.TryGetValue(FullTextGroup.Licence, out var truckHits) ? truckHits[0] : null;
        var truckGroupHeld = truckGroupHit is not null && candidate.CoversTruck(truckGroupHit);

        // An ad that asks for what the candidate has: a fit point, once per kind (a truck licence apart from a
        // ticket), before the compression like the other points.
        var held = asked.Where(r => r.State == RequirementState.Have).Select(r => (r.Kind, r.Truck, r.Phrase, r.Where)).ToList();
        if (truckHeld)
            held.Add((RequirementKind.Licence, true, truckAsked!.Value.Value, ScoreWhere.Text));
        if (truckGroupHeld)
            held.Add((RequirementKind.Licence, true, truckGroupHit!, ScoreWhere.FullText));
        foreach (var requirement in held.DistinctBy(r => (r.Kind, r.Truck)))
            Add(ScoreRule.RequirementHeld, requirement.Phrase, requirement.Where, policy.HaveRequirementBonus);

        // ── 3. Compression ────────────────────────────────────────────────
        if (score > policy.CompressAbove)
        {
            var above = policy.CompressAbove;
            var compressed = Math.Min(100, above + (int)Math.Round((score - above) / (double)policy.CompressionDivisor, MidpointRounding.ToEven));
            Add(ScoreRule.Compression, above.ToString(System.Globalization.CultureInfo.InvariantCulture), ScoreWhere.None, compressed - score);
        }

        // ── 4. Restrictions and penalties ─────────────────────────────────
        foreach (var (group, rule, points) in FullTextRestrictions(policy))
        {
            if (!groups.TryGetValue(group, out var hits))
                continue;
            // The work-rights phrases and the truck licence are read from a captured ad instead (below):
            // the search gives no context ("HR licence desirable"), the ad does.
            if (described && group is FullTextGroup.Citizen or FullTextGroup.Ambiguous or FullTextGroup.Rights12Months
                    or FullTextGroup.WhvWithResidency or FullTextGroup.Licence)
                continue;
            if (whv && group is FullTextGroup.Citizen or FullTextGroup.Rights12Months or FullTextGroup.WhvWithResidency)
                continue;
            // Same reason, counted once.
            if (group == FullTextGroup.WhvWithResidency && groups.ContainsKey(FullTextGroup.Citizen))
                continue;
            if (group == FullTextGroup.Licence && truckGroupHeld && (truckAsked is null || truckHeld))
                continue;
            Add(rule, hits[0], ScoreWhere.FullText, points);
        }

        if (described)
        {
            if (!whv && rules.CitizenshipRequired.Match(desc) is { Success: true } descCitizen)
                Add(ScoreRule.FullTextCitizenship, descCitizen.Value, ScoreWhere.Description, -policy.CitizenshipPenalty);
            if (policy.FullTextGroups.GetValueOrDefault(FullTextGroup.Ambiguous)?.FirstOrDefault(p => ListingPatterns.Signal(p).IsMatch(desc)) is { } ambiguous)
                Add(ScoreRule.FullTextAmbiguousRights, ambiguous, ScoreWhere.Description, -policy.AmbiguousRightsPenalty);
        }

        // Requirements of the profile found in the full ad: once per kind, a truck licence apart from a
        // ticket, at the penalty of the candidate's state on it (the worst, when the ad asks for several of a
        // kind; one the candidate has costs nothing). A truck licence already counted (local text or the
        // policy's licence group) is not counted again; a training title is where the trade certificate is
        // earned.
        var truckCounted = (!described && groups.ContainsKey(FullTextGroup.Licence)) || truckAsked is not null;
        var requirements = asked
            .Select(r => (r.Kind, r.Truck, r.Phrase, r.Where, Points: RequirementPenalty(r.State, policy)))
            .Where(r => r.Points > 0)
            .GroupBy(r => (r.Kind, r.Truck))
            .Select(g => g.MaxBy(r => r.Points));
        foreach (var requirement in requirements)
        {
            if (requirement.Truck && truckCounted)
                continue;
            if (requirement.Kind == RequirementKind.Qualification && trainingTitle)
                continue;
            var rule = requirement switch
            {
                { Truck: true } => ScoreRule.FullTextTruckLicence,
                { Kind: RequirementKind.Licence } => ScoreRule.FullTextLicence,
                { Kind: RequirementKind.Experience } => ScoreRule.FullTextExperience,
                _ => ScoreRule.FullTextQualification,
            };
            Add(rule, requirement.Phrase, requirement.Where, -requirement.Points);
        }

        if (policy.ApprenticeshipPenalty > 0 && trainingTitle)
            Add(ScoreRule.Apprenticeship, "apprentice", ScoreWhere.Title, -policy.ApprenticeshipPenalty);

        if (policy.LongContractPenalty > 0 && rules.LongContract.Match(fullAndDesc) is { Success: true } longContract)
            Add(ScoreRule.LongContract, longContract.Value, ScoreWhere.Text, -policy.LongContractPenalty);

        if (rules.CommissionOnly.Match(fullAndDesc) is { Success: true } commission && !groups.ContainsKey(FullTextGroup.Commission))
            Add(ScoreRule.CommissionOnly, commission.Value, ScoreWhere.Text, -policy.CommissionOnlyPenalty);
        if (rules.EnglishRequired.Match(fullAndDesc) is { Success: true } english)
            Add(ScoreRule.EnglishRequired, english.Value, ScoreWhere.Text, -policy.EnglishRequiredPenalty);
        if (truckAsked is { } truck && !truckHeld && (described || !groups.ContainsKey(FullTextGroup.Licence)))
        {
            if (truck.Desirable)
                Add(ScoreRule.TruckLicence, $"{truck.Value} (desirable)", ScoreWhere.Text, -policy.DesirableTruckLicencePenalty);
            else
                Add(ScoreRule.TruckLicence, truck.Value, ScoreWhere.Text, -policy.TruckLicencePenalty);
        }

        if (policy.VisaRequired && !sponsorConfirmed)
            Add(ScoreRule.NoSponsorship, "sponsor", ScoreWhere.None, -(sponsorDenied ? policy.SponsorDeniedPenalty : policy.NoSponsorPenalty));

        if (job.PostedAt is { } posted)
        {
            var age = today.DayNumber - DateOnly.FromDateTime(posted.UtcDateTime).DayNumber;
            if (age > policy.StaleAfterDays)
                Add(ScoreRule.Stale, age.ToString(System.Globalization.CultureInfo.InvariantCulture), ScoreWhere.PostedDate, -policy.StalePenalty);
            else if (age > policy.AgingAfterDays)
                Add(ScoreRule.Aging, age.ToString(System.Globalization.CultureInfo.InvariantCulture), ScoreWhere.PostedDate, -policy.AgingPenalty);
        }

        // ── 5. Qualification cap ──────────────────────────────────────────
        (string Detail, ScoreWhere Where)? reason = null;
        if (!rules.EntryTitle.IsMatch(title) && profile.QualifiedTitles.FirstOrDefault(t => ListingPatterns.QualifiedTitle(t).IsMatch(title)) is { } qualified)
            reason = (qualified, ScoreWhere.Title);
        if (reason is null && !trainingTitle && profile.DisqualifySignals.FirstOrDefault(s => ListingPatterns.Signal(s).IsMatch(full)) is { } disqualify)
            reason = (disqualify, ScoreWhere.Text);
        if (reason is { } r && score > policy.DisqualifyMaxScore)
            Add(ScoreRule.QualificationRequired, r.Detail, r.Where, policy.DisqualifyMaxScore - score);

        return new FitScore(Math.Clamp(score, 1, 100), items);
    }

    private static int RequirementPenalty(RequirementState state, ScoringPolicy policy) => state switch
    {
        RequirementState.Have => 0,
        RequirementState.Obtainable => policy.ObtainableRequirementPenalty,
        RequirementState.Missing => policy.MissingRequirementPenalty,
        RequirementState.OutOfReach => policy.OutOfReachRequirementPenalty,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static (string Group, ScoreRule Rule, int Points)[] FullTextRestrictions(ScoringPolicy policy) =>
    [
        (FullTextGroup.Citizen, ScoreRule.FullTextCitizenship, -policy.CitizenshipPenalty),
        (FullTextGroup.WhvWithResidency, ScoreRule.FullTextWhvWithResidency, -policy.WhvWithResidencyPenalty),
        (FullTextGroup.Ambiguous, ScoreRule.FullTextAmbiguousRights, -policy.AmbiguousRightsPenalty),
        (FullTextGroup.Rights12Months, ScoreRule.FullTextTwelveMonthRights, -policy.TwelveMonthRightsPenalty),
        (FullTextGroup.Commission, ScoreRule.FullTextCommission, -policy.CommissionOnlyPenalty),
        (FullTextGroup.Licence, ScoreRule.FullTextTruckLicence, -policy.TruckLicencePenalty),
    ];

    /// <summary>
    /// Groups with at least one phrase found, each with the phrases found, in policy order. A term found
    /// with a context word ("australian citizen + FIFO", see <see cref="ScoringPolicy.ContextGroups"/>)
    /// counts as the term; a mark that is itself a policy term ("working holiday + citizen") is never
    /// read as another term plus a context word.
    /// </summary>
    private static Dictionary<string, List<string>> FullTextHits(
        IReadOnlyCollection<string> found, IReadOnlyDictionary<string, IReadOnlyList<string>> groups)
    {
        var policyTerms = groups.Values.SelectMany(t => t).Select(t => t.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var lower = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in found.Select(t => t.ToLowerInvariant()))
        {
            lower.Add(term);
            var cut = term.LastIndexOf(" + ", StringComparison.Ordinal);
            if (cut > 0 && !policyTerms.Contains(term))
                lower.Add(term[..cut]);
        }
        return groups
            .Select(g => (g.Key, Hits: g.Value.Where(t => lower.Contains(t.ToLowerInvariant())).ToList()))
            .Where(g => g.Hits.Count > 0)
            .ToDictionary(g => g.Key, g => g.Hits, StringComparer.Ordinal);
    }
}
