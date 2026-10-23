using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Domain.Collection;
using JobRadar.Domain.Market;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Operations;

/// <summary>How the unattended pipeline has been doing, for the status page.</summary>
/// <param name="NextRun">Next scheduled pass, in the schedule time zone.</param>
/// <param name="LastRunMissed">The last scheduled pass did not start (machine off, or the worker down).</param>
/// <param name="LastPass">Runs of the most recent pass, to list what failed or was cut; null if nothing ran.</param>
public sealed record PipelineStatus(
    DateTimeOffset NextRun,
    DateTimeOffset? LastStart,
    bool LastRunMissed,
    CollectionPass? LastPass,
    IReadOnlyList<DailyRunSummary> Days,
    IReadOnlyList<MarketSnapshot> Snapshots);

public sealed class StatusService(
    ICollectionRunRepository runs,
    IMarketRepository market,
    IOptions<CollectionOptions> options,
    TimeProvider time)
{
    public const int Days = 10;

    /// <summary>How far before the last start to look for the rest of its pass; a pass takes minutes.</summary>
    private static readonly TimeSpan PassLookBack = TimeSpan.FromHours(12);

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
            last is { } l ? CollectionPass.Last(await runs.ListStartedSinceAsync(l - PassLookBack, ct)) : null,
            await runs.SummariseRecentDaysAsync(Days, zone, ct),
            (await market.GetSnapshotsAsync("seek", today.AddDays(-Days), today, ct)).Reverse().ToList());
    }
}
