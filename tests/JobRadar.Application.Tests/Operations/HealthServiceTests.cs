using JobRadar.Application.Abstractions;
using JobRadar.Application.Operations;
using JobRadar.Application.Tests.Collection;
using JobRadar.Domain.Collection;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Operations;

public class HealthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Storage(bool up) : IStorageHealth
    {
        public Task<bool> CanConnectAsync(CancellationToken ct = default) => Task.FromResult(up);
    }

    private static async Task<HealthReport> CheckAsync(bool databaseUp, DateTimeOffset? lastPass)
    {
        var runs = new InMemoryRuns();
        if (lastPass is { } at)
            await runs.RecordAsync(new CollectionRun("seek", "dev @ perth", 2, at, at.AddMinutes(1), 1, 0, true, null), Ct);
        return await new HealthService(new Storage(databaseUp), runs, new FakeTimeProvider(Now)).CheckAsync(Ct);
    }

    [Fact]
    public async Task A_pass_within_a_day_and_a_bit_is_healthy()
    {
        var report = await CheckAsync(databaseUp: true, Now.AddHours(-25));

        Assert.True(report.Healthy);
        Assert.Null(report.Problem);
    }

    [Fact]
    public async Task A_pass_older_than_that_is_not_and_says_how_old()
    {
        var report = await CheckAsync(databaseUp: true, Now.AddHours(-30));

        Assert.False(report.Healthy);
        Assert.True(report.DatabaseReachable);
        Assert.Equal("last pass started 30 h ago", report.Problem);
    }

    [Fact]
    public async Task A_database_that_does_not_answer_is_named_as_the_problem()
    {
        // The watchdog reads this to leave the worker alone: restarting it would not bring the database up.
        var report = await CheckAsync(databaseUp: false, Now.AddHours(-1));

        Assert.False(report.Healthy);
        Assert.False(report.DatabaseReachable);
    }

    [Fact]
    public async Task No_pass_ever_is_not_healthy()
    {
        Assert.Equal("no pass has ever run", (await CheckAsync(databaseUp: true, null)).Problem);
    }
}
