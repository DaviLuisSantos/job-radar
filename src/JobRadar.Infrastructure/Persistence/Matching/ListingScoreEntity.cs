using JobRadar.Infrastructure.Persistence.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Matching;

/// <summary>Row of <c>listing_scores</c>: one per (listing, profile), replaced on every scoring pass.</summary>
public class ListingScoreEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Profile { get; set; } = "";
    public int Score { get; set; }
    /// <summary>The <c>ScoreItem</c> list as JSON, enums by name.</summary>
    public string Explanation { get; set; } = "[]";
    public string RulesHash { get; set; } = "";
    public DateTimeOffset ScoredAt { get; set; }
}

public class ListingScoreEntityConfiguration : IEntityTypeConfiguration<ListingScoreEntity>
{
    public void Configure(EntityTypeBuilder<ListingScoreEntity> b)
    {
        b.ToTable("listing_scores");
        b.HasKey(e => new { e.Source, e.SourceId, e.Profile });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Profile).HasColumnName("profile").HasMaxLength(64);
        b.Property(e => e.Score).HasColumnName("score");
        b.Property(e => e.Explanation).HasColumnName("explanation").HasColumnType("jsonb");
        b.Property(e => e.RulesHash).HasColumnName("rules_hash").HasMaxLength(16);
        b.Property(e => e.ScoredAt).HasColumnName("scored_at");

        // Triage reads "best listings of a profile".
        b.HasIndex(e => new { e.Profile, e.Score }).HasDatabaseName("ix_listing_scores_profile_score");
        b.HasOne<ListingEntity>().WithMany()
            .HasForeignKey(e => new { e.Source, e.SourceId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
