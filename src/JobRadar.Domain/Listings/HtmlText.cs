using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Domain.Listings;

/// <summary>
/// An ad sent as HTML, as plain text the scorer and the page can read: blocks and line breaks become new lines, list
/// items start with "• ", tags go, entities are decoded. Not a sanitiser: the result is shown as text, never as markup.
/// </summary>
public static partial class HtmlText
{
    public static string? ToPlain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        var text = Scripts().Replace(html, " ");
        text = LineBreak().Replace(text, "\n");
        text = ListItem().Replace(text, "\n• ");
        text = BlockEnd().Replace(text, "\n");
        text = Tag().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        text = Spaces().Replace(text, " ");
        text = SpacedNewLine().Replace(text, "\n");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim() is { Length: > 0 } plain ? plain : null;
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Scripts();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</(p|div|ul|ol|h[1-6]|tr|table|section|blockquote)\s*>|<(p|div|ul|ol|h[1-6]|tr|table|section|blockquote)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@" *\n *")]
    private static partial Regex SpacedNewLine();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
