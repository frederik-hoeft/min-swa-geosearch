using MudBlazor;

namespace GeoSearch.Web.Client.Services.Theming;

/// <summary>Consistent GeoSearch design tokens for all MudBlazor surfaces.</summary>
internal static class GeoSearchMudTheme
{
    private static readonly string[] s_fontFamily = ["var(--gs-font-sans)"];

    public static MudTheme Theme { get; } = CreateTheme();

    private static MudTheme CreateTheme()
    {
        Shadow shadows = new();
        shadows.Elevation[1] = "0 1px 3px rgba(18, 41, 47, 0.06)";
        shadows.Elevation[4] = "0 14px 36px rgba(18, 41, 47, 0.09)";

        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#117A68",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#42616A",
                SecondaryContrastText = "#FFFFFF",
                Tertiary = "#D4A153",
                Info = "#327EA8",
                Success = "#16805D",
                Warning = "#B87319",
                Error = "#C84448",
                TextPrimary = "#192C32",
                TextSecondary = "#61747A",
                TextDisabled = "#9BAAAF",
                Background = "#F3F7F6",
                BackgroundGray = "#EBF1EF",
                Surface = "#FFFFFF",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#192C32",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#192C32",
                LinesDefault = "#DCE6E3",
                LinesInputs = "#AEC5BE",
                Divider = "#DCE6E3",
                TableHover = "#EBF1EF",
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#69D3B4",
                PrimaryContrastText = "#12252B",
                Secondary = "#B0C7C8",
                SecondaryContrastText = "#12252B",
                Tertiary = "#E2B86F",
                Info = "#8DC5E5",
                Success = "#69D3B4",
                Warning = "#E2B86F",
                Error = "#FF9698",
                TextPrimary = "#EBF5F2",
                TextSecondary = "#AFC5C4",
                Background = "#101E24",
                BackgroundGray = "#1B3036",
                Surface = "#1D3036",
                DrawerBackground = "#1D3036",
                DrawerText = "#EBF5F2",
                AppbarBackground = "#1D3036",
                AppbarText = "#EBF5F2",
                LinesDefault = "#355056",
                LinesInputs = "#527278",
                Divider = "#355056",
            },
            Typography = new Typography
            {
                Default = Preset<DefaultTypography>("0.9375rem", "400", "1.5"),
                H1 = Preset<H1Typography>("2.125rem", "650", "1.18"),
                H2 = Preset<H2Typography>("1.75rem", "650", "1.22"),
                H3 = Preset<H3Typography>("1.5rem", "600", "1.25"),
                H4 = Preset<H4Typography>("1.375rem", "600", "1.3"),
                H5 = Preset<H5Typography>("1.1875rem", "600", "1.35"),
                H6 = Preset<H6Typography>("1rem", "600", "1.4"),
                Subtitle1 = Preset<Subtitle1Typography>("0.9375rem", "600", "1.4"),
                Subtitle2 = Preset<Subtitle2Typography>("0.875rem", "600", "1.4"),
                Body1 = Preset<Body1Typography>("0.9375rem", "400", "1.5"),
                Body2 = Preset<Body2Typography>("0.875rem", "400", "1.5"),
                Button = Preset<ButtonTypography>("0.875rem", "600", "1.25"),
                Caption = Preset<CaptionTypography>("0.75rem", "400", "1.4"),
            },
            LayoutProperties = new LayoutProperties
            {
                AppbarHeight = "64px",
                DefaultBorderRadius = "10px",
            },
            Shadows = shadows,
        };
    }

    private static T Preset<T>(string size, string weight, string lineHeight)
        where T : BaseTypography, new() => new()
        {
            FontFamily = s_fontFamily,
            FontSize = size,
            FontWeight = weight,
            LineHeight = lineHeight,
            LetterSpacing = "0",
            TextTransform = "none",
        };
}
