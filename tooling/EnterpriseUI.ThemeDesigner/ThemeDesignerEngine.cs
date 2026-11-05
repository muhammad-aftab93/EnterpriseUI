using System.Text.Json;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.ThemeDesigner;

public sealed record ThemeExportResult(string ThemeName, string CssVariables, string JsonTokens, string CSharpBuilderCode)
{
    public string DocumentJson { get; init; } = string.Empty;
}

/// <summary>
/// Theme Designer engine for visual customization, token auditing, and cross-platform token code export.
/// </summary>
public class ThemeDesignerEngine
{
    private Theme _activeTheme;
    private ThemeDocument _metadata;

    public Theme ActiveTheme => _activeTheme;

    public ThemeDesignerEngine()
    {
        _activeTheme = Theming.Themes.BuiltInThemes.CreateFluent(ColorScheme.Light);
        _metadata = ThemeDocument.FromTheme(_activeTheme);
    }

    public void LoadTheme(Theme theme)
    {
        _activeTheme = theme ?? throw new ArgumentNullException(nameof(theme));
        _metadata = ThemeDocument.FromTheme(theme);
    }

    public void UpdateToken(string tokenName, string value)
    {
        var document = CreateDocument();
        document.Tokens[tokenName] = value;
        _activeTheme = document.Build();
    }

    public ThemeExportResult Export()
    {
        var css = _activeTheme.ToCssVariables("eui");
        var documentJson = CreateDocument().ToJson();
        var json = JsonSerializer.Serialize(_activeTheme.DesignTokens, new JsonSerializerOptions { WriteIndented = true });
        var source = new System.Text.StringBuilder();
        source.AppendLine($"var myTheme = ThemeBuilder.Create({JsonSerializer.Serialize(_activeTheme.Name)})");
        source.AppendLine($"    .BasedOn(ThemeType.{_activeTheme.Type})");
        source.AppendLine($"    .WithColorScheme(ColorScheme.{_activeTheme.Scheme})");
        foreach (var (key, value) in _activeTheme.DesignTokens.OrderBy(token => token.Key, StringComparer.Ordinal))
            source.AppendLine($"    .WithToken({JsonSerializer.Serialize(key)}, {JsonSerializer.Serialize(value)})");
        source.Append("    .Build();");
        var csharp = source.ToString();

        return new ThemeExportResult(_activeTheme.Name, css, json, csharp) { DocumentJson = documentJson };
    }

    public Theme Import(string json, Func<string, ITheme?>? resolveBase = null)
    {
        var document = ThemeDocument.FromJson(json);
        var theme = document.Build(resolveBase);
        LoadTheme(theme);
        _metadata = document;
        return theme;
    }

    private ThemeDocument CreateDocument()
    {
        var document = ThemeDocument.FromTheme(_activeTheme, _metadata.Version);
        document.Author = _metadata.Author;
        document.Brand = _metadata.Brand;
        document.License = _metadata.License;
        return document;
    }
}

