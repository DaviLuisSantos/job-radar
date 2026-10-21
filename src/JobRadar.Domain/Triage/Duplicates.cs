using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Triage;

/// <summary>
/// The same job posted several times (reposts, or one ad per city) shows as one row: same title
/// and same company, compared ignoring case and spacing.
/// </summary>
public static partial class Duplicates
{
    /// <summary>
    /// Keeps the first row of each (title, company), in the order given (pass rows best first), and
    /// records the others on it. Rows without a company are never grouped: "Private Advertiser"
    /// style gaps would merge unrelated jobs.
    /// </summary>
    public static IReadOnlyList<TriageRow> Collapse(IEnumerable<TriageRow> ordered)
    {
        var kept = new List<TriageRow>();
        var firstByKey = new Dictionary<(string, string), int>();

        foreach (var row in ordered)
        {
            var company = Normalise(row.Company);
            if (company.Length == 0)
            {
                kept.Add(row);
                continue;
            }

            var key = (Normalise(row.Title), company);
            if (!firstByKey.TryGetValue(key, out var index))
            {
                firstByKey[key] = kept.Count;
                kept.Add(row);
                continue;
            }

            var first = kept[index];
            var location = ScoringLocation(row.Location);
            var others = location is not null && location != ScoringLocation(first.Location) && !first.OtherLocations.Contains(location)
                ? [.. first.OtherLocations, location]
                : first.OtherLocations;
            kept[index] = first with { Copies = [.. first.Copies, row.Id], OtherLocations = others };
        }
        return kept;
    }

    private static string? ScoringLocation(ListingLocation location) => Matching.ScoringInput.FormatLocation(location);

    private static string Normalise(string? text) =>
        text is null ? "" : Spaces().Replace(text.Trim().ToLowerInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
