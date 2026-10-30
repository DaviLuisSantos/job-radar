using JobRadar.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace JobRadar.Infrastructure.Tests.Databases;

/// <summary>Same contract tests as <see cref="PostgresCollection"/>, against SQL Server.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>Real SQL Server 2022, same image as the sqlserver service of docker-compose.yml.</summary>
public sealed class SqlServerFixture() : DatabaseFixture(DatabaseProvider.SqlServer)
{
    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;

    protected override async Task<string> StartAsync()
    {
        _container = new MsSqlBuilder(Image).Build();
        await _container.StartAsync();
        // The container's connection string points at master. A database the migrations create gets the model's
        // collation; an existing one keeps its own.
        return new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "jobradar" }.ConnectionString;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
