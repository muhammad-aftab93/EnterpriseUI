using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Charts;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Grids;

namespace EnterpriseUI.Dashboard;

public enum WidgetType
{
    KpiCard,
    EnterpriseChart,
    Grid,
    Gauge,
    Custom
}

public class KpiCardData
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public double? ChangePercentage { get; set; }
    public bool IsPositiveTrend { get; set; } = true;
}

public class DashboardWidget : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public WidgetType Type { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public object? Content { get; set; }
}

/// <summary>
/// Superset Interactive Business Intelligence Dashboard combining DevExpress Dashboard and Telerik Dashboard layout.
/// </summary>
public class Dashboard : ObservableObject, IUIComponent, IThemeableControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string Title { get; set; } = "Executive Overview";
    public int ColumnsCount { get; set; } = 4;
    public List<DashboardWidget> Widgets { get; } = new();
    public string? ThemeOverride { get; set; }

    public void AddKpi(string title, string value, double changePercent, string? subtitle = null)
    {
        Widgets.Add(new DashboardWidget
        {
            Title = title,
            Type = WidgetType.KpiCard,
            Content = new KpiCardData
            {
                Title = title,
                Value = value,
                ChangePercentage = changePercent,
                IsPositiveTrend = changePercent >= 0,
                Subtitle = subtitle
            }
        });
        OnPropertyChanged(nameof(Widgets));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

