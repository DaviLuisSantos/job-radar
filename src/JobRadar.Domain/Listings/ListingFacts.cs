using System.Text.RegularExpressions;

namespace JobRadar.Domain.Listings;

/// <summary>What a fact read from an ad is about. The order is the order they are shown in.</summary>
public enum FactKind
{
    Pay,
    Travel,
    Roster,
    Accommodation,
    Meals,
    Flights,
    Start,
    Shift,
    Duration,
}

/// <summary>A short fact read from an ad, as a label: "$45/hr", "2:1 roster", "Accommodation".</summary>
public sealed record ListingFact(FactKind Kind, string Text);

/// <summary>
/// Reads the facts a candidate looks for first (pay, roster, accommodation, start...) from an ad's text, so triage can
/// show them next to the title instead of a teaser. Only what the ad says plainly, at most one fact per kind; a fact
/// stated as not offered ("accommodation is not provided") is not read. For showing, not scoring: profile signals
/// still read title and summary only.
/// </summary>
public static class ListingFacts
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static Regex R(string pattern) => new(pattern, Options);

    // An amount with the period it is paid in: a dollar figure alone is often a project size or a bonus.
    private static readonly Regex Pay = R(
        @"\$\s?\d{1,3}(?:[,.]\d{3})*(?:\.\d{1,2})?\s?k?"
        + @"(?:\s?(?:-|–|to)\s?\$?\s?\d{1,3}(?:[,.]\d{3})*(?:\.\d{1,2})?\s?k?)?"
        + @"\s?(?:\+\s?super(?:annuation)?\s?)?"
        + @"(?:per\s+hour|p/?h\b|/\s?h(?:ou)?r\b|an\s+hour|hourly|ph\b|per\s+annum|p\.?\s?a\.?(?=\W)|/\s?(?:yr|year)\b|per\s+year|a\s+year"
        + @"|per\s+day|/\s?day\b|a\s+day|per\s+week|p/?w\b|/\s?(?:wk|week)\b|a\s+week|per\s+shift|\+\s?super(?:annuation)?)");

    private static readonly Regex Fifo = R(@"\b(?:FIFO|fly[\s-]in[\s,/-]+(?:and\s+)?fly[\s-]out)\b");
    private static readonly Regex Dido = R(@"\b(?:DIDO|drive[\s-]in[\s,/-]+(?:and\s+)?drive[\s-]out)\b");
    private static readonly Regex Bibo = R(@"\b(?:BIBO|bus[\s-]in[\s,/-]+(?:and\s+)?bus[\s-]out)\b");

    // "2:1 roster", "8/6 swing", "roster of 14:7"; a time of day ("7:30") never sits next to these words.
    private static readonly Regex RosterRatio = R(
        @"\b(\d{1,2})\s?[:/]\s?(\d{1,2})\s+(?:roster|swing|rotation|ratio)\b|\b(?:roster|swing|rotation)(?:\s+of)?\s+(\d{1,2})\s?[:/]\s?(\d{1,2})\b");
    private static readonly Regex RosterWeeks = R(
        @"\b(\d{1,2})\s+(weeks?|days?)\s+on\s*[,/&]?\s*(?:and\s+)?(\d{1,2})\s+(?:weeks?|days?)\s+off\b");
    private static readonly Regex EvenTime = R(@"\beven[\s-]time\s+roster\b");

    private static readonly Regex Accommodation = R(
        @"\b(?:accommodation|accom)\s+(?:is\s+|will\s+be\s+)?(?:provided|supplied|included|available|offered|on[\s-]site|paid)\b"
        + @"|\b(?:free|paid|subsidi[sz]ed|camp|onsite|on-site|shared|including|includes|with)\s+accommodation\b"
        + @"|\b(?:flights|travel)\s+(?:and|&)\s+accommodation\s+(?:are\s+)?(?:provided|paid|included|covered|supplied)\b"
        + @"|\baccommodation\s+(?:and|&)\s+meals\b|\bmeals\s+(?:and|&)\s+accommodation\b");
    private static readonly Regex Meals = R(
        @"\bmeals\s+(?:are\s+)?(?:provided|included|supplied)\b|\baccommodation\s+(?:and|&)\s+meals\b|\bmeals\s+(?:and|&)\s+accommodation\b"
        + @"|\b(?:free|all)\s+meals\b");
    private static readonly Regex Flights = R(
        @"\bflights?\s+(?:are\s+|will\s+be\s+)?(?:provided|paid|included|covered|supplied)\b|\b(?:paid|free|return|FIFO)\s+flights?\b"
        + @"|\bflights\s+(?:and|&)\s+accommodation\s+(?:are\s+)?(?:provided|paid|included|covered|supplied)\b");

    private static readonly Regex Start = R(
        @"\b(?:immediate|asap)\s+starts?\b|\bstart(?:ing)?\s+(?:asap|immediately|straight\s+away|tomorrow|this\s+week|next\s+week)\b");

    private static readonly Regex Nights = R(@"\bnight\s?shifts?\b");
    private static readonly Regex DaysAndNights = R(@"\b(?:days?\s+(?:and|&|/)\s+nights?|rotating\s+shifts?|rotational\s+shifts?|day/night)\b");
    private static readonly Regex DayShift = R(@"\bday\s?shifts?\s+only\b");
    private static readonly Regex Weekdays = R(@"\bmonday\s+to\s+friday\b|\bmon(?:day)?\s?[-–]\s?fri(?:day)?\b");

    private static readonly Regex Length = R(
        @"\b(\d{1,2})[\s-]+(weeks?|months?)\s+(?:contract|assignment|project|role|position|placement|engagement|shutdown)\b"
        + @"|\b(?:contract|assignment|project|placement)\s+(?:of|for)\s+(\d{1,2})\s+(weeks?|months?)\b");

    // "accommodation is not provided", "no flights", "unable to provide accommodation": a few words before or after.
    private static readonly Regex Negation = R(@"\b(?:no|not|without|unable|cannot|can't|isn't|aren't|won't|nor)\b");

    /// <summary>The facts the text states, in <see cref="FactKind"/> order.</summary>
    /// <param name="text">The ad's text: the full description, or the summary when there is none.</param>
    /// <param name="hasPayField">The source already gives the pay: none is read from the text.</param>
    public static IReadOnlyList<ListingFact> Read(string? text, bool hasPayField = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var facts = new List<ListingFact>();

        if (!hasPayField && Pay.Match(text) is { Success: true } pay)
            facts.Add(new(FactKind.Pay, Tidy(pay.Value)));

        if (Stated(Fifo, text) is not null)
            facts.Add(new(FactKind.Travel, "FIFO"));
        else if (Stated(Dido, text) is not null)
            facts.Add(new(FactKind.Travel, "DIDO"));
        else if (Stated(Bibo, text) is not null)
            facts.Add(new(FactKind.Travel, "BIBO"));

        if (RosterRatio.Match(text) is { Success: true } ratio)
        {
            var (on, off) = ratio.Groups[1].Success ? (ratio.Groups[1].Value, ratio.Groups[2].Value) : (ratio.Groups[3].Value, ratio.Groups[4].Value);
            facts.Add(new(FactKind.Roster, $"{on}:{off} roster"));
        }
        else if (RosterWeeks.Match(text) is { Success: true } weeks)
        {
            var unit = weeks.Groups[2].Value.StartsWith("week", StringComparison.OrdinalIgnoreCase) ? "wk" : "d";
            facts.Add(new(FactKind.Roster, $"{weeks.Groups[1].Value}{unit} on / {weeks.Groups[3].Value}{unit} off"));
        }
        else if (EvenTime.IsMatch(text))
        {
            facts.Add(new(FactKind.Roster, "Even-time roster"));
        }

        if (Stated(Accommodation, text) is not null)
            facts.Add(new(FactKind.Accommodation, "Accommodation"));
        if (Stated(Meals, text) is not null)
            facts.Add(new(FactKind.Meals, "Meals"));
        if (Stated(Flights, text) is not null)
            facts.Add(new(FactKind.Flights, "Flights paid"));
        if (Stated(Start, text) is not null)
            facts.Add(new(FactKind.Start, "Immediate start"));

        if (DaysAndNights.IsMatch(text))
            facts.Add(new(FactKind.Shift, "Days and nights"));
        else if (Stated(Nights, text) is not null)
            facts.Add(new(FactKind.Shift, "Night shift"));
        else if (DayShift.IsMatch(text))
            facts.Add(new(FactKind.Shift, "Day shift"));
        else if (Weekdays.IsMatch(text))
            facts.Add(new(FactKind.Shift, "Mon–Fri"));

        if (Length.Match(text) is { Success: true } length)
        {
            var (count, unit) = length.Groups[1].Success ? (length.Groups[1].Value, length.Groups[2].Value) : (length.Groups[3].Value, length.Groups[4].Value);
            var singular = unit.TrimEnd('s', 'S').ToLowerInvariant();
            facts.Add(new(FactKind.Duration, $"{count} {(count == "1" ? singular : singular + "s")}"));
        }

        return facts;
    }

    /// <summary>The first match not negated in the few words before it or right after it.</summary>
    private static Match? Stated(Regex pattern, string text)
    {
        for (var m = pattern.Match(text); m.Success; m = m.NextMatch())
        {
            var before = text.AsSpan(Math.Max(0, m.Index - 30), Math.Min(30, m.Index)).ToString();
            var after = text.AsSpan(m.Index + m.Length, Math.Min(15, text.Length - m.Index - m.Length)).ToString();
            if (!Negation.IsMatch(LastClause(before)) && !Negation.IsMatch(FirstClause(after)))
                return m;
        }
        return null;
    }

    private static string LastClause(string text) => text[(text.LastIndexOfAny(['.', ';', '\n', '•']) + 1)..];

    private static string FirstClause(string text) => text.IndexOfAny(['.', ';', '\n', '•', ',']) is var cut and >= 0 ? text[..cut] : text;

    private static string Tidy(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").Replace(" k", "k", StringComparison.Ordinal);
}
