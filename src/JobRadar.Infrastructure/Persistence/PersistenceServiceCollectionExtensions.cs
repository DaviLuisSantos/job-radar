using JobRadar.Application.Abstractions;
using JobRadar.Infrastructure.Persistence.Collection;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Market;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Persistence.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "JobRadar";

    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Missing connection string '{ConnectionStringName}' (ConnectionStrings:{ConnectionStringName}).");
        var provider = DatabaseProviders.Parse(configuration[DatabaseProviders.ConfigurationKey]);

        services.AddDbContext<JobRadarDbContext>(o => o
            .UseJobRadarDatabase(provider, connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IListingRepository, EfListingRepository>();
        services.AddScoped<IListingDescriptionRepository, EfListingDescriptionRepository>();
        services.AddScoped<ICollectionRunRepository, EfCollectionRunRepository>();
        services.AddScoped<IListingScoreRepository, EfListingScoreRepository>();
        services.AddScoped<IListingTermRepository, EfListingTermRepository>();
        services.AddScoped<ICandidateRequirementRepository, EfCandidateRequirementRepository>();
        services.AddScoped<ITrackingRepository, EfTrackingRepository>();
        services.AddScoped<IMarketVisitRepository, EfMarketVisitRepository>();
        services.AddScoped<IMarketRepository, EfMarketRepository>();
        services.AddScoped<IStorageHealth, PostgresStorageHealth>();
        return services;
    }
}
