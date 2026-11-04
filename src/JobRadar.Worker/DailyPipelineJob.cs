using JobRadar.Application.Collection;
using JobRadar.Application.Matching;
using JobRadar.Application.Settings;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker;

/// <summary>
/// Runs the <see cref="DailyPipeline"/> once a day at <see cref="CollectionOptions.DailyAt"/>, and at
/// start when the last scheduled pass was missed. A failed pass waits for the next day; it never
/// stops the worker. While idle it watches the stored settings and stops the host when they change, so the
/// service manager restarts the worker with the new configuration; it never does that during a pass.
/// </summary>
internal sealed class DailyPipelineJob(
    IServiceScopeFactory scopes,
    DailyPipeline pipeline,
    ProfileCatalog catalog,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    IServiceProvider services,
    ILogger<DailyPipelineJob> log) : BackgroundService
{
    private SettingsRevisionWatcher? _watcher;

    /// <summary>Resolved late: the configuration and the host lifetime exist in a host, not in a bare container.</summary>
    private SettingsRevisionWatcher Watcher => _watcher ??= new SettingsRevisionWatcher(
        scopes, time, services.GetService<IConfiguration>()?[SettingsRevisionWatcher.ConfigurationKey], log);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone);

        if (catalog.Profiles.Count == 0)
        {
            log.LogWarning("No profiles in Matching:Profiles (see appsettings.Local.example.json); nothing to do");
            await IdleAsync(Timeout.InfiniteTimeSpan, stoppingToken);
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
            if (await IdleAsync(next - time.GetUtcNow(), stoppingToken))
                return;
            await pipeline.RunAsync(fullHistory: false, stoppingToken);
        }
    }

    /// <summary>
    /// Waits for <paramref name="max"/> in slices, checking the stored settings. True when they changed and the host
    /// was asked to stop; false when the time ran out (or the worker is stopping).
    /// </summary>
    private async Task<bool> IdleAsync(TimeSpan max, CancellationToken ct)
    {
        try
        {
            if (!await Watcher.WaitForChangeAsync(max, ct))
                return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        log.LogInformation("Settings changed in the database; restarting to load them");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        return true;
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
