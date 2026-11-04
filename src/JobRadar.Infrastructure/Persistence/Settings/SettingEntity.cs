using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Settings;

/// <summary>Row of <c>settings</c>: one configuration section as JSON text.</summary>
public class SettingEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class SettingEntityConfiguration : IEntityTypeConfiguration<SettingEntity>
{
    public void Configure(EntityTypeBuilder<SettingEntity> b)
    {
        b.ToTable("settings");
        b.HasKey(e => e.Key);

        b.Property(e => e.Key).HasColumnName("key").HasMaxLength(200);
        b.Property(e => e.Value).HasColumnName("value");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
