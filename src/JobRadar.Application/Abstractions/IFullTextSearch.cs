using JobRadar.Domain.Matching;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Optional capability of a job source: tell which of its listings contain a phrase anywhere in
/// the full ad, without opening the ad. Seek's search covers the whole text, so it can; a source
/// that cannot simply does not register this.
/// </summary>
public interface IFullTextSearch
{
    /// <summary>Same value as <c>ListingId.Source</c> of the listings it answers for.</summary>
    string Source { get; }

    /// <summary>Listings anywhere in Australia posted in the last <paramref name="postedWithinDays"/> days.</summary>
    Task<FullTextResult> FindAsync(FullTextTerm term, int postedWithinDays, CancellationToken ct = default);
}
