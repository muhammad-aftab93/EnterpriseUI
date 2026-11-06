using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Gauges;

public class GaugeRange
{
    public double From { get; set; }
    public double To { get; set; }
    public string Color { get; set; } = "#107c10";
}

/// <summary>
/// Superset Gauge component.
/// </summary>
public class EnterpriseRadialGauge : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private double _value;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public double Value { get => _value; set => SetProperty(ref _value, value); }
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double StartAngle { get; set; } = -120;
    public double EndAngle { get; set; } = 120;
    public string Unit { get; set; } = "%";
    public List<GaugeRange> Ranges { get; } = new();

    public AriaRole Role { get; set; } = AriaRole.ProgressBar;
    public string? AriaLabel { get; set; } = "Radial Gauge";
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

public class EnterpriseLinearGauge : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private double _value;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public double Value { get => _value; set => SetProperty(ref _value, value); }
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public bool IsVertical { get; set; } = true;
    public List<GaugeRange> Ranges { get; } = new();

    public AriaRole Role { get; set; } = AriaRole.ProgressBar;
    public string? AriaLabel { get; set; } = "Linear Gauge";
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
