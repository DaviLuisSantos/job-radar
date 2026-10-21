using JobRadar.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Integrations.Prototype;

public static class PrototypeServiceCollectionExtensions
{
    public static IServiceCollection AddPrototypeImport(this IServiceCollection services)
    {
        services.AddTransient<IPrototypeArchive, PrototypeArchive>();
        return services;
    }
}
