using JobRadar.Domain.Matching;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// The candidate's state on each catalog requirement (today in Postgres, table <c>candidate_requirements</c>).
/// One candidate, the author, as everything else in v1.
/// </summary>
public interface ICandidateRequirementRepository
{
    /// <summary>
    /// Every state the candidate set (an item missing here takes the default of its kind) and their years of
    /// experience per profile (table <c>candidate_experience</c>).
    /// </summary>
    Task<CandidateRequirements> GetAsync(CancellationToken ct = default);

    /// <summary>Sets the state of a catalog item; null clears it back to the default.</summary>
    Task SetAsync(string itemId, RequirementState? state, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>Sets the candidate's years of experience in a profile's line of work; null forgets them.</summary>
    Task SetExperienceAsync(string profile, double? years, DateTimeOffset at, CancellationToken ct = default);
}
