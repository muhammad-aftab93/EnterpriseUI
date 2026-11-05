using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EnterpriseUI.Abstractions.Theming;

namespace EnterpriseUI.Theming;

public sealed class ThemeDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "1.0.0";
    public string Name { get; set; } = string.Empty;
    public ThemeType Type { get; set; } = ThemeType.Fluent;
    public ColorScheme Scheme { get; set; } = ColorScheme.Light;
    public string? BaseTheme { get; set; }
    public string? Brand { get; set; }
    public string? Author { get; set; }
    public string? License { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Tokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly Regex TokenName = new("^[A-Za-z][A-Za-z0-9_-]{0,127}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static ThemeDocument FromTheme(ITheme theme, string version = "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(theme);
        return new ThemeDocument
        {
            Name = theme.Name,
            Type = theme.Type,
            Scheme = theme.Scheme,
            Version = version,
            Tokens = new Dictionary<string, string>(theme.DesignTokens, StringComparer.OrdinalIgnoreCase)
        };
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static ThemeDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 262144) throw new InvalidDataException("Theme document exceeds the 256 KiB character limit.");
        var document = JsonSerializer.Deserialize<ThemeDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Theme document cannot be null.");
        document.Validate();
        return document;
    }

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException($"Unsupported theme schema: {SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Name.Any(char.IsControl))
            throw new InvalidDataException("A theme name of 1-128 printable characters is required.");
        if (!System.Version.TryParse(Version, out _)) throw new InvalidDataException("Theme version must be a numeric version such as 1.0.0.");
        if (!Enum.IsDefined(Type) || !Enum.IsDefined(Scheme)) throw new InvalidDataException("Unknown theme type or color scheme.");
        if (Tokens == null || Variables == null || Tokens.Count + Variables.Count > 1024)
            throw new InvalidDataException("Theme documents must contain at most 1024 tokens and variables.");
        if (Tokens.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Tokens.Count
            || Variables.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Variables.Count)
            throw new InvalidDataException("Token and variable names must be unique ignoring case.");
        foreach (var pair in Tokens.Concat(Variables))
        {
            if (!TokenName.IsMatch(pair.Key)) throw new InvalidDataException($"Invalid token or variable name: {pair.Key}.");
            if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 4096 || pair.Value.Any(char.IsControl)
                || pair.Value.IndexOfAny(new[] { ';', '{', '}', '<', '>' }) >= 0)
                throw new InvalidDataException($"Invalid value for {pair.Key}.");
        }
    }

    public Theme Build(Func<string, ITheme?>? resolveBase = null)
    {
        Validate();
        var builder = ThemeBuilder.Create(Name).BasedOn(Type).WithColorScheme(Scheme);
        if (!string.IsNullOrWhiteSpace(BaseTheme))
        {
            var parent = resolveBase?.Invoke(BaseTheme) ?? throw new InvalidDataException($"Base theme '{BaseTheme}' was not found.");
            builder.BasedOn(parent).WithColorScheme(Scheme);
        }
        var variables = new Dictionary<string, string>(Variables, StringComparer.OrdinalIgnoreCase);
        string Resolve(string value, HashSet<string> visiting)
        {
            if (!value.StartsWith("var(", StringComparison.Ordinal) || !value.EndsWith(')')) return value;
            var name = value[4..^1].Trim().TrimStart('-');
            if (!variables.TryGetValue(name, out var variable)) throw new InvalidDataException($"Unknown theme variable: {name}.");
            if (!visiting.Add(name)) throw new InvalidDataException($"Cyclic theme variable: {name}.");
            var resolved = Resolve(variable, visiting);
            visiting.Remove(name);
            return resolved;
        }
        foreach (var (key, value) in Tokens)
            builder.WithToken(key, Resolve(value, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        return builder.Build();
    }

    public static IReadOnlyList<Theme> BuildAll(IEnumerable<ThemeDocument> documents, IEnumerable<ITheme>? baseThemes = null)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var definitions = new Dictionary<string, ThemeDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            document.Validate();
            if (!definitions.TryAdd(document.Name, document)) throw new InvalidDataException($"Duplicate theme: {document.Name}.");
        }
        var built = new Dictionary<string, ITheme>(StringComparer.OrdinalIgnoreCase);
        var external = new Dictionary<string, ITheme>(StringComparer.OrdinalIgnoreCase);
        foreach (var theme in baseThemes ?? Array.Empty<ITheme>()) external[theme.Name] = theme;
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ITheme? Resolve(string name)
        {
            if (built.TryGetValue(name, out var theme)) return theme;
            if (!definitions.TryGetValue(name, out var document)) return external.GetValueOrDefault(name);
            if (!visiting.Add(name)) throw new InvalidDataException($"Cyclic theme inheritance: {name}.");
            var result = document.Build(Resolve);
            visiting.Remove(name);
            built[name] = result;
            return result;
        }
        return definitions.Keys.Select(name => (Theme)Resolve(name)!).ToArray();
    }
}