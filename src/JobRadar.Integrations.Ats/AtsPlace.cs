using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Ats;

/// <summary>Where a job is and which of our markets it belongs to; <see cref="Market"/> is the one the job is sorted into.</summary>
/// <param name="Eligibility">Who may apply, for the remote markets ("Brazil", "LATAM", "Worldwide"); null for a place.</param>
internal sealed record AtsPlace(JobMarket Market, ListingLocation Location, string? Eligibility)
{
    private static readonly Regex RemoteWord = new(@"\b(remote|remoto|work from home|anywhere|worldwide)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WorldWord = new(@"\b(anywhere|worldwide)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BrazilWord = new(@"\b(brazil|brasil)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LatamWord = new(@"\b(latam|latin america|south america|america latina)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AustraliaWord = new(@"\baustralia\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> AustralianCities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sydney"] = "NSW", ["Melbourne"] = "VIC", ["Brisbane"] = "QLD", ["Perth"] = "WA", ["Adelaide"] = "SA",
        ["Hobart"] = "TAS", ["Canberra"] = "ACT", ["Darwin"] = "NT", ["Gold Coast"] = "QLD",
        ["Wollongong"] = "NSW", ["Geelong"] = "VIC", ["Cairns"] = "QLD", ["Townsville"] = "QLD",
    };

    private static readonly Dictionary<string, string> BrazilianCities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sao Paulo"] = "SP", ["Rio de Janeiro"] = "RJ", ["Belo Horizonte"] = "MG", ["Brasilia"] = "DF", ["Curitiba"] = "PR",
        ["Porto Alegre"] = "RS", ["Recife"] = "PE", ["Fortaleza"] = "CE", ["Campinas"] = "SP", ["Florianopolis"] = "SC",
        ["Goiania"] = "GO", ["Vitoria"] = "ES", ["Belem"] = "PA", ["Manaus"] = "AM",
    };

    private static readonly HashSet<string> AustralianStateWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "NSW", "VIC", "QLD", "WA", "SA", "TAS", "ACT", "NT", "New South Wales", "Victoria", "Queensland", "Western Australia",
        "South Australia", "Tasmania", "Australian Capital Territory", "Northern Territory",
    };

    private static readonly HashSet<string> BrazilianStateWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };

    /// <summary>
    /// The place the board gives, from the job's location and its offices; null when the job is in none of our markets.
    /// A job open to "Remote" alone is dropped: boards mean a country by it, most often the United States.
    /// </summary>
    public static AtsPlace? Parse(IEnumerable<string?> texts)
    {
        var all = texts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).Distinct().ToList();
        var remote = all.Any(RemoteWord.IsMatch);
        var australia = all.Select(AustralianPlace).FirstOrDefault(p => p is not null);
        var brazil = all.Select(BrazilianPlace).FirstOrDefault(p => p is not null);

        if (remote)
        {
            if (brazil is not null || all.Any(BrazilWord.IsMatch))
                return new AtsPlace(JobMarket.LatamRemote, ListingLocation.Unknown(Country.Remote), "Brazil");
            if (all.Any(LatamWord.IsMatch))
                return new AtsPlace(JobMarket.LatamRemote, ListingLocation.Unknown(Country.Remote), "LATAM");
            if (australia is not null)
                return new AtsPlace(JobMarket.AU, australia, null);
            return all.Any(WorldWord.IsMatch)
                ? new AtsPlace(JobMarket.GlobalRemote, ListingLocation.Unknown(Country.Remote), RemoteEligibility.Worldwide)
                : null;
        }

        if (australia is not null)
            return new AtsPlace(JobMarket.AU, australia, null);
        return brazil is not null ? new AtsPlace(JobMarket.BR, brazil, null) : null;
    }

    private static ListingLocation? AustralianPlace(string text)
    {
        var parts = Parts(text);
        if (AustraliaWord.IsMatch(text))
        {
            var city = parts.FirstOrDefault(AustralianCities.ContainsKey);
            return city is null
                ? new ListingLocation(Country.AU, parts.Count > 1 ? parts[0] : null, null, null, null)
                : new ListingLocation(Country.AU, city, null, AustralianCities[city], null);
        }
        // A city alone, or with a state of the country ("Melbourne, FL" is not Melbourne).
        return CityAlone(parts, AustralianCities, AustralianStateWords, Country.AU);
    }

    private static ListingLocation? BrazilianPlace(string text)
    {
        var parts = Parts(text);
        if (BrazilWord.IsMatch(text))
        {
            var city = parts.FirstOrDefault(p => BrazilianCities.ContainsKey(Fold(p)));
            return city is null
                ? new ListingLocation(Country.BR, parts.Count > 1 ? parts[0] : null, null, null, null)
                : new ListingLocation(Country.BR, city, null, BrazilianCities[Fold(city)], null);
        }
        return CityAlone(parts, BrazilianCities, BrazilianStateWords, Country.BR);
    }

    private static ListingLocation? CityAlone(
        IReadOnlyList<string> parts, Dictionary<string, string> cities, HashSet<string> stateWords, Country country)
    {
        if (parts.Count == 0 || !cities.TryGetValue(Fold(parts[0]), out var state))
            return null;
        var rest = parts.Skip(1).ToList();
        return rest.Count == 0 || rest.All(stateWords.Contains) ? new ListingLocation(country, parts[0], null, state, null) : null;
    }

    private static List<string> Parts(string text) =>
        text.Split([',', ';', '(', ')', '/', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Lower accents away, so "São Paulo" and "Sao Paulo" are one key.</summary>
    private static string Fold(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
