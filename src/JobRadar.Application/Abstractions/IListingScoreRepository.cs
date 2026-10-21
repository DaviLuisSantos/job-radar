using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Abstractions;

/// <summary>Scores per listing and profile (today in Postgres, table <c>listing_scores</c>).</summary>
public interface IListingScoreRepository
{
    /// <summary>Inserts or replaces the score of each (listing, profile).</summary>
    Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default);

    /// <summary>
    /// Deletes the scores last saved before <paramref name="before"/>: after a full pass, those of
    /// listings no profile search finds any more and of profiles no longer configured.
    /// </summary>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default);

    /// <summary>
    /// Stored scores of every listing the candidate has judged, with the verdict that counts
    /// (explicit label, else the application status; see <c>Judgement.Effective</c>).
    /// </summary>
    Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default);

    /// <summary>Profiles that have scores, alphabetically.</summary>
    Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Every listing of the profile that passes the filter, best score first, then most recent.
    /// Paging is ignored: duplicates are collapsed before paging, by the caller.
    /// </summary>
    Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default);

    /// <summary>The listing's score in every profile that scored it.</summary>
    Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default);
}
