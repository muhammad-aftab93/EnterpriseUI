namespace EnterpriseUI.Abstractions.Theming;

public enum ThemeType
{
    Default,
    Fluent,
    Material,
    Bootstrap,
    Office,
    Modern,
    Windows,
    HighContrast
}

public enum ColorScheme
{
    Light,
    Dark,
    HighContrast
}

public interface ITheme
{
    string Name { get; }
    ThemeType Type { get; }
    ColorScheme Scheme { get; }
    IReadOnlyDictionary<string, string> DesignTokens { get; }
    string GetToken(string tokenName, string fallback = "");
}

public interface IThemeManager
{
    ITheme CurrentTheme { get; }
    void ApplyTheme(ITheme theme);
    void SetColorScheme(ColorScheme scheme);
    event EventHandler<ITheme>? ThemeChanged;
}

public interface IThemeableControl
{
    string? ThemeOverride { get; set; }
    void OnThemeChanged(ITheme newTheme);
}

