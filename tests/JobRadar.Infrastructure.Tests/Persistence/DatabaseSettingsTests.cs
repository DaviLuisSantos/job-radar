using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using JobRadar.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace JobRadar.Infrastructure.Tests.Persistence;

/// <summary>The expansion of stored sections into configuration keys: pure, no database.</summary>
public sealed class DatabaseSettingsTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(10));

    [Fact]
    public void A_section_becomes_nested_keys_with_array_indexes_and_the_revision_is_added()
    {
        var data = DatabaseSettings.Expand(
        [
            new Setting("Matching:Profiles", """{"dev":{"Market":"AU","Keywords":["dotnet","azure"]}}""", T),
            new Setting("Market:Terms", """{"Enabled":true,"Limit":5}""", T.AddMinutes(5)),
        ]);

        Assert.Equal("AU", data["Matching:Profiles:dev:Market"]);
        Assert.Equal("dotnet", data["Matching:Profiles:dev:Keywords:0"]);
        Assert.Equal("azure", data["Matching:Profiles:dev:Keywords:1"]);
        Assert.Equal("True", data["Market:Terms:Enabled"]);
        Assert.Equal("5", data["Market:Terms:Limit"]);
        Assert.Equal($"2|{T.AddMinutes(5).UtcDateTime:O}", data[SettingsRevisionWatcher.ConfigurationKey]);
    }

    [Fact]
    public void No_rows_still_declare_the_empty_revision()
    {
        var data = DatabaseSettings.Expand([]);

        Assert.Equal("0|", Assert.Single(data).Value);
        Assert.Equal(SettingsRevisionWatcher.ConfigurationKey, Assert.Single(data).Key);
    }

    [Fact]
    public void A_row_with_broken_json_counts_as_absent_instead_of_stopping_the_start()
    {
        var data = DatabaseSettings.Expand(
        [
            new Setting("Matching:Profiles", "{nope", T),
            new Setting("Market:Terms", """{"Limit":5}""", T),
        ]);

        Assert.False(data.ContainsKey("Matching:Profiles"));
        Assert.Equal("5", data["Market:Terms:Limit"]);
        Assert.StartsWith("2|", data[SettingsRevisionWatcher.ConfigurationKey]);
    }

    [Fact]
    public void The_source_without_a_connection_string_is_empty_but_still_carries_the_revision()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Some:Key"] = "x" })
            .AddJobRadarDatabaseSettings()
            .Build();

        Assert.Equal("x", configuration["Some:Key"]);
        Assert.Equal("0|", configuration[SettingsRevisionWatcher.ConfigurationKey]);
    }

    [Fact]
    public void Only_the_connection_and_provider_keys_are_taken_from_the_environment_and_the_arguments()
    {
        var env = DatabaseSettings.FromEnvironment(new Dictionary<string, string>
        {
            ["ConnectionStrings__JobRadar"] = "Host=db",
            ["Database__Provider"] = "SqlServer",
            ["Matching__Profiles"] = "ignored",
            ["PATH"] = "ignored",
        });
        Assert.Equal(2, env.Count);
        Assert.Equal("Host=db", env["ConnectionStrings:JobRadar"]);
        Assert.Equal("SqlServer", env["Database:Provider"]);

        var args = DatabaseSettings.FromArguments(
            ["--ConnectionStrings:JobRadar=Host=arg", "--Database:Provider", "Postgres", "/Other=1", "--Other=2", "free text"]);
        Assert.Equal(2, args.Count);
        Assert.Equal("Host=arg", args["ConnectionStrings:JobRadar"]);
        Assert.Equal("Postgres", args["Database:Provider"]);
    }

    [Fact]
    public void Only_a_missing_table_or_database_counts_as_a_missing_schema()
    {
        static PostgresException Postgres(string state) => new("m", "ERROR", "ERROR", state);

        Assert.True(DatabaseSettings.IsMissingSchema(Postgres("42P01")));
        Assert.True(DatabaseSettings.IsMissingSchema(Postgres("3D000")));
        Assert.True(DatabaseSettings.IsMissingSchema(new InvalidOperationException("wrapped", Postgres("42P01"))));
        Assert.False(DatabaseSettings.IsMissingSchema(Postgres("28P01")));
        Assert.False(DatabaseSettings.IsMissingSchema(new InvalidOperationException("other")));
    }
}
