using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Listings;

public class ListingEntityConfiguration : IEntityTypeConfiguration<ListingEntity>
{
    public void Configure(EntityTypeBuilder<ListingEntity> b)
    {
        b.ToTable("listings");
        b.HasKey(e => new { e.Source, e.SourceId });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Title).HasColumnName("title").HasMaxLength(300);
        b.Property(e => e.Company).HasColumnName("company").HasMaxLength(300);
        b.Property(e => e.Suburb).HasColumnName("suburb").HasMaxLength(120);
        b.Property(e => e.Region).HasColumnName("region").HasMaxLength(120);
        b.Property(e => e.State).HasColumnName("state").HasMaxLength(3);
        b.Property(e => e.Postcode).HasColumnName("postcode").HasMaxLength(4);
        b.Property(e => e.WorkType).HasColumnName("work_type").HasMaxLength(16);
        b.Property(e => e.SalaryText).HasColumnName("salary_text").HasMaxLength(300);
        b.Property(e => e.PostedAt).HasColumnName("posted_at");
        b.Property(e => e.Url).HasColumnName("url").HasMaxLength(500);
        b.Property(e => e.SourceCategory).HasColumnName("source_category").HasMaxLength(200);
        b.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");
        b.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");

        b.HasIndex(e => e.FirstSeenAt).HasDatabaseName("ix_listings_first_seen_at");
    }
}
