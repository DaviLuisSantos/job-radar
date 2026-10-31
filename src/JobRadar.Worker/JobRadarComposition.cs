using JobRadar.Application;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Telemetry;
using JobRadar.Integrations.Ats;
using JobRadar.Integrations.Gupy;
using JobRadar.Integrations.Himalayas;
using JobRadar.Integrations.Jobicy;
using JobRadar.Integrations.LinkedIn;
using JobRadar.Integrations.Prototype;
using JobRadar.Integrations.Seek;

namespace JobRadar.Worker;

/// <summary>
/// Composition root: the only place that knows every piece. Tests build the container
/// through this same method, so a missing registration fails a test, not production.
/// </summary>
public static class JobRadarComposition
{
    public static IServiceCollection AddJobRadar(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplication(configuration);
        services.AddPersistence(configuration);
        services.AddJobRadarTelemetry(configuration, "jobradar-worker");
        services.AddAtsIntegration(configuration);
        services.AddSeekIntegration(configuration);
        services.AddGupyIntegration(configuration);
        services.AddHimalayasIntegration(configuration);
        services.AddJobicyIntegration(configuration);
        services.AddLinkedInIntegration(configuration);
        // One-off import command: the archive reader and the service that uses it live only in the worker.
        services.AddPrototypeImport();
        services.AddScoped<JobRadar.Application.Import.PrototypeImportService>();
        services.AddSingleton<DailyPipeline>();
        services.AddHostedService<DailyPipelineJob>();
        return services;
    }
}
