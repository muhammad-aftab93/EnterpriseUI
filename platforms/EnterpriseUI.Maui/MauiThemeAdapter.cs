using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.Maui;

public static class MauiThemeAdapter
{
    public static string ResolveToken(ITheme theme, string tokenName, string fallback = "")
    {
        return theme.GetToken(tokenName, fallback);
    }
}

