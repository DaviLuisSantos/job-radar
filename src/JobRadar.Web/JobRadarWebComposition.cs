using JobRadar.Application;
using JobRadar.Infrastructure.Postgres;

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
        services.AddPostgresInfrastructure(configuration);
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddSingleton<Backend>();
        return services;
    }
}
