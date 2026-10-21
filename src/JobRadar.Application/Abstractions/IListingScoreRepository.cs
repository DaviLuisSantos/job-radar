using JobRadar.Domain.Matching;

namespace JobRadar.Application.Abstractions;

/// <summary>Scores per listing and profile (today in Postgres, table <c>listing_scores</c>).</summary>
public interface IListingScoreRepository
{
    /// <summary>Inserts or replaces the score of each (listing, profile).</summary>
    Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default);
}
