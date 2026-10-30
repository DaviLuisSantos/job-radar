using MudBlazor;

namespace JobRadar.Web.Components.Layout;

/// <summary>
/// MudBlazor's palette, matched to the colour tokens of app.css so the components and the pages not yet moved to them
/// look like one site, in light and dark. The system font: nothing fetched from outside.
/// </summary>
public static class JobRadarTheme
{
    private static readonly string[] SystemFont = ["system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2563eb",
            Background = "#f6f7f9",
            Surface = "#ffffff",
            AppbarBackground = "#ffffff",
            AppbarText = "#1c2330",
            DrawerBackground = "#ffffff",
            TextPrimary = "#1c2330",
            TextSecondary = "#5d6776",
            Success = "#15803d",
            Error = "#b42318",
            Warning = "#a15c07",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#6c9cff",
            Background = "#0f141b",
            Surface = "#171e27",
            AppbarBackground = "#171e27",
            AppbarText = "#e6eaf0",
            DrawerBackground = "#171e27",
            TextPrimary = "#e6eaf0",
            TextSecondary = "#98a3b3",
            Success = "#4ade80",
            Error = "#f87171",
            Warning = "#fbbf24",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = SystemFont },
            // Sentence case, as the rest of the site: MudBlazor's buttons are upper case by default.
            Button = new ButtonTypography { FontFamily = SystemFont, TextTransform = "none", FontWeight = "500" },
        },
    };
}
