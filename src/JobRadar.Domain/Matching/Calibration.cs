using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Matching;

/// <summary>A stored score of a listing the candidate has judged.</summary>
public sealed record JudgedScore(ListingId Id, string Title, string Profile, FitScore Score, Verdict Verdict);

/// <summary>How a profile's scores agree with the candidate.</summary>
/// <param name="PairAccuracy">Of every (good, bad) pair, the share where good scores higher; null without both.</param>
/// <param name="GoodAtAlert">Good listings at or above the alert threshold.</param>
/// <param name="BadVisible">Bad listings still at or above the visibility threshold (they should sink).</param>
/// <param name="Disagreements">Good below the alert threshold and bad still visible, worst first.</param>
public sealed record ProfileCalibration(
    string Profile,
    int Good,
    int Bad,
    double? PairAccuracy,
    int GoodAtAlert,
    int BadVisible,
    IReadOnlyList<JudgedScore> Disagreements);

/// <summary>
/// Measures the score against the candidate's judgement. Run it before and after changing a weight:
/// the change is good if pair accuracy goes up and disagreements go down.
/// </summary>
public static class Calibration
{
    /// <summary>A listing worth alerting about.</summary>
    public const int AlertThreshold = 75;

    /// <summary>Below this a listing sinks out of sight; a bad one above it is a miss.</summary>
    public const int VisibleThreshold = 50;

    /// <summary>
    /// Judged listings per profile before the pair accuracy says much: a first target, not a stopping
    /// point (a model needs a few hundred).
    /// </summary>
    public const int LabelGoal = 50;

    public static IReadOnlyList<ProfileCalibration> Measure(IEnumerable<JudgedScore> judged) => judged
        .GroupBy(j => j.Profile, StringComparer.Ordinal)
        .OrderBy(g => g.Key, StringComparer.Ordinal)
        .Select(Profile)
        .ToList();

    private static ProfileCalibration Profile(IGrouping<string, JudgedScore> scores)
    {
        var good = scores.Where(s => s.Verdict == Verdict.Good).ToList();
        var bad = scores.Where(s => s.Verdict == Verdict.Bad).ToList();

        double? pairs = good.Count > 0 && bad.Count > 0
            ? good.Sum(g => bad.Count(b => g.Score.Final > b.Score.Final)) / (double)(good.Count * bad.Count)
            : null;

        var missedGood = good.Where(g => g.Score.Final < AlertThreshold).OrderBy(g => g.Score.Final);
        var visibleBad = bad.Where(b => b.Score.Final >= VisibleThreshold).OrderByDescending(b => b.Score.Final);

        return new ProfileCalibration(
            scores.Key, good.Count, bad.Count, pairs,
            GoodAtAlert: good.Count(g => g.Score.Final >= AlertThreshold),
            BadVisible: bad.Count(b => b.Score.Final >= VisibleThreshold),
            Disagreements: missedGood.Concat(visibleBad).ToList());
    }
}
