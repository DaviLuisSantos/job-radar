using JobRadar.Application.Settings;

namespace JobRadar.Web;

/// <summary>
/// Configuration is read at start, so when the stored settings change the site asks its host to stop and the
/// service manager (Docker restart policy, Windows service recovery) starts it again with the new values.
/// </summary>
internal sealed class SettingsRevisionHostedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<SettingsRevisionHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watcher = new SettingsRevisionWatcher(
            scopes, time, configuration[SettingsRevisionWatcher.ConfigurationKey], log);
        try
        {
            if (!await watcher.WaitForChangeAsync(Timeout.InfiniteTimeSpan, stoppingToken))
                return;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        log.LogInformation("Settings changed in the database; restarting to load them");
        lifetime.StopApplication();
    }
}
