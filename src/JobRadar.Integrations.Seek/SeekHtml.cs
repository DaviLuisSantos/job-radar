using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Integrations.Seek;

/// <summary>Seek's ad HTML as plain text: blocks become lines, list items get a bullet, tags go away.</summary>
internal static partial class SeekHtml
{
    public static string ToText(string html)
    {
        var text = LineBreaks().Replace(html, "\n");
        text = ListItems().Replace(text, "\n• ");
        text = Tags().Replace(text, "");
        return WebUtility.HtmlDecode(text);
    }

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|ul|ol|h[1-6])>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();
}
