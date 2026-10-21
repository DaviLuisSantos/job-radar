using System.Text.RegularExpressions;

namespace JobRadar.Domain.Market;

/// <summary>A count from a snapshot that passed <see cref="SnapshotQuality"/>.</summary>
public sealed record DailyCount(DateOnly Date, string Category, string City, string? Term, int Total);

/// <summary>A listing as the market store keeps it: when it was first and last seen open.</summary>
public sealed record SeenListing(
    string Category, string City, string Title, string? Company,
    int? AnnualMin, int? AnnualMax, DateTimeOffset? PostedAt, DateOnly FirstSeen, DateOnly LastSeen);

public sealed record Mention(string Term, double Average, double? Share);

/// <param name="Median">Of the middle of each advertised range, per year.</param>
/// <param name="WithSalary">How many unique listings state a salary.</param>
public sealed record SalaryStats(double Median, double P25, double P75, int WithSalary);

/// <param name="Seen">Listings open at some point in the month (one per id).</param>
/// <param name="Unique">After collapsing reposts (same title and company in the city).</param>
/// <param name="PostedInMonth">Unique listings first posted in the month.</param>
public sealed record ListingStats(int Seen, int Unique, int PostedInMonth, SalaryStats? Salary);

/// <param name="AverageOpen">Daily average of open listings over the month's good snapshots.</param>
/// <param name="ChangeVsPreviousMonth">Relative change of <see cref="AverageOpen"/>; null without a previous month.</param>
public sealed record CityReport(
    string City, double AverageOpen, double? ChangeVsPreviousMonth, IReadOnlyList<Mention> Mentions, ListingStats? Listings);

public sealed record CategoryReport(string Category, IReadOnlyList<CityReport> Cities);

/// <param name="DaysCovered">Distinct days with a good snapshot.</param>
/// <param name="Skipped">Snapshots of the month left out (partial or suspect).</param>
public sealed record MarketReport(
    string Month, int DaysCovered, IReadOnlyList<MarketSnapshot> Skipped, IReadOnlyList<CategoryReport> Categories);

/// <summary>
/// The monthly Raio-X from good snapshots only. Aggregates, never individual listings: those are
/// what the source's terms protect. Ported from the prototype.
/// </summary>
public static partial class MonthlyReport
{
    public static MarketReport? Build(
        string month,
        IReadOnlyList<string> categoryOrder,
        IReadOnlyList<string> cityOrder,
        IReadOnlyList<string> termOrder,
        IReadOnlyCollection<DailyCount> current,
        IReadOnlyCollection<DailyCount> previous,
        IReadOnlyCollection<SeenListing> listings,
        IReadOnlyList<MarketSnapshot> skipped)
    {
        if (current.Count == 0)
            return null;

        var averages = Averages(current);
        var previousAverages = Averages(previous);
        var days = current.Select(c => c.Date).Distinct().Count();
        var rank = cityOrder.Select((city, i) => (city, i)).DistinctBy(x => x.city).ToDictionary(x => x.city, x => x.i);

        var categories = categoryOrder
            .Where(cat => averages.Keys.Any(k => k.Category == cat))
            .Select(cat => new CategoryReport(cat, averages.Keys
                .Where(k => k.Category == cat && k.Term is null)
                .Select(k => k.City)
                .Distinct()
                // Configured order (full cities first); a city no longer configured goes last.
                .OrderBy(city => rank.GetValueOrDefault(city, int.MaxValue))
                .ThenBy(city => city, StringComparer.Ordinal)
                .Select(city => City(month, cat, city, termOrder, averages, previousAverages, listings))
                .ToList()))
            .ToList();

        return new MarketReport(month, days, skipped, categories);
    }

    private static CityReport City(
        string month, string category, string city, IReadOnlyList<string> termOrder,
        Dictionary<(string Category, string City, string? Term), double> averages,
        Dictionary<(string Category, string City, string? Term), double> previous,
        IReadOnlyCollection<SeenListing> listings)
    {
        var total = averages[(category, city, null)];
        double? change = previous.TryGetValue((category, city, null), out var p) && p > 0 ? (total - p) / p : null;

        var mentions = termOrder
            .Where(t => averages.ContainsKey((category, city, t)))
            .Select(t =>
            {
                var n = averages[(category, city, t)];
                return new Mention(t, n, total > 0 ? n / total : null);
            })
            .ToList();

        var here = listings.Where(l => l.Category == category && l.City == city).ToList();
        return new CityReport(city, total, change, mentions, here.Count == 0 ? null : Stats(month, here));
    }

    private static ListingStats Stats(string month, List<SeenListing> listings)
    {
        var unique = Dedupe(listings);
        var postedInMonth = Dedupe(listings.Where(l => l.PostedAt?.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) == month)).Count;

        var mids = unique
            .Select(l => (l.AnnualMin, l.AnnualMax) switch
            {
                ({ } lo, { } hi) => (double?)((lo + hi) / 2.0),
                ({ } lo, null) => lo,
                (null, { } hi) => hi,
                _ => null,
            })
            .OfType<double>()
            .Order()
            .ToList();

        var salary = mids.Count == 0 ? null : new SalaryStats(Percentile(mids, .5), Percentile(mids, .25), Percentile(mids, .75), mids.Count);
        return new ListingStats(listings.Count, unique.Count, postedInMonth, salary);
    }

    /// <summary>Same title and company in the same city with different ids: keep the first seen.</summary>
    internal static List<SeenListing> Dedupe(IEnumerable<SeenListing> listings) => listings
        .OrderBy(l => l.FirstSeen)
        .DistinctBy(l => $"{Normalise(l.Title)}|{Normalise(l.Company)}|{l.City}")
        .ToList();

    /// <summary>Linear interpolation between closest ranks (the usual default of numpy and pandas).</summary>
    internal static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        var position = (sorted.Count - 1) * p;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static Dictionary<(string Category, string City, string? Term), double> Averages(IEnumerable<DailyCount> counts) => counts
        .GroupBy(c => (c.Category, c.City, c.Term))
        .ToDictionary(g => g.Key, g => g.Average(c => (double)c.Total));

    private static string Normalise(string? text) =>
        text is null ? "" : NonAlphanumeric().Replace(text.ToLowerInvariant(), " ").Trim();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
