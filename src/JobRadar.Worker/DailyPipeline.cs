using JobRadar.Application.Collection;
using JobRadar.Application.Market;
using JobRadar.Application.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker;

/// <summary>
/// One pass: collect every profile's searches, mark the full-text terms of what was collected, fetch
/// the full text of the listings that matter, score what each profile found, and take the day's
/// market snapshot (Raio-X). Each stage is isolated: a
/// failed stage does not stop the next one. The scheduled job and the <c>collect</c> command run this
/// same pass.
/// </summary>
internal sealed class DailyPipeline(
    IServiceScopeFactory scopes,
    ProfileCatalog catalog,
    IOptions<CollectionOptions> options,
    TimeProvider time,
    ILogger<DailyPipeline> log)
{
    /// <param name="fullHistory">Ignore the incremental windows and fetch everything the source still has.</param>
    /// <returns>Names of the stages that failed.</returns>
    public async Task<IReadOnlyList<string>> RunAsync(bool fullHistory, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var failed = new List<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        await StageAsync("Collection", sp => sp.GetRequiredService<ListingCollector>().CollectAsync(catalog.Searches, fullHistory, ct), failed, ct);
        await StageAsync("Full-text terms", sp => sp.GetRequiredService<FullTextTermService>().TagAsync(ct), failed, ct);
        await StageAsync("Full descriptions", sp => sp.GetRequiredService<DescriptionFetchService>().FetchAsync(ct), failed, ct);
        await StageAsync("Scoring", sp => sp.GetRequiredService<ListingScoringService>().ScoreAllAsync(zone, ct), failed, ct);
        await StageAsync("Market snapshot", sp => sp.GetRequiredService<MarketSnapshotService>().TakeAsync(Today(zone), ct), failed, ct);

        log.LogInformation("Pass finished in {Seconds:N1} s{Failed}", watch.Elapsed.TotalSeconds,
            failed.Count == 0 ? "" : $"; failed: {string.Join(", ", failed)}");
        return failed;
    }

    private DateOnly Today(TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);

    private async Task StageAsync(string stage, Func<IServiceProvider, Task> work, List<string> failed, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await work(scope.ServiceProvider);
            log.LogInformation("{Stage} took {Seconds:N1} s", stage, watch.Elapsed.TotalSeconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "{Stage} failed; retrying at the next run", stage);
            failed.Add(stage);
        }
    }
}
