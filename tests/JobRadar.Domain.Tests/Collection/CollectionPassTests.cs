using JobRadar.Domain.Collection;

namespace JobRadar.Domain.Tests.Collection;

public class CollectionPassTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 24, 6, 0, 0, TimeSpan.FromHours(8));

    private static CollectionRun Run(DateTimeOffset at, string key = "fifo @ perth", bool complete = true, string? error = null) =>
        new("seek", key, PostedWithinDays: 2, at, at.AddSeconds(20), Found: 5, New: 1, complete, error);

    [Fact]
    public void No_runs_no_pass() => Assert.Null(CollectionPass.Last([]));

    [Fact]
    public void The_last_pass_starts_after_the_first_long_pause_since_every_run_before_ended()
    {
        // As on 2026-10-23: a pass recovered at 18:04 (last run started 18:10) and the scheduled one at 19:01.
        var recovered = new[] { Run(Morning.AddMinutes(-57)), Run(Morning.AddMinutes(-51), "term: backpackers") };
        var pass = new[] { Run(Morning), Run(Morning.AddMinutes(2), "term: backpackers"), Run(Morning.AddMinutes(14)) };

        var last = CollectionPass.Last([.. pass.Reverse(), Run(Morning.AddDays(-1)), .. recovered])!;

        Assert.Equal(pass, last.Runs);
        Assert.Equal(Morning, last.StartedAt);
        Assert.Equal(Morning.AddMinutes(14).AddSeconds(20), last.FinishedAt);
    }

    [Fact]
    public void A_long_run_keeps_the_pass_open_until_it_ends()
    {
        // One search retrying for 20 minutes; the terms start only when it ends.
        var slow = Run(Morning) with { FinishedAt = Morning.AddMinutes(20) };
        var terms = Run(Morning.AddMinutes(21), "term: backpackers");

        var last = CollectionPass.Last([Run(Morning.AddSeconds(-5)), slow, terms])!;

        Assert.Equal(3, last.Runs.Count);
    }

    [Fact]
    public void Problems_list_failures_first_then_cut_runs_by_search()
    {
        var pass = new CollectionPass(
        [
            Run(Morning, "term: permanent resident", complete: false),
            Run(Morning, "fifo @ perth"),
            Run(Morning, "term: australian citizen", complete: false),
            Run(Morning, "labourer @ perth", error: "HttpRequestException: 503"),
        ]);

        Assert.Equal(
            ["labourer @ perth", "term: australian citizen", "term: permanent resident"],
            pass.Problems.Select(r => r.SearchKey));
    }
}
