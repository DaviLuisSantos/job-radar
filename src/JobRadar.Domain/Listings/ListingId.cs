namespace JobRadar.Domain.Listings;

/// <summary>
/// Identity of a listing: the source it came from plus the id that source gives it.
/// The same job on two sites is two listings (grouped later), never one.
/// </summary>
public readonly record struct ListingId
{
    private const char Separator = ':';

    public string Source { get; }
    public string SourceId { get; }

    public ListingId(string source, string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (source.Contains(Separator))
            throw new ArgumentException($"Source must not contain '{Separator}'.", nameof(source));

        Source = source.Trim().ToLowerInvariant();
        SourceId = sourceId.Trim();
    }

    /// <summary>Parses the "source:sourceId" form produced by <see cref="ToString"/>.</summary>
    public static ListingId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var i = value.IndexOf(Separator);
        if (i <= 0 || i == value.Length - 1)
            throw new FormatException($"Expected 'source{Separator}id', got '{value}'.");
        return new ListingId(value[..i], value[(i + 1)..]);
    }

    public override string ToString() => $"{Source}{Separator}{SourceId}";
}
