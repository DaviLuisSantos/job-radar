using JobRadar.Infrastructure.Postgres.Collection;
using JobRadar.Infrastructure.Postgres.Listings;
using JobRadar.Infrastructure.Postgres.Market;
using JobRadar.Infrastructure.Postgres.Matching;
using JobRadar.Infrastructure.Postgres.Tracking;
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
    public DbSet<ListingSearchHitEntity> ListingSearchHits => Set<ListingSearchHitEntity>();
    public DbSet<CollectionRunEntity> CollectionRuns => Set<CollectionRunEntity>();
    public DbSet<ListingScoreEntity> ListingScores => Set<ListingScoreEntity>();
    public DbSet<ListingTermEntity> ListingTerms => Set<ListingTermEntity>();
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<ApplicationEventEntity> ApplicationEvents => Set<ApplicationEventEntity>();
    public DbSet<ListingLabelEntity> ListingLabels => Set<ListingLabelEntity>();
    public DbSet<MarketSnapshotEntity> MarketSnapshots => Set<MarketSnapshotEntity>();
    public DbSet<MarketCountEntity> MarketCounts => Set<MarketCountEntity>();
    public DbSet<MarketListingEntity> MarketListings => Set<MarketListingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobRadarDbContext).Assembly);
}
