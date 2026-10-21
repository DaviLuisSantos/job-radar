using JobRadar.Application.Collection;
using JobRadar.Domain.Listings;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker;

/// <summary>
/// Runs the collection once a day at <see cref="CollectionOptions.DailyAt"/>. A failed pass is
/// logged and the job waits for the next day: it never stops the worker.
/// </summary>
internal sealed class DailyCollectionJob(
    IServiceScopeFactory scopes,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    ILogger<DailyCollectionJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone);
        var searches = o.Searches.Select(s => new ListingSearch(s.Keywords, s.Location)).ToList();

        if (searches.Count == 0)
        {
            log.LogWarning("No searches configured in {Section}:Searches; nothing to collect", CollectionOptions.Section);
            return;
        }

        if (o.RunOnStartup)
            await RunAsync(searches, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = DailySchedule.NextAfter(time.GetUtcNow(), o.DailyAt, zone);
            log.LogInformation("Next collection at {Next} ({Zone})", TimeZoneInfo.ConvertTime(next, zone), o.TimeZone);
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await RunAsync(searches, stoppingToken);
        }
    }

    private async Task RunAsync(IReadOnlyList<ListingSearch> searches, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ListingCollector>().CollectAsync(searches, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Collection pass failed; retrying at the next scheduled time");
        }
    }
}
