using System.Globalization;
using System.Text.RegularExpressions;

namespace JobRadar.Domain.Market;

public enum SalaryPeriod
{
    Hour,
    Day,
    Week,
    Year,
}

/// <summary>An advertised salary, in its own period and as an annual equivalent.</summary>
public sealed record Salary(decimal Min, decimal Max, SalaryPeriod Period, int AnnualMin, int AnnualMax)
{
    /// <summary>Middle of the advertised range, per year.</summary>
    public decimal AnnualMid => (AnnualMin + AnnualMax) / 2m;
}

/// <summary>
/// Reads salary labels as Seek shows them ("$50 - $65 per hour", "$165k - $175k + Super",
/// "Up to $1,050 per day") and makes them comparable per year. Ported from the prototype.
/// </summary>
public static partial class SalaryParser
{
    /// <summary>Full-time year: 40 h × 52 weeks, 5 days × 52 weeks.</summary>
    private static readonly Dictionary<SalaryPeriod, int> PerYear = new()
    {
        [SalaryPeriod.Hour] = 2080,
        [SalaryPeriod.Day] = 260,
        [SalaryPeriod.Week] = 52,
        [SalaryPeriod.Year] = 1,
    };

    /// <summary>Numbers outside these ranges are not salaries ("$5 coffee", phone numbers, "$2m project").</summary>
    private static readonly Dictionary<SalaryPeriod, (decimal Low, decimal High)> Plausible = new()
    {
        [SalaryPeriod.Hour] = (10, 500),
        [SalaryPeriod.Day] = (80, 5_000),
        [SalaryPeriod.Week] = (400, 20_000),
        [SalaryPeriod.Year] = (20_000, 1_000_000),
    };

    public static Salary? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "—")
            return null;

        var period = DetectPeriod(text);
        var (low, high) = Plausible[period];
        var numbers = Amounts(text).Where(n => n >= low && n <= high).ToList();
        if (numbers.Count == 0)
            return null;

        var (min, max) = (numbers.Min(), numbers.Max());
        var multiplier = PerYear[period];
        return new Salary(min, max, period, (int)(min * multiplier), (int)(max * multiplier));
    }

    internal static SalaryPeriod DetectPeriod(string text)
    {
        var t = text.ToLowerInvariant();
        if (Hourly().IsMatch(t))
            return SalaryPeriod.Hour;
        if (Daily().IsMatch(t))
            return SalaryPeriod.Day;
        if (Weekly().IsMatch(t))
            return SalaryPeriod.Week;
        return SalaryPeriod.Year;
    }

    private static IEnumerable<decimal> Amounts(string text)
    {
        foreach (Match m in Amount().Matches(text))
        {
            if (!decimal.TryParse(m.Groups[1].Value.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                continue;
            yield return m.Groups[2].Value.ToLowerInvariant() switch
            {
                "k" => value * 1_000,
                "m" => value * 1_000_000,
                _ => value,
            };
        }
    }

    [GeneratedRegex(@"\$\s*([\d,]+(?:\.\d+)?)\s*(k|m)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Amount();

    [GeneratedRegex(@"\bper\s+hour\b|p\.h\b|/h\b|hourly\b")]
    private static partial Regex Hourly();

    [GeneratedRegex(@"\bper\s+day\b|p\.d\b|/day\b|daily\b")]
    private static partial Regex Daily();

    [GeneratedRegex(@"\bper\s+week\b|/week\b|weekly\b")]
    private static partial Regex Weekly();
}
