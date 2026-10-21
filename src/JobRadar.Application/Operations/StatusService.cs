using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Market;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Operations;

/// <summary>How the unattended pipeline has been doing, for the status page.</summary>
/// <param name="NextRun">Next scheduled pass, in the schedule time zone.</param>
/// <param name="LastRunMissed">The last scheduled pass did not start (machine off, or the worker down).</param>
public sealed record PipelineStatus(
    DateTimeOffset NextRun,
    DateTimeOffset? LastStart,
    bool LastRunMissed,
    IReadOnlyList<DailyRunSummary> Days,
    IReadOnlyList<MarketSnapshot> Snapshots);

public sealed class StatusService(
    ICollectionRunRepository runs,
    IMarketRepository market,
    IOptions<CollectionOptions> options,
    TimeProvider time)
{
    public const int Days = 10;

    public async Task<PipelineStatus> GetAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var last = await runs.GetLastStartAsync(ct);

        return new PipelineStatus(
            TimeZoneInfo.ConvertTime(DailySchedule.NextAfter(now, o.DailyAt, zone), zone),
            last,
            DailySchedule.Missed(last, now, o.DailyAt, zone),
            await runs.SummariseRecentDaysAsync(Days, zone, ct),
            (await market.GetSnapshotsAsync("seek", today.AddDays(-Days), today, ct)).Reverse().ToList());
    }
}
