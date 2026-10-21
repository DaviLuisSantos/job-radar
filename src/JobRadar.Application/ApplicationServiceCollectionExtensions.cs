using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobRadar.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services read the clock through TimeProvider so tests can pin "now".
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
