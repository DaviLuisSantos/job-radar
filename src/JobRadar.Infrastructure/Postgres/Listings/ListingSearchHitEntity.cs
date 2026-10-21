using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Listings;

/// <summary>
/// Row of <c>listing_search_hits</c>: this search found this listing at least once. It decides
/// which profiles score a listing (a profile scores what its own searches found).
/// </summary>
public class ListingSearchHitEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string SearchKey { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; }
}

public class ListingSearchHitEntityConfiguration : IEntityTypeConfiguration<ListingSearchHitEntity>
{
    public void Configure(EntityTypeBuilder<ListingSearchHitEntity> b)
    {
        b.ToTable("listing_search_hits");
        b.HasKey(e => new { e.Source, e.SourceId, e.SearchKey });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.SearchKey).HasColumnName("search_key").HasMaxLength(300);
        b.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");

        b.HasIndex(e => e.SearchKey).HasDatabaseName("ix_listing_search_hits_search_key");
        b.HasOne<ListingEntity>().WithMany()
            .HasForeignKey(e => new { e.Source, e.SourceId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
