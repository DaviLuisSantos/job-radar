using System.Collections;
using System.Text.Json;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace JobRadar.Infrastructure.Persistence.Settings;

/// <summary>
/// The stored sections as configuration. The connection string and provider come from the sources that exist
/// when the configuration is built (files, environment, command line): this source cannot read itself.
/// </summary>
internal sealed class DatabaseConfigurationSource(string[]? args = null) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var (provider, connectionString) = ReadConnection(builder);
        return new DatabaseConfigurationProvider(() =>
            string.IsNullOrWhiteSpace(connectionString) ? DatabaseSettings.Expand([]) : DatabaseSettings.Read(provider, connectionString));
    }

    private (DatabaseProvider Provider, string? ConnectionString) ReadConnection(IConfigurationBuilder builder)
    {
        // A temporary configuration: the sources added so far (JSON), then the environment and the command line,
        // which a ConfigurationManager has not added yet when this source is built.
        var temporary = new ConfigurationBuilder();
        foreach (var (key, value) in builder.Properties)
            temporary.Properties[key] = value;
        foreach (var source in builder.Sources.Where(s => !ReferenceEquals(s, this)))
            temporary.Add(source);
        temporary.AddInMemoryCollection(DatabaseSettings.FromEnvironment(Environment.GetEnvironmentVariables()));
        temporary.AddInMemoryCollection(DatabaseSettings.FromArguments(args ?? []));

        var root = temporary.Build();
        try
        {
            return (DatabaseProviders.Parse(root[DatabaseProviders.ConfigurationKey]), root[DatabaseSettings.ConnectionKey]);
        }
        finally
        {
            (root as IDisposable)?.Dispose();
        }
    }
}

internal sealed class DatabaseConfigurationProvider(Func<Dictionary<string, string?>> read) : ConfigurationProvider
{
    public override void Load() => Data = read();
}

internal static class DatabaseSettings
{
    public const string ConnectionKey = "ConnectionStrings:" + PersistenceServiceCollectionExtensions.ConnectionStringName;

    private static readonly string[] ConnectionKeys = [ConnectionKey, DatabaseProviders.ConfigurationKey];

    /// <summary>Rows to configuration keys, plus the revision the process loaded. Pure.</summary>
    public static Dictionary<string, string?> Expand(IReadOnlyList<Setting> rows)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            try
            {
                foreach (var (key, value) in SettingsJson.Flatten(SettingsJson.Nest(row.Key, row.Json)))
                    data[key] = value;
            }
            catch (JsonException)
            {
                // The API validates before saving; a row someone edited by hand to garbage must not stop the process
                // from starting (the fix is a PUT through the API), so it counts as absent.
            }
        }
        data[SettingsRevisionWatcher.ConfigurationKey] = EfSettingsRepository.Revision(rows.Select(r => r.UpdatedAt).ToList());
        return data;
    }

    /// <summary>The expanded rows, or none when the database or the table is not there yet (before the first migration).</summary>
    public static Dictionary<string, string?> Read(DatabaseProvider provider, string connectionString)
    {
        var options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseJobRadarDatabase(provider, connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        try
        {
            using var db = new JobRadarDbContext(options);
            return Expand(db.Settings.OrderBy(s => s.Key).ToList().Select(EfSettingsRepository.ToDomain).ToList());
        }
        catch (Exception e) when (IsMissingSchema(e))
        {
            return Expand([]);
        }
    }

    /// <summary>Postgres: undefined_table 42P01, invalid_catalog_name 3D000. SQL Server: invalid object 208, cannot open database 4060.</summary>
    public static bool IsMissingSchema(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: "42P01" or "3D000" })
                return true;
            if (e is SqlException sql && sql.Errors.Cast<SqlError>().Any(x => x.Number is 208 or 4060))
                return true;
        }
        return false;
    }

    /// <summary>Environment variables use <c>__</c> where configuration keys use <c>:</c>; only the two keys that matter.</summary>
    public static Dictionary<string, string?> FromEnvironment(IDictionary variables)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in variables)
        {
            var key = ((string)entry.Key).Replace("__", ":", StringComparison.Ordinal);
            if (ConnectionKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                result[key] = entry.Value as string;
        }
        return result;
    }

    /// <summary><c>--Key=value</c>, <c>--Key value</c> and <c>/Key=value</c>, like the command-line configuration source.</summary>
    public static Dictionary<string, string?> FromArguments(IReadOnlyList<string> args)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var prefix = arg.StartsWith("--", StringComparison.Ordinal) ? 2 : arg.StartsWith('/') ? 1 : 0;
            if (prefix == 0)
                continue;
            var body = arg[prefix..];
            string key;
            string? value;
            var eq = body.IndexOf('=', StringComparison.Ordinal);
            if (eq >= 0)
            {
                key = body[..eq];
                value = body[(eq + 1)..];
            }
            else if (prefix == 2 && i + 1 < args.Count)
            {
                key = body;
                value = args[++i];
            }
            else
                continue;
            if (ConnectionKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                result[key] = value;
        }
        return result;
    }
}
