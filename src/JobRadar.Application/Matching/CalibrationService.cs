using JobRadar.Application.Abstractions;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Matching;

/// <summary>The stored scores against the candidate's judgement, per profile.</summary>
public sealed class CalibrationService(IListingScoreRepository scores, ListingScoringService scoring)
{
    public async Task<IReadOnlyList<ProfileCalibration>> MeasureAsync(CancellationToken ct = default) =>
        Calibration.Measure(await scores.ListJudgedAsync(ct));

    /// <summary>
    /// The same measure with the judged listings scored again by this build's rules, without saving: the
    /// "after" of a rule change, read before it ships. The stored scores are the "before".
    /// </summary>
    /// <param name="assume">Requirement states to measure instead of the stored ones (null: the default).</param>
    public async Task<IReadOnlyList<ProfileCalibration>> MeasureRescoredAsync(
        TimeZoneInfo zone, CancellationToken ct = default, IReadOnlyDictionary<string, RequirementState?>? assume = null) =>
        Calibration.Measure(await scoring.RescoreAsync(await scores.ListJudgedAsync(ct), zone, ct, assume));
}
