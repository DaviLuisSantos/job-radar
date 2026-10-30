using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to build migrations; never at runtime.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobRadarDbContext>
{
    // Matches docker-compose.yml. Override with JOBRADAR_CONNECTION to target another database.
    private const string LocalDefault =
        "Host=localhost;Port=5432;Database=jobradar;Username=jobradar;Password=jobradar";

    public JobRadarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JOBRADAR_CONNECTION") ?? LocalDefault;
        var options = new DbContextOptionsBuilder<JobRadarDbContext>().UseNpgsql(connectionString).Options;
        return new JobRadarDbContext(options);
    }
}
