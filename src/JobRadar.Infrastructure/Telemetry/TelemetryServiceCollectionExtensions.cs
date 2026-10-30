using JobRadar.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobRadar.Infrastructure.Telemetry;

/// <summary>
/// Sends the pass's traces, metrics and logs (<see cref="JobRadarTelemetry"/>, HTTP calls to the sources, Npgsql) to an
/// OTLP collector, such as the Aspire dashboard of docker-compose.yml. Only when <c>Telemetry:OtlpEndpoint</c> is set:
/// without it nothing is collected or sent, and the logs stay where they were.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    public const string EndpointKey = "Telemetry:OtlpEndpoint";

    /// <param name="serviceName">"jobradar-worker" or "jobradar-web": how the dashboard tells them apart.</param>
    /// <param name="tracing">What the host adds, such as the site's request instrumentation.</param>
    public static IServiceCollection AddJobRadarTelemetry(
        this IServiceCollection services, IConfiguration configuration, string serviceName,
        Action<TracerProviderBuilder>? tracing = null, Action<MeterProviderBuilder>? metrics = null)
    {
        if (configuration[EndpointKey] is not { Length: > 0 } endpoint)
            return services;
        var otlp = new Uri(endpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddSource(JobRadarTelemetry.Name).AddSource("Npgsql").AddHttpClientInstrumentation();
                tracing?.Invoke(t);
                t.AddOtlpExporter(o => o.Endpoint = otlp);
            })
            .WithMetrics(m =>
            {
                m.AddMeter(JobRadarTelemetry.Name).AddHttpClientInstrumentation();
                metrics?.Invoke(m);
                m.AddOtlpExporter(o => o.Endpoint = otlp);
            })
            .WithLogging(l => l.AddOtlpExporter(o => o.Endpoint = otlp), o =>
            {
                // RunId and Stage live in scopes (DailyPipeline): without them a failed search has no pass to point to.
                o.IncludeScopes = true;
                o.IncludeFormattedMessage = true;
            });
        return services;
    }
}
