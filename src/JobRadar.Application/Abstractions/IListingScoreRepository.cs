using JobRadar.Domain.Matching;

namespace JobRadar.Application.Abstractions;

/// <summary>Scores per listing and profile (today in Postgres, table <c>listing_scores</c>).</summary>
public interface IListingScoreRepository
{
    /// <summary>Inserts or replaces the score of each (listing, profile).</summary>
    Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default);

    /// <summary>
    /// Stored scores of every listing the candidate has judged, with the verdict that counts
    /// (explicit label, else the application status; see <c>Judgement.Effective</c>).
    /// </summary>
    Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default);
}
