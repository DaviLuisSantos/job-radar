namespace JobRadar.Domain.Listings;

/// <summary>
/// What a profile looks for, and so which sources it searches and which rules read the ads. Not a country: a remote
/// job is in no country, and the same job can serve two markets; where a listing is stays in its
/// <see cref="ListingLocation.Country"/>.
/// </summary>
public enum JobMarket
{
    /// <summary>Jobs in Australia, for a working holiday visa.</summary>
    AU,

    /// <summary>Jobs in Brazil, on site, hybrid or remote within the country.</summary>
    BR,

    /// <summary>Remote jobs restricted to a region that includes Brazil (LATAM, Americas, South America, Brazil).</summary>
    LatamRemote,

    /// <summary>Remote jobs open to anywhere (Worldwide, Anywhere).</summary>
    GlobalRemote,
}

public static class JobMarkets
{
    /// <summary>By name, ignoring case; null when no market has it.</summary>
    public static JobMarket? Parse(string? value) =>
        Enum.GetValues<JobMarket>().Cast<JobMarket?>()
            .FirstOrDefault(m => m.ToString()!.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
