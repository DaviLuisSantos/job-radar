using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The phrases that decide a score, ported from the prototype. Texts are matched lower-cased;
/// the patterns are case-insensitive anyway.
/// </summary>
internal static class ListingPatterns
{
    // Compiled: each pattern runs against every listing of every profile, every day.
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static Regex R(string pattern) => new(pattern, Options);

    // ── Sponsorship ───────────────────────────────────────────────────────────

    /// <summary>"no sponsorship", "sponsorship is not available", "unable to sponsor"...</summary>
    public static readonly Regex SponsorshipDenied = R(
        @"\b(no|not|without)\s+sponsor"
        + @"|sponsor\w*\s+(?:is\s+)?not\s+\w+"
        + @"|sponsor\w*\s+not\s+(?:available|offered|provided|considered)"
        + @"|visa\s+sponsor\w*\s+(?:is\s+)?not"
        + @"|(?:unable|cannot|can't|not\s+able)\s+to\s+sponsor"
        + @"|sponsor\w*\s+will\s+not");

    /// <summary>Specific enough to confirm sponsorship ("visa" alone is ambiguous). Substrings.</summary>
    public static readonly string[] SponsorshipOffered =
    [
        "482", "491", "494",
        "employer sponsor", "employer-sponsor",
        "visa sponsorship available", "sponsorship available",
        "sponsorship offered", "sponsorship considered",
        "will sponsor", "open to sponsor",
        "we sponsor", "we will sponsor",
    ];

    // ── Work rights ───────────────────────────────────────────────────────────

    /// <summary>Citizenship or PR required, or clearances that imply it.</summary>
    public static readonly Regex CitizenshipRequired = R(
        @"australian\s+citizen"
        + @"|\bau\s+citizen"
        + @"|citizen(?:s)?\s+only"
        + @"|must\s+be\s+(?:an?\s+)?(?:australian|citizen)"
        + @"|must\s+hold\s+(?:australian\s+)?citizen"
        + @"|proof\s+of\s+(?:australian\s+)?(?:citizen|permanent\s+residen)"
        + @"|must\s+(?:hold|have|provide|show)\s+(?:australian\s+)?permanent\s+residen"
        + @"|nv[12]\s+(?:security\s+)?clearance"
        + @"|top\s+secret\s+clearance"
        + @"|new\s+zealand\s+citizen"
        + @"|(?:full\s+)?australian\s+working\s+rights\s*[-–—]?\s*essential");

    /// <summary>Work rights a Working Holiday visa (12 months, 6 per employer) cannot give.</summary>
    public static readonly Regex WorkRightsIncompatible = R(
        @"(?:unlimited|unrestricted|permanent)\s+(?:australian\s+)?work(?:ing)?\s+rights"
        + @"|(?:12|twelve)\s*\+?\s*months?\s+(?:of\s+)?(?:valid\s+|remaining\s+)?(?:australian\s+)?work(?:ing)?\s+rights"
        + @"|work(?:ing)?\s+rights\s+for\s+(?:at\s+least\s+|a\s+minimum\s+of\s+|minimum\s+)?(?:12|twelve)\s+months"
        + @"|no\s+(?:temporary|working\s+holiday)\s+visas?"
        + @"|(?:temporary|working\s+holiday)\s+visa\s+holders?\s+(?:will\s+not|are\s+not|need\s+not|cannot|can't)"
        + @"|not\s+(?:open|available)\s+to\s+(?:temporary|working\s+holiday)\s+visa");

    /// <summary>Welcomes working holiday makers or short work rights.</summary>
    public static readonly Regex WorkingHolidayFriendly = R(
        @"working\s+holiday|backpackers?|\b(?:417|462)\s+visa|\b88\s+days"
        + @"|second[\s-]year\s+visa|visa\s+holders\s+welcome"
        + @"|(?:6|six)\s+months?\s+(?:of\s+)?(?:valid\s+)?work(?:ing)?\s+rights"
        + @"|work(?:ing)?\s+rights\s+(?:valid\s+)?for\s+the\s+next\s+(?:6|six)\s+months");

    /// <summary>Identified position: reserved for Aboriginal and Torres Strait Islander people.</summary>
    public static readonly Regex IdentifiedRole = R(
        @"\bindigenous\b|\bfirst\s+nations\b|aboriginal\s+(?:and/or|and|or|&)\s+torres\s+strait");

    /// <summary>Longer than the six months a WHV allows per employer.</summary>
    public static readonly Regex LongContract = R(
        @"\b(?:9|12|18|24|nine|twelve|eighteen)[\s-]+months?\s+(?:fixed[\s-]term\s+)?contract"
        + @"|\b(?:1|2|one|two)[\s-]+years?\s+(?:fixed[\s-]term\s+)?contract");

    public static readonly Regex CommissionOnly = R(@"(?<!not\s)(?<!no\s)\bcommission[\s-]+(?:only|based)\b");

    /// <summary>
    /// Truck licence classes (HR/HC/MR/MC): years of holding a car licence away for a newcomer, not a
    /// course. Also "HR truck driver's licence", "HC class licence", "heavy combination".
    /// </summary>
    public static readonly Regex TruckLicence = R(
        @"\b(?:HR|HC|MR|MC)(?:[\s-]+(?:class|truck|rigid|combination))?[\s-]+(?:driver['’]?s?[\s-]+)?(?:licen[cs]e|driver)"
        + @"|\b(?:HR|HC|MR|MC)\s*/\s*(?:HR|HC|MR|MC)\b"
        + @"|\bheavy\s+(?:rigid|combination)\b");

    public static readonly Regex FlexibleWorkType = R("contract|temp|casual");

    // ── Seniority and entry level ─────────────────────────────────────────────

    public static readonly Regex SeniorRank = R(@"\b(?:senior|snr|sr)s?\b");

    public static readonly Regex LeadershipTitle = R(
        @"\b(?:lead|leader|principal|head|manager|director|architect"
        + @"|superintendent|supervisor|foreman|foremen|chief|staff(?:\s+[\w.-]+){0,3}?\s+engineer)s?\b");

    public static readonly Regex MidTitle = R(@"\b(?:mid|intermediate)\b");

    /// <summary>A title naming several roles ("Graduate/ Junior Project Manager"): the entry mark wins.</summary>
    public static readonly Regex JuniorTitle = R(@"\b(?:graduate|junior|entry[\s-]level|assistant|apprentice\w*|trainee\w*)\b");

    /// <summary>Training role: "Certificate III" in the text is the course offered, not a requirement.</summary>
    public static readonly Regex TrainingTitle = R(@"\b(?:apprentice\w*|pre-apprentice\w*|trainee\w*|cadet\w*)\b");

    /// <summary>
    /// Entry title: lifts the qualified-title cap ("Drillers Offsider" is not a driller). "Hand" is
    /// kitchen or farm hand, never "leading hand", which supervises qualified trades.
    /// </summary>
    public static readonly Regex EntryTitle = R(
        @"\b(?:apprentice\w*|pre-apprentice\w*|trainee\w*|cadet\w*|assistant|offsiders?"
        + @"|labourers?|helpers?|(?<!leading\s)hands?|graduate|junior|entry)\b|\bTA\b|\btrades?\s+assist");

    /// <summary>Negative signals about qualifications, skipped for training titles.</summary>
    public static readonly string[] QualificationWords = ["cert", "qualif"];

    public static readonly string[] NoExperience =
    [
        "no experience required", "no experience necessary", "no experience needed",
        "training provided", "will train", "willing to train", "entry level", "school leaver",
        "traineeship", "trainee position", "no qualifications required",
        "attitude over experience", "right attitude", "eager to learn",
    ];

    // ── Signals ───────────────────────────────────────────────────────────────

    private static readonly Regex Negation = R(
        @"\b(?:no|not|without|cannot|can't|can\s+not|unable\s+to|never|don't|do\s+not|won't|will\s+not)\b");

    private const int NegationWindow = 80;

    private static readonly ConcurrentDictionary<string, Regex> Signals = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Regex> Titles = new(StringComparer.Ordinal);

    /// <summary>
    /// The signal as a whole token. Lookarounds instead of \b, which fails on tokens that start
    /// or end with a symbol (C#, .NET, $36).
    /// </summary>
    public static Regex Signal(string signal) =>
        Signals.GetOrAdd(signal, s => R(@"(?<!\w)" + Regex.Escape(s) + @"(?!\w)"));

    /// <summary>Like <see cref="Signal"/>, accepting a plural ("Plumbers").</summary>
    public static Regex QualifiedTitle(string title) =>
        Titles.GetOrAdd(title, t => R(@"(?<!\w)" + Regex.Escape(t) + @"s?(?!\w)"));

    /// <summary>Said of a requirement right after it: nice to have, not a condition ("HR licence highly regarded").</summary>
    private static readonly Regex Desirable = R(
        @"\b(?:desirable|advantage(?:ous)?|preferred|highly\s+regarded|well\s+regarded|bonus|not\s+(?:essential|mandatory|required))\b");

    private const int ClauseWindow = 40;

    /// <summary>
    /// The truck licence the text asks for: the first one not negated in its own clause before it ("no HR
    /// licence needed"), preferring one that is a condition over one said to be desirable right after it.
    /// Null when the text asks for none. The clause, not <see cref="NegationWindow"/>: in a list of
    /// requirements a "no" on the line above ("no experience needed") says nothing about the licence.
    /// </summary>
    public static (string Value, bool Desirable)? TruckLicenceAsked(string text)
    {
        (string Value, bool Desirable)? optional = null;
        for (var m = TruckLicence.Match(text); m.Success; m = m.NextMatch())
        {
            if (Negation.IsMatch(Clause(text.AsSpan(Math.Max(0, m.Index - ClauseWindow), Math.Min(ClauseWindow, m.Index)), fromEnd: true)))
                continue;
            var end = m.Index + m.Length;
            if (!Desirable.IsMatch(Clause(text.AsSpan(end, Math.Min(ClauseWindow, text.Length - end)), fromEnd: false)))
                return (m.Value, false);
            optional ??= (m.Value, true);
        }
        return optional;
    }

    /// <summary>The part of <paramref name="text"/> in the same clause as the match next to it.</summary>
    private static ReadOnlySpan<char> Clause(ReadOnlySpan<char> text, bool fromEnd)
    {
        ReadOnlySpan<char> breaks = ['\n', '.', ';', '•', '·'];
        if (fromEnd)
        {
            var cut = text.LastIndexOfAny(breaks);
            return cut < 0 ? text : text[(cut + 1)..];
        }
        var stop = text.IndexOfAny(breaks);
        return stop < 0 ? text : text[..stop];
    }

    /// <summary>True when the signal appears at least once without a negation in the 80 characters before it.</summary>
    public static bool MatchesUnnegated(string signal, string text)
    {
        for (var m = Signal(signal).Match(text); m.Success; m = m.NextMatch())
        {
            var start = Math.Max(0, m.Index - NegationWindow);
            if (!Negation.IsMatch(text.AsSpan(start, m.Index - start)))
                return true;
        }
        return false;
    }
}
