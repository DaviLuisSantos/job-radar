using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Integrations.LinkedIn.Api;

/// <summary>
/// What the two guest pages are made of, read by the class names LinkedIn's own site styles them with (the same ones
/// the open-source scrapers read). No HTML parser: the markup is flat and regular, and a change there is caught by
/// the live test, not hidden by a lenient parser.
/// </summary>
internal static partial class LinkedInHtml
{
    /// <summary>
    /// <c>ClassOpen + "name" + ClassClose</c> matches a class attribute holding that class anywhere in its list
    /// (<c>class="salary compensation__salary"</c>), and not a longer class that starts with it.
    /// </summary>
    public const string ClassOpen = @"class=""(?:[^""]*\s)?";

    public const string ClassClose = @"(?:\s[^""]*)?""";

    /// <summary>Text of an HTML fragment: tags gone, entities decoded, whitespace collapsed; null when nothing is left.</summary>
    public static string? Text(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return null;
        var text = WebUtility.HtmlDecode(Tag().Replace(fragment, " ")).Replace(' ', ' ');
        text = Spaces().Replace(text, " ").Trim();
        return text.Length > 0 ? text : null;
    }

    /// <summary>The first capture of <paramref name="pattern"/> in <paramref name="html"/>, as text; null when absent.</summary>
    public static string? First(string html, Regex pattern) =>
        pattern.Match(html) is { Success: true } m ? Text(m.Groups[1].Value) : null;

    /// <summary>
    /// The content of the element whose opening tag <paramref name="openTag"/> matches, up to its own closing tag,
    /// counting the nested elements of the same name; null when the tag is not there.
    /// </summary>
    public static string? Element(string html, Regex openTag, string name)
    {
        var open = openTag.Match(html);
        if (!open.Success)
            return null;
        var start = open.Index + open.Length;
        var depth = 1;
        var opens = new Regex($"<{name}\\b", RegexOptions.IgnoreCase);
        var closes = new Regex($"</{name}\\s*>", RegexOptions.IgnoreCase);
        var position = start;
        while (depth > 0)
        {
            var nextOpen = opens.Match(html, position);
            var nextClose = closes.Match(html, position);
            if (!nextClose.Success)
                return html[start..];
            if (nextOpen.Success && nextOpen.Index < nextClose.Index)
            {
                depth++;
                position = nextOpen.Index + nextOpen.Length;
            }
            else
            {
                depth--;
                position = nextClose.Index + nextClose.Length;
                if (depth == 0)
                    return html[start..nextClose.Index];
            }
        }
        return null;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
