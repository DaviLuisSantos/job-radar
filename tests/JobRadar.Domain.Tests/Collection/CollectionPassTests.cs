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
    public void The_last_pass_stops_at_the_first_long_pause()
    {
        var yesterday = Run(Morning.AddDays(-1));
        var earlierToday = Run(Morning.AddHours(-1).AddMinutes(-1));   // a recovered pass, an hour before
        var pass = new[] { Run(Morning), Run(Morning.AddMinutes(2), "term: backpackers"), Run(Morning.AddMinutes(50)) };

        var last = CollectionPass.Last([.. pass.Reverse(), yesterday, earlierToday])!;

        Assert.Equal(pass, last.Runs);
        Assert.Equal(Morning, last.StartedAt);
        Assert.Equal(Morning.AddMinutes(50).AddSeconds(20), last.FinishedAt);
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
