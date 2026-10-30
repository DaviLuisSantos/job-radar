using System.Globalization;
using System.Text.RegularExpressions;

namespace JobRadar.Domain.Listings;

/// <summary>
/// The UTC offsets an employer accepts, from the first to the last: "UTC-5 to UTC-2". Remote boards give a list;
/// the translators write it as one part of the summary, and <see cref="ListingConditions"/> reads it back.
/// </summary>
public sealed partial record TimeZoneWindow(double From, double To)
{
    /// <summary>Rio de Janeiro and São Paulo: no daylight saving since 2019.</summary>
    public const double Brazil = -3;

    /// <summary>The window of a list of offsets; null when it spans the whole world, which restricts nothing.</summary>
    public static TimeZoneWindow? Of(IEnumerable<double> offsets)
    {
        var list = offsets.ToList();
        if (list.Count == 0)
            return null;
        var (from, to) = (list.Min(), list.Max());
        return from <= -11 && to >= 12 ? null : new TimeZoneWindow(from, to);
    }

    /// <summary>Whether a candidate at <paramref name="offset"/> is inside, give or take <paramref name="slack"/> hours.</summary>
    public bool Accepts(double offset, double slack = 0) => offset >= From - slack && offset <= To + slack;

    public override string ToString() => From == To ? $"UTC{Offset(From)}" : $"UTC{Offset(From)} to UTC{Offset(To)}";

    public static TimeZoneWindow? Parse(string? text)
    {
        if (text is null || Pattern().Match(text.Trim()) is not { Success: true } m)
            return null;
        var from = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var to = m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : from;
        return new TimeZoneWindow(from, to);
    }

    private static string Offset(double hours) =>
        hours == 0 ? "+0" : (hours > 0 ? "+" : "") + hours.ToString("0.##", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^UTC([+-]?\d{1,2}(?:\.\d+)?)(?: to UTC([+-]?\d{1,2}(?:\.\d+)?))?$")]
    private static partial Regex Pattern();
}
