using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>The database behind <see cref="JobRadarDbContext"/>, chosen by <c>Database:Provider</c>.</summary>
public enum DatabaseProvider
{
    Postgres,
    SqlServer,
}

public static class DatabaseProviders
{
    public const string ConfigurationKey = "Database:Provider";

    /// <summary>Each provider has its own migrations: EF Core cannot share them between databases.</summary>
    public static string MigrationsAssembly(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => "JobRadar.Infrastructure.Migrations.Postgres",
        DatabaseProvider.SqlServer => "JobRadar.Infrastructure.Migrations.SqlServer",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    /// <summary>The value of <see cref="ConfigurationKey"/>; none means Postgres, the production database.</summary>
    public static DatabaseProvider Parse(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DatabaseProvider.Postgres
        // By name only: Enum.TryParse would also take "0".
        : Enum.GetValues<DatabaseProvider>().Cast<DatabaseProvider?>()
            .FirstOrDefault(p => p.ToString()!.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Unknown database provider '{value}' ({ConfigurationKey}). Use one of: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");

    /// <summary>The one place the context meets a provider: the site, the worker, the migrations and the tests.</summary>
    public static TBuilder UseJobRadarDatabase<TBuilder>(
        this TBuilder options, DatabaseProvider provider, string connectionString) where TBuilder : DbContextOptionsBuilder
    {
        switch (provider)
        {
            case DatabaseProvider.Postgres:
                options.UseNpgsql(connectionString, o => o.MigrationsAssembly(MigrationsAssembly(provider)));
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, o => o.MigrationsAssembly(MigrationsAssembly(provider)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }
        return options;
    }
}
