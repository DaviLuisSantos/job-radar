using JobRadar.Application.Collection;
using JobRadar.Application.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker;

/// <summary>
/// Runs the <see cref="DailyPipeline"/> once a day at <see cref="CollectionOptions.DailyAt"/>, and at
/// start when the last scheduled pass was missed. A failed pass waits for the next day; it never
/// stops the worker.
/// </summary>
internal sealed class DailyPipelineJob(
    IServiceScopeFactory scopes,
    DailyPipeline pipeline,
    ProfileCatalog catalog,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    ILogger<DailyPipelineJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone);

        if (catalog.Profiles.Count == 0)
        {
            log.LogWarning("No profiles in Matching:Profiles (see appsettings.Local.example.json); nothing to do");
            return;
        }
        log.LogInformation("{Profiles} profiles, {Searches} distinct searches", catalog.Profiles.Count, catalog.Searches.Count);

        if (!await WaitForStorageAsync(stoppingToken))
            return;

        if (o.RunOnStartup || await MissedLastRunAsync(o, zone, stoppingToken))
            await pipeline.RunAsync(fullHistory: false, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = DailySchedule.NextAfter(time.GetUtcNow(), o.DailyAt, zone);
            log.LogInformation("Next run at {Next} ({Zone})", TimeZoneInfo.ConvertTime(next, zone), o.TimeZone);
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await pipeline.RunAsync(fullHistory: false, stoppingToken);
        }
    }


    /// <summary>
    /// The database may come up after the service (Docker Desktop starts at login). Waits with a
    /// growing pause; gives up only when the worker stops.
    /// </summary>
    private async Task<bool> WaitForStorageAsync(CancellationToken ct)
    {
        var pause = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            await using (var scope = scopes.CreateAsyncScope())
            {
                if (await scope.ServiceProvider.GetRequiredService<JobRadar.Application.Abstractions.IStorageHealth>().CanConnectAsync(ct))
                    return true;
            }
            log.LogWarning("Database not reachable yet; retrying in {Pause}", pause);
            await Task.Delay(pause, time, ct);
            pause = TimeSpan.FromSeconds(Math.Min(pause.TotalSeconds * 2, 120));
        }
        return false;
    }

    private async Task<bool> MissedLastRunAsync(CollectionOptions o, TimeZoneInfo zone, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var last = await scope.ServiceProvider.GetRequiredService<JobRadar.Application.Abstractions.ICollectionRunRepository>().GetLastStartAsync(ct);
        var missed = DailySchedule.Missed(last, time.GetUtcNow(), o.DailyAt, zone);
        if (missed)
            log.LogInformation("Last scheduled run was missed (last start {Last}); running now", last);
        return missed;
    }


}
