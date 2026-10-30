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

    // ── Contracts ─────────────────────────────────────────────────────────────

    /// <summary>A fixed term of nine months or more: longer than a profile with short engagements can take.</summary>
    public static readonly Regex LongContract = R(
        @"\b(?:9|12|18|24|nine|twelve|eighteen)[\s-]+months?\s+(?:fixed[\s-]term\s+)?contract"
        + @"|\b(?:1|2|one|two)[\s-]+years?\s+(?:fixed[\s-]term\s+)?contract");

    public static readonly Regex CommissionOnly = R(@"(?<!not\s)(?<!no\s)\bcommission[\s-]+(?:only|based)\b");

    /// <summary>
    /// Truck licence classes (HR/HC/MR/MC): years of holding a car licence away for a newcomer, not a
    /// course. Also "HR truck driver's licence", "HC class licence", "HC unrestricted licence", "heavy combination", and a
    /// class with its condition letter ("HR-B Drivers License", "HC (B) licence").
    /// </summary>
    public static readonly Regex TruckLicence = R(
        @"\b(?:HR|HC|MR|MC)(?:-[a-z](?=[\s-])|\s?\([a-z]\)|\s[a-z](?=\s+class\b))?(?:[\s-]+(?:class|truck|rigid|combination|unrestricted|open|full))?[\s-]+(?:driver['’]?s?[\s-]+)?(?:licen[cs]e|driver)"
        + @"|\b(?:HR|HC|MR|MC)\s*/\s*(?:HR|HC|MR|MC)\b"
        + @"|\bheavy\s+(?:rigid|combination)\b");

    /// <summary>
    /// "minimum 3 years experience", "3+ years of civil experience", "at least 2 years' hands-on experience",
    /// "2-3 yrs experience": the number of years, up to four words before "experience".
    /// </summary>
    private static readonly Regex YearsOfExperience = R(
        @"\b(\d{1,2})\s*(?:\+|plus)?\s*(?:(?:-|–|to)\s*\d{1,2}\s*\+?\s*)?(?:years?|yrs?)['’]?\s+(?:of\s+)?(?:[\w/&-]+\s+){0,4}?experience\b");

    /// <summary>
    /// The fewest years of experience the text asks for, null when it names none. The fewest because an ad that
    /// asks "5 years preferred, 2 minimum" takes 2; a range ("2-3 years") counts from its start.
    /// </summary>
    public static int? YearsAsked(string text)
    {
        int? fewest = null;
        foreach (Match m in YearsOfExperience.Matches(text))
        {
            var years = int.Parse(m.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
            if (years is >= 1 and <= 20 && (fewest is null || years < fewest))
                fewest = years;
        }
        return fewest;
    }

    /// <summary>The truck licence classes a text names, to compare with the ones the candidate holds.</summary>
    public static readonly Regex TruckClass = R(@"\b(?:MR|HR|HC|MC)\b|\bheavy\s+(?:rigid|combination)\b");

    public static readonly Regex FlexibleWorkType = R("contract|temp|casual");

    // ── Seniority and entry level ─────────────────────────────────────────────

    public static readonly Regex SeniorRank = R(@"\b(?:senior|snr|sr)s?\b");

    public static readonly Regex LeadershipTitle = R(
        @"\b(?:lead|leader|principal|head|manager|director|architect"
        + @"|superintendent|supervisor|foreman|foremen|chief|staff(?:\s+[\w.-]+){0,3}?\s+engineer)s?\b");

    public static readonly Regex MidTitle = R(@"\b(?:mid|intermediate)\b");

    /// <summary>A title naming several roles ("Graduate/ Junior Project Manager"): the entry mark wins.</summary>
    /// <summary>Junior title: lifts the leadership and senior exclusions. "Assistant Village Manager" is management.</summary>
    public static readonly Regex JuniorTitle = R(
        @"\b(?:graduate|junior|entry[\s-]level|assistant(?!(?:\s+[\w/&-]+){0,3}?\s+manager)|apprentice\w*|trainee\w*)\b");

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

    /// <summary>Said of a requirement right after it: a condition ("HR licence is essential").</summary>
    private static readonly Regex Essential = R(@"(?<!not\s)\b(?:essential|mandatory|required|must)\b");

    private const int ClauseWindow = 40;

    /// <summary>
    /// The truck licence the text asks for: the first one not negated in its own clause before it ("no HR
    /// licence needed"), preferring one that is a condition over one said to be desirable right after it.
    /// Null when the text asks for none. The clause, not <see cref="NegationWindow"/>: in a list of
    /// requirements a "no" on the line above ("no experience needed") says nothing about the licence. What
    /// follows a licence is read up to the next one ("HR licence is essential - HC licence desirable"), and
    /// a word that makes it a condition wins over one that makes it optional.
    /// </summary>
    public static (string Value, bool Desirable)? TruckLicenceAsked(string text)
    {
        (string Value, bool Desirable)? optional = null;
        for (var m = TruckLicence.Match(text); m.Success; m = m.NextMatch())
        {
            if (Negation.IsMatch(Clause(text.AsSpan(Math.Max(0, m.Index - ClauseWindow), Math.Min(ClauseWindow, m.Index)), fromEnd: true)))
                continue;
            var end = m.Index + m.Length;
            var next = m.NextMatch();
            var length = Math.Min(ClauseWindow, (next.Success ? next.Index : text.Length) - end);
            var after = Clause(text.AsSpan(end, length), fromEnd: false);
            if (Essential.IsMatch(after) || !Desirable.IsMatch(after))
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
