using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobRadar.Application;

/// <summary>
/// The traces and metrics of the daily pass, under one name. Emitted with what .NET has built in; the worker and the
/// site decide whether anything listens (<c>Telemetry:OtlpEndpoint</c>). Tags carry the source, the country and the
/// stage, never a title, a company or a candidate's data.
/// </summary>
public static class JobRadarTelemetry
{
    public const string Name = "JobRadar";

    public static readonly ActivitySource Source = new(Name);

    public static readonly Meter Meter = new(Name);

    /// <summary>Listings a search returned, new or known.</summary>
    public static readonly Counter<long> JobsCollected = Meter.CreateCounter<long>(
        "jobradar.jobs.collected", unit: "{listing}", description: "Listings the searches returned");

    public static readonly Counter<long> JobsNew = Meter.CreateCounter<long>(
        "jobradar.jobs.new", unit: "{listing}", description: "Listings seen for the first time");

    /// <summary>Returned again: already in the database, from an earlier pass or another search.</summary>
    public static readonly Counter<long> JobsKnown = Meter.CreateCounter<long>(
        "jobradar.jobs.known", unit: "{listing}", description: "Listings already known when a search returned them");

    public static readonly Counter<long> SourceErrors = Meter.CreateCounter<long>(
        "jobradar.source.errors", unit: "{error}", description: "Searches that failed, by source and error type");

    public static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>(
        "jobradar.stage.duration", unit: "s", description: "Time each stage of the daily pass took");

    public static readonly Histogram<double> RunDuration = Meter.CreateHistogram<double>(
        "jobradar.run.duration", unit: "s", description: "Time a whole daily pass took");
}
