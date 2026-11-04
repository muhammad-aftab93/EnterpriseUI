using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace EnterpriseUI.Grids;

public enum ColumnPinPosition
{
    None,
    Left,
    Right
}

public enum ColumnTextAlignment
{
    Left,
    Center,
    Right
}

public enum GridEditMode
{
    None,
    InCell,
    InlineRow,
    PopupModal,
    Batch
}

public enum GridSelectionMode
{
    None,
    SingleRow,
    MultipleRows,
    CheckboxRow,
    CellRange
}

public enum FilterMenuType
{
    Default,
    ExcelList,
    DateRange,
    NumericRange,
    ExpressionBuilder
}

/// <summary>
/// Column band container for multi-level stacked and nested headers.
/// Superset of DevExpress BandedGridView and Telerik ColumnGroupsViewDefinition.
/// </summary>
public class GridColumnBand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Visible { get; set; } = true;
    public List<GridColumnBand> ChildBands { get; } = new();
    public List<GridColumn> Columns { get; } = new();
}

/// <summary>
/// Serializable column layout profile for presets, workspace persistence and runtime column chooser.
/// </summary>
public class ColumnLayoutProfile
{
    public string ProfileName { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ColumnStateSnapshot> Columns { get; set; } = new();
}

public class ColumnStateSnapshot
{
    public string Field { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public double? Width { get; set; }
    public int DisplayIndex { get; set; }
    public ColumnPinPosition PinPosition { get; set; }
    public ListSortDirection? SortDirection { get; set; }
    public int SortIndex { get; set; } = -1;
}

/// <summary>
/// Definition and behavioral configuration for an enterprise Grid column.
/// </summary>
public class GridColumn
{
    public string Field { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double? Width { get; set; }
    public double MinWidth { get; set; } = 40;
    public double? MaxWidth { get; set; }
    public bool Visible { get; set; } = true;
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public bool Groupable { get; set; } = true;
    public bool Resizable { get; set; } = true;
    public bool Reorderable { get; set; } = true;
    public ColumnPinPosition PinPosition { get; set; } = ColumnPinPosition.None;
    public ColumnTextAlignment Alignment { get; set; } = ColumnTextAlignment.Left;
    public string? FormatString { get; set; }
    public string? Tooltip { get; set; }

    // Multi-column sorting
    public ListSortDirection? SortDirection { get; set; }
    public int SortIndex { get; set; } = -1;
    public IComparer<object>? CustomComparer { get; set; }

    // Excel & Advanced filtering
    public FilterMenuType FilterType { get; set; } = FilterMenuType.Default;
    public bool AllowFilterSearch { get; set; } = true;
    public HashSet<object> ExcelFilterValues { get; } = new();

    // Column Bands & Stacking
    public string? BandId { get; set; }

    // Column Chooser & Visibility Governance
    public bool AllowColumnChooser { get; set; } = true;
    public bool CanHide { get; set; } = true;
    public bool Searchable { get; set; } = true;

    // Aggregates & Summaries
    public object? FooterAggregate { get; set; }

    // Templates
    public object? HeaderTemplate { get; set; }
    public object? CellTemplate { get; set; }
    public object? EditorTemplate { get; set; }
    public object? FooterTemplate { get; set; }
}

public class GridCommandColumn : GridColumn
{
    public GridCommandColumn()
    {
        Title = "Actions";
        Sortable = false;
        Filterable = false;
        Groupable = false;
        Width = 120;
    }
}

public enum FormatRuleType
{
    HighlightCell,
    DataBar,
    ColorScale,
    IconSet
}

public enum FormatConditionOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    TopN,
    BottomN
}

/// <summary>
/// Conditional formatting rule defining Excel-style visual indicators, data bars, and gradient fills.
/// Equivalent/superset of DevExpress GridFormatRule and Telerik ConditionalFormattingObject.
/// </summary>
public class GridFormatRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Format Rule";
    public string ColumnField { get; set; } = string.Empty;
    public FormatRuleType RuleType { get; set; } = FormatRuleType.HighlightCell;
    public FormatConditionOperator Operator { get; set; } = FormatConditionOperator.GreaterThan;
    public object? Value1 { get; set; }
    public object? Value2 { get; set; }
    public string? BackgroundColorHex { get; set; }
    public string? ForegroundColorHex { get; set; }
    public bool ApplyToRow { get; set; }
    public bool Enabled { get; set; } = true;
}

public enum HierarchyMode
{
    MasterDetail,
    SelfReferencing,
    TabbedDetail
}

/// <summary>
/// Master-detail relation configuration for unlimited nesting and dynamic relational grids.
/// Equivalent to DevExpress LevelTree and Telerik GridViewHierarchyRow.
/// </summary>
public class GridHierarchyRelation
{
    public string RelationName { get; set; } = string.Empty;
    public string KeyField { get; set; } = string.Empty;
    public string ForeignField { get; set; } = string.Empty;
    public HierarchyMode Mode { get; set; } = HierarchyMode.MasterDetail;
    public Type? ChildItemType { get; set; }
    public Func<object, IEnumerable>? DetailItemsSelector { get; set; }
}

public enum GridClipboardCopyMode
{
    None,
    Cells,
    CellsWithHeaders,
    Html,
    Csv
}

