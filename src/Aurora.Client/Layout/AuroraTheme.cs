using MudBlazor;

namespace Aurora.Client.Layout;

// Keep the shared shell aligned with Integration Hub's navigation and content palette.
internal static class AuroraTheme
{
    private static readonly string[] Fonts = ["Segoe UI", "Tahoma", "Geneva", "Verdana", "sans-serif"];

    public static MudTheme Hub { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2563eb",
            Secondary = "#2d3748",
            Background = "#f0f2f5",
            Surface = "#ffffff",
            AppbarBackground = "#2d3748",
            AppbarText = "#ffffff",
            DrawerBackground = "#2d3748",
            DrawerText = "#ffffff",
            TextPrimary = "#2d3748",
            TextSecondary = "#6b7280",
            Divider = "#e5e7eb",
            LinesDefault = "#d1d5db",
            TableLines = "#e5e7eb",
            Success = "#15803d",
            Warning = "#b45309",
            Error = "#dc2626",
            Info = "#2563eb"
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Fonts },
            H1 = new H1Typography { FontFamily = Fonts },
            H2 = new H2Typography { FontFamily = Fonts },
            H3 = new H3Typography { FontFamily = Fonts },
            H4 = new H4Typography { FontFamily = Fonts },
            H5 = new H5Typography { FontFamily = Fonts, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = Fonts, FontWeight = "600" },
            Subtitle1 = new Subtitle1Typography { FontFamily = Fonts },
            Subtitle2 = new Subtitle2Typography { FontFamily = Fonts },
            Body1 = new Body1Typography { FontFamily = Fonts },
            Body2 = new Body2Typography { FontFamily = Fonts },
            Button = new ButtonTypography { FontFamily = Fonts, TextTransform = "none", LetterSpacing = "0", FontWeight = "600" },
            Caption = new CaptionTypography { FontFamily = Fonts },
            Overline = new OverlineTypography { FontFamily = Fonts }
        }
    };
}
