using System.Globalization;
using JobRadar.Application.Collection;
using JobRadar.Application.Import;
using JobRadar.Application.Market;
using JobRadar.Application.Matching;
using JobRadar.Application.Tracking;
using JobRadar.Domain.Matching;

namespace JobRadar.Worker.Cli;

/// <summary>Runs a <see cref="CliCommand"/> against the same services the worker uses. Returns the exit code.</summary>
public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextWriter output, CancellationToken ct = default)
    {
        var command = CliCommand.Parse(args, out var error);
        if (command is null)
        {
            await output.WriteLineAsync(error);
            await output.WriteLineAsync(CliCommand.Usage);
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        try
        {
            switch (command)
            {
                case CliCommand.SetStatus c:
                    var app = await sp.GetRequiredService<TrackingService>().SetStatusAsync(c.Id, c.Status, ct);
                    await output.WriteLineAsync($"{c.Id}: {app.Status} ({app.History.Count} changes)");
                    break;
                case CliCommand.SetNotes c:
                    await sp.GetRequiredService<TrackingService>().SetNotesAsync(c.Id, c.Notes, ct);
                    await output.WriteLineAsync($"{c.Id}: notes saved");
                    break;
                case CliCommand.SetLabel c:
                    await sp.GetRequiredService<TrackingService>().LabelAsync(c.Id, c.Verdict, c.Note, ct);
                    await output.WriteLineAsync($"{c.Id}: {(c.Verdict?.ToString() ?? "label cleared")}");
                    break;
                case CliCommand.Calibrate:
                    WriteCalibration(await sp.GetRequiredService<CalibrationService>().MeasureAsync(ct), output);
                    break;
                case CliCommand.TakeMarketSnapshot:
                    foreach (var s in await sp.GetRequiredService<MarketSnapshotService>().TakeAsync(LocalToday(sp), ct))
                    {
                        await output.WriteLineAsync($"{s.Source} {s.Date:yyyy-MM-dd}: {s.Status}, {s.Saved}/{s.Expected} counts, {s.Listings} listings");
                        foreach (var issue in s.Issues)
                            await output.WriteLineAsync($"  - {issue}");
                    }
                    break;
                case CliCommand.Collect c:
                    var failed = await sp.GetRequiredService<DailyPipeline>().RunAsync(c.FullHistory, ct);
                    await output.WriteLineAsync(failed.Count == 0 ? "Pass finished." : $"Pass finished; failed: {string.Join(", ", failed)}");
                    return failed.Count == 0 ? 0 : 1;
                case CliCommand.ImportPrototype c:
                    WriteImport(await sp.GetRequiredService<PrototypeImportService>().ImportAsync(c.Path, c.DryRun, ct), c.DryRun, output);
                    break;
                case CliCommand.MarketReport c:
                    var month = c.Month ?? LocalToday(sp).ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    var report = await sp.GetRequiredService<MarketReportService>().BuildAsync("seek", month, ct);
                    if (report is null)
                    {
                        await output.WriteLineAsync($"No good market snapshot in {month}. Take one with: market");
                        return 1;
                    }
                    var markdown = RaioXMarkdown.Render(report);
                    if (c.OutputFile is { } file)
                    {
                        await File.WriteAllTextAsync(file, markdown, ct);
                        await output.WriteLineAsync($"Raio-X {month} written to {Path.GetFullPath(file)}");
                    }
                    else
                    {
                        await output.WriteAsync(markdown);
                    }
                    break;
            }
            return 0;
        }
        catch (ListingNotFoundException ex)
        {
            await output.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            await output.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static DateOnly LocalToday(IServiceProvider sp)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CollectionOptions>>().Value.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(sp.GetRequiredService<TimeProvider>().GetUtcNow(), zone).DateTime);
    }

    internal static void WriteImport(ImportReport r, bool dryRun, TextWriter o)
    {
        o.WriteLine(dryRun ? "Dry run: nothing written. In the file:" : "Imported:");
        o.WriteLine(dryRun ? $"  jobs            {r.JobsInFile:N0}" : $"  listings        {r.ListingsNew:N0} new, {r.ListingsKnown:N0} already here (of {r.JobsInFile:N0})");
        o.WriteLine(dryRun ? $"  terms           {r.TermsInFile:N0}" : $"  terms           {r.TermsNew:N0} new (of {r.TermsInFile:N0})");
        o.WriteLine(dryRun ? $"  labels          {r.LabelsInFile:N0}" : $"  labels          {r.LabelsAdded:N0} added, {r.LabelsSkipped:N0} kept as they were (of {r.LabelsInFile:N0})");
        o.WriteLine(dryRun ? $"  applications    {r.ApplicationsInFile:N0}" : $"  applications    {r.ApplicationsAdded:N0} added, {r.ApplicationsSkipped:N0} kept as they were (of {r.ApplicationsInFile:N0})");
        if (r.ApplicationsByStatus.Count > 0)
            o.WriteLine("                  as " + string.Join(", ", r.ApplicationsByStatus.OrderBy(s => s.Key).Select(s => $"{s.Key} {s.Value:N0}")));
        o.WriteLine(dryRun ? $"  market days     {r.SnapshotsInFile:N0}" : $"  market days     {r.SnapshotsAdded:N0} added, {r.SnapshotsSkipped:N0} already here (of {r.SnapshotsInFile:N0})");
        o.WriteLine(dryRun ? $"  market listings {r.MarketListingsInFile:N0}" : $"  market listings {r.MarketListingsMerged:N0} merged");
        if (!dryRun)
            o.WriteLine("Scores of the imported listings are computed in the next daily pass.");
    }

    internal static void WriteCalibration(IReadOnlyList<ProfileCalibration> results, TextWriter o)
    {
        if (results.Count == 0)
        {
            o.WriteLine("No judged listings yet. Label some (label <id> good|bad) or set application statuses.");
            return;
        }

        o.WriteLine($"{"Profile",-14} {"Good",5} {"Bad",5} {"Pairs",7} {$"Good≥{Calibration.AlertThreshold}",9} {$"Bad≥{Calibration.VisibleThreshold}",8}");
        foreach (var r in results)
        {
            var pairs = r.PairAccuracy is { } p ? p.ToString("P0", CultureInfo.InvariantCulture) : "—";
            o.WriteLine($"{r.Profile,-14} {r.Good,5} {r.Bad,5} {pairs,7} {$"{r.GoodAtAlert}/{r.Good}",9} {$"{r.BadVisible}/{r.Bad}",8}");
        }

        var misses = results.SelectMany(r => r.Disagreements).ToList();
        if (misses.Count == 0)
        {
            o.WriteLine($"No disagreements: good listings at {Calibration.AlertThreshold}+, bad ones under {Calibration.VisibleThreshold}.");
            return;
        }

        o.WriteLine();
        o.WriteLine("Where the score disagrees with you (top 3 reasons each):");
        foreach (var m in misses)
        {
            var why = string.Join(", ", m.Score.Items.OrderByDescending(i => Math.Abs(i.Points)).Take(3)
                .Select(i => $"{i.Rule}:{i.Detail} {i.Points:+#;-#;0}"));
            o.WriteLine($"  {m.Verdict,-4} {m.Score.Final,3}  [{m.Profile}] {m.Title} ({m.Id})  {why}");
        }
    }
}
