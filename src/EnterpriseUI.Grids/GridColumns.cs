using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using EnterpriseUI.Data;

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
    Batch,
    Form
}

public enum GridSelectionMode
{
    None,
    SingleRow,
    MultipleRows,
    CheckboxRow,
    CellRange,
    SingleCell,
    Column
}

public enum FilterMenuType
{
    Default,
    ExcelList,
    DateRange,
    NumericRange,
    ExpressionBuilder
}

/// <summary>How a column renders and edits its value.</summary>
public enum GridColumnType
{
    /// <summary>Chosen from the property type: bool becomes a checkbox, everything else text.</summary>
    Auto,
    Text,
    Numeric,
    Boolean,
    Date,
    ComboBox,
    Progress,
    Hyperlink,
    Command
}

public enum GridAutoSizeMode
{
    None,
    /// <summary>The last scrollable column stretches to fill free viewport width.</summary>
    Fill,
    /// <summary>Columns are sized to their widest sampled value once data loads.</summary>
    BestFit
}

/// <summary>
/// Column band container for multi-level stacked and nested headers.
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

public class ColumnSortSnapshot
{
    public string Field { get; set; } = string.Empty;
    public ListSortDirection Direction { get; set; }
}

public class ColumnGroupSnapshot
{
    public string Field { get; set; } = string.Empty;
    public ListSortDirection Direction { get; set; }
}

/// <summary>
/// Serializable layout for presets, workspace persistence and the runtime column chooser. Covers column state, sorting,
/// grouping, filtering and paging so a user's view can be restored exactly.
/// </summary>
public class ColumnLayoutProfile
{
    public string ProfileName { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ColumnStateSnapshot> Columns { get; set; } = new();
    public List<ColumnSortSnapshot> Sorts { get; set; } = new();
    public List<ColumnGroupSnapshot> Groups { get; set; } = new();

    /// <summary>Filter tree written by <see cref="FilterSerializer"/>; null when no filters were active.</summary>
    public string? FiltersJson { get; set; }
    public int? PageSize { get; set; }
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

/// <summary>A footer aggregate shown for a column.</summary>
public sealed class GridColumnSummary
{
    public AggregateFunction Function { get; set; } = AggregateFunction.Sum;

    /// <summary>Composite format; {0} is the value, e.g. "Total: {0:C2}".</summary>
    public string? Format { get; set; }

    /// <summary>Overrides <see cref="Function"/> with custom logic over the column's non-null values.</summary>
    public Func<IReadOnlyList<object?>, object?>? Custom { get; set; }

    public string? Label { get; set; }

    public GridColumnSummary() { }

    public GridColumnSummary(AggregateFunction function, string? format = null)
    {
        Function = function;
        Format = format;
    }
}

/// <summary>
/// Definition and behavioral configuration for an enterprise Grid column.
/// </summary>
public class GridColumn
{
    private Func<object, object?>? _valueSelector;
    private CompiledExpression? _compiled;
    private string? _expressionError;
    private string? _selectorExpression;

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
    public bool ReadOnly { get; set; }
    public ColumnPinPosition PinPosition { get; set; } = ColumnPinPosition.None;
    public ColumnTextAlignment Alignment { get; set; } = ColumnTextAlignment.Left;
    public GridColumnType ColumnType { get; set; } = GridColumnType.Auto;
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

    /// <summary>Spreadsheet-style expression computed per row, e.g. <c>[UnitPrice] * [Quantity]</c>. Makes the column calculated and read-only.</summary>
    public string? Expression { get; set; }

    /// <summary>Source field accumulated down the sorted rows; the column shows the running total of that field.</summary>
    public string? RunningTotalOf { get; set; }

    /// <summary>Footer aggregates computed over every row matching the current filters.</summary>
    public List<GridColumnSummary> FooterSummaries { get; } = new();

    /// <summary>Rules run when a cell of this column is edited.</summary>
    public List<GridValidationRule> ValidationRules { get; } = new();

    /// <summary>Values offered by the editor of a <see cref="GridColumnType.ComboBox"/> column.</summary>
    public IReadOnlyList<object>? ComboItems { get; set; }

    public bool IsCalculated => !string.IsNullOrWhiteSpace(Expression);
    public bool IsRunningTotal => !string.IsNullOrWhiteSpace(RunningTotalOf);
    public bool IsEditable => !ReadOnly && !IsCalculated && !IsRunningTotal;

    /// <summary>Aggregates over the column's non-null values, for backwards compatibility with the single-summary property.</summary>
    public object? FooterAggregate { get; set; }

    // Templates
    public object? HeaderTemplate { get; set; }
    public object? CellTemplate { get; set; }
    public object? EditorTemplate { get; set; }
    public object? FooterTemplate { get; set; }

    /// <summary>Reads the column value from a row: the expression for calculated columns, otherwise the property path. An invalid expression yields null; see <see cref="ExpressionError"/>.</summary>
    public object? GetValue(object? item)
    {
        if (item == null) return null;
        if (IsCalculated)
        {
            return Compiled()?.Evaluate(item);
        }

        return PropertyPath.GetValue(item, Field);
    }

    /// <summary>Why <see cref="Expression"/> failed to compile, or null when it is valid or not set.</summary>
    public string? ExpressionError
    {
        get
        {
            Compiled();
            return _expressionError;
        }
    }

    private CompiledExpression? Compiled()
    {
        if (!string.Equals(_selectorExpression, Expression, StringComparison.Ordinal))
        {
            _selectorExpression = Expression;
            _valueSelector = null;
            _compiled = null;
            _expressionError = null;
            if (!string.IsNullOrWhiteSpace(Expression))
            {
                ExpressionEngine.TryCompile(Expression, out _compiled, out _expressionError);
            }
        }

        return _compiled;
    }

    /// <summary>Whole-row value accessor used by sort, group and aggregate descriptors of calculated columns; null for plain columns.</summary>
    public Func<object, object?>? ValueSelector
    {
        get
        {
            if (!IsCalculated)
            {
                return null;
            }

            var compiled = Compiled();
            return _valueSelector ??= compiled == null ? static _ => null : row => compiled.Evaluate(row);
        }
    }

    internal GridColumnType EffectiveType(Type? propertyType)
    {
        if (ColumnType != GridColumnType.Auto) return ColumnType;
        var type = propertyType == null ? null : Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(bool)) return GridColumnType.Boolean;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)) return GridColumnType.Date;
        return GridColumnType.Text;
    }
}

public class GridCommandColumn : GridColumn
{
    public GridCommandColumn()
    {
        Title = "Actions";
        Sortable = false;
        Filterable = false;
        Groupable = false;
        ColumnType = GridColumnType.Command;
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

public enum GridClipboardCopyMode
{
    None,
    Cells,
    CellsWithHeaders,
    Html,
    Csv
}
