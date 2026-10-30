using JobRadar.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace JobRadar.Infrastructure.Tests.Databases;

/// <summary>
/// One container for every contract test class. Classes in a collection run one at a time,
/// so a test never sees tables another class is truncating.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

/// <summary>Real Postgres, same major version as docker-compose.yml.</summary>
public sealed class PostgresFixture() : DatabaseFixture(DatabaseProvider.Postgres)
{
    private PostgreSqlContainer? _container;

    protected override async Task<string> StartAsync()
    {
        _container = new PostgreSqlBuilder("postgres:18").Build();
        await _container.StartAsync();
        return _container.GetConnectionString();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
