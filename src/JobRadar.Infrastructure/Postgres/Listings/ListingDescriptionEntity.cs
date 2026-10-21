using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Listings;

/// <summary>Row of <c>listing_descriptions</c>: the full ad text the candidate captured, one per listing.</summary>
public class ListingDescriptionEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary><c>browser</c> or <c>pasted</c>.</summary>
    public string Origin { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
}

public class ListingDescriptionEntityConfiguration : IEntityTypeConfiguration<ListingDescriptionEntity>
{
    public void Configure(EntityTypeBuilder<ListingDescriptionEntity> b)
    {
        b.ToTable("listing_descriptions");
        b.HasKey(e => new { e.Source, e.SourceId });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Text).HasColumnName("text");
        b.Property(e => e.Origin).HasColumnName("origin").HasMaxLength(16);
        b.Property(e => e.CapturedAt).HasColumnName("captured_at");

        b.HasOne<ListingEntity>().WithOne()
            .HasForeignKey<ListingDescriptionEntity>(e => new { e.Source, e.SourceId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
