using System.Drawing;
using System.Globalization;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Bridge that translates EnterpriseUI design tokens into WinForms Graphics, Fonts, and Colors.
/// </summary>
public static class EnterpriseThemeEngine
{
    public static Color GetColor(ITheme theme, string tokenName, Color fallback)
    {
        var hex = theme.GetToken(tokenName);
        if (string.IsNullOrEmpty(hex)) return fallback;

        try
        {
            if (hex.StartsWith('#'))
            {
                if (hex.Length == 7)
                {
                    return Color.FromArgb(
                        int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber),
                        int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber),
                        int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber));
                }
                else if (hex.Length == 9)
                {
                    return Color.FromArgb(
                        int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber),
                        int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber),
                        int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber),
                        int.Parse(hex.Substring(7, 2), NumberStyles.HexNumber));
                }
            }
        }
        catch { }

        return fallback;
    }

    public static void ApplyToControl(Control control, ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(theme);

        var bg = GetColor(theme, DesignTokens.Background, SystemColors.Control);
        var fg = GetColor(theme, DesignTokens.TextColor, SystemColors.ControlText);

        control.BackColor = bg;
        control.ForeColor = fg;

        foreach (Control child in control.Controls)
        {
            ApplyToControl(child, theme);
        }
    }
}

