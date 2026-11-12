namespace EnterpriseUI.Editors.Interactivity;

public enum KeyKind { Character, Shift, CapsLock, Backspace, Enter, Tab, Space, Layer, Close, Clear, Custom }

/// <summary>One key. <see cref="Width"/> is a weight relative to a normal key (1).</summary>
public sealed record KeyDef(string Label, string? ShiftLabel = null, KeyKind Kind = KeyKind.Character, double Width = 1, string? Value = null, string? ShiftValue = null, string? TargetLayer = null, string? Command = null)
{
    public static KeyDef Char(string label, string? shift = null) => new(label, shift);
    public static KeyDef Layer(string label, string targetLayer, double width = 1.5) => new(label, null, KeyKind.Layer, width, TargetLayer: targetLayer);
    public static KeyDef Special(string label, KeyKind kind, double width = 1.5) => new(label, null, kind, width);
    public static KeyDef CommandKey(string label, string command, double width = 1) => new(label, null, KeyKind.Custom, width, Command: command);
}

public sealed record KeyRow(IReadOnlyList<KeyDef> Keys)
{
    public double TotalWidth => Keys.Sum(k => k.Width);
}

public sealed record KeyboardLayer(string Name, IReadOnlyList<KeyRow> Rows);

public sealed class KeyboardLayout
{
    public KeyboardLayout(string name, IEnumerable<KeyboardLayer> layers, string? defaultLayer = null)
    {
        Name = name;
        Layers = layers.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        DefaultLayer = defaultLayer ?? Layers.Keys.First();
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, KeyboardLayer> Layers { get; }
    public string DefaultLayer { get; }

    private static KeyRow Row(params KeyDef[] keys) => new(keys);

    private static IEnumerable<KeyDef> Letters(string letters) => letters.Select(c => KeyDef.Char(c.ToString(), char.ToUpperInvariant(c).ToString()));

    public static KeyboardLayout Qwerty() => new("QWERTY", new[]
    {
        new KeyboardLayer("letters", new[]
        {
            Row(new KeyDef("`", "~"), new KeyDef("1", "!"), new KeyDef("2", "@"), new KeyDef("3", "#"), new KeyDef("4", "$"), new KeyDef("5", "%"), new KeyDef("6", "^"),
                new KeyDef("7", "&"), new KeyDef("8", "*"), new KeyDef("9", "("), new KeyDef("0", ")"), new KeyDef("-", "_"), new KeyDef("=", "+"), KeyDef.Special("⌫", KeyKind.Backspace, 1.6)),
            Row(new[] { KeyDef.Special("Tab", KeyKind.Tab, 1.4) }.Concat(Letters("qwertyuiop")).Concat(new[] { new KeyDef("[", "{"), new KeyDef("]", "}"), new KeyDef("\\", "|", Width: 1.2) }).ToArray()),
            Row(new[] { KeyDef.Special("Caps", KeyKind.CapsLock, 1.7) }.Concat(Letters("asdfghjkl")).Concat(new[] { new KeyDef(";", ":"), new KeyDef("'", "\""), KeyDef.Special("Enter", KeyKind.Enter, 1.9) }).ToArray()),
            Row(new[] { KeyDef.Special("Shift", KeyKind.Shift, 2.2) }.Concat(Letters("zxcvbnm")).Concat(new[] { new KeyDef(",", "<"), new KeyDef(".", ">"), new KeyDef("/", "?"), KeyDef.Special("Shift", KeyKind.Shift, 2.2) }).ToArray()),
            Row(KeyDef.Layer("?123", "symbols", 1.6), KeyDef.Special("Space", KeyKind.Space, 8), KeyDef.Special("Hide", KeyKind.Close, 1.6))
        }),
        new KeyboardLayer("symbols", new[]
        {
            Row(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }.Select(d => KeyDef.Char(d)).Append(KeyDef.Special("⌫", KeyKind.Backspace, 1.6)).ToArray()),
            Row("@#$%&*()-+".Select(c => KeyDef.Char(c.ToString())).ToArray()),
            Row("!?:;\"'/\\_=".Select(c => KeyDef.Char(c.ToString())).Append(KeyDef.Special("Enter", KeyKind.Enter, 1.9)).ToArray()),
            Row("€£¥©®™°±×÷".Select(c => KeyDef.Char(c.ToString())).ToArray()),
            Row(KeyDef.Layer("ABC", "letters", 1.6), KeyDef.Special("Space", KeyKind.Space, 8), KeyDef.Special("Hide", KeyKind.Close, 1.6))
        })
    }, "letters");

    public static KeyboardLayout Numeric() => new("Numeric", new[]
    {
        new KeyboardLayer("pad", new[]
        {
            Row(KeyDef.Char("7"), KeyDef.Char("8"), KeyDef.Char("9"), KeyDef.Special("⌫", KeyKind.Backspace, 1)),
            Row(KeyDef.Char("4"), KeyDef.Char("5"), KeyDef.Char("6"), KeyDef.Special("C", KeyKind.Clear, 1)),
            Row(KeyDef.Char("1"), KeyDef.Char("2"), KeyDef.Char("3"), KeyDef.Special("Enter", KeyKind.Enter, 1)),
            Row(KeyDef.Char("-"), KeyDef.Char("0"), KeyDef.Char("."), KeyDef.Special("Hide", KeyKind.Close, 1))
        })
    });

    /// <summary>Symbol layers for punctuation, currency and math; extend by building a layout with your own <see cref="KeyboardLayer"/>s.</summary>
    public static KeyboardLayout Symbols() => new("Symbols", new[]
    {
        new KeyboardLayer("currency", new[]
        {
            Row("$€£¥₹₽₩₪₺₫".Select(c => KeyDef.Char(c.ToString())).ToArray()),
            Row("%‰°′″§¶©®™".Select(c => KeyDef.Char(c.ToString())).ToArray()),
            Row(KeyDef.Layer("Math", "math", 2), KeyDef.Special("Space", KeyKind.Space, 5), KeyDef.Special("⌫", KeyKind.Backspace, 2), KeyDef.Special("Hide", KeyKind.Close, 2))
        }),
        new KeyboardLayer("math", new[]
        {
            Row("±×÷≠≈≤≥∞√∑".Select(c => KeyDef.Char(c.ToString())).ToArray()),
            Row("αβγδεθλμπσ".Select(c => KeyDef.Char(c.ToString(), char.ToUpperInvariant(c).ToString())).ToArray()),
            Row(KeyDef.Special("Shift", KeyKind.Shift, 2), KeyDef.Layer("Currency", "currency", 2), KeyDef.Special("Space", KeyKind.Space, 3), KeyDef.Special("⌫", KeyKind.Backspace, 2), KeyDef.Special("Hide", KeyKind.Close, 2))
        })
    });
}

public enum KeyAction { None, Insert, Backspace, Enter, Tab, Clear, Close, Command }

public readonly record struct KeyPressResult(KeyAction Action, string Text = "", string? Command = null);

/// <summary>Shift, caps lock and layer state of a keyboard layout; turns key presses into text or editing actions.</summary>
public sealed class VirtualKeyboardState
{
    private KeyboardLayout _layout;
    private string _layer;

    public VirtualKeyboardState(KeyboardLayout layout)
    {
        _layout = layout;
        _layer = layout.DefaultLayer;
    }

    public KeyboardLayout Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            _layer = value.DefaultLayer;
            Shift = false;
            CapsLock = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ActiveLayerName
    {
        get => _layer;
        set
        {
            if (!_layout.Layers.ContainsKey(value) || string.Equals(_layer, value, StringComparison.OrdinalIgnoreCase)) return;
            _layer = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public KeyboardLayer ActiveLayer => _layout.Layers[_layer];

    /// <summary>One-shot shift; cleared after the next character.</summary>
    public bool Shift { get; private set; }
    public bool CapsLock { get; private set; }

    /// <summary>True when letters are currently upper case (shift xor caps lock).</summary>
    public bool Upper => Shift ^ CapsLock;

    public event EventHandler? Changed;

    public string LabelOf(KeyDef key)
    {
        if (key.Kind != KeyKind.Character) return key.Label;
        if (Upper) return key.ShiftLabel ?? key.Label.ToUpperInvariant();
        return key.Label;
    }

    public KeyPressResult Press(KeyDef key)
    {
        switch (key.Kind)
        {
            case KeyKind.Shift:
                Shift = !Shift;
                Changed?.Invoke(this, EventArgs.Empty);
                return new KeyPressResult(KeyAction.None);
            case KeyKind.CapsLock:
                CapsLock = !CapsLock;
                Changed?.Invoke(this, EventArgs.Empty);
                return new KeyPressResult(KeyAction.None);
            case KeyKind.Layer:
                if (key.TargetLayer != null) ActiveLayerName = key.TargetLayer;
                Shift = false;
                Changed?.Invoke(this, EventArgs.Empty);
                return new KeyPressResult(KeyAction.None);
            case KeyKind.Backspace: return new KeyPressResult(KeyAction.Backspace);
            case KeyKind.Enter: return new KeyPressResult(KeyAction.Enter, "\n");
            case KeyKind.Tab: return new KeyPressResult(KeyAction.Tab, "\t");
            case KeyKind.Clear: return new KeyPressResult(KeyAction.Clear);
            case KeyKind.Close: return new KeyPressResult(KeyAction.Close);
            case KeyKind.Space: return new KeyPressResult(KeyAction.Insert, " ");
            case KeyKind.Custom: return new KeyPressResult(KeyAction.Command, string.Empty, key.Command);
        }

        var text = Upper ? key.ShiftValue ?? key.ShiftLabel ?? key.Value?.ToUpperInvariant() ?? key.Label.ToUpperInvariant() : key.Value ?? key.Label;
        if (Shift)
        {
            Shift = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return new KeyPressResult(KeyAction.Insert, text);
    }
}
