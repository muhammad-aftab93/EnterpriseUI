using System.Text;
using System.Collections.ObjectModel;
using EnterpriseUI.Abstractions.Theming;

namespace EnterpriseUI.Theming;

/// <summary>
/// Immutable enterprise theme definition with token resolution and CSS variable generation.
/// </summary>
public class Theme : ITheme
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReadOnlyDictionary<string, string> _readOnlyTokens;

    public string Name { get; }
    public ThemeType Type { get; }
    public ColorScheme Scheme { get; }
    public IReadOnlyDictionary<string, string> DesignTokens => _readOnlyTokens;

    public Theme(string name, ThemeType type, ColorScheme scheme, IDictionary<string, string>? tokens = null)
    {
        Name = name;
        Type = type;
        Scheme = scheme;
        _readOnlyTokens = new ReadOnlyDictionary<string, string>(_tokens);

        if (tokens != null)
        {
            foreach (var kvp in tokens)
            {
                _tokens[kvp.Key] = kvp.Value;
            }
        }
    }

    public string GetToken(string tokenName, string fallback = "")
    {
        return _tokens.TryGetValue(tokenName, out var val) ? val : fallback;
    }

    public string ToCssVariables(string prefix = "eui")
    {
        var sb = new StringBuilder();
        sb.Append($":root[data-theme=\"{Name.ToLowerInvariant()}\"] {{\n");
        foreach (var (key, value) in _tokens)
        {
            sb.Append($"  --{prefix}-{key}: {value};\n");
        }
        sb.Append("}\n");
        return sb.ToString();
    }
}
