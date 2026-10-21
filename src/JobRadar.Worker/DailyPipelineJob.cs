using JobRadar.Application.Collection;
using JobRadar.Application.Market;
using JobRadar.Application.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker;

/// <summary>
/// Once a day at <see cref="CollectionOptions.DailyAt"/>: collect every profile's searches, mark
/// the full-text terms of what was collected, score what each profile found, and take the day's
/// market snapshot (Raio-X). Each stage is
/// isolated: a failed stage does not stop the next one, and a failed pass waits for the next day.
/// It never stops the worker.
/// </summary>
internal sealed class DailyPipelineJob(
    IServiceScopeFactory scopes,
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

        if (o.RunOnStartup)
            await RunAsync(zone, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = DailySchedule.NextAfter(time.GetUtcNow(), o.DailyAt, zone);
            log.LogInformation("Next run at {Next} ({Zone})", TimeZoneInfo.ConvertTime(next, zone), o.TimeZone);
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await RunAsync(zone, stoppingToken);
        }
    }

    private async Task RunAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        await StageAsync("Collection", sp => sp.GetRequiredService<ListingCollector>().CollectAsync(catalog.Searches, ct), ct);
        await StageAsync("Full-text terms", sp => sp.GetRequiredService<FullTextTermService>().TagAsync(ct), ct);
        await StageAsync("Scoring", sp => sp.GetRequiredService<ListingScoringService>().ScoreAllAsync(zone, ct), ct);
        await StageAsync("Market snapshot", sp => sp.GetRequiredService<MarketSnapshotService>().TakeAsync(Today(zone), ct), ct);
    }

    private DateOnly Today(TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);

    private async Task StageAsync(string stage, Func<IServiceProvider, Task> work, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await work(scope.ServiceProvider);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "{Stage} failed; retrying at the next scheduled run", stage);
        }
    }
}
