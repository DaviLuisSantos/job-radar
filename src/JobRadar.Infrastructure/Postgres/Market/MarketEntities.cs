using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Market;

/// <summary>Row of <c>market_snapshots</c>: one per source and day, with its quality status.</summary>
public class MarketSnapshotEntity
{
    public string Source { get; set; } = "";
    public DateOnly SnapshotDate { get; set; }
    public string Status { get; set; } = "";
    public int Expected { get; set; }
    public int Saved { get; set; }
    public int Listings { get; set; }
    public string? Issues { get; set; }
    public DateTimeOffset TakenAt { get; set; }
}

/// <summary>Row of <c>market_counts</c>. <see cref="Term"/> is "" for the total of open listings.</summary>
public class MarketCountEntity
{
    public string Source { get; set; } = "";
    public DateOnly SnapshotDate { get; set; }
    public string Category { get; set; } = "";
    public string City { get; set; } = "";
    public string Term { get; set; } = "";
    public int Total { get; set; }
}

/// <summary>Row of <c>market_listings</c>: a listing open in a category and city between two dates.</summary>
public class MarketListingEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Category { get; set; } = "";
    public string City { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? WorkType { get; set; }
    public string? SalaryText { get; set; }
    public string? SalaryPeriod { get; set; }
    public int? SalaryAnnualMin { get; set; }
    public int? SalaryAnnualMax { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public DateOnly FirstSeen { get; set; }
    public DateOnly LastSeen { get; set; }
}

public class MarketSnapshotEntityConfiguration : IEntityTypeConfiguration<MarketSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<MarketSnapshotEntity> b)
    {
        b.ToTable("market_snapshots");
        b.HasKey(e => new { e.Source, e.SnapshotDate });
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SnapshotDate).HasColumnName("snapshot_date");
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(e => e.Expected).HasColumnName("expected");
        b.Property(e => e.Saved).HasColumnName("saved");
        b.Property(e => e.Listings).HasColumnName("listings");
        b.Property(e => e.Issues).HasColumnName("issues");
        b.Property(e => e.TakenAt).HasColumnName("taken_at");
    }
}

public class MarketCountEntityConfiguration : IEntityTypeConfiguration<MarketCountEntity>
{
    public void Configure(EntityTypeBuilder<MarketCountEntity> b)
    {
        b.ToTable("market_counts");
        b.HasKey(e => new { e.Source, e.SnapshotDate, e.Category, e.City, e.Term });
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SnapshotDate).HasColumnName("snapshot_date");
        b.Property(e => e.Category).HasColumnName("category").HasMaxLength(64);
        b.Property(e => e.City).HasColumnName("city").HasMaxLength(64);
        b.Property(e => e.Term).HasColumnName("term").HasMaxLength(64);
        b.Property(e => e.Total).HasColumnName("total");
        b.HasOne<MarketSnapshotEntity>().WithMany()
            .HasForeignKey(e => new { e.Source, e.SnapshotDate })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MarketListingEntityConfiguration : IEntityTypeConfiguration<MarketListingEntity>
{
    public void Configure(EntityTypeBuilder<MarketListingEntity> b)
    {
        b.ToTable("market_listings");
        b.HasKey(e => new { e.Source, e.SourceId, e.Category, e.City });
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Category).HasColumnName("category").HasMaxLength(64);
        b.Property(e => e.City).HasColumnName("city").HasMaxLength(64);
        b.Property(e => e.Title).HasColumnName("title").HasMaxLength(300);
        b.Property(e => e.Company).HasColumnName("company").HasMaxLength(300);
        b.Property(e => e.Location).HasColumnName("location").HasMaxLength(200);
        b.Property(e => e.WorkType).HasColumnName("work_type").HasMaxLength(16);
        b.Property(e => e.SalaryText).HasColumnName("salary_text").HasMaxLength(300);
        b.Property(e => e.SalaryPeriod).HasColumnName("salary_period").HasMaxLength(8);
        b.Property(e => e.SalaryAnnualMin).HasColumnName("salary_annual_min");
        b.Property(e => e.SalaryAnnualMax).HasColumnName("salary_annual_max");
        b.Property(e => e.PostedAt).HasColumnName("posted_at");
        b.Property(e => e.FirstSeen).HasColumnName("first_seen");
        b.Property(e => e.LastSeen).HasColumnName("last_seen");
        b.HasIndex(e => new { e.Source, e.FirstSeen, e.LastSeen }).HasDatabaseName("ix_market_listings_seen");
    }
}
