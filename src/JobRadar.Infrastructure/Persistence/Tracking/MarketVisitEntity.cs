using JobRadar.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Tracking;

/// <summary>Row of <c>market_visits</c>: when the candidate last opened a market's "Today" page.</summary>
public class MarketVisitEntity
{
    public string Market { get; set; } = "";
    public DateTimeOffset VisitedAt { get; set; }
}

public class MarketVisitEntityConfiguration : IEntityTypeConfiguration<MarketVisitEntity>
{
    public void Configure(EntityTypeBuilder<MarketVisitEntity> b)
    {
        b.ToTable("market_visits");
        b.HasKey(e => e.Market);
        b.Property(e => e.Market).HasColumnName("market").HasMaxLength(16);
        b.Property(e => e.VisitedAt).HasColumnName("visited_at");
    }
}

/// <summary>Spelling of markets in the database.</summary>
internal static class MarketVisitMapper
{
    internal static string ToColumn(JobMarket market) => market switch
    {
        JobMarket.AU => "au",
        JobMarket.BR => "br",
        JobMarket.LatamRemote => "latam-remote",
        JobMarket.GlobalRemote => "global-remote",
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, null),
    };

    internal static JobMarket ToMarket(string column) => column switch
    {
        "au" => JobMarket.AU,
        "br" => JobMarket.BR,
        "latam-remote" => JobMarket.LatamRemote,
        "global-remote" => JobMarket.GlobalRemote,
        _ => throw new InvalidOperationException($"Unknown market '{column}' in the database."),
    };
}
