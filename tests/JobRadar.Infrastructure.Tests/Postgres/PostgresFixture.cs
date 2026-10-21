using System.Runtime.CompilerServices;
using JobRadar.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace JobRadar.Infrastructure.Tests.Postgres;

/// <summary>Contract tests run only with JOBRADAR_DB_TESTS=1, because they need Docker.</summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "JOBRADAR_DB_TESTS";

    public static bool Enabled => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    public DatabaseFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Enabled)
            Skip = $"Set {EnvironmentVariable}=1 to run (needs Docker).";
    }
}

/// <summary>
/// One container for every contract test class. Classes in a collection run one at a time,
/// so a test never sees tables another class is truncating.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

/// <summary>
/// Real Postgres, same major version as docker-compose.yml, schema built by the real migrations.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public async ValueTask InitializeAsync()
    {
        if (!DatabaseFactAttribute.Enabled)
            return;

        _container = new PostgreSqlBuilder("postgres:18").Build();
        await _container.StartAsync();

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public JobRadarDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(_container?.GetConnectionString()
                ?? throw new InvalidOperationException("Container not started."))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new JobRadarDbContext(options);
    }

    /// <summary>Every test starts from empty tables.</summary>
    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE market_counts, market_snapshots, market_listings, application_events, applications, listing_labels, listing_terms, listing_descriptions, listing_scores, listing_search_hits, listing_payloads, listings, collection_runs");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
