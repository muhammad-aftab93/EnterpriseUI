using EnterpriseUI.Abstractions.Theming;

namespace EnterpriseUI.Theming.Themes;

public static class BuiltInThemes
{
    public static Theme CreateFluent(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#60cdff" : "#0078d4",
            [DesignTokens.PrimaryHover] = isDark ? "#78d4ff" : "#106ebe",
            [DesignTokens.PrimaryActive] = isDark ? "#4bbef7" : "#005a9e",
            [DesignTokens.PrimaryText] = isDark ? "#000000" : "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#323232" : "#edebe9",
            [DesignTokens.SecondaryHover] = isDark ? "#3c3c3c" : "#e1dfdd",
            [DesignTokens.SecondaryText] = isDark ? "#ffffff" : "#201f1e",

            [DesignTokens.Success] = "#107c10",
            [DesignTokens.Warning] = "#ffb900",
            [DesignTokens.Danger] = "#d13438",
            [DesignTokens.Info] = "#0078d4",

            [DesignTokens.Background] = isDark ? "#202020" : "#f3f3f3",
            [DesignTokens.Surface] = isDark ? "#2c2c2c" : "#ffffff",
            [DesignTokens.SurfaceAlt] = isDark ? "#383838" : "#faf9f8",
            [DesignTokens.BorderColor] = isDark ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)",
            [DesignTokens.BorderColorHover] = isDark ? "rgba(255, 255, 255, 0.18)" : "rgba(0, 0, 0, 0.18)",

            [DesignTokens.FontFamily] = "'Segoe UI Variable', 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif",
            [DesignTokens.FontSizeBase] = "14px",
            [DesignTokens.FontSizeSm] = "12px",
            [DesignTokens.FontSizeLg] = "16px",
            [DesignTokens.TextColor] = isDark ? "#ffffff" : "#242424",
            [DesignTokens.TextMuted] = isDark ? "#a19f9d" : "#605e5c",

            [DesignTokens.BorderRadius] = "4px",
            [DesignTokens.BorderRadiusSm] = "2px",
            [DesignTokens.BorderRadiusLg] = "8px",
            [DesignTokens.ControlHeight] = "32px",
            [DesignTokens.ControlPaddingX] = "12px",
            [DesignTokens.ControlPaddingY] = "6px",

            [DesignTokens.ShadowSm] = "0 1px 2px rgba(0,0,0,0.12)",
            [DesignTokens.ShadowMd] = "0 4px 8px rgba(0,0,0,0.14)",
            [DesignTokens.ShadowLg] = "0 8px 16px rgba(0,0,0,0.16)",
            [DesignTokens.TransitionDuration] = "150ms",
            [DesignTokens.TransitionTiming] = "cubic-bezier(0.1, 0.9, 0.2, 1.0)"
        };

        return new Theme(isDark ? "Fluent-Dark" : "Fluent-Light", ThemeType.Fluent, scheme, tokens);
    }

    public static Theme CreateMaterial(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#d0bcff" : "#6750a4",
            [DesignTokens.PrimaryHover] = isDark ? "#e8def8" : "#7d5260",
            [DesignTokens.PrimaryActive] = isDark ? "#c4adfc" : "#4f378b",
            [DesignTokens.PrimaryText] = isDark ? "#381e72" : "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#ccc2dc" : "#625b71",
            [DesignTokens.SecondaryHover] = isDark ? "#e8def8" : "#7a7289",
            [DesignTokens.SecondaryText] = isDark ? "#332d41" : "#ffffff",

            [DesignTokens.Success] = "#4caf50",
            [DesignTokens.Warning] = "#ff9800",
            [DesignTokens.Danger] = "#f44336",
            [DesignTokens.Info] = "#2196f3",

            [DesignTokens.Background] = isDark ? "#141218" : "#fef7ff",
            [DesignTokens.Surface] = isDark ? "#211f26" : "#f7f2fa",
            [DesignTokens.SurfaceAlt] = isDark ? "#2b2930" : "#ece6f0",
            [DesignTokens.BorderColor] = isDark ? "#938f99" : "#79747e",
            [DesignTokens.BorderColorHover] = isDark ? "#cac4d0" : "#49454f",

            [DesignTokens.FontFamily] = "'Roboto', 'Segoe UI', -apple-system, sans-serif",
            [DesignTokens.FontSizeBase] = "14px",
            [DesignTokens.FontSizeSm] = "12px",
            [DesignTokens.FontSizeLg] = "16px",
            [DesignTokens.TextColor] = isDark ? "#e6e1e5" : "#1d1b20",
            [DesignTokens.TextMuted] = isDark ? "#938f99" : "#49454f",

            [DesignTokens.BorderRadius] = "8px",
            [DesignTokens.BorderRadiusSm] = "4px",
            [DesignTokens.BorderRadiusLg] = "16px",
            [DesignTokens.ControlHeight] = "40px",
            [DesignTokens.ControlPaddingX] = "16px",
            [DesignTokens.ControlPaddingY] = "8px",

            [DesignTokens.ShadowSm] = "0px 1px 3px 1px rgba(0, 0, 0, 0.15)",
            [DesignTokens.ShadowMd] = "0px 2px 6px 2px rgba(0, 0, 0, 0.15)",
            [DesignTokens.ShadowLg] = "0px 4px 8px 3px rgba(0, 0, 0, 0.15)",
            [DesignTokens.TransitionDuration] = "200ms",
            [DesignTokens.TransitionTiming] = "cubic-bezier(0.2, 0.0, 0, 1.0)"
        };

        return new Theme(isDark ? "Material-Dark" : "Material-Light", ThemeType.Material, scheme, tokens);
    }

    public static Theme CreateBootstrap(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#3d8bfd" : "#0d6efd",
            [DesignTokens.PrimaryHover] = isDark ? "#0b5ed7" : "#0b5ed7",
            [DesignTokens.PrimaryActive] = isDark ? "#0a58ca" : "#0a58ca",
            [DesignTokens.PrimaryText] = "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#828a91" : "#6c757d",
            [DesignTokens.SecondaryHover] = isDark ? "#5c636a" : "#5a6268",
            [DesignTokens.SecondaryText] = "#ffffff",

            [DesignTokens.Success] = "#198754",
            [DesignTokens.Warning] = "#ffc107",
            [DesignTokens.Danger] = "#dc3545",
            [DesignTokens.Info] = "#0dcaf0",

            [DesignTokens.Background] = isDark ? "#212529" : "#ffffff",
            [DesignTokens.Surface] = isDark ? "#2b3035" : "#f8f9fa",
            [DesignTokens.SurfaceAlt] = isDark ? "#343a40" : "#e9ecef",
            [DesignTokens.BorderColor] = isDark ? "#495057" : "#dee2e6",
            [DesignTokens.BorderColorHover] = isDark ? "#6c757d" : "#ced4da",

            [DesignTokens.FontFamily] = "system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
            [DesignTokens.FontSizeBase] = "14px",
            [DesignTokens.FontSizeSm] = "12px",
            [DesignTokens.FontSizeLg] = "16px",
            [DesignTokens.TextColor] = isDark ? "#dee2e6" : "#212529",
            [DesignTokens.TextMuted] = isDark ? "#adb5bd" : "#6c757d",

            [DesignTokens.BorderRadius] = "6px",
            [DesignTokens.BorderRadiusSm] = "4px",
            [DesignTokens.BorderRadiusLg] = "8px",
            [DesignTokens.ControlHeight] = "36px",
            [DesignTokens.ControlPaddingX] = "12px",
            [DesignTokens.ControlPaddingY] = "6px",

            [DesignTokens.ShadowSm] = "0 .125rem .25rem rgba(0,0,0,.075)",
            [DesignTokens.ShadowMd] = "0 .5rem 1rem rgba(0,0,0,.15)",
            [DesignTokens.ShadowLg] = "0 1rem 3rem rgba(0,0,0,.175)",
            [DesignTokens.TransitionDuration] = "150ms",
            [DesignTokens.TransitionTiming] = "ease-in-out"
        };

        return new Theme(isDark ? "Bootstrap-Dark" : "Bootstrap-Light", ThemeType.Bootstrap, scheme, tokens);
    }

    public static Theme CreateOffice(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#2886de" : "#185abd", // Office Blue
            [DesignTokens.PrimaryHover] = isDark ? "#3a96ec" : "#104a9e",
            [DesignTokens.PrimaryActive] = isDark ? "#176ac2" : "#0d3b80",
            [DesignTokens.PrimaryText] = "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#3b3a39" : "#e1dfdd",
            [DesignTokens.SecondaryHover] = isDark ? "#484644" : "#d2d0ce",
            [DesignTokens.SecondaryText] = isDark ? "#ffffff" : "#323130",

            [DesignTokens.Success] = "#107c41", // Excel Green
            [DesignTokens.Warning] = "#d83b01", // Office Warm
            [DesignTokens.Danger] = "#a80000",
            [DesignTokens.Info] = "#185abd",

            [DesignTokens.Background] = isDark ? "#1e1e1e" : "#f3f2f1",
            [DesignTokens.Surface] = isDark ? "#252423" : "#ffffff",
            [DesignTokens.SurfaceAlt] = isDark ? "#2d2c2c" : "#faf9f8",
            [DesignTokens.BorderColor] = isDark ? "#3b3a39" : "#e1dfdd",
            [DesignTokens.BorderColorHover] = isDark ? "#605e5c" : "#a19f9d",

            [DesignTokens.FontFamily] = "'Segoe UI', 'Segoe UI Web', Tahoma, Geneva, Verdana, sans-serif",
            [DesignTokens.FontSizeBase] = "13px",
            [DesignTokens.FontSizeSm] = "11px",
            [DesignTokens.FontSizeLg] = "15px",
            [DesignTokens.TextColor] = isDark ? "#f3f2f1" : "#323130",
            [DesignTokens.TextMuted] = isDark ? "#a19f9d" : "#605e5c",

            [DesignTokens.BorderRadius] = "2px",
            [DesignTokens.BorderRadiusSm] = "1px",
            [DesignTokens.BorderRadiusLg] = "4px",
            [DesignTokens.ControlHeight] = "28px",
            [DesignTokens.ControlPaddingX] = "8px",
            [DesignTokens.ControlPaddingY] = "4px",

            [DesignTokens.ShadowSm] = "0 1.6px 3.6px 0 rgba(0,0,0,.132)",
            [DesignTokens.ShadowMd] = "0 3.2px 7.2px 0 rgba(0,0,0,.132)",
            [DesignTokens.ShadowLg] = "0 6.4px 14.4px 0 rgba(0,0,0,.132)",
            [DesignTokens.TransitionDuration] = "100ms",
            [DesignTokens.TransitionTiming] = "ease"
        };

        return new Theme(isDark ? "Office-Dark" : "Office-Light", ThemeType.Office, scheme, tokens);
    }

    public static Theme CreateModern(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#6366f1" : "#4f46e5", // Indigo Modern
            [DesignTokens.PrimaryHover] = isDark ? "#818cf8" : "#4338ca",
            [DesignTokens.PrimaryActive] = isDark ? "#4f46e5" : "#3730a3",
            [DesignTokens.PrimaryText] = "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#1e293b" : "#f1f5f9",
            [DesignTokens.SecondaryHover] = isDark ? "#334155" : "#e2e8f0",
            [DesignTokens.SecondaryText] = isDark ? "#f8fafc" : "#0f172a",

            [DesignTokens.Success] = "#10b981",
            [DesignTokens.Warning] = "#f59e0b",
            [DesignTokens.Danger] = "#ef4444",
            [DesignTokens.Info] = "#06b6d4",

            [DesignTokens.Background] = isDark ? "#0b0f19" : "#f8fafc",
            [DesignTokens.Surface] = isDark ? "#111827" : "#ffffff",
            [DesignTokens.SurfaceAlt] = isDark ? "#1f2937" : "#f1f5f9",
            [DesignTokens.BorderColor] = isDark ? "#1f2937" : "#e2e8f0",
            [DesignTokens.BorderColorHover] = isDark ? "#374151" : "#cbd5e1",

            [DesignTokens.FontFamily] = "'Inter', -apple-system, BlinkMacSystemFont, sans-serif",
            [DesignTokens.FontSizeBase] = "14px",
            [DesignTokens.FontSizeSm] = "12px",
            [DesignTokens.FontSizeLg] = "16px",
            [DesignTokens.TextColor] = isDark ? "#f3f4f6" : "#0f172a",
            [DesignTokens.TextMuted] = isDark ? "#9ca3af" : "#64748b",

            [DesignTokens.BorderRadius] = "6px",
            [DesignTokens.BorderRadiusSm] = "4px",
            [DesignTokens.BorderRadiusLg] = "12px",
            [DesignTokens.ControlHeight] = "36px",
            [DesignTokens.ControlPaddingX] = "14px",
            [DesignTokens.ControlPaddingY] = "7px",

            [DesignTokens.ShadowSm] = "0 1px 2px 0 rgb(0 0 0 / 0.05)",
            [DesignTokens.ShadowMd] = "0 4px 6px -1px rgb(0 0 0 / 0.1)",
            [DesignTokens.ShadowLg] = "0 10px 15px -3px rgb(0 0 0 / 0.1)",
            [DesignTokens.TransitionDuration] = "150ms",
            [DesignTokens.TransitionTiming] = "cubic-bezier(0.4, 0, 0.2, 1)"
        };

        return new Theme(isDark ? "Modern-Dark" : "Modern-Light", ThemeType.Modern, scheme, tokens);
    }

    public static Theme CreateWindows(ColorScheme scheme = ColorScheme.Light)
    {
        var isDark = scheme == ColorScheme.Dark;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#4cc2ff" : "#005fb8",
            [DesignTokens.PrimaryHover] = isDark ? "#6dd0ff" : "#004f99",
            [DesignTokens.PrimaryActive] = isDark ? "#3bb0f0" : "#003f7a",
            [DesignTokens.PrimaryText] = isDark ? "#000000" : "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#2d2d2d" : "#e5e5e5",
            [DesignTokens.SecondaryHover] = isDark ? "#383838" : "#d9d9d9",
            [DesignTokens.SecondaryText] = isDark ? "#ffffff" : "#1b1b1b",

            [DesignTokens.Success] = "#0f7b0f",
            [DesignTokens.Warning] = "#9d5d00",
            [DesignTokens.Danger] = "#c42b1c",
            [DesignTokens.Info] = "#005fb8",

            [DesignTokens.Background] = isDark ? "#1c1c1c" : "#f9f9f9",
            [DesignTokens.Surface] = isDark ? "#282828" : "#ffffff",
            [DesignTokens.SurfaceAlt] = isDark ? "#323232" : "#f3f3f3",
            [DesignTokens.BorderColor] = isDark ? "rgba(255, 255, 255, 0.07)" : "rgba(0, 0, 0, 0.06)",
            [DesignTokens.BorderColorHover] = isDark ? "rgba(255, 255, 255, 0.12)" : "rgba(0, 0, 0, 0.12)",

            [DesignTokens.FontFamily] = "'Segoe UI Variable Text', 'Segoe UI', sans-serif",
            [DesignTokens.FontSizeBase] = "14px",
            [DesignTokens.FontSizeSm] = "12px",
            [DesignTokens.FontSizeLg] = "16px",
            [DesignTokens.TextColor] = isDark ? "#ffffff" : "#1b1b1b",
            [DesignTokens.TextMuted] = isDark ? "#a0a0a0" : "#5d5d5d",

            [DesignTokens.BorderRadius] = "6px",
            [DesignTokens.BorderRadiusSm] = "4px",
            [DesignTokens.BorderRadiusLg] = "8px",
            [DesignTokens.ControlHeight] = "32px",
            [DesignTokens.ControlPaddingX] = "12px",
            [DesignTokens.ControlPaddingY] = "6px",

            [DesignTokens.ShadowSm] = "0 2px 4px rgba(0,0,0,0.04)",
            [DesignTokens.ShadowMd] = "0 4px 8px rgba(0,0,0,0.08)",
            [DesignTokens.ShadowLg] = "0 8px 16px rgba(0,0,0,0.12)",
            [DesignTokens.TransitionDuration] = "167ms",
            [DesignTokens.TransitionTiming] = "cubic-bezier(0, 0, 0, 1)"
        };

        return new Theme(isDark ? "Windows-Dark" : "Windows-Light", ThemeType.Windows, scheme, tokens);
    }

    public static Theme CreateHighContrast(bool isDark = true)
    {
        var scheme = ColorScheme.HighContrast;
        var tokens = new Dictionary<string, string>
        {
            [DesignTokens.Primary] = isDark ? "#ffff00" : "#0000ff", // Yellow on Black or Blue on White
            [DesignTokens.PrimaryHover] = isDark ? "#00ffff" : "#000080",
            [DesignTokens.PrimaryActive] = isDark ? "#00ff00" : "#0000aa",
            [DesignTokens.PrimaryText] = isDark ? "#000000" : "#ffffff",

            [DesignTokens.Secondary] = isDark ? "#000000" : "#ffffff",
            [DesignTokens.SecondaryHover] = isDark ? "#1a1a1a" : "#f0f0f0",
            [DesignTokens.SecondaryText] = isDark ? "#ffffff" : "#000000",

            [DesignTokens.Success] = "#00ff00",
            [DesignTokens.Warning] = "#ffff00",
            [DesignTokens.Danger] = "#ff0000",
            [DesignTokens.Info] = "#00ffff",

            [DesignTokens.Background] = isDark ? "#000000" : "#ffffff",
            [DesignTokens.Surface] = isDark ? "#000000" : "#ffffff",
            [DesignTokens.SurfaceAlt] = isDark ? "#121212" : "#f5f5f5",
            [DesignTokens.BorderColor] = isDark ? "#ffffff" : "#000000",
            [DesignTokens.BorderColorHover] = isDark ? "#ffff00" : "#0000ff",

            [DesignTokens.FontFamily] = "'Segoe UI', Consolas, sans-serif",
            [DesignTokens.FontSizeBase] = "15px",
            [DesignTokens.FontSizeSm] = "13px",
            [DesignTokens.FontSizeLg] = "18px",
            [DesignTokens.TextColor] = isDark ? "#ffffff" : "#000000",
            [DesignTokens.TextMuted] = isDark ? "#00ffff" : "#0000aa",

            [DesignTokens.BorderRadius] = "0px",
            [DesignTokens.BorderRadiusSm] = "0px",
            [DesignTokens.BorderRadiusLg] = "0px",
            [DesignTokens.ControlHeight] = "36px",
            [DesignTokens.ControlPaddingX] = "12px",
            [DesignTokens.ControlPaddingY] = "6px",

            [DesignTokens.ShadowSm] = "none",
            [DesignTokens.ShadowMd] = "none",
            [DesignTokens.ShadowLg] = "none",
            [DesignTokens.TransitionDuration] = "0ms",
            [DesignTokens.TransitionTiming] = "step-start"
        };

        return new Theme(isDark ? "HighContrast-Black" : "HighContrast-White", ThemeType.HighContrast, scheme, tokens);
    }

    public static Theme CreateDefault(ColorScheme scheme = ColorScheme.Light)
    {
        return CreateFluent(scheme);
    }
}

