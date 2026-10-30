using JobRadar.Application;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Telemetry;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace JobRadar.Web;

/// <summary>
/// Composition root of the triage site: the application services and the database, without the
/// collection pipeline (that is the worker's job). Tests build the container through this method.
/// </summary>
public static class JobRadarWebComposition
{
    public static IServiceCollection AddJobRadarWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplication(configuration);
        services.AddPersistence(configuration);
        services.AddJobRadarTelemetry(configuration, "jobradar-web",
            tracing: t => t.AddAspNetCoreInstrumentation(), metrics: m => m.AddAspNetCoreInstrumentation());
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddMudServices();
        services.AddSingleton<Backend>();
        return services;
    }
}
