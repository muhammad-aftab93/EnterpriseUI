using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Layout;

public enum DockPosition
{
    Left,
    Right,
    Top,
    Bottom,
    Document,
    Floating,
    AutoHide
}

public class DockPanel : ObservableObject
{
    private bool _isVisible = true;

    public string Title { get; set; } = string.Empty;
    public DockPosition Position { get; set; } = DockPosition.Left;
    public double Width { get; set; } = 250;
    public double Height { get; set; } = 200;
    public object? Content { get; set; }
    public bool CanClose { get; set; } = true;
    public bool CanFloat { get; set; } = true;
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
}

/// <summary>
/// Superset Docking and Document Layout Manager inspired by DevExpress DockManager and Telerik RadDocking.
/// </summary>
public class DockManager : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<DockPanel> Panels { get; } = new();

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Dock Manager";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public void Dock(DockPanel panel, DockPosition position)
    {
        panel.Position = position;
        if (!Panels.Contains(panel)) Panels.Add(panel);
        OnPropertyChanged(nameof(Panels));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Superset Modal Dialog and Window control.
/// </summary>
public class Window : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private bool _isOpen;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get => _isOpen; set => SetProperty(ref _isOpen, value); }
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Title { get; set; } = "Dialog";
    public double Width { get; set; } = 500;
    public double Height { get; set; } = 400;
    public bool IsModal { get; set; } = true;
    public bool IsDraggable { get; set; } = true;
    public bool IsResizable { get; set; } = true;
    public object? Content { get; set; }

    public AriaRole Role { get; set; } = AriaRole.Dialog;
    public string? AriaLabel { get; set; } = "Window Dialog";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get => !_isOpen; set => _isOpen = !value; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public void Open() => IsVisible = true;
    public void Close() => IsVisible = false;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

