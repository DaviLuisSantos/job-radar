using JobRadar.Infrastructure.Persistence.Collection;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Persistence.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>
/// The only mappings that differ by database. Everything else is in the entity configurations, the same for
/// every provider; a difference goes here, never as an <c>if</c> in a configuration.
/// </summary>
internal static class ProviderModel
{
    /// <summary>
    /// Case- and accent-sensitive, like Postgres. The default (<c>*_CI_AS</c>) ignores both, so keys that differ
    /// only in case would collide and a search would match what Postgres does not. UTF-8 so text is stored as
    /// on Postgres.
    /// </summary>
    public const string SqlServerCollation = "Latin1_General_100_CS_AS_SC_UTF8";

    public static void Apply(ModelBuilder modelBuilder, DatabaseFacade database)
    {
        if (database.IsNpgsql())
        {
            modelBuilder.Entity<ListingPayloadEntity>().Property(e => e.Payload).HasColumnType("jsonb");
            modelBuilder.Entity<ListingScoreEntity>().Property(e => e.Explanation).HasColumnType("jsonb");
            modelBuilder.Entity<CollectionRunEntity>().Property(e => e.Id).UseIdentityAlwaysColumn();
            modelBuilder.Entity<ApplicationEventEntity>().Property(e => e.Id).UseIdentityAlwaysColumn();
        }
        else if (database.IsSqlServer())
        {
            // JSON text lands in nvarchar(max) and ids in IDENTITY columns by convention.
            modelBuilder.UseCollation(SqlServerCollation);
        }
    }
}
