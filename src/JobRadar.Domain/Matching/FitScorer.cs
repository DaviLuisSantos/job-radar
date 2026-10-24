using static JobRadar.Domain.Matching.ListingPatterns;

namespace JobRadar.Domain.Matching;

/// <summary>
/// Fit score of a listing for a profile. Pure: same input, same score.
///
/// Order matters and is part of the rules:
/// 1. Exclusions (score 0): citizenship/PR, work rights a WHV cannot give, identified roles,
///    leadership and (for most profiles) senior titles.
/// 2. Points from base 50: profile signals (title worth twice the body), sponsorship, remote,
///    FIFO, entry level, WHV-friendly, flexible work type, preferred location.
/// 3. Compression above 80, so the best listings keep their order instead of tying at 100.
/// 4. Restrictions and penalties after compression, so a listing with points to spare cannot
///    absorb them: full-text phrases, apprenticeships, long contracts, commission only, truck
///    licence, no sponsorship (when required), age.
/// 5. Qualification cap: a listing that needs a qualification the candidate lacks stays
///    visible below the ones they can apply to.
/// </summary>
public static class FitScorer
{
    /// <summary>Bump when a rule changes, so stored scores are recomputed.</summary>
    public const int RulesVersion = 8;

    private const int ExclusionPoints = -50;

    /// <summary>
    /// Per requirement kind found in the full ad. Moderate: the search gives no context, and
    /// "cert III an advantage" matches as well as "cert III essential".
    /// </summary>
    private const int RequirementPenalty = 20;

    /// <summary>
    /// A truck licence class (HR/HC/MR/MC) the ad asks for: it takes years of holding a car licence, so
    /// a newcomer cannot get one for the job. Told by the candidate (2026-10-23): these ads are bad.
    /// </summary>
    private const int TruckLicencePenalty = 40;

    /// <summary>A truck licence the ad calls desirable ("MR licence highly regarded"): not a condition.</summary>
    private const int DesirableTruckLicencePenalty = 10;

    /// <summary>
    /// A ticket earned with a short course (HRWL, VOC): the candidate would get one for the job. Lower
    /// than <see cref="RequirementPenalty"/>, which stands for years of experience or a trade.
    /// </summary>
    private const int TicketPenalty = 10;

    public static FitScore Score(ScoringInput job, MatchingProfile profile, ScoringPolicy policy, DateOnly today)
    {
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
        if (CitizenshipRequired.Match(full) is { Success: true } citizen)
            return Excluded(ScoreRule.CitizenshipRequired, citizen.Value, ScoreWhere.Text);
        if (WorkRightsIncompatible.Match(full) is { Success: true } rights)
            return Excluded(ScoreRule.WorkRightsIncompatible, rights.Value, ScoreWhere.Text);
        // Specific enough to exclude on the full ad too. Citizenship is not: "Australian or New Zealand
        // citizens, PR or valid working visa holders" welcomes a WHV; it is weighed below.
        if (described && WorkRightsIncompatible.Match(desc) is { Success: true } descRights)
            return Excluded(ScoreRule.WorkRightsIncompatible, descRights.Value, ScoreWhere.Description);
        if (groups.TryGetValue(FullTextGroup.Exclude, out var excludeHits))
            return Excluded(ScoreRule.WorkRightsIncompatible, excludeHits[0], ScoreWhere.FullText);
        if (IdentifiedRole.Match(title) is { Success: true } identified)
            return Excluded(ScoreRule.IdentifiedRole, identified.Value, ScoreWhere.Title);

        var juniorTitle = JuniorTitle.IsMatch(title);
        if (policy.ExcludeSeniorTitles && !juniorTitle)
        {
            if (LeadershipTitle.Match(title) is { Success: true } lead)
                return Excluded(ScoreRule.LeadershipTitle, lead.Value, ScoreWhere.Title);
            if (!profile.AllowSenior && SeniorRank.Match(title) is { Success: true } senior)
                return Excluded(ScoreRule.SeniorTitle, senior.Value, ScoreWhere.Title);
        }

        var trainingTitle = TrainingTitle.IsMatch(title);
        var score = FitScore.Base;

        void Add(ScoreRule rule, string detail, ScoreWhere where, int points)
        {
            items.Add(new ScoreItem(rule, detail, where, points));
            score += points;
        }

        // "Senior" subtracts for profiles that allow it; "Mid to Senior" does not.
        if (profile.AllowSenior && !juniorTitle && SeniorRank.IsMatch(title) && !MidTitle.IsMatch(title))
            Add(ScoreRule.SeniorPenalty, "senior", ScoreWhere.Title, -15);

        // ── 2. Points ─────────────────────────────────────────────────────
        foreach (var signal in profile.PositiveSignals)
        {
            if (MatchesUnnegated(signal, title))
                Add(ScoreRule.PositiveSignal, signal, ScoreWhere.Title, 16);
            else if (MatchesUnnegated(signal, body))
                Add(ScoreRule.PositiveSignal, signal, ScoreWhere.Body, 8);
        }

        foreach (var signal in profile.NegativeSignals)
        {
            if (trainingTitle && QualificationWords.Any(w => signal.Contains(w, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (Signal(signal).IsMatch(title))
                Add(ScoreRule.NegativeSignal, signal, ScoreWhere.Title, -30);
            else if (Signal(signal).IsMatch(body))
                Add(ScoreRule.NegativeSignal, signal, ScoreWhere.Body, -15);
        }

        // "sponsor" as a substring also covers "sponsorship", "sponsored"; the offered phrases
        // are substrings too ("no sponsorship available" contains one), so both are gated by
        // the denial pattern.
        var sponsorDenied = SponsorshipDenied.IsMatch(full);
        var sponsorConfirmed = !sponsorDenied && SponsorshipOffered.Any(full.Contains);
        if (sponsorConfirmed || (full.Contains("sponsor", StringComparison.Ordinal) && !sponsorDenied))
            Add(ScoreRule.SponsorshipMentioned, "sponsor", ScoreWhere.Text, policy.SponsorBonus);
        else if (sponsorDenied)
            Add(ScoreRule.SponsorshipDenied, "sponsor", ScoreWhere.Text, 0);

        if (profile.RewardRemote && Signal("remote").IsMatch(full))
            Add(ScoreRule.Remote, "remote", ScoreWhere.Text, 10);
        else if (profile.RewardRemote && Signal("hybrid").IsMatch(full))
            Add(ScoreRule.Hybrid, "hybrid", ScoreWhere.Text, 5);

        // Not twice when the profile already rewards FIFO as a signal.
        var fifoInProfile = profile.PositiveSignals.Any(s =>
            s.Contains("fifo", StringComparison.OrdinalIgnoreCase) || s.Contains("fly-in", StringComparison.OrdinalIgnoreCase));
        if (!fifoInProfile && (Signal("fifo").IsMatch(full) || Signal("fly-in fly-out").IsMatch(full)))
            Add(ScoreRule.Fifo, "fifo", ScoreWhere.Text, 15);

        if (profile.EntryLevelBoost)
        {
            foreach (var phrase in NoExperience)
            {
                if (MatchesUnnegated(phrase, full))
                    Add(ScoreRule.EntryLevel, phrase, ScoreWhere.Text, 12);
            }
        }

        // The full-text search gives no context: next to citizenship or residency, a working holiday
        // mention is mostly a refusal. The listing's own text still counts: it was read, not searched.
        // A captured ad has the context the search lacks: it replaces the searched WHV phrases, and its
        // mention counts unless negated just before ("unable to accept working holiday visas"); a
        // refusal after it ("working holiday visa holders will not") was excluded above.
        var whvLocal = WorkingHolidayFriendly.Match(full);
        var whvDescribed = described && WorkingHolidayFriendly.Match(desc) is { Success: true } inDesc && MatchesUnnegated(inDesc.Value, desc)
            ? inDesc.Value
            : null;
        var whvDoubtful = groups.ContainsKey(FullTextGroup.WhvWithResidency);
        var whvHits = described || whvDoubtful ? null : groups.GetValueOrDefault(FullTextGroup.Whv);
        var whv = whvLocal.Success || whvDescribed is not null || whvHits is not null;
        if (whvLocal.Success)
            Add(ScoreRule.WorkingHolidayFriendly, whvLocal.Value, ScoreWhere.Text, 15);
        else if (whvDescribed is not null)
            Add(ScoreRule.WorkingHolidayFriendly, whvDescribed, ScoreWhere.Description, 15);
        else if (whvHits is not null)
            Add(ScoreRule.WorkingHolidayFriendly, whvHits[0], ScoreWhere.FullText, 15);

        // Contract, temp and casual fit the six months per employer.
        if (job.WorkTypeLabel is { } workType && FlexibleWorkType.IsMatch(workType))
            Add(ScoreRule.FlexibleWorkType, workType, ScoreWhere.WorkType, 5);

        var location = job.LocationLabel ?? "";
        if (profile.PreferredLocations.FirstOrDefault(l => Signal(l).IsMatch(location)) is { } preferred)
            Add(ScoreRule.PreferredLocation, preferred, ScoreWhere.Location, 10);

        // ── 3. Compression above 80 ───────────────────────────────────────
        if (score > 80)
        {
            var compressed = Math.Min(100, 80 + (int)Math.Round((score - 80) / 3.0, MidpointRounding.ToEven));
            Add(ScoreRule.Compression, "80", ScoreWhere.None, compressed - score);
        }

        // ── 4. Restrictions and penalties ─────────────────────────────────
        foreach (var (group, rule, points) in FullTextRestrictions)
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
            Add(rule, hits[0], ScoreWhere.FullText, points);
        }

        if (described)
        {
            if (!whv && CitizenshipRequired.Match(desc) is { Success: true } descCitizen)
                Add(ScoreRule.FullTextCitizenship, descCitizen.Value, ScoreWhere.Description, -55);
            if (policy.FullTextGroups.GetValueOrDefault(FullTextGroup.Ambiguous)?.FirstOrDefault(p => Signal(p).IsMatch(desc)) is { } ambiguous)
                Add(ScoreRule.FullTextAmbiguousRights, ambiguous, ScoreWhere.Description, -10);
        }

        // Requirements of the profile found in the full ad: once per kind, a truck licence apart from a
        // ticket. A truck licence already counted (local text or the policy's licence group) is not counted
        // again; a training title is where the trade certificate is earned. The context word only narrows
        // the search: a phrase found with any context ("HC licence + FIFO", searched for another profile) is
        // in the ad. A captured ad is read instead of the search, which also matches what the ad does not
        // say; there the phrase counts when not negated just before it ("no HR licence needed").
        var found = job.FullTextTerms.Select(t => new FullTextTerm(t).Phrases[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var truckAsked = TruckLicenceAsked(fullAndDesc);
        var truckCounted = (!described && groups.ContainsKey(FullTextGroup.Licence)) || truckAsked is not null;
        var requirements = profile.Requirements.Terms()
            .Select(r => (r.Kind, Truck: r.Kind == RequirementKind.Licence && TruckLicence.IsMatch(r.Phrase), r.Phrase,
                Where: described ? (MatchesUnnegated(r.Phrase, desc) ? ScoreWhere.Description : ScoreWhere.None)
                    : found.Contains(r.Phrase) ? ScoreWhere.FullText : ScoreWhere.None))
            .Where(r => r.Where != ScoreWhere.None)
            .DistinctBy(r => (r.Kind, r.Truck));
        foreach (var requirement in requirements)
        {
            if (requirement.Truck && truckCounted)
                continue;
            if (requirement.Kind == RequirementKind.Qualification && trainingTitle)
                continue;
            var (rule, points) = requirement switch
            {
                { Truck: true } => (ScoreRule.FullTextTruckLicence, TruckLicencePenalty),
                { Kind: RequirementKind.Licence } => (ScoreRule.FullTextLicence, TicketPenalty),
                { Kind: RequirementKind.Experience } => (ScoreRule.FullTextExperience, RequirementPenalty),
                _ => (ScoreRule.FullTextQualification, RequirementPenalty),
            };
            Add(rule, requirement.Phrase, requirement.Where, -points);
        }

        if (policy.ApprenticeshipPenalty > 0 && trainingTitle)
            Add(ScoreRule.Apprenticeship, "apprentice", ScoreWhere.Title, -policy.ApprenticeshipPenalty);

        if (policy.LongContractPenalty > 0 && LongContract.Match(fullAndDesc) is { Success: true } longContract)
            Add(ScoreRule.LongContract, longContract.Value, ScoreWhere.Text, -policy.LongContractPenalty);

        if (CommissionOnly.Match(fullAndDesc) is { Success: true } commission && !groups.ContainsKey(FullTextGroup.Commission))
            Add(ScoreRule.CommissionOnly, commission.Value, ScoreWhere.Text, -30);
        if (truckAsked is { } truck && (described || !groups.ContainsKey(FullTextGroup.Licence)))
        {
            if (truck.Desirable)
                Add(ScoreRule.TruckLicence, $"{truck.Value} (desirable)", ScoreWhere.Text, -DesirableTruckLicencePenalty);
            else
                Add(ScoreRule.TruckLicence, truck.Value, ScoreWhere.Text, -TruckLicencePenalty);
        }

        if (policy.VisaRequired && !sponsorConfirmed)
            Add(ScoreRule.NoSponsorship, "sponsor", ScoreWhere.None, -(sponsorDenied ? 15 : policy.NoSponsorPenalty));

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
        if (!EntryTitle.IsMatch(title) && profile.QualifiedTitles.FirstOrDefault(t => QualifiedTitle(t).IsMatch(title)) is { } qualified)
            reason = (qualified, ScoreWhere.Title);
        if (reason is null && !trainingTitle && profile.DisqualifySignals.FirstOrDefault(s => Signal(s).IsMatch(full)) is { } disqualify)
            reason = (disqualify, ScoreWhere.Text);
        if (reason is { } r && score > policy.DisqualifyMaxScore)
            Add(ScoreRule.QualificationRequired, r.Detail, r.Where, policy.DisqualifyMaxScore - score);

        return new FitScore(Math.Clamp(score, 1, 100), items);
    }

    private static readonly (string Group, ScoreRule Rule, int Points)[] FullTextRestrictions =
    [
        (FullTextGroup.Citizen, ScoreRule.FullTextCitizenship, -55),
        (FullTextGroup.WhvWithResidency, ScoreRule.FullTextWhvWithResidency, -55),
        (FullTextGroup.Ambiguous, ScoreRule.FullTextAmbiguousRights, -10),
        (FullTextGroup.Rights12Months, ScoreRule.FullTextTwelveMonthRights, -40),
        (FullTextGroup.Commission, ScoreRule.FullTextCommission, -30),
        (FullTextGroup.Licence, ScoreRule.FullTextTruckLicence, -TruckLicencePenalty),
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
