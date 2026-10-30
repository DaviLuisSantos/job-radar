using System.Globalization;
using System.Text.RegularExpressions;

namespace JobRadar.Domain.Matching;

public enum PayPeriod
{
    Hour,
    Day,
    Week,
    Month,
    Year,
}

/// <summary>A pay range as an ad states it: "USD 4,000–6,000 per month", "$45/hr", "R$ 12k".</summary>
public sealed record Pay(decimal Min, decimal Max, string Currency, PayPeriod Period)
{
    /// <summary>"USD 4,000–6,000 per month": the detail of a score item, worded again by the site.</summary>
    public override string ToString()
    {
        var range = Min == Max ? N(Min) : $"{N(Min)}–{N(Max)}";
        return $"{Currency} {range} per {Period.ToString().ToLowerInvariant()}";
    }

    private static string N(decimal v) => v.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>The range per month, for comparing with <see cref="PayBand"/>: a year is 12 months, a week 52/12.</summary>
    public (decimal Min, decimal Max) Monthly(decimal hoursPerMonth) => Period switch
    {
        PayPeriod.Hour => (Min * hoursPerMonth, Max * hoursPerMonth),
        PayPeriod.Day => (Min * hoursPerMonth / 8, Max * hoursPerMonth / 8),
        PayPeriod.Week => (Min * 52 / 12, Max * 52 / 12),
        PayPeriod.Month => (Min, Max),
        PayPeriod.Year => (Min / 12, Max / 12),
        _ => throw new ArgumentOutOfRangeException(nameof(Period), Period, null),
    };
}

/// <summary>
/// Reads a <see cref="Pay"/> from a salary field or an ad. Only dollars (read as USD in a remote ad) and reais, and
/// euros and pounds as currencies the scorer cannot weigh. Without a period, the size of the number decides: 20,000 or
/// more is a year, under 300 an hour, the rest a month. Not the Raio-X's <c>SalaryParser</c>, which is AUD per year.
/// </summary>
public static partial class PayParser
{
    /// <summary>A salary field: the first amount in it is the pay.</summary>
    public static Pay? Parse(string? text) =>
        string.IsNullOrWhiteSpace(text) || Amount().Match(text) is not { Success: true } m ? null : Read(m);

    /// <summary>
    /// An ad, where most amounts are not the pay: a meal voucher, a stipend, a funding round. An amount counts only
    /// with a pay word just before it ("salary", "remuneração", "pay range") and no benefit word there, and never
    /// followed by "million" or "billion".
    /// </summary>
    public static Pay? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        for (var m = Amount().Match(text); m.Success; m = m.NextMatch())
        {
            var before = text.Substring(Math.Max(0, m.Index - Context), Math.Min(Context, m.Index));
            var after = text.Substring(m.Index + m.Length, Math.Min(15, text.Length - m.Index - m.Length));
            if (PayWord().IsMatch(before) && !BenefitWord().IsMatch(before) && !Huge().IsMatch(after) && Read(m) is { } pay)
                return pay;
        }
        return null;
    }

    /// <summary>How far before an amount its pay word may be.</summary>
    private const int Context = 40;

    private static Pay? Read(Match m)
    {
        var currency = Currency(m.Groups["cur1"].Value + m.Groups["cur2"].Value);
        if (currency is null)
            return null;
        var min = Number(m.Groups["min"].Value, m.Groups["k1"].Success || m.Groups["k2"].Success && !m.Groups["max"].Success);
        var max = m.Groups["max"].Success ? Number(m.Groups["max"].Value, m.Groups["k2"].Success) : min;
        // "40–60k": the k of the upper bound is the lower's too.
        if (m.Groups["max"].Success && m.Groups["k2"].Success && !m.Groups["k1"].Success && min * 1000 <= max)
            min *= 1000;
        if (min <= 0 || max < min)
            return null;
        var period = PeriodOf(m.Groups["period"].Value) ?? (max >= 20_000 ? PayPeriod.Year : max < 300 ? PayPeriod.Hour : PayPeriod.Month);
        return new Pay(min, max, currency, period);
    }

    private static decimal Number(string digits, bool thousands)
    {
        // "4,000" and "4.000" are thousands, "1.800,00" too with cents; "38.50" and "1,8" are decimals.
        var grouped = Regex.Match(digits, @"^(\d{1,3}(?:[.,]\d{3})+)(?:[.,](\d{1,2}))?$");
        var clean = grouped.Success
            ? grouped.Groups[1].Value.Replace(",", "").Replace(".", "") + (grouped.Groups[2].Success ? "." + grouped.Groups[2].Value : "")
            : digits.Replace(",", ".");
        var value = decimal.Parse(clean, CultureInfo.InvariantCulture);
        return thousands ? value * 1000 : value;
    }

    private static string? Currency(string symbol) => symbol.Trim().ToUpperInvariant() switch
    {
        "USD" or "US$" or "$" or "U$" => "USD",
        "BRL" or "R$" => "BRL",
        "EUR" or "€" => "EUR",
        "GBP" or "£" => "GBP",
        _ => null,
    };

    // Month first: "month" and "monthly" contain an h.
    private static PayPeriod? PeriodOf(string word) => word.Trim().ToLowerInvariant() switch
    {
        "" => null,
        var w when w.Contains("mo") || w.Contains("mês") || w.Contains("mes") || w.Contains("mensa") => PayPeriod.Month,
        var w when w.Contains("hour") || w.Contains("hr") || w.EndsWith('h') => PayPeriod.Hour,
        var w when w.Contains("week") || w.Contains("wk") => PayPeriod.Week,
        var w when w.Contains("day") || w.Contains("dai") || w.Contains("dia") => PayPeriod.Day,
        _ => PayPeriod.Year,
    };

    [GeneratedRegex(
        @"(?<cur1>USD|US\$|U\$|BRL|R\$|EUR|GBP|\$|€|£)\s?(?<min>\d{1,3}(?:[.,]\d{3})+(?:[.,]\d{1,2})?(?!\d)|\d+(?:[.,]\d{1,2})?)\s?(?<k1>k\b)?"
        + @"(?:\s?(?:-|–|—|to|a)\s?(?:USD|US\$|BRL|R\$|EUR|GBP|\$|€|£)?\s?(?<max>\d{1,3}(?:[.,]\d{3})+(?:[.,]\d{1,2})?(?!\d)|\d+(?:[.,]\d{1,2})?)\s?(?<k2>k\b)?)?"
        + @"\s?(?<cur2>USD|BRL|EUR|GBP)?"
        + @"\s?(?<period>(?:per|/|an?|a\s+cada)\s?(?:hour|hr|h|day|dia|week|wk|month|mo|mês|mes|year|yr|annum)\b|hourly|daily|weekly|monthly|yearly|annually|annual|p\.?a\.?(?=\W|$)|mensais|por\s+m[eê]s)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Amount();

    [GeneratedRegex(@"\b(?:salary|salaries|salári|salari|remunera|compensation|pay\b|pay\s+range|base|faixa|rate|wage|ctc|ote)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PayWord();

    [GeneratedRegex(@"\b(?:vale|voucher|stipend|benefit|benefício|beneficio|bonus|bônus|auxílio|auxilio|incentiv|allowance|reimburs|reembolso|funding|raised|revenue|valuation|spend|budget)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BenefitWord();

    [GeneratedRegex(@"^\s*(?:million|billion|milhões|bilhões|mi\b|bi\b|mil\b|bn\b|m\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Huge();
}

/// <summary>What a profile wants to earn in one engagement, per month, in its currency.</summary>
public sealed record PayBand(string Currency, decimal MonthlyFloor, decimal MonthlyTarget);

/// <summary>
/// A profile's pay expectations (configuration, outside Git): one band for contractor work and one for EOR. Direct
/// and unknown engagements use the contractor band when the ad pays in dollars and the EOR band when in reais; the
/// rate converts only when the currencies differ.
/// </summary>
public sealed record Compensation(PayBand? Contractor, PayBand? Eor, decimal UsdToBrl, decimal HoursPerMonth)
{
    public PayBand? BandFor(Engagement engagement, string payCurrency) => engagement switch
    {
        Engagement.Contractor => Contractor,
        Engagement.Eor => Eor,
        _ => payCurrency == "BRL" ? Eor ?? Contractor : Contractor ?? Eor,
    };

    /// <summary>An amount in <paramref name="from"/> as <paramref name="to"/>; null for currencies there is no rate for.</summary>
    public decimal? Convert(decimal amount, string from, string to) => (from, to) switch
    {
        _ when from == to => amount,
        ("USD", "BRL") => amount * UsdToBrl,
        ("BRL", "USD") => amount / UsdToBrl,
        _ => null,
    };
}
