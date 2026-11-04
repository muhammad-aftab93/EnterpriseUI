using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Charts;

public enum SeriesType
{
    Bar,
    Column,
    Line,
    Spline,
    Area,
    SplineArea,
    Pie,
    Donut,
    Scatter,
    Bubble,
    Candlestick,
    Ohlc,
    Radar,
    Polar,
    Funnel,
    Waterfall,
    Treemap,
    Sankey
}

public class ChartDataPoint
{
    public object? Category { get; set; }
    public double Value { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? Open { get; set; }
    public double? Close { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
}

public class ChartSeries : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public SeriesType Type { get; set; } = SeriesType.Column;
    public List<ChartDataPoint> DataPoints { get; set; } = new();
    public string? Color { get; set; }
    public bool Visible { get; set; } = true;
}

public enum AxisType
{
    Categorical,
    Numeric,
    DateTime,
    Logarithmic
}

public class ChartAxis
{
    public string Title { get; set; } = string.Empty;
    public AxisType Type { get; set; } = AxisType.Categorical;
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool ShowGridLines { get; set; } = true;
}

/// <summary>
/// Superset EnterpriseChart component combining Telerik EnterpriseChart and DevExpress ChartControl.
/// </summary>
public class EnterpriseChart : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Title { get; set; } = string.Empty;
    public List<ChartSeries> Series { get; } = new();
    public ChartAxis XAxis { get; set; } = new() { Type = AxisType.Categorical };
    public ChartAxis YAxis { get; set; } = new() { Type = AxisType.Numeric };
    public bool EnableTooltips { get; set; } = true;
    public bool EnableZoomPan { get; set; } = true;

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Data EnterpriseChart";
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

