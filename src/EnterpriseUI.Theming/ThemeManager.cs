using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming.Themes;

namespace EnterpriseUI.Theming;

/// <summary>
/// Fluent Theme Builder API for custom brand and design system construction.
/// </summary>
public sealed class ThemeBuilder
{
    private readonly string _name;
    private ThemeType _baseType = ThemeType.Default;
    private ColorScheme _scheme = ColorScheme.Light;
    private ITheme? _baseTheme;
    private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    private ThemeBuilder(string name)
    {
        _name = name;
    }

    public static ThemeBuilder Create(string name) => new(name);

    public ThemeBuilder BasedOn(ThemeType baseType)
    {
        _baseTheme = null;
        _baseType = baseType;
        return this;
    }

    public ThemeBuilder BasedOn(ITheme baseTheme)
    {
        ArgumentNullException.ThrowIfNull(baseTheme);
        _baseTheme = baseTheme;
        _baseType = baseTheme.Type;
        _scheme = baseTheme.Scheme;
        return this;
    }

    public ThemeBuilder WithColorScheme(ColorScheme scheme)
    {
        _scheme = scheme;
        return this;
    }

    public ThemeBuilder WithPrimaryColor(string hexColor, string? hover = null, string? active = null, string? text = null)
    {
        _overrides[DesignTokens.Primary] = hexColor;
        if (hover != null) _overrides[DesignTokens.PrimaryHover] = hover;
        if (active != null) _overrides[DesignTokens.PrimaryActive] = active;
        if (text != null) _overrides[DesignTokens.PrimaryText] = text;
        return this;
    }

    public ThemeBuilder WithFontFamily(string fontFamily)
    {
        _overrides[DesignTokens.FontFamily] = fontFamily;
        return this;
    }

    public ThemeBuilder WithBorderRadius(string radius, string? sm = null, string? lg = null)
    {
        _overrides[DesignTokens.BorderRadius] = radius;
        if (sm != null) _overrides[DesignTokens.BorderRadiusSm] = sm;
        if (lg != null) _overrides[DesignTokens.BorderRadiusLg] = lg;
        return this;
    }

    public ThemeBuilder WithToken(string key, string value)
    {
        _overrides[key] = value;
        return this;
    }

    public Theme Build()
    {
        Theme baseTheme = _baseType switch
        {
            ThemeType.Fluent => BuiltInThemes.CreateFluent(_scheme),
            ThemeType.Material => BuiltInThemes.CreateMaterial(_scheme),
            ThemeType.Bootstrap => BuiltInThemes.CreateBootstrap(_scheme),
            ThemeType.Office => BuiltInThemes.CreateOffice(_scheme),
            ThemeType.Modern => BuiltInThemes.CreateModern(_scheme),
            ThemeType.Windows => BuiltInThemes.CreateWindows(_scheme),
            ThemeType.HighContrast => BuiltInThemes.CreateHighContrast(_scheme != ColorScheme.Light),
            _ => BuiltInThemes.CreateDefault(_scheme)
        };

        var mergedTokens = new Dictionary<string, string>(_baseTheme?.DesignTokens ?? baseTheme.DesignTokens, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in _overrides)
        {
            mergedTokens[k] = v;
        }

        return new Theme(_name, _baseType, _scheme, mergedTokens);
    }
}

/// <summary>
/// Runtime theme manager with instant hot-swapping and subscriber notification.
/// </summary>
public sealed class ThemeManager : IThemeManager
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    private readonly Dictionary<string, ITheme> _registeredThemes = new(StringComparer.OrdinalIgnoreCase);
    private ITheme _currentTheme;

    public event EventHandler<ITheme>? ThemeChanged;

    public ITheme CurrentTheme => _currentTheme;
    public IReadOnlyCollection<ITheme> AvailableThemes => _registeredThemes.Values;

    public ThemeManager()
    {
        // Register default themes
        RegisterTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateFluent(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateMaterial(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateMaterial(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateBootstrap(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateBootstrap(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateOffice(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateOffice(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateModern(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateModern(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateWindows(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateWindows(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateHighContrast(isDark: true));
        RegisterTheme(BuiltInThemes.CreateHighContrast(isDark: false));
        RegisterTheme(BuiltInThemes.CreateStudio(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateStudio(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateModernOffice(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateModernOffice(ColorScheme.Dark));
        RegisterTheme(BuiltInThemes.CreateCompactBusiness(ColorScheme.Light));
        RegisterTheme(BuiltInThemes.CreateCompactBusiness(ColorScheme.Dark));

        _currentTheme = _registeredThemes["Fluent-Light"];
    }

    public void RegisterTheme(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _registeredThemes[theme.Name] = theme;
    }

    public void ApplyTheme(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _registeredThemes[theme.Name] = theme;
        _currentTheme = theme;
        ThemeChanged?.Invoke(this, _currentTheme);
    }

    public void ApplyTheme(string themeName)
    {
        if (_registeredThemes.TryGetValue(themeName, out var theme))
        {
            ApplyTheme(theme);
        }
        else
        {
            throw new KeyNotFoundException($"Theme '{themeName}' is not registered.");
        }
    }

    public Theme ImportTheme(string json, bool apply = false)
    {
        var document = ThemeDocument.FromJson(json);
        var theme = document.Build(name => _registeredThemes.GetValueOrDefault(name));
        if (apply) ApplyTheme(theme);
        else RegisterTheme(theme);
        return theme;
    }

    public string ExportTheme(string version = "1.0.0") => ThemeDocument.FromTheme(CurrentTheme, version).ToJson();

    public async Task<Theme> ReloadThemeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(filePath);
        if (file.Length > 1048576) throw new InvalidDataException("Theme files must be at most 1 MiB.");
        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        return ImportTheme(json, apply: true);
    }

    public void SetColorScheme(ColorScheme scheme)
    {
        var targetType = _currentTheme.Type;
        var matching = _registeredThemes.Values.FirstOrDefault(t => t.Type == targetType && t.Scheme == scheme);
        if (matching != null)
        {
            ApplyTheme(matching);
        }
    }
}

