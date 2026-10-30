using JobRadar.Infrastructure.Persistence.Collection;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Persistence.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
            // Ids land in IDENTITY columns by convention; JSON in nvarchar(max), which takes any text: the check
            // refuses what jsonb refuses.
            modelBuilder.UseCollation(SqlServerCollation);
            modelBuilder.Entity<ListingPayloadEntity>()
                .ToTable(t => t.HasCheckConstraint("ck_listing_payloads_payload_json", "ISJSON(payload) = 1"));
            modelBuilder.Entity<ListingScoreEntity>()
                .ToTable(t => t.HasCheckConstraint("ck_listing_scores_explanation_json", "ISJSON(explanation) = 1"));

            // The SQL Server provider compares text keys ignoring case in the change tracker, as its default
            // collation would; with the collation above that would merge "Cafe" and "cafe" before the database
            // ever sees them. Ordinal, as on Postgres. Runtime only: comparers are not in the migrations.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(string)))
                property.SetValueComparer(OrdinalComparer);
        }
    }

    private static readonly ValueComparer<string> OrdinalComparer = new(
        (a, b) => string.Equals(a, b, StringComparison.Ordinal),
        v => v == null ? 0 : StringComparer.Ordinal.GetHashCode(v),
        v => v);
}
