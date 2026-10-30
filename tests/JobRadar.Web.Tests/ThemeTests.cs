using JobRadar.Web.Components.Layout;

namespace JobRadar.Web.Tests;

/// <summary>
/// The dark palette goes out as CSS in the page head, so the page is dark before the circuit connects. It must be
/// MudBlazor's own variables for the dark palette, and only under the attribute the head script sets.
/// </summary>
public class ThemeTests
{
    [Fact]
    public void The_dark_css_holds_the_dark_palette_under_the_theme_attribute()
    {
        var css = JobRadarTheme.DarkCss;

        Assert.StartsWith("html:root[data-theme=\"dark\"]{", css, StringComparison.Ordinal);
        Assert.EndsWith("}", css, StringComparison.Ordinal);
        Assert.Contains("--mud-palette-surface: rgba(21,27,35,1);", css, StringComparison.Ordinal);   // PaletteDark.Surface #151b23
        Assert.Contains("--mud-palette-primary: rgba(124,156,255,1);", css, StringComparison.Ordinal);
        Assert.DoesNotContain("<", css, StringComparison.Ordinal);   // goes inside a style element as markup
    }
}
