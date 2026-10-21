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
    public const int RulesVersion = 4;

    private const int ExclusionPoints = -50;

    /// <summary>
    /// Per requirement kind found in the full ad. Moderate: the search gives no context, and
    /// "cert III an advantage" matches as well as "cert III essential".
    /// </summary>
    private const int RequirementPenalty = 20;

    public static FitScore Score(ScoringInput job, MatchingProfile profile, ScoringPolicy policy, DateOnly today)
    {
        var title = job.Title.ToLowerInvariant();
        var body = string.Join(" ", job.Summary ?? "", job.LocationLabel ?? "", job.WorkTypeLabel ?? "").ToLowerInvariant();
        var full = $"{title} {body}";
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
        var whvLocal = WorkingHolidayFriendly.Match(full);
        var whvDoubtful = groups.ContainsKey(FullTextGroup.WhvWithResidency);
        var whvHits = whvDoubtful ? null : groups.GetValueOrDefault(FullTextGroup.Whv);
        var whv = whvLocal.Success || whvHits is not null;
        if (whvLocal.Success)
            Add(ScoreRule.WorkingHolidayFriendly, whvLocal.Value, ScoreWhere.Text, 15);
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
            if (whv && group is FullTextGroup.Citizen or FullTextGroup.Rights12Months or FullTextGroup.WhvWithResidency)
                continue;
            // Same reason, counted once.
            if (group == FullTextGroup.WhvWithResidency && groups.ContainsKey(FullTextGroup.Citizen))
                continue;
            Add(rule, hits[0], ScoreWhere.FullText, points);
        }

        // Requirements of the profile found in the full ad: once per kind. A truck licence already
        // counted (local text or the policy's licence group) is not counted again; a training title
        // is where the trade certificate is earned.
        var found = job.FullTextTerms.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var truckCounted = groups.ContainsKey(FullTextGroup.Licence) || TruckLicence.IsMatch(full);
        foreach (var requirement in profile.Requirements.Terms().Where(r => found.Contains(r.Term)).DistinctBy(r => r.Kind))
        {
            if (requirement.Kind == RequirementKind.Licence && truckCounted)
                continue;
            if (requirement.Kind == RequirementKind.Qualification && trainingTitle)
                continue;
            var rule = requirement.Kind switch
            {
                RequirementKind.Experience => ScoreRule.FullTextExperience,
                RequirementKind.Qualification => ScoreRule.FullTextQualification,
                _ => ScoreRule.FullTextLicence,
            };
            Add(rule, requirement.Phrase, ScoreWhere.FullText, -RequirementPenalty);
        }

        if (policy.ApprenticeshipPenalty > 0 && trainingTitle)
            Add(ScoreRule.Apprenticeship, "apprentice", ScoreWhere.Title, -policy.ApprenticeshipPenalty);

        if (policy.LongContractPenalty > 0 && LongContract.Match(full) is { Success: true } longContract)
            Add(ScoreRule.LongContract, longContract.Value, ScoreWhere.Text, -policy.LongContractPenalty);

        if (CommissionOnly.Match(full) is { Success: true } commission && !groups.ContainsKey(FullTextGroup.Commission))
            Add(ScoreRule.CommissionOnly, commission.Value, ScoreWhere.Text, -30);
        if (TruckLicence.Match(full) is { Success: true } truck && !groups.ContainsKey(FullTextGroup.Licence))
            Add(ScoreRule.TruckLicence, truck.Value, ScoreWhere.Text, -20);

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
        (FullTextGroup.Licence, ScoreRule.FullTextTruckLicence, -20),
    ];

    /// <summary>Groups with at least one phrase found, each with the phrases found, in policy order.</summary>
    private static Dictionary<string, List<string>> FullTextHits(
        IReadOnlyCollection<string> found, IReadOnlyDictionary<string, IReadOnlyList<string>> groups)
    {
        var lower = found.Select(t => t.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        return groups
            .Select(g => (g.Key, Hits: g.Value.Where(t => lower.Contains(t.ToLowerInvariant())).ToList()))
            .Where(g => g.Hits.Count > 0)
            .ToDictionary(g => g.Key, g => g.Hits, StringComparer.Ordinal);
    }
}
