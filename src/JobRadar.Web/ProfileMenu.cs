using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;

namespace JobRadar.Web;

/// <summary>The profiles of one market, as the top bar shows them.</summary>
public sealed record ProfileGroup(JobMarket Market, string Label, IReadOnlyList<string> Profiles);

/// <summary>
/// The top bar's profiles, by market: every configured profile, scored or not yet (a new profile shows before its
/// first pass, with an empty list), plus any profile that still has scores but no configuration. A profile the
/// configuration does not know is taken as Australian, the market every old profile was in.
/// </summary>
public static class ProfileMenu
{
    public static IReadOnlyList<ProfileGroup> Groups(IEnumerable<ProfileDefinition> configured, IEnumerable<string> scored)
    {
        var markets = new Dictionary<string, JobMarket>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in configured)
            markets.TryAdd(p.Name, p.Market);
        foreach (var name in scored)
            markets.TryAdd(name, JobMarket.AU);

        return markets
            .GroupBy(p => p.Value)
            .OrderBy(g => g.Key)
            .Select(g => new ProfileGroup(g.Key, Label(g.Key), g.Select(p => p.Key).Order(StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    public static string Label(JobMarket market) => market switch
    {
        JobMarket.AU => "Australia",
        JobMarket.BR => "Brazil",
        JobMarket.LatamRemote => "Remote LATAM",
        JobMarket.GlobalRemote => "Remote global",
        _ => market.ToString(),
    };
}
