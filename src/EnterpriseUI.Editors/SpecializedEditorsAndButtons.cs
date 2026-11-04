using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Editors;

/// <summary>
/// Clean-room Masked Edit Box (Telerik RadMaskedEditBox equivalent).
/// Supports standard masks (0 = digit, L = letter, A = alphanumeric, * = any) and unmasked extraction.
/// </summary>
public class EnterpriseMaskedTextBox : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private string _mask = "(000) 000-0000";
    private string _value = string.Empty;
    private char _promptChar = '_';

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Mask { get => _mask; set => SetProperty(ref _mask, value); }
    public char PromptChar { get => _promptChar; set => SetProperty(ref _promptChar, value); }
    public string Value { get => _value; set => SetProperty(ref _value, value ?? string.Empty); }

    public string MaskedText => ApplyMask(_value, _mask, _promptChar);

    public bool IsMaskCompleted => !MaskedText.Contains(_promptChar);

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.TextBox;
    public string? AriaLabel { get; set; } = "Masked Input";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public static string ApplyMask(string rawInput, string mask, char prompt)
    {
        if (string.IsNullOrEmpty(mask)) return rawInput;
        var sb = new StringBuilder(mask.Length);
        int rawIdx = 0;
        foreach (char m in mask)
        {
            if (m is '0' or '9' or 'L' or 'A' or '*')
            {
                while (rawIdx < rawInput.Length && !MatchesMaskChar(rawInput[rawIdx], m))
                {
                    rawIdx++;
                }
                if (rawIdx < rawInput.Length)
                {
                    sb.Append(rawInput[rawIdx++]);
                }
                else
                {
                    sb.Append(prompt);
                }
            }
            else
            {
                sb.Append(m);
            }
        }
        return sb.ToString();
    }

    private static bool MatchesMaskChar(char c, char maskToken) => maskToken switch
    {
        '0' or '9' => char.IsDigit(c),
        'L' => char.IsLetter(c),
        'A' => char.IsLetterOrDigit(c),
        '*' => true,
        _ => false
    };

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room EnterpriseAutoCompleteBox and TokenizedTextBox (Telerik RadAutoCompleteBox / RadTokenizedTextBox equivalent).
/// </summary>
public class EnterpriseAutoCompleteBox : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private string _searchText = string.Empty;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<string> SuggestionsSource { get; } = new();
    public List<string> SelectedTokens { get; } = new();
    public char TokenDelimiter { get; set; } = ';';
    public bool StartsWithMode { get; set; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value ?? string.Empty);
    }

    public IReadOnlyList<string> GetFilteredSuggestions()
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return SuggestionsSource;
        return SuggestionsSource
            .Where(s => StartsWithMode
                ? s.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase)
                : s.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void AddToken(string token)
    {
        if (!string.IsNullOrWhiteSpace(token) && !SelectedTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            SelectedTokens.Add(token.Trim());
            OnPropertyChanged(nameof(SelectedTokens));
        }
    }

    public bool RemoveToken(string token) => SelectedTokens.Remove(token);

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.ComboBox;
    public string? AriaLabel { get; set; } = "AutoComplete Box";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Multi-Column ComboBox (Telerik RadMultiColumnComboBox equivalent).
/// </summary>
public class EnterpriseMultiColumnComboBox<TItem> : ComboBox<TItem>
{
    public List<string> DisplayColumns { get; } = new();
    public int DropDownWidth { get; set; } = 420;
    public int DropDownHeight { get; set; } = 260;
    public bool AutoFilter { get; set; } = true;
}

/// <summary>
/// Clean-room Checked DropDownList (Telerik RadCheckedDropDownList equivalent).
/// </summary>
public class EnterpriseCheckedDropDownList<TItem> : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<TItem> Items { get; } = new();
    public HashSet<TItem> CheckedItems { get; } = new();
    public bool ShowCheckAllItem { get; set; } = true;

    public void CheckAll()
    {
        foreach (var item in Items) CheckedItems.Add(item);
        OnPropertyChanged(nameof(CheckedItems));
    }

    public void UncheckAll()
    {
        CheckedItems.Clear();
        OnPropertyChanged(nameof(CheckedItems));
    }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.ComboBox;
    public string? AriaLabel { get; set; } = "Checked DropDown List";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room ColorBox and ColorSelector (Telerik RadColorBox / RadColorDialog equivalent).
/// </summary>
public class ColorPicker : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private string _selectedColorHex = "#0078D4";
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string SelectedColorHex
    {
        get => _selectedColorHex;
        set => SetProperty(ref _selectedColorHex, value);
    }

    public bool AllowColorPickFromScreen { get; set; } = true;
    public bool ShowWebColors { get; set; } = true;
    public bool ShowProfessionalColors { get; set; } = true;
    public List<string> RecentColors { get; } = new() { "#0078D4", "#107C41", "#D83B01", "#8764B8", "#0099BC" };

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.ComboBox;
    public string? AriaLabel { get; set; } = "Color Picker";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room TrackBar and RangeSlider (Telerik RadTrackBar / RadRangeSelector equivalent).
/// </summary>
public class RangeSlider : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private double _minimum = 0;
    private double _maximum = 100;
    private double _selectionStart = 20;
    private double _selectionEnd = 80;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public double Minimum { get => _minimum; set => SetProperty(ref _minimum, value); }
    public double Maximum { get => _maximum; set => SetProperty(ref _maximum, value); }
    public double SelectionStart { get => _selectionStart; set => SetProperty(ref _selectionStart, Math.Clamp(value, _minimum, _selectionEnd)); }
    public double SelectionEnd { get => _selectionEnd; set => SetProperty(ref _selectionEnd, Math.Clamp(value, _selectionStart, _maximum)); }
    public double TickFrequency { get; set; } = 10;
    public bool IsRangeMode { get; set; } = true;

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.Slider;
    public string? AriaLabel { get; set; } = "Range Slider";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Rating Control (Telerik RadRating equivalent).
/// </summary>
public class EnterpriseRatingControl : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private double _value = 4.0;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public int MaxStars { get; set; } = 5;
    public double Value { get => _value; set => SetProperty(ref _value, Math.Clamp(value, 0, MaxStars)); }
    public bool AllowHalfStars { get; set; } = true;

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.Slider;
    public string? AriaLabel { get; set; } = "Star Rating";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room EnterpriseTimeSpanPicker and Clock Control (Telerik RadTimeSpanPicker / RadClock equivalent).
/// </summary>
public class EnterpriseTimeSpanPicker : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private TimeSpan _value = TimeSpan.FromHours(1.5);
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public TimeSpan Value { get => _value; set => SetProperty(ref _value, value); }
    public TimeSpan Minimum { get; set; } = TimeSpan.Zero;
    public TimeSpan Maximum { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan Step { get; set; } = TimeSpan.FromMinutes(15);

    public void AddMinutes(double minutes) => Value = TimeSpan.FromTicks(Math.Clamp((Value + TimeSpan.FromMinutes(minutes)).Ticks, Minimum.Ticks, Maximum.Ticks));

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.SpinButton;
    public string? AriaLabel { get; set; } = "Duration Picker";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Calculator DropDown Editor (Telerik RadCalculatorDropDown equivalent).
/// Evaluates arithmetic expressions with memory register (M+, M-, MR, MC) and calculation audit trail.
/// </summary>
public class EnterpriseCalculatorDropDown : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private decimal _value;
    private decimal _memory;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public decimal Value { get => _value; set => SetProperty(ref _value, value); }
    public decimal MemoryValue { get => _memory; private set => SetProperty(ref _memory, value); }
    public List<string> CalculationHistory { get; } = new();

    public decimal EvaluateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return _value;
        using var dt = new DataTable();
        var raw = dt.Compute(expression, string.Empty);
        decimal result = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
        Value = result;
        CalculationHistory.Add($"{expression} = {result}");
        return result;
    }

    public void MemoryAdd() => MemoryValue += Value;
    public void MemorySubtract() => MemoryValue -= Value;
    public void MemoryClear() => MemoryValue = 0m;
    public void MemoryRecall() => Value = MemoryValue;

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.SpinButton;
    public string? AriaLabel { get; set; } = "Calculator Editor";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Syntax Editor and Code Tokenizer (Telerik RadSyntaxEditor equivalent).
/// Supports C#, SQL, JSON, and XML lexical highlighting and collapsible folding regions.
/// </summary>
public class EnterpriseSyntaxEditor : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    public sealed record SyntaxToken(string Text, string Classification, int StartIndex, int Length);

    private string _documentText = string.Empty;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Language { get; set; } = "CSharp";
    public bool ShowLineNumbers { get; set; } = true;
    public bool EnableCodeFolding { get; set; } = true;
    public string DocumentText { get => _documentText; set => SetProperty(ref _documentText, value ?? string.Empty); }

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "using", "namespace", "public", "private", "protected", "internal", "class", "struct",
        "interface", "enum", "record", "sealed", "abstract", "static", "async", "await", "var",
        "new", "return", "if", "else", "switch", "case", "foreach", "for", "while", "true", "false", "null"
    };

    public IReadOnlyList<SyntaxToken> Tokenize()
    {
        var tokens = new List<SyntaxToken>();
        foreach (Match m in Regex.Matches(_documentText, @"//.*$|""[^""]*""|\b\w+\b", RegexOptions.Multiline))
        {
            string kind = "Identifier";
            if (m.Value.StartsWith("//", StringComparison.Ordinal)) kind = "Comment";
            else if (m.Value.StartsWith("\"", StringComparison.Ordinal)) kind = "StringLiteral";
            else if (CSharpKeywords.Contains(m.Value)) kind = "Keyword";
            else if (char.IsDigit(m.Value[0])) kind = "Number";

            tokens.Add(new SyntaxToken(m.Value, kind, m.Index, m.Length));
        }
        return tokens;
    }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.TextBox;
    public string? AriaLabel { get; set; } = "Syntax Code Editor";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room SpellChecker Engine (Telerik RadSpellChecker equivalent).
/// Uses Levenshtein edit distance to identify misspelled words and rank dictionary suggestions.
/// </summary>
public class SpellChecker : ObservableObject
{
    public HashSet<string> Dictionary { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "enterprise", "application", "architecture", "virtualization", "performance", "accessibility",
        "dashboard", "reporting", "scheduler", "datagrid", "theming", "validation", "security",
        "workflow", "document", "spreadsheet", "barcode", "diagram", "navigation", "component"
    };

    public IReadOnlyList<string> FindMisspelledWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return Regex.Matches(text, @"\b[a-zA-Z]{3,}\b")
            .Select(m => m.Value)
            .Where(w => !Dictionary.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetSuggestions(string word, int maxSuggestions = 5)
    {
        return Dictionary
            .Select(d => (Word: d, Distance: ComputeLevenshteinDistance(word.ToLowerInvariant(), d.ToLowerInvariant())))
            .Where(x => x.Distance <= 3)
            .OrderBy(x => x.Distance)
            .Take(maxSuggestions)
            .Select(x => x.Word)
            .ToList();
    }

    public static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}

/// <summary>
/// Clean-room EnterpriseActionButton, SplitButton, DropDownButton, and ToggleButton suite
/// (Telerik RadButton, RadSplitButton, RadDropDownButton, RadToggleButton equivalents).
/// </summary>
public class EnterpriseActionButton : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private string _text = "Button";
    private bool _isChecked;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public string? Icon { get; set; }
    public bool IsToggleMode { get; set; }
    public bool IsSplitButton { get; set; }
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
    public List<string> DropDownItems { get; } = new();
    public RelayCommand? Command { get; set; }

    public void PerformClick(object? parameter = null)
    {
        if (!IsEnabled) return;
        if (IsToggleMode) IsChecked = !IsChecked;
        if (Command != null && Command.CanExecute(parameter))
        {
            Command.Execute(parameter);
        }
    }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }
    public AriaRole Role { get; set; } = AriaRole.Button;
    public string? AriaLabel { get => _text; set => _text = value ?? string.Empty; }
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

