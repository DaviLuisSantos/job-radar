using System.Runtime.CompilerServices;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Databases;

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
/// A real database in a container, schema built by that provider's real migrations. Each contract test class is
/// abstract and has one subclass per database, so the same tests prove both.
/// </summary>
public abstract class DatabaseFixture(DatabaseProvider provider) : IAsyncLifetime
{
    private string? _connectionString;

    public DatabaseProvider Provider { get; } = provider;

    public async ValueTask InitializeAsync()
    {
        if (!DatabaseFactAttribute.Enabled)
            return;

        _connectionString = await StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>Starts the container; returns the connection string of a database that does not exist yet.</summary>
    protected abstract Task<string> StartAsync();

    public JobRadarDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseJobRadarDatabase(Provider, _connectionString ?? throw new InvalidOperationException("Container not started."))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new JobRadarDbContext(options);
    }

    /// <summary>Every test starts from empty tables. Children first: SQL Server has no TRUNCATE across foreign keys.</summary>
    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.MarketCounts.ExecuteDeleteAsync();
        await db.MarketSnapshots.ExecuteDeleteAsync();
        await db.MarketListings.ExecuteDeleteAsync();
        await db.ApplicationEvents.ExecuteDeleteAsync();
        await db.Applications.ExecuteDeleteAsync();
        await db.ListingLabels.ExecuteDeleteAsync();
        await db.ListingTerms.ExecuteDeleteAsync();
        await db.ListingDescriptions.ExecuteDeleteAsync();
        await db.ListingScores.ExecuteDeleteAsync();
        await db.ListingSearchHits.ExecuteDeleteAsync();
        await db.ListingPayloads.ExecuteDeleteAsync();
        await db.Listings.ExecuteDeleteAsync();
        await db.CollectionRuns.ExecuteDeleteAsync();
        await db.AtsBoards.ExecuteDeleteAsync();
        await db.CandidateRequirements.ExecuteDeleteAsync();
        await db.CandidateExperience.ExecuteDeleteAsync();
        await db.MarketVisits.ExecuteDeleteAsync();
    }

    public abstract ValueTask DisposeAsync();
}
