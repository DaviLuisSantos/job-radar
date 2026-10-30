using JobRadar.Domain.Listings;
using JobRadar.Integrations.LinkedIn.Api;

namespace JobRadar.Integrations.LinkedIn;

/// <summary>LinkedIn's result card → neutral <see cref="Listing"/>. Everything LinkedIn-specific stops here.</summary>
internal static class LinkedInListingTranslator
{
    public const string SourceName = "linkedin";

    /// <summary>
    /// The listing, or null when the card is not in the market's country: LinkedIn answers a location it cannot
    /// resolve with jobs from elsewhere (the United States, mostly), and a card from another country is not a job of
    /// this market.
    /// </summary>
    /// <param name="remote">The search asked for remote work: the card does not say it, the summary does.</param>
    public static Listing? ToListing(LinkedInJobCard card, JobMarket market, bool remote)
    {
        var country = Country(market);
        if (LinkedInLocation.Parse(card.Location, country) is not { } location)
            return null;
        return new Listing(
            Id: new ListingId(SourceName, card.Id),
            Title: card.Title,
            Company: card.Company,
            Location: location,
            WorkType: WorkType.Unknown,
            SalaryText: card.Salary,
            PostedAt: card.PostedOn is { } day ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
            Url: ListingUrl.Choose(card.Link, $"https://www.linkedin.com/jobs/view/{card.Id}"),
            SourceCategory: null,
            Summary: remote ? Remote(market) : null);
    }

    internal static Country Country(JobMarket market) => market switch
    {
        JobMarket.AU => Domain.Listings.Country.AU,
        JobMarket.BR => Domain.Listings.Country.BR,
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, "LinkedIn serves jobs in a country, not the remote markets."),
    };

    /// <summary>The word the market's rules read as remote work (Gupy writes the same for Brazil).</summary>
    private static string Remote(JobMarket market) => market == JobMarket.BR ? "Remoto" : "Remote";
}

/// <summary>
/// The card's location, "Osborne Park, Western Australia, Australia" or "Greater Perth Area", into the canonical
/// one. The country must be named, or the place be a "Greater … Area": anything else is from another country.
/// </summary>
internal static class LinkedInLocation
{
    public static ListingLocation? Parse(string? text, Country country)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ListingLocation.Unknown(country);
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[^1].Equals(CountryName(country), StringComparison.OrdinalIgnoreCase))
        {
            var states = States(country);
            return parts.Length switch
            {
                1 => ListingLocation.Unknown(country),
                2 => states.TryGetValue(parts[0], out var state)
                    ? new ListingLocation(country, null, null, state, null)
                    : new ListingLocation(country, parts[0], null, null, null),
                _ => new ListingLocation(country, parts[0], null, states.GetValueOrDefault(parts[1]), null),
            };
        }
        if (parts.Length == 1 && parts[0].StartsWith("Greater ", StringComparison.OrdinalIgnoreCase))
        {
            var region = parts[0]["Greater ".Length..];
            if (region.EndsWith(" Area", StringComparison.OrdinalIgnoreCase))
                region = region[..^" Area".Length];
            return new ListingLocation(country, null, region.Trim(), null, null);
        }
        return null;
    }

    internal static string CountryName(Country country) => country switch
    {
        Country.AU => "Australia",
        Country.BR => "Brazil",
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, null),
    };

    private static Dictionary<string, string> States(Country country) => country == Country.BR ? BrazilStates : AustralianStates;

    private static readonly Dictionary<string, string> AustralianStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["New South Wales"] = "NSW", ["Victoria"] = "VIC", ["Queensland"] = "QLD", ["Western Australia"] = "WA",
        ["South Australia"] = "SA", ["Tasmania"] = "TAS", ["Australian Capital Territory"] = "ACT", ["Northern Territory"] = "NT",
    };

    /// <summary>LinkedIn writes the states in English, which only changes the Federal District.</summary>
    private static readonly Dictionary<string, string> BrazilStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acre"] = "AC", ["Alagoas"] = "AL", ["Amapá"] = "AP", ["Amazonas"] = "AM", ["Bahia"] = "BA", ["Ceará"] = "CE",
        ["Distrito Federal"] = "DF", ["Federal District"] = "DF", ["Espírito Santo"] = "ES", ["Goiás"] = "GO",
        ["Maranhão"] = "MA", ["Mato Grosso"] = "MT", ["Mato Grosso do Sul"] = "MS", ["Minas Gerais"] = "MG", ["Pará"] = "PA",
        ["Paraíba"] = "PB", ["Paraná"] = "PR", ["Pernambuco"] = "PE", ["Piauí"] = "PI", ["Rio de Janeiro"] = "RJ",
        ["Rio Grande do Norte"] = "RN", ["Rio Grande do Sul"] = "RS", ["Rondônia"] = "RO", ["Roraima"] = "RR",
        ["Santa Catarina"] = "SC", ["São Paulo"] = "SP", ["Sergipe"] = "SE", ["Tocantins"] = "TO",
    };
}
