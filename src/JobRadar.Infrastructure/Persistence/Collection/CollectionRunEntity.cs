using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Collection;

/// <summary>Row of <c>collection_runs</c>.</summary>
public class CollectionRunEntity
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string SearchKey { get; set; } = "";
    public int? PostedWithinDays { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int Found { get; set; }
    public int New { get; set; }
    public bool Complete { get; set; }
    public string? Error { get; set; }
}

public class CollectionRunEntityConfiguration : IEntityTypeConfiguration<CollectionRunEntity>
{
    public void Configure(EntityTypeBuilder<CollectionRunEntity> b)
    {
        b.ToTable("collection_runs");
        b.HasKey(e => e.Id);

        b.Property(e => e.Id).HasColumnName("id");
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SearchKey).HasColumnName("search_key").HasMaxLength(300);
        b.Property(e => e.PostedWithinDays).HasColumnName("posted_within_days");
        b.Property(e => e.StartedAt).HasColumnName("started_at");
        b.Property(e => e.FinishedAt).HasColumnName("finished_at");
        b.Property(e => e.Found).HasColumnName("found");
        b.Property(e => e.New).HasColumnName("new");
        b.Property(e => e.Complete).HasColumnName("complete");
        b.Property(e => e.Error).HasColumnName("error").HasMaxLength(2000);

        b.HasIndex(e => new { e.Source, e.SearchKey, e.StartedAt }).HasDatabaseName("ix_collection_runs_search");
    }
}
