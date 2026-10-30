using JobRadar.Domain.Tracking;
using JobRadar.Infrastructure.Persistence.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Tracking;

/// <summary>Row of <c>applications</c>: the current state of the candidate's application to a listing.</summary>
public class ApplicationEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset? AppliedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Row of <c>application_events</c>: one status change. Append-only.</summary>
public class ApplicationEventEntity
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

/// <summary>Row of <c>listing_labels</c>.</summary>
public class ListingLabelEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Verdict { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset LabeledAt { get; set; }
}

public class ApplicationEntityConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> b)
    {
        b.ToTable("applications");
        b.HasKey(e => new { e.Source, e.SourceId });
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(e => e.AppliedAt).HasColumnName("applied_at");
        b.Property(e => e.Notes).HasColumnName("notes");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(e => e.Status).HasDatabaseName("ix_applications_status");
        b.HasOne<ListingEntity>().WithMany().HasForeignKey(e => new { e.Source, e.SourceId }).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationEventEntityConfiguration : IEntityTypeConfiguration<ApplicationEventEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEventEntity> b)
    {
        b.ToTable("application_events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id");
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.FromStatus).HasColumnName("from_status").HasMaxLength(16);
        b.Property(e => e.ToStatus).HasColumnName("to_status").HasMaxLength(16);
        b.Property(e => e.At).HasColumnName("at");
        b.HasIndex(e => new { e.Source, e.SourceId, e.At }).HasDatabaseName("ix_application_events_listing");
        b.HasOne<ApplicationEntity>().WithMany().HasForeignKey(e => new { e.Source, e.SourceId }).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ListingLabelEntityConfiguration : IEntityTypeConfiguration<ListingLabelEntity>
{
    public void Configure(EntityTypeBuilder<ListingLabelEntity> b)
    {
        b.ToTable("listing_labels");
        b.HasKey(e => new { e.Source, e.SourceId });
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.Verdict).HasColumnName("verdict").HasMaxLength(8);
        b.Property(e => e.Note).HasColumnName("note");
        b.Property(e => e.LabeledAt).HasColumnName("labeled_at");
        b.HasOne<ListingEntity>().WithMany().HasForeignKey(e => new { e.Source, e.SourceId }).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Spelling of statuses and verdicts in the database.</summary>
internal static class TrackingMapper
{
    internal static string ToColumn(ApplicationStatus s) => s switch
    {
        ApplicationStatus.Reviewed => "reviewed",
        ApplicationStatus.Dismissed => "dismissed",
        ApplicationStatus.Applied => "applied",
        ApplicationStatus.Interview => "interview",
        ApplicationStatus.Offer => "offer",
        ApplicationStatus.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null),
    };

    internal static ApplicationStatus ToStatus(string column) => column switch
    {
        "reviewed" => ApplicationStatus.Reviewed,
        "dismissed" => ApplicationStatus.Dismissed,
        "applied" => ApplicationStatus.Applied,
        "interview" => ApplicationStatus.Interview,
        "offer" => ApplicationStatus.Offer,
        "declined" => ApplicationStatus.Declined,
        _ => throw new InvalidOperationException($"Unknown application status '{column}' in the database."),
    };

    internal static string ToColumn(Verdict v) => v == Verdict.Good ? "good" : "bad";

    internal static Verdict ToVerdict(string column) => column switch
    {
        "good" => Verdict.Good,
        "bad" => Verdict.Bad,
        _ => throw new InvalidOperationException($"Unknown verdict '{column}' in the database."),
    };
}
