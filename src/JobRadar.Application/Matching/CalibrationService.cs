using JobRadar.Application.Abstractions;
using JobRadar.Domain.Matching;

namespace JobRadar.Application.Matching;

/// <summary>The stored scores against the candidate's judgement, per profile.</summary>
public sealed class CalibrationService(IListingScoreRepository scores)
{
    public async Task<IReadOnlyList<ProfileCalibration>> MeasureAsync(CancellationToken ct = default) =>
        Calibration.Measure(await scores.ListJudgedAsync(ct));
}
