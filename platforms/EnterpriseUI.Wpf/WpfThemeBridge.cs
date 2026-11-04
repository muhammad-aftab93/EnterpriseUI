using System.Windows.Media;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.Wpf;

public static class WpfThemeBridge
{
    public static Color ToWpfColor(ITheme theme, string tokenName, Color fallback)
    {
        var hex = theme.GetToken(tokenName);
        if (string.IsNullOrEmpty(hex)) return fallback;

        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            if (converted is Color color) return color;
        }
        catch { }

        return fallback;
    }

    public static SolidColorBrush ToBrush(ITheme theme, string tokenName, Color fallback)
    {
        return new SolidColorBrush(ToWpfColor(theme, tokenName, fallback));
    }
}

