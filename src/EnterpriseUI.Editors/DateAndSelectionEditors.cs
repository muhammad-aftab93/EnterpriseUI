using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Editors;

/// <summary>
/// Superset DateTime Editor combining Telerik DatePicker / TimePicker and DevExpress DateEdit.
/// </summary>
public class DatePicker : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private DateTime? _value;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public DateTime? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public DateTime MinDate { get; set; } = DateTime.MinValue;
    public DateTime MaxDate { get; set; } = DateTime.MaxValue;
    public string Format { get; set; } = "d";
    public bool ShowTime { get; set; }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.ComboBox;
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

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Superset Dropdown and Lookup Editor combining Telerik ComboBox / DropDownList and DevExpress LookUpEdit / SearchLookUpEdit.
/// </summary>
public class ComboBox<TItem> : ObservableObject, IInteractiveControl, IDataBoundControl<TItem>, IThemeableControl, IAccessibleControl
{
    private TItem? _selectedItem;
    private IEnumerable<TItem>? _dataSource;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string TextField { get; set; } = "Name";
    public string ValueField { get; set; } = "Id";
    public bool AllowFiltering { get; set; } = true;
    public bool AllowCustomValues { get; set; }
    public string? Placeholder { get; set; }

    public IEnumerable<TItem>? DataSource { get => _dataSource; set => SetProperty(ref _dataSource, value); }
    public TItem? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
    public IReadOnlyList<TItem> SelectedItems => _selectedItem != null ? new[] { _selectedItem } : Array.Empty<TItem>();

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.ComboBox;
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

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RefreshDataAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Superset Toggle Switch combining Telerik Switch and DevExpress ToggleSwitch.
/// </summary>
public class Switch : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private bool _isChecked;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public string OnLabel { get; set; } = "On";
    public string OffLabel { get; set; } = "Off";

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.Switch;
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

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
