using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Tests.Matching;

public class CalibrationTests
{
    private static JudgedScore J(string id, int score, Verdict verdict, string profile = "tech") =>
        new(new ListingId("seek", id), $"Job {id}", profile, new FitScore(score, []), verdict);

    [Fact]
    public void Pair_accuracy_is_the_share_of_good_bad_pairs_ordered_right()
    {
        // Pairs: (90>40 ✓) (90>60 ✓) (55>40 ✓) (55>60 ✗) → 3 of 4.
        var result = Assert.Single(Calibration.Measure(
            [J("g1", 90, Verdict.Good), J("g2", 55, Verdict.Good), J("b1", 40, Verdict.Bad), J("b2", 60, Verdict.Bad)]));

        Assert.Equal(0.75, result.PairAccuracy);
        Assert.Equal((2, 2), (result.Good, result.Bad));
    }

    [Fact]
    public void A_tie_counts_as_wrong()
    {
        Assert.Equal(0.0, Assert.Single(Calibration.Measure([J("g", 50, Verdict.Good), J("b", 50, Verdict.Bad)])).PairAccuracy);
    }

    [Fact]
    public void Without_both_verdicts_there_is_no_pair_accuracy()
    {
        Assert.Null(Assert.Single(Calibration.Measure([J("g", 80, Verdict.Good)])).PairAccuracy);
    }

    [Fact]
    public void Disagreements_are_good_below_the_alert_and_bad_still_visible_worst_first()
    {
        var result = Assert.Single(Calibration.Measure(
        [
            J("good-ok", 80, Verdict.Good), J("good-low", 60, Verdict.Good), J("good-lower", 30, Verdict.Good),
            J("bad-ok", 20, Verdict.Bad), J("bad-visible", 55, Verdict.Bad), J("bad-high", 85, Verdict.Bad),
        ]));

        Assert.Equal(1, result.GoodAtAlert);
        Assert.Equal(2, result.BadVisible);
        Assert.Equal(["good-lower", "good-low", "bad-high", "bad-visible"], result.Disagreements.Select(d => d.Id.SourceId));
    }

    [Fact]
    public void Each_profile_is_measured_on_its_own()
    {
        var results = Calibration.Measure([J("1", 80, Verdict.Good, "tech"), J("1", 20, Verdict.Good, "fifo")]);

        Assert.Equal(["fifo", "tech"], results.Select(r => r.Profile));
        Assert.Equal(0, results[0].GoodAtAlert);
        Assert.Equal(1, results[1].GoodAtAlert);
    }
}
