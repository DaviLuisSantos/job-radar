using JobRadar.Application.Abstractions;

namespace JobRadar.Application.Operations;

/// <param name="Problem">Why it is not healthy, for the watchdog's log; null when it is.</param>
/// <param name="DatabaseReachable">False means restarting the worker would not help: the database is down.</param>
public sealed record HealthReport(bool Healthy, bool DatabaseReachable, DateTimeOffset? LastPassStarted, string? Problem);

/// <summary>
/// Whether the unattended system is doing its job: the database answers and a pass started within
/// <see cref="MaxPassAge"/> (a day plus slack: a pass that ran late is fine, one that never ran is not). A failed
/// stage is not unhealthy: it is retried at the next pass and shows on the status page.
/// </summary>
public sealed class HealthService(IStorageHealth storage, ICollectionRunRepository runs, TimeProvider time)
{
    public static readonly TimeSpan MaxPassAge = TimeSpan.FromHours(26);

    public async Task<HealthReport> CheckAsync(CancellationToken ct = default)
    {
        if (!await storage.CanConnectAsync(ct))
            return new HealthReport(false, false, null, "database not reachable");

        var last = await runs.GetLastStartAsync(ct);
        var now = time.GetUtcNow();
        if (last is null)
            return new HealthReport(false, true, null, "no pass has ever run");
        if (now - last > MaxPassAge)
            return new HealthReport(false, true, last, $"last pass started {(now - last.Value).TotalHours:N0} h ago");
        return new HealthReport(true, true, last, null);
    }
}
