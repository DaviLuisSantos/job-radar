namespace JobRadar.Domain.Listings;

/// <summary>
/// The link of a listing from what a source sends, which is not always a usable address: one Gupy vacancy had a link
/// that does not parse, and an application link can be longer than the column holds. The first candidate that is an
/// absolute http(s) address of at most <see cref="MaxLength"/> characters wins; the last is the board's own page for
/// the job, which always is.
/// </summary>
public static class ListingUrl
{
    /// <summary>The size of <c>listings.url</c>.</summary>
    public const int MaxLength = 500;

    public static Uri Choose(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate?.Trim() is { Length: > 0 and <= MaxLength } text
                && Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                return uri;
        }
        throw new ArgumentException("No usable address among the candidates; the last must be the board's own page.", nameof(candidates));
    }
}
