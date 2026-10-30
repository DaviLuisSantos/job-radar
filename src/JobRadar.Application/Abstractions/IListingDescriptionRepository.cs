using JobRadar.Domain.Listings;

namespace JobRadar.Application.Abstractions;

/// <summary>Full ad texts the candidate captured (today in Postgres, table <c>listing_descriptions</c>).</summary>
public interface IListingDescriptionRepository
{
    /// <summary>Stores the text, replacing an earlier capture of the same listing.</summary>
    Task SaveAsync(ListingDescription description, CancellationToken ct = default);

    Task<ListingDescription?> FindAsync(ListingId id, CancellationToken ct = default);

    /// <summary>The captured text of each of these listings that has one.</summary>
    Task<IReadOnlyDictionary<ListingId, string>> GetTextsAsync(IReadOnlyCollection<ListingId> ids, CancellationToken ct = default);

    /// <summary>
    /// The title and full ad (null when none was stored) of every listing of <paramref name="country"/> first seen
    /// from <paramref name="from"/> up to, not including, <paramref name="to"/>: what a month's ads asked for.
    /// </summary>
    Task<IReadOnlyList<(string Title, string? Description)>> ListAdsFirstSeenAsync(
        Country country, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>
    /// Listings of <paramref name="source"/> with no text yet, posted (or first seen, when the date is
    /// unknown) since <paramref name="postedSince"/>, whose best score in any profile is at least
    /// <paramref name="minScore"/>: best score first, then most recent. Before them, whatever their age
    /// or score, the listings the candidate labelled or tracked: calibration reads those, and a good one
    /// the score undervalues is exactly the case the full text should explain. At most <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<ListingId>> ListToFetchAsync(
        string source, int minScore, DateTimeOffset postedSince, int limit, CancellationToken ct = default);
}
