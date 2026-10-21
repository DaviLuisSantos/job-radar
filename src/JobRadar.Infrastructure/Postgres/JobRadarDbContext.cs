using JobRadar.Infrastructure.Postgres.Collection;
using JobRadar.Infrastructure.Postgres.Listings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Postgres;

/// <summary>
/// The system's own database and source of truth: durable, schema changed only by migrations.
/// Tracking is off by default; writes opt in with <c>AsTracking()</c>.
/// </summary>
public class JobRadarDbContext(DbContextOptions<JobRadarDbContext> options) : DbContext(options)
{
    public DbSet<ListingEntity> Listings => Set<ListingEntity>();
    public DbSet<ListingPayloadEntity> ListingPayloads => Set<ListingPayloadEntity>();
    public DbSet<CollectionRunEntity> CollectionRuns => Set<CollectionRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobRadarDbContext).Assembly);
}
