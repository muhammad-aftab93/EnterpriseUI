using System.Globalization;
using System.Text;

namespace EnterpriseUI.Framework;

public enum SelectorCombinator { None, Descendant, Child }

/// <summary>Attribute test of a selector, such as <c>[mode=dark]</c> or <c>[readonly]</c>.</summary>
public sealed record AttributeTest(string Name, string? Value);

/// <summary>One compound selector: <c>Type#id.class:state[attr=value]</c>.</summary>
public sealed class CompoundSelector
{
    public string? TypeName { get; init; }
    public string? Id { get; init; }
    public IReadOnlyList<string> Classes { get; init; } = Array.Empty<string>();
    public ElementStates States { get; init; }
    public IReadOnlyList<AttributeTest> Attributes { get; init; } = Array.Empty<AttributeTest>();

    public bool Matches(EnterpriseElement element)
    {
        if (TypeName is not null && TypeName != "*" && !string.Equals(TypeName, element.TypeName, StringComparison.OrdinalIgnoreCase)) return false;
        if (Id is not null && !string.Equals(Id, element.Id, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var name in Classes) if (!element.HasClass(name)) return false;
        if (!element.HasState(States)) return false;
        foreach (var test in Attributes)
        {
            var actual = element.GetAttribute(test.Name);
            if (actual is null || (test.Value is not null && !string.Equals(actual, test.Value, StringComparison.OrdinalIgnoreCase))) return false;
        }

        return true;
    }
}

/// <summary>A complex selector (compound selectors joined by descendant or child combinators) with CSS style specificity.</summary>
public sealed class StyleSelector
{
    private readonly List<(CompoundSelector Compound, SelectorCombinator Combinator)> _parts;

    private StyleSelector(List<(CompoundSelector, SelectorCombinator)> parts, string text)
    {
        _parts = parts;
        Text = text;
        var ids = 0;
        var classes = 0;
        var types = 0;
        foreach (var (compound, _) in parts)
        {
            if (compound.Id is not null) ids++;
            classes += compound.Classes.Count + compound.Attributes.Count + CountBits((int)compound.States);
            if (compound.TypeName is not null && compound.TypeName != "*") types++;
        }

        Specificity = ids * 10000 + classes * 100 + types;
    }

    public string Text { get; }

    public int Specificity { get; }

    public static StyleSelector Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("A selector cannot be empty.");
        var parts = new List<(CompoundSelector, SelectorCombinator)>();
        var i = 0;
        var combinator = SelectorCombinator.None;
        var source = text.Trim();
        while (i < source.Length)
        {
            var spaced = false;
            while (i < source.Length && char.IsWhiteSpace(source[i])) { i++; spaced = true; }
            if (i >= source.Length) break;
            if (source[i] == '>')
            {
                combinator = SelectorCombinator.Child;
                i++;
                continue;
            }

            if (parts.Count > 0 && combinator == SelectorCombinator.None) combinator = spaced ? SelectorCombinator.Descendant : throw new FormatException($"Unexpected character '{source[i]}' in selector '{text}'.");
            parts.Add((ParseCompound(source, ref i, text), combinator));
            combinator = SelectorCombinator.None;
        }

        if (parts.Count == 0) throw new FormatException("A selector cannot be empty.");
        return new StyleSelector(parts, source);
    }

    private static CompoundSelector ParseCompound(string s, ref int i, string full)
    {
        string? type = null, id = null;
        var classes = new List<string>();
        var attrs = new List<AttributeTest>();
        var states = ElementStates.None;
        var start = i;
        if (i < s.Length && (char.IsLetter(s[i]) || s[i] == '*' || s[i] == '_'))
        {
            var begin = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '*' or '_' or '-')) i++;
            type = s[begin..i];
        }

        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '>')
        {
            var marker = s[i++];
            switch (marker)
            {
                case '#': id = ReadIdent(s, ref i, full); break;
                case '.': classes.Add(ReadIdent(s, ref i, full)); break;
                case ':': states |= ParseState(ReadIdent(s, ref i, full), full); break;
                case '[':
                {
                    var end = s.IndexOf(']', i);
                    if (end < 0) throw new FormatException($"Missing ']' in selector '{full}'.");
                    var body = s[i..end];
                    i = end + 1;
                    var eq = body.IndexOf('=');
                    attrs.Add(eq < 0 ? new AttributeTest(body.Trim(), null) : new AttributeTest(body[..eq].Trim(), body[(eq + 1)..].Trim().Trim('"', '\'')));
                    break;
                }
                default: throw new FormatException($"Unexpected character '{marker}' in selector '{full}'.");
            }
        }

        if (i == start) throw new FormatException($"Invalid selector '{full}'.");
        return new CompoundSelector { TypeName = type, Id = id, Classes = classes, States = states, Attributes = attrs };
    }

    private static string ReadIdent(string s, ref int i, string full)
    {
        var begin = i;
        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '-')) i++;
        if (begin == i) throw new FormatException($"Expected a name in selector '{full}'.");
        return s[begin..i];
    }

    private static ElementStates ParseState(string name, string full) => name.ToLowerInvariant() switch
    {
        "hover" => ElementStates.Hover,
        "focus" => ElementStates.Focus,
        "pressed" or "active" => ElementStates.Pressed,
        "disabled" => ElementStates.Disabled,
        "selected" => ElementStates.Selected,
        "checked" => ElementStates.Checked,
        _ => throw new FormatException($"Unknown pseudo-class ':{name}' in selector '{full}'.")
    };

    public bool Matches(EnterpriseElement element) => MatchFrom(_parts.Count - 1, element);

    private bool MatchFrom(int index, EnterpriseElement element)
    {
        var (compound, combinator) = _parts[index];
        if (!compound.Matches(element)) return false;
        if (index == 0) return true;
        switch (combinator)
        {
            case SelectorCombinator.Child:
                return element.Parent is not null && MatchFrom(index - 1, element.Parent);
            default:
                for (var ancestor = element.Parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    if (MatchFrom(index - 1, ancestor)) return true;
                }

                return false;
        }
    }

    private static int CountBits(int value)
    {
        var count = 0;
        for (; value != 0; value &= value - 1) count++;
        return count;
    }

    public override string ToString() => Text;
}

public sealed record StyleDeclaration(string Name, string Value, bool Important);

/// <summary>A selector with its declarations; <see cref="Order"/> breaks specificity ties (later wins).</summary>
public sealed class StyleRule
{
    public StyleRule(StyleSelector selector, IReadOnlyList<StyleDeclaration> declarations, int order)
    {
        Selector = selector;
        Declarations = declarations;
        Order = order;
    }

    public StyleSelector Selector { get; }
    public IReadOnlyList<StyleDeclaration> Declarations { get; }
    public int Order { get; internal set; }
}

/// <summary>Ordered list of rules; parse CSS-like text with <see cref="Parse"/>.</summary>
public sealed class StyleSheet
{
    private readonly List<StyleRule> _rules = new();

    public IReadOnlyList<StyleRule> Rules => _rules;

    public StyleRule Add(string selector, params (string Name, string Value)[] declarations)
    {
        var rule = new StyleRule(StyleSelector.Parse(selector), declarations.Select(d => new StyleDeclaration(d.Name.Trim().ToLowerInvariant(), d.Value.Trim(), false)).ToList(), _rules.Count);
        _rules.Add(rule);
        return rule;
    }

    /// <summary>Parses rules such as <c>Panel &gt; Button.primary:hover { background: #0a64ad; padding: 4 8 !important; }</c>; comma separated selectors share the declarations.</summary>
    public static StyleSheet Parse(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var sheet = new StyleSheet();
        var text = StripComments(css);
        var i = 0;
        while (true)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) break;
            var open = text.IndexOf('{', i);
            if (open < 0) throw new FormatException($"Expected '{{' after selector at position {i}.");
            var close = text.IndexOf('}', open);
            if (close < 0) throw new FormatException($"Missing '}}' for the rule starting at position {open}.");
            var selectors = text[i..open].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var declarations = ParseDeclarations(text[(open + 1)..close]);
            foreach (var selector in selectors)
                sheet._rules.Add(new StyleRule(StyleSelector.Parse(selector), declarations, sheet._rules.Count));
            i = close + 1;
        }

        return sheet;
    }

    private static List<StyleDeclaration> ParseDeclarations(string body)
    {
        var list = new List<StyleDeclaration>();
        foreach (var raw in SplitDeclarations(body))
        {
            var colon = raw.IndexOf(':');
            if (colon <= 0) throw new FormatException($"Invalid declaration '{raw.Trim()}'.");
            var name = raw[..colon].Trim().ToLowerInvariant();
            var value = raw[(colon + 1)..].Trim();
            var important = false;
            if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
            {
                important = true;
                value = value[..^"!important".Length].Trim();
            }

            list.Add(new StyleDeclaration(name, value, important));
        }

        return list;
    }

    private static IEnumerable<string> SplitDeclarations(string body)
    {
        var depth = 0;
        var current = new StringBuilder();
        foreach (var ch in body)
        {
            if (ch == '(') depth++;
            else if (ch == ')') depth = Math.Max(0, depth - 1);
            if (ch == ';' && depth == 0)
            {
                if (current.ToString().Trim().Length > 0) yield return current.ToString();
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.ToString().Trim().Length > 0) yield return current.ToString();
    }

    private static string StripComments(string css)
    {
        var builder = new StringBuilder(css.Length);
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) throw new FormatException("Unterminated comment.");
                i = end + 1;
                continue;
            }

            builder.Append(css[i]);
        }

        return builder.ToString();
    }
}

public readonly record struct StyleColor(byte A, byte R, byte G, byte B)
{
    public static bool TryParse(string? text, out StyleColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length == 6) hex += "FF";
            if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number)) return false;
            color = new StyleColor((byte)(number & 0xFF), (byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8));
            return true;
        }

        var open = value.IndexOf('(');
        if (open > 0 && value.EndsWith(')') && (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)))
        {
            var args = value[(open + 1)..^1].Split(',').Select(a => a.Trim()).ToArray();
            if (args.Length is not (3 or 4)) return false;
            if (!byte.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var r) || !byte.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var g) ||
                !byte.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var b)) return false;
            byte a = 255;
            if (args.Length == 4)
            {
                if (!double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha)) return false;
                a = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
            }

            color = new StyleColor(a, r, g, b);
            return true;
        }

        switch (value.ToLowerInvariant())
        {
            case "transparent": color = new StyleColor(0, 0, 0, 0); return true;
            case "black": color = new StyleColor(255, 0, 0, 0); return true;
            case "white": color = new StyleColor(255, 255, 255, 255); return true;
            case "red": color = new StyleColor(255, 255, 0, 0); return true;
            case "green": color = new StyleColor(255, 0, 128, 0); return true;
            case "blue": color = new StyleColor(255, 0, 0, 255); return true;
            default: return false;
        }
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{(A == 255 ? string.Empty : A.ToString("X2", CultureInfo.InvariantCulture))}";
}

public readonly record struct StyleLength(double Value, string Unit)
{
    public static bool TryParse(string? text, out StyleLength length)
    {
        length = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();
        var split = value.Length;
        while (split > 0 && char.IsLetter(value[split - 1]) || split > 0 && value[split - 1] == '%') split--;
        if (!double.TryParse(value[..split], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
        var unit = value[split..].ToLowerInvariant();
        if (unit.Length == 0) unit = "px";
        if (unit is not ("px" or "pt" or "em" or "%")) return false;
        length = new StyleLength(number, unit);
        return true;
    }

    /// <summary>Converts to device-independent pixels; <paramref name="basis"/> is the reference size for % and em.</summary>
    public double ToPixels(double basis = 16) => Unit switch
    {
        "pt" => Value * 96d / 72d,
        "em" => Value * basis,
        "%" => Value / 100d * basis,
        _ => Value
    };
}

public readonly record struct StyleThickness(double Left, double Top, double Right, double Bottom)
{
    public static bool TryParse(string? text, out StyleThickness thickness)
    {
        thickness = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!StyleLength.TryParse(parts[i], out var length)) return false;
            values[i] = length.ToPixels();
        }

        switch (values.Length)
        {
            case 1: thickness = new StyleThickness(values[0], values[0], values[0], values[0]); return true;
            case 2: thickness = new StyleThickness(values[1], values[0], values[1], values[0]); return true;
            case 4: thickness = new StyleThickness(values[3], values[0], values[1], values[2]); return true;
            default: return false;
        }
    }
}

/// <summary>Result of the cascade for one element: property values after specificity, importance, inheritance and <c>var()</c> substitution.</summary>
public sealed class ComputedStyle
{
    private readonly Dictionary<string, string> _values;

    internal ComputedStyle(Dictionary<string, string> values) => _values = values;

    public IReadOnlyDictionary<string, string> Values => _values;

    public string? Get(string name) => _values.TryGetValue(name.ToLowerInvariant(), out var value) ? value : null;

    public string GetOrDefault(string name, string fallback) => Get(name) ?? fallback;

    public bool TryGetColor(string name, out StyleColor color) => StyleColor.TryParse(Get(name), out color);

    public bool TryGetLength(string name, out StyleLength length) => StyleLength.TryParse(Get(name), out length);

    public bool TryGetThickness(string name, out StyleThickness thickness) => StyleThickness.TryParse(Get(name), out thickness);

    public bool TryGetNumber(string name, out double number) => double.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}

/// <summary>Runs the cascade over one or more style sheets; results are cached until the tree or the sheets change.</summary>
public sealed class StyleResolver
{
    private static readonly HashSet<string> Inherited = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "font-family", "font-size", "font-weight", "font-style", "line-height", "cursor", "visibility", "text-align"
    };

    private readonly List<StyleSheet> _sheets = new();
    private readonly Dictionary<EnterpriseElement, ComputedStyle> _cache = new();
    private EnterpriseElement? _watched;

    public IReadOnlyList<StyleSheet> Sheets => _sheets;

    public void AddSheet(StyleSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        _sheets.Add(sheet);
        Invalidate();
    }

    public bool RemoveSheet(StyleSheet sheet)
    {
        var removed = _sheets.Remove(sheet);
        if (removed) Invalidate();
        return removed;
    }

    /// <summary>Clears cached results automatically whenever anything under <paramref name="root"/> changes.</summary>
    public void Watch(EnterpriseElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_watched is not null) _watched.Changed -= OnTreeChanged;
        _watched = root;
        root.Changed += OnTreeChanged;
    }

    private void OnTreeChanged(object? sender, ElementChangedEventArgs e) => Invalidate();

    public void Invalidate() => _cache.Clear();

    public ComputedStyle Resolve(EnterpriseElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_cache.TryGetValue(element, out var cached)) return cached;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parent = element.Parent is null ? null : Resolve(element.Parent);
        if (parent is not null)
        {
            foreach (var pair in parent.Values)
            {
                if (Inherited.Contains(pair.Key) || pair.Key.StartsWith("--", StringComparison.Ordinal)) values[pair.Key] = pair.Value;
            }
        }

        var matched = _sheets.SelectMany((sheet, sheetIndex) => sheet.Rules.Select(rule => (rule, sheetIndex)))
            .Where(item => item.rule.Selector.Matches(element))
            .ToList();
        var applied = matched
            .SelectMany(item => item.rule.Declarations.Select(d => (Declaration: d, item.rule.Selector.Specificity, Order: item.sheetIndex * 100000 + item.rule.Order)))
            .OrderBy(x => x.Declaration.Important)
            .ThenBy(x => x.Specificity)
            .ThenBy(x => x.Order);
        foreach (var entry in applied) values[entry.Declaration.Name] = entry.Declaration.Value;

        foreach (var key in values.Keys.ToList()) values[key] = Substitute(values[key], values, 0);
        var computed = new ComputedStyle(values);
        _cache[element] = computed;
        return computed;
    }

    private static string Substitute(string value, Dictionary<string, string> values, int depth)
    {
        const string marker = "var(";
        var start = value.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0 || depth > 8) return value;
        var level = 0;
        var end = -1;
        for (var i = start + marker.Length - 1; i < value.Length; i++)
        {
            if (value[i] == '(') level++;
            else if (value[i] == ')' && --level == 0) { end = i; break; }
        }

        if (end < 0) return value;
        var body = value[(start + marker.Length)..end];
        var comma = body.IndexOf(',');
        var name = (comma < 0 ? body : body[..comma]).Trim();
        var fallback = comma < 0 ? string.Empty : body[(comma + 1)..].Trim();
        var resolved = values.TryGetValue(name, out var found) && !found.Contains("var(" + name, StringComparison.Ordinal) ? found : fallback;
        return Substitute(value[..start] + resolved + value[(end + 1)..], values, depth + 1);
    }
}
