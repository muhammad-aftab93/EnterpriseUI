using System;
using System.Collections.Generic;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public enum GridRowKind
{
    /// <summary>A row of the grid's own data (also tree rows in self-referencing mode).</summary>
    Data,
    GroupHeader,
    GroupFooter,
    /// <summary>Title and column header of a child table under an expanded parent row.</summary>
    DetailHeader,
    /// <summary>One row of a child table.</summary>
    DetailData,
    /// <summary>Property summary shown when master/detail is enabled without any relation.</summary>
    DetailSummary
}

/// <summary>One displayed row: a data row, a group header or footer, or a row of a nested child table.</summary>
public sealed class GridRow<TItem>
{
    public GridRowKind Kind { get; init; }

    /// <summary>Position among the rows of the current page.</summary>
    public int Index { get; init; }

    /// <summary>Nesting depth: group level, detail level or tree level.</summary>
    public int Level { get; init; }

    /// <summary>The grid item for data and detail-summary rows; default for group and child rows.</summary>
    public TItem? Item { get; init; }

    /// <summary>The object behind the row: <see cref="Item"/> for data rows, the child object for detail rows.</summary>
    public object? RowObject { get; init; }

    /// <summary>For detail rows, the object whose children are shown.</summary>
    public object? Parent { get; init; }
    public GridHierarchyRelation? Relation { get; init; }
    public GroupResult<TItem>? Group { get; init; }

    /// <summary>Stable identity of a group across refreshes, e.g. <c>Region=EMEA/Priority=High</c>.</summary>
    public string? GroupPath { get; init; }

    /// <summary>Position in the filtered, sorted data ignoring groups and details; -1 for rows without one.</summary>
    public int DataIndex { get; init; } = -1;

    public bool IsExpandable { get; init; }
    public bool IsExpanded { get; init; }

    /// <summary>True while a lazily loaded relation is fetching its children.</summary>
    public bool IsLoading { get; init; }

    public bool IsDataRow => Kind == GridRowKind.Data;
}

public enum GridPagingMode
{
    /// <summary>All rows form one list; rows are loaded on demand while scrolling.</summary>
    None,
    /// <summary>The filtered, sorted result is read once and paged in memory.</summary>
    Client,
    /// <summary>Each page is requested from the data source with skip/take.</summary>
    Server,
    /// <summary>Continuous scrolling over page-aligned fetches; no page navigation UI.</summary>
    Virtual,
    /// <summary>Pages are appended on demand and rows accumulate (load more / infinite scroll).</summary>
    Infinite
}

public sealed class GridRowStyleEventArgs<TItem> : EventArgs
{
    public GridRowStyleEventArgs(TItem item, int rowIndex)
    {
        Item = item;
        RowIndex = rowIndex;
    }

    public TItem Item { get; }
    public int RowIndex { get; }

    /// <summary>Background as #RRGGBB; null keeps the theme colour.</summary>
    public string? BackColorHex { get; set; }
    public string? ForeColorHex { get; set; }
    public bool Bold { get; set; }
}

/// <summary>Snapshot of the grid's query for the status bar and accessibility announcements.</summary>
public sealed record GridQueryDescription(int SortCount, int FilterCount, int GroupCount, string? SearchText);
