using System.Text;
using MudBlazor;

namespace JobRadar.Web.Components.Layout;

/// <summary>
/// The site's only palette. MudBlazor's provider writes the light one; the dark one goes out as CSS in the page's
/// head (<see cref="DarkCss"/>), switched by <c>data-theme</c> on the html element, which a script in the head sets
/// before the first paint. So the prerendered page is already in the right theme, before the circuit connects.
/// app.css takes its own tokens from these variables. The system font: nothing fetched from outside.
/// </summary>
public static class JobRadarTheme
{
    private static readonly string[] SystemFont = ["system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2f5bea",
            Secondary = "#5b6475",
            Tertiary = "#0f766e",
            Background = "#f6f7f9",
            BackgroundGray = "#eef0f4",
            Surface = "#ffffff",
            AppbarBackground = "#ffffff",
            AppbarText = "#161b26",
            DrawerBackground = "#ffffff",
            DrawerText = "#161b26",
            TextPrimary = "#161b26",
            TextSecondary = "#5b6475",
            ActionDefault = "#5b6475",
            LinesDefault = "#e2e5ea",
            LinesInputs = "#cdd2da",
            TableLines = "#e2e5ea",
            Divider = "#e2e5ea",
            Success = "#16803c",
            Error = "#c0362c",
            Warning = "#b45309",
            Info = "#2f5bea",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7c9cff",
            PrimaryContrastText = "#0d1117",
            Secondary = "#9aa4b2",
            Tertiary = "#2dd4bf",
            Background = "#0d1117",
            BackgroundGray = "#1b222c",
            Surface = "#151b23",
            AppbarBackground = "#151b23",
            AppbarText = "#e7ebf0",
            DrawerBackground = "#151b23",
            DrawerText = "#e7ebf0",
            TextPrimary = "#e7ebf0",
            TextSecondary = "#9aa4b2",
            ActionDefault = "#9aa4b2",
            LinesDefault = "#29313c",
            LinesInputs = "#3a4350",
            TableLines = "#29313c",
            Divider = "#29313c",
            Success = "#4ade80",
            Error = "#f87171",
            Warning = "#fbbf24",
            Info = "#7c9cff",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = SystemFont },
            // Sentence case, as the rest of the site: MudBlazor's buttons are upper case by default.
            Button = new ButtonTypography { FontFamily = SystemFont, TextTransform = "none", FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
    };

    /// <summary>MudBlazor's variables for the dark palette, for <c>html[data-theme=dark]</c>.</summary>
    public static string DarkCss { get; } = $"html:root[data-theme=\"dark\"]{{{DarkVariables.Generate()}}}";

    /// <summary>A provider never rendered: it only writes the dark palette's variables the way MudBlazor does.</summary>
    private sealed class DarkVariables : MudThemeProvider
    {
        public static string Generate()
        {
#pragma warning disable BL0005, MUD0012 // Set outside a render on purpose: this instance is never rendered.
            var provider = new DarkVariables { Theme = JobRadarTheme.Theme, IsDarkMode = true };
#pragma warning restore BL0005, MUD0012
            var css = new StringBuilder();
            provider.GenerateTheme(css);
            return css.ToString();
        }
    }
}
