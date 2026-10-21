using JobRadar.Application.Collection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobRadar.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Services read the clock through TimeProvider so tests can pin "now".
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<CollectionOptions>()
            .Bind(configuration.GetSection(CollectionOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Unknown Collection:TimeZone.")
            .ValidateOnStart();

        services.AddScoped<ListingCollector>();
        return services;
    }
}
