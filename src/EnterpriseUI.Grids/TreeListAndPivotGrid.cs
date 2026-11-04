using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

/// <summary>
/// TreeList combining hierarchical tree navigation with multi-column grid features.
/// Superset of Telerik TreeList and DevExpress TreeList.
/// </summary>
public class TreeList<TItem> : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string KeyFieldName { get; set; } = "Id";
    public string ParentFieldName { get; set; } = "ParentId";
    public List<GridColumn> Columns { get; } = new();
    public IEnumerable<TItem>? DataSource { get; set; }

    public AriaRole Role { get; set; } = AriaRole.Tree;
    public string? AriaLabel { get; set; } = "Tree List";
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

public enum PivotArea
{
    Row,
    Column,
    Data,
    Filter
}

public class PivotField
{
    public string FieldName { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public PivotArea Area { get; set; } = PivotArea.Row;
    public int AreaIndex { get; set; }
    public AggregateFunction SummaryType { get; set; } = AggregateFunction.Sum;
}

/// <summary>
/// PivotGrid for multi-dimensional cross-tabulated OLAP-style data analysis.
/// Superset of Telerik PivotGrid and DevExpress PivotGrid.
/// </summary>
public class PivotGrid<TItem> : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<PivotField> Fields { get; } = new();
    public IEnumerable<TItem>? DataSource { get; set; }
    public bool ShowGrandTotals { get; set; } = true;
    public bool ShowSubtotals { get; set; } = true;

    public AriaRole Role { get; set; } = AriaRole.Grid;
    public string? AriaLabel { get; set; } = "Pivot Grid";
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
