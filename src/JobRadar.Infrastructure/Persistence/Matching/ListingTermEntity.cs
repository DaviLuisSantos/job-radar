using JobRadar.Infrastructure.Persistence.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Matching;

/// <summary>Row of <c>listing_terms</c>: this listing's full text contains this term.</summary>
public class ListingTermEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    /// <summary>The term exactly as the scoring policy spells it.</summary>
    public string Term { get; set; } = "";
    public DateTimeOffset FoundAt { get; set; }
}

public class ListingTermEntityConfiguration : IEntityTypeConfiguration<ListingTermEntity>
{
    public void Configure(EntityTypeBuilder<ListingTermEntity> b)
    {
        b.ToTable("listing_terms");
        b.HasKey(e => new { e.Source, e.SourceId, e.Term });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Term).HasColumnName("term").HasMaxLength(200);
        b.Property(e => e.FoundAt).HasColumnName("found_at");

        b.HasOne<ListingEntity>().WithMany()
            .HasForeignKey(e => new { e.Source, e.SourceId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
