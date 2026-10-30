using System.Globalization;
using JobRadar.Application.Collection;
using JobRadar.Application.Import;
using JobRadar.Application.Market;
using JobRadar.Application.Matching;
using JobRadar.Application.Tracking;
using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
                case CliCommand.Calibrate c:
                    var calibration = sp.GetRequiredService<CalibrationService>();
                    if (c.Rescore)
                        await output.WriteLineAsync($"Scored again with this build's rules (version {FitScorer.RulesVersion})"
                            + (c.Assume is { } assumed ? ", assuming " + string.Join(", ",
                                assumed.States.Select(a => $"{a.Key}={(a.Value is { } v ? StateWord(v) : "default")}")
                                    .Concat(assumed.Years.Select(a => $"years:{a.Key}={(a.Value is { } y ? y.ToString(CultureInfo.InvariantCulture) : "default")}"))) : "")
                            + "; nothing saved.");
                    WriteCalibration(c.Rescore ? await calibration.MeasureRescoredAsync(LocalZone(sp), ct, c.Assume) : await calibration.MeasureAsync(ct), output);
                    break;
                case CliCommand.ListRequirements:
                    WriteRequirements(await sp.GetRequiredService<CandidateRequirementService>().ListAsync(ct), output);
                    break;
                case CliCommand.SetRequirement c:
                    var (item, rescored) = await sp.GetRequiredService<CandidateRequirementService>().SetAsync(c.Item, c.State, ct);
                    await output.WriteLineAsync($"{item.Id}: {(c.State is { } state ? StateWord(state) : "back to default")}; {rescored:N0} scores updated");
                    break;
                case CliCommand.SetExperience c:
                    var updated = await sp.GetRequiredService<CandidateRequirementService>().SetExperienceAsync(c.Profile, c.Years, ct);
                    await output.WriteLineAsync(
                        $"{c.Profile.Trim().ToLowerInvariant()}: {(c.Years is { } y ? $"{y.ToString(CultureInfo.InvariantCulture)} years of experience" : "years forgotten")}; {updated:N0} scores updated");
                    break;
                case CliCommand.ImportCandidate c:
                    var facts = CandidateFacts.Parse(await File.ReadAllTextAsync(c.Path, ct));
                    WriteCandidateImport(await sp.GetRequiredService<CandidateRequirementService>().ImportAsync(facts, c.DryRun, ct), c.DryRun, output);
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
                case CliCommand.CopyDatabase c:
                    await using (var target = new JobRadarDbContext(new DbContextOptionsBuilder<JobRadarDbContext>()
                                     .UseJobRadarDatabase(c.Provider, c.ConnectionString).Options))
                    {
                        var copied = await DatabaseCopy.CopyAsync(sp.GetRequiredService<JobRadarDbContext>(), target, ct);
                        foreach (var t in copied)
                            await output.WriteLineAsync($"{t.Table,-24}{t.Rows,10:N0}");
                        await output.WriteLineAsync($"Copied {copied.Sum(t => t.Rows):N0} rows to {c.Provider}.");
                    }
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
        catch (TargetNotEmptyException ex)
        {
            await output.WriteLineAsync(ex.Message);
            return 1;
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
        catch (Exception ex) when (ex is UnknownRequirementException or UnknownProfileException or FormatException)
        {
            await output.WriteLineAsync(ex.Message);
            return 2;
        }
    }

    private const string RequirementsTakeEffect =
        "Scores where these items may be asked are updated; the rest of each profile follows in the next daily pass.";

    private static string StateWord(RequirementState state) => state switch
    {
        RequirementState.Have => "have",
        RequirementState.Obtainable => "obtainable",
        RequirementState.Missing => "missing",
        _ => "out-of-reach",
    };

    internal static void WriteRequirements(IReadOnlyList<RequirementStatus> requirements, TextWriter o)
    {
        o.WriteLine($"{"Item",-22} {"Kind",-14} {"State",-22} Profiles");
        foreach (var r in requirements)
        {
            var state = StateWord(r.State) + (r.IsDefault ? " (default)" : "");
            var profiles = r.Profiles.Count > 0 ? string.Join(", ", r.Profiles) : "—";
            o.WriteLine($"{r.Item.Id,-22} {r.Item.Kind,-14} {state,-22} {profiles}");
        }
    }

    internal static void WriteCandidateImport(CandidateImportReport r, bool dryRun, TextWriter o)
    {
        o.WriteLine(r.Changes.Count + r.Experience.Count == 0
            ? "Nothing to change: the states already match the facts."
            : dryRun ? "Dry run: nothing written. Would set:" : "Set:");
        foreach (var c in r.Changes)
            o.WriteLine($"  {c.ItemId,-22} {(c.From is { } f ? StateWord(f) : "default"),-12} -> {StateWord(c.To),-12} ({c.Fact})");
        foreach (var e in r.Experience)
            o.WriteLine($"  {"years: " + e.Profile,-22} {(e.From is { } f ? f.ToString(CultureInfo.InvariantCulture) : "unknown"),-12} -> {e.To.ToString(CultureInfo.InvariantCulture),-12} ({e.Fact})");
        if (r.NotInCatalog.Count > 0)
            o.WriteLine("Not in the requirement catalog (no ad is weighed by them yet): " + string.Join("; ", r.NotInCatalog));
        if (!dryRun && r.Changes.Count + r.Experience.Count > 0)
            o.WriteLine(RequirementsTakeEffect);
    }

    private static TimeZoneInfo LocalZone(IServiceProvider sp) =>
        TimeZoneInfo.FindSystemTimeZoneById(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CollectionOptions>>().Value.TimeZone);

    private static DateOnly LocalToday(IServiceProvider sp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(sp.GetRequiredService<TimeProvider>().GetUtcNow(), LocalZone(sp)).DateTime);

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
        if (r.SnapshotDays.Count > 0)
            o.WriteLine("                  dated " + string.Join(", ", r.SnapshotDays.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) + " (schedule time zone)");
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
