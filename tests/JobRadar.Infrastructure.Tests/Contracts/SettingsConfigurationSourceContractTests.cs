using JobRadar.Application.Settings;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Persistence.Settings;
using JobRadar.Infrastructure.Tests.Databases;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace JobRadar.Infrastructure.Tests.Contracts;

/// <summary>The database as a configuration source, against a real database of each kind.</summary>
public abstract class SettingsConfigurationSourceContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset T = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(10));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ConnectionString()
    {
        using var ctx = db.NewContext();
        return ctx.Database.GetConnectionString()!;
    }

    private IConfigurationRoot Configuration(string connectionString, Dictionary<string, string?>? after = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:JobRadar"] = connectionString,
                ["Database:Provider"] = db.Provider.ToString(),
                ["Market:Terms:Limit"] = "1",
                ["Market:Terms:FromFile"] = "kept",
            })
            .AddJobRadarDatabaseSettings()
            .AddInMemoryCollection(after ?? [])
            .Build();

    [DatabaseFact]
    public async Task Stored_sections_override_the_files_and_the_later_sources_override_them()
    {
        await db.ResetAsync();
        await using (var ctx = db.NewContext())
        {
            var repo = new EfSettingsRepository(ctx);
            await repo.SaveAsync("Market:Terms", """{"Limit":5,"Words":["a","b"]}""", T, Ct);
            await repo.SaveAsync("Matching:Profiles", """{"dev":{"Market":"AU"}}""", T.AddMinutes(1), Ct);
        }

        var configuration = Configuration(ConnectionString(), new() { ["Market:Terms:Words:1"] = "env" });

        Assert.Equal("5", configuration["Market:Terms:Limit"]);
        Assert.Equal("kept", configuration["Market:Terms:FromFile"]);
        Assert.Equal("a", configuration["Market:Terms:Words:0"]);
        Assert.Equal("env", configuration["Market:Terms:Words:1"]);
        Assert.Equal("AU", configuration["Matching:Profiles:dev:Market"]);
        Assert.Equal($"2|{T.AddMinutes(1).UtcDateTime:O}", configuration[SettingsRevisionWatcher.ConfigurationKey]);
    }

    [DatabaseFact]
    public async Task An_empty_table_leaves_the_files_alone_and_declares_the_empty_revision()
    {
        await db.ResetAsync();

        var configuration = Configuration(ConnectionString());

        Assert.Equal("1", configuration["Market:Terms:Limit"]);
        Assert.Equal("0|", configuration[SettingsRevisionWatcher.ConfigurationKey]);
    }

    [DatabaseFact]
    public void A_database_that_does_not_exist_yet_gives_an_empty_source()
    {
        var absent = db.Provider == DatabaseProvider.Postgres
            ? new NpgsqlConnectionStringBuilder(ConnectionString()) { Database = "jobradar_absent" }.ConnectionString
            : new SqlConnectionStringBuilder(ConnectionString()) { InitialCatalog = "jobradar_absent" }.ConnectionString;

        var configuration = Configuration(absent);

        Assert.Equal("1", configuration["Market:Terms:Limit"]);
        Assert.Equal("0|", configuration[SettingsRevisionWatcher.ConfigurationKey]);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresSettingsConfigurationSourceContractTests(PostgresFixture db) : SettingsConfigurationSourceContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerSettingsConfigurationSourceContractTests(SqlServerFixture db) : SettingsConfigurationSourceContractTests(db);
