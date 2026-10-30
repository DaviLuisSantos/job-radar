using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;

namespace JobRadar.Web;

/// <summary>The profiles of one market, as the top bar shows them.</summary>
public sealed record ProfileGroup(Country Country, string Label, IReadOnlyList<string> Profiles);

/// <summary>
/// The top bar's profiles, by market: every configured profile, scored or not yet (a new profile shows before its
/// first pass, with an empty list), plus any profile that still has scores but no configuration. A profile the
/// configuration does not know is taken as Australian, the market every old profile was in.
/// </summary>
public static class ProfileMenu
{
    public static IReadOnlyList<ProfileGroup> Groups(IEnumerable<ProfileDefinition> configured, IEnumerable<string> scored)
    {
        var countries = new Dictionary<string, Country>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in configured)
            countries.TryAdd(p.Name, p.Country);
        foreach (var name in scored)
            countries.TryAdd(name, Country.AU);

        return countries
            .GroupBy(p => p.Value)
            .OrderBy(g => g.Key)
            .Select(g => new ProfileGroup(g.Key, Label(g.Key), g.Select(p => p.Key).Order(StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    public static string Label(Country country) => country switch
    {
        Country.AU => "Australia",
        Country.BR => "Brazil",
        _ => country.ToString(),
    };
}
