using JobRadar.Application;
using JobRadar.Infrastructure.Postgres;
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
        services.AddPostgresInfrastructure(configuration);
        services.AddSeekIntegration(configuration);
        services.AddHostedService<DailyCollectionJob>();
        return services;
    }
}
