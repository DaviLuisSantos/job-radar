using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Listings;

/// <summary>
/// Row of <c>listing_payloads</c>: one per distinct payload of a listing. An unchanged ad seen
/// every day stays one row; an edited ad adds a version.
/// </summary>
public class ListingPayloadEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    /// <summary>SHA-256 of the payload text exactly as received, lowercase hex.</summary>
    public string PayloadHash { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; }

    internal static string Hash(string payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}

public class ListingPayloadEntityConfiguration : IEntityTypeConfiguration<ListingPayloadEntity>
{
    public void Configure(EntityTypeBuilder<ListingPayloadEntity> b)
    {
        b.ToTable("listing_payloads");
        b.HasKey(e => new { e.Source, e.SourceId, e.PayloadHash });

        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32);
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(64);
        b.Property(e => e.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsFixedLength();
        // jsonb normalises whitespace and key order; the hash is taken before, on the text received.
        b.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");

        b.HasOne<ListingEntity>().WithMany()
            .HasForeignKey(e => new { e.Source, e.SourceId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
