using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobRadar.Infrastructure.Migrations.SqlServer;

/// <summary>Used only by <c>dotnet ef</c> to build migrations and the bundle; never at runtime.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobRadarDbContext>
{
    // Matches the sqlserver service of docker-compose.yml. Override with JOBRADAR_CONNECTION to target another database.
    private const string LocalDefault =
        "Server=localhost,1433;Database=jobradar;User Id=sa;Password=JobRadar-local-1;TrustServerCertificate=True";

    public JobRadarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JOBRADAR_CONNECTION") ?? LocalDefault;
        var options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseJobRadarDatabase(DatabaseProvider.SqlServer, connectionString).Options;
        return new JobRadarDbContext(options);
    }
}
