using Microsoft.Extensions.Configuration;

namespace JobRadar.Infrastructure.Persistence.Settings;

public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the configuration sections stored in the database as a source. Call it after the JSON files and before
    /// the environment variables and the command line, so the database overrides the files and the environment
    /// still overrides the database. <paramref name="args"/> are the command-line arguments the host passes to its
    /// own command-line source (used only to find the connection string and provider).
    /// Without a connection string, or before the table exists, the source is empty.
    /// </summary>
    public static IConfigurationBuilder AddJobRadarDatabaseSettings(this IConfigurationBuilder builder, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new DatabaseConfigurationSource(args));
    }
}
