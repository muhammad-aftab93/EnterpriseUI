using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Layout;

/// <summary>
/// Clean-room Multi-Panel SplitContainer (Telerik RadSplitContainer equivalent).
/// Supports proportional sizing, minimum size constraints, and panel collapse/restore.
/// </summary>
public class SplitContainer : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public sealed class SplitPanelInfo : ObservableObject
    {
        private double _sizeRatio = 0.5;
        private bool _isCollapsed;
        public string Name { get; set; } = string.Empty;
        public double SizeRatio { get => _sizeRatio; set => SetProperty(ref _sizeRatio, Math.Clamp(value, 0.05, 0.95)); }
        public double MinSizePx { get; set; } = 60;
        public bool IsCollapsed { get => _isCollapsed; set => SetProperty(ref _isCollapsed, value); }
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public bool IsVertical { get; set; }
    public int SplitterWidth { get; set; } = 5;
    public List<SplitPanelInfo> Panels { get; } = new()
    {
        new SplitPanelInfo { Name = "Panel1", SizeRatio = 0.5 },
        new SplitPanelInfo { Name = "Panel2", SizeRatio = 0.5 }
    };

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Split Container";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room DataLayout and LayoutControl (Telerik RadDataLayout / RadLayoutControl equivalent).
/// Auto-generates form layout items from any bound entity type via reflection.
/// </summary>
public class DataLayout<TEntity> : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public sealed record DataLayoutFieldItem(string PropertyName, string Label, Type PropertyType, string EditorKind, bool IsReadOnly);

    private TEntity? _currentEntity;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public int ColumnCount { get; set; } = 2;
    public TEntity? CurrentEntity
    {
        get => _currentEntity;
        set
        {
            if (SetProperty(ref _currentEntity, value))
            {
                AutoGenerateFields();
            }
        }
    }

    public List<DataLayoutFieldItem> Fields { get; } = new();

    public DataLayout()
    {
        AutoGenerateFields();
    }

    public void AutoGenerateFields()
    {
        Fields.Clear();
        foreach (var prop in typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            string editor = t == typeof(bool) ? "Switch"
                : t == typeof(DateTime) || t == typeof(DateTimeOffset) ? "DatePicker"
                : t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double) ? "NumericTextBox"
                : t.IsEnum ? "ComboBox"
                : "TextBox";

            Fields.Add(new DataLayoutFieldItem(prop.Name, SplitCamelCase(prop.Name), prop.PropertyType, editor, !prop.CanWrite));
        }
    }

    private static string SplitCamelCase(string input)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
    }

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Data Layout Form";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room Multi-Page Wizard Control (Telerik RadWizard equivalent).
/// Supports Welcome, Standard, and Completion pages with validation-gated Next/Previous/Finish transitions.
/// </summary>
public class WizardControl : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public sealed class WizardPage : ObservableObject
    {
        public string Title { get; set; } = string.Empty;
        public string HeaderDescription { get; set; } = string.Empty;
        public bool IsWelcomePage { get; set; }
        public bool IsCompletionPage { get; set; }
        public Func<bool>? ValidateStep { get; set; }
        public object? Content { get; set; }
    }

    private int _selectedPageIndex;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<WizardPage> Pages { get; } = new();
    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set => SetProperty(ref _selectedPageIndex, Pages.Count == 0 ? 0 : Math.Clamp(value, 0, Pages.Count - 1));
    }

    public WizardPage? SelectedPage => Pages.Count > 0 ? Pages[SelectedPageIndex] : null;
    public bool CanMoveNext => SelectedPageIndex < Pages.Count - 1 && (SelectedPage?.ValidateStep?.Invoke() ?? true);
    public bool CanMovePrevious => SelectedPageIndex > 0;
    public bool IsCompleted { get; private set; }

    public event Action<WizardPage>? PageChanged;
    public event Action? Finished;

    public bool MoveNext()
    {
        if (!CanMoveNext) return false;
        SelectedPageIndex++;
        if (SelectedPage != null) PageChanged?.Invoke(SelectedPage);
        return true;
    }

    public bool MovePrevious()
    {
        if (!CanMovePrevious) return false;
        SelectedPageIndex--;
        if (SelectedPage != null) PageChanged?.Invoke(SelectedPage);
        return true;
    }

    public void Finish()
    {
        if (SelectedPage?.ValidateStep?.Invoke() ?? true)
        {
            IsCompleted = true;
            Finished?.Invoke();
        }
    }

    public AriaRole Role { get; set; } = AriaRole.Dialog;
    public string? AriaLabel { get; set; } = "Setup Wizard";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Clean-room CollapsiblePanel and TaskDialog (Telerik RadCollapsiblePanel / RadTaskDialog equivalents).
/// </summary>
public class CollapsiblePanel : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private bool _isExpanded = true;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string HeaderText { get; set; } = "Details Section";
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                AriaExpanded = value;
            }
        }
    }

    public void Toggle() => IsExpanded = !IsExpanded;

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get => HeaderText; set => HeaderText = value ?? string.Empty; }
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; } = true;
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class TaskDialog : ObservableObject
{
    public sealed record CommandLinkOption(string Id, string Title, string Description);

    public string WindowTitle { get; set; } = "EnterpriseUI Task Dialog";
    public string MainInstruction { get; set; } = "Select deployment strategy";
    public string Content { get; set; } = string.Empty;
    public string VerificationText { get; set; } = "Do not ask again for this workspace";
    public bool IsVerificationChecked { get; set; }
    public string ExpandedInformation { get; set; } = string.Empty;
    public List<CommandLinkOption> CommandLinks { get; } = new();
    public string? SelectedCommandId { get; set; }
}
