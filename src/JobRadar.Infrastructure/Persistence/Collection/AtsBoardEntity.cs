using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Collection;

/// <summary>Row of <c>ats_boards</c>: one company's job board on one ATS platform.</summary>
public class AtsBoardEntity
{
    public string Platform { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? ETag { get; set; }
    public int? LastStatus { get; set; }
    public DateTimeOffset? LastSweptAt { get; set; }
    public DateTimeOffset? LastHitAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int JobsTotal { get; set; }
    public int JobsKept { get; set; }
}

public class AtsBoardEntityConfiguration : IEntityTypeConfiguration<AtsBoardEntity>
{
    public void Configure(EntityTypeBuilder<AtsBoardEntity> b)
    {
        b.ToTable("ats_boards");
        b.HasKey(e => new { e.Platform, e.Slug });

        b.Property(e => e.Platform).HasColumnName("platform").HasMaxLength(32);
        b.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(120);
        b.Property(e => e.ETag).HasColumnName("etag").HasMaxLength(200);
        b.Property(e => e.LastStatus).HasColumnName("last_status");
        b.Property(e => e.LastSweptAt).HasColumnName("last_swept_at");
        b.Property(e => e.LastHitAt).HasColumnName("last_hit_at");
        b.Property(e => e.ConsecutiveFailures).HasColumnName("consecutive_failures");
        b.Property(e => e.JobsTotal).HasColumnName("jobs_total");
        b.Property(e => e.JobsKept).HasColumnName("jobs_kept");

        b.HasIndex(e => new { e.Platform, e.LastSweptAt }).HasDatabaseName("ix_ats_boards_sweep");
    }
}
