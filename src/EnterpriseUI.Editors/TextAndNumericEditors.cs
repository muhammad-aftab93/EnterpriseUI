using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Editors;

/// <summary>
/// Superset Text Editor.
/// </summary>
public class TextBox : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private string _text = string.Empty;
    private bool _isReadOnly;
    private bool _isEnabled = true;
    private bool _isVisible = true;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string? Placeholder { get; set; }
    public bool ShowClearButton { get; set; } = true;
    public bool IsPassword { get; set; }
    public int MaxLength { get; set; } = int.MaxValue;
    public string? PrefixIcon { get; set; }
    public string? SuffixIcon { get; set; }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get => _isReadOnly; set => SetProperty(ref _isReadOnly, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.TextBox;
    public string? AriaLabel { get; set; }
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }

    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync()
    {
        IsFocused = true;
        OnPropertyChanged(nameof(IsFocused));
        return ValueTask.FromResult(true);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public enum NumericFormat
{
    Standard,
    Currency,
    Percentage,
    Scientific
}

/// <summary>
/// Superset Numeric Editor.
/// </summary>
public class EnterpriseNumericTextBox : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private decimal? _value;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public decimal? Value
    {
        get => _value;
        set => SetProperty(ref _value, Clamp(value));
    }

    public decimal Min { get; set; } = decimal.MinValue;
    public decimal Max { get; set; } = decimal.MaxValue;
    public decimal Step { get; set; } = 1;
    public int Decimals { get; set; } = 2;
    public NumericFormat Format { get; set; } = NumericFormat.Standard;
    public bool ShowSpinButtons { get; set; } = true;

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.SpinButton;
    public string? AriaLabel { get; set; }
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }

    public string? ThemeOverride { get; set; }

    public void StepUp() => Value = (Value ?? 0) + Step;
    public void StepDown() => Value = (Value ?? 0) - Step;

    private decimal? Clamp(decimal? val)
    {
        if (val == null) return null;
        if (val < Min) return Min;
        if (val > Max) return Max;
        return val;
    }

    public ValueTask<bool> FocusAsync()
    {
        IsFocused = true;
        OnPropertyChanged(nameof(IsFocused));
        return ValueTask.FromResult(true);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

