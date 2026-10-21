using JobRadar.Application.Abstractions;
using JobRadar.Infrastructure.Postgres.Collection;
using JobRadar.Infrastructure.Postgres.Listings;
using JobRadar.Infrastructure.Postgres.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Infrastructure.Postgres;

public static class PostgresServiceCollectionExtensions
{
    public const string ConnectionStringName = "JobRadar";

    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Missing connection string '{ConnectionStringName}' (ConnectionStrings:{ConnectionStringName}).");

        services.AddDbContext<JobRadarDbContext>(o => o
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IListingRepository, PostgresListingRepository>();
        services.AddScoped<ICollectionRunRepository, PostgresCollectionRunRepository>();
        services.AddScoped<IListingScoreRepository, PostgresListingScoreRepository>();
        services.AddScoped<IListingTermRepository, PostgresListingTermRepository>();
        return services;
    }
}
