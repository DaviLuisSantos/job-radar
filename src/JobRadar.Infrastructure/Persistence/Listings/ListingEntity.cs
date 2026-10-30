namespace JobRadar.Infrastructure.Persistence.Listings;

/// <summary>Row of <c>listings</c>, column by column. Only the mapper turns it into domain.</summary>
public class ListingEntity
{
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string Country { get; set; } = "";
    public string? Suburb { get; set; }
    public string? Region { get; set; }
    public string? State { get; set; }
    public string? Postcode { get; set; }
    public string? WorkType { get; set; }
    public string? SalaryText { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public string Url { get; set; } = "";
    public string? SourceCategory { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
