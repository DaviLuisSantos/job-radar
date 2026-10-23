using System.Text.RegularExpressions;

namespace JobRadar.Domain.Listings;

/// <summary>How a full ad text reached the system.</summary>
public enum DescriptionOrigin
{
    /// <summary>Sent from the ad page open in the candidate's browser (bookmarklet).</summary>
    Browser,

    /// <summary>Pasted by hand on the listing page.</summary>
    Pasted,

    /// <summary>Fetched from the source's own data endpoint (Seek: the one its site loads ad text from).</summary>
    Source,
}

/// <summary>
/// The full text of an ad: fetched from the source's data endpoint, or captured by the candidate from
/// a page they opened. Ad pages themselves sit behind an anti-bot challenge and are never fetched.
/// Personal use only: it never goes into the Raio-X, the repository or a test fixture.
/// </summary>
public sealed partial record ListingDescription(ListingId Id, string Text, DescriptionOrigin Origin, DateTimeOffset CapturedAt)
{
    /// <summary>Longer than any real ad; what passes it is page chrome.</summary>
    public const int MaxLength = 20_000;

    /// <summary>Shorter than this is a selection or an empty page, not an ad.</summary>
    public const int MinLength = 80;

    /// <summary>
    /// Trimmed, with line endings unified, spaces collapsed and at most one blank line in a row;
    /// null when too short to be an ad. Cut at <see cref="MaxLength"/>.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var text = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        text = Spaces().Replace(text, " ");
        text = string.Join('\n', text.Split('\n').Select(l => l.Trim()));
        text = BlankLines().Replace(text, "\n\n").Trim();
        if (text.Length < MinLength)
            return null;
        return text.Length > MaxLength ? text[..MaxLength] : text;
    }

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
