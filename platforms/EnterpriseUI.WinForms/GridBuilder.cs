using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Fluent configuration of an <see cref="EnterpriseDataGrid{TItem}"/>: columns, bands, paging, selection, editing, query state,
/// summaries, formatting and validation. <see cref="Build"/> returns the configured control.
/// </summary>
public sealed class GridBuilder<TItem>
{
    private readonly EnterpriseDataGrid<TItem> _grid = new() { Dock = DockStyle.Fill };
    private IEnumerable<TItem>? _data;

    private GridBuilder()
    {
    }

    public static GridBuilder<TItem> Create() => new();

    /// <summary>Sets the accessible name announced by screen readers.</summary>
    public GridBuilder<TItem> Label(string? label)
    {
        _grid.AriaLabel = label;
        return this;
    }

    public GridBuilder<TItem> Column(string field, string? title = null, double? width = null, Action<GridColumn>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var column = new GridColumn { Field = field, Title = title ?? string.Empty, Width = width };
        configure?.Invoke(column);
        _grid.Columns.Add(column);
        return this;
    }

    /// <summary>Adds a top-level header band above the given columns.</summary>
    public GridBuilder<TItem> Band(string title, params string[] fields)
    {
        var band = new GridColumnBand { Title = title };
        _grid.Bands.Add(band);
        Assign(band, fields);
        return this;
    }

    /// <summary>Adds a band nested under an existing band (found by title).</summary>
    public GridBuilder<TItem> NestedBand(string parentTitle, string title, params string[] fields)
    {
        var parent = FindBand(_grid.Bands, parentTitle) ?? throw new InvalidOperationException($"Band '{parentTitle}' was not added yet.");
        var band = new GridColumnBand { Title = title };
        parent.ChildBands.Add(band);
        Assign(band, fields);
        return this;
    }

    public GridBuilder<TItem> Paging(GridPagingMode mode, int pageSize = 25)
    {
        _grid.PagingMode = mode;
        _grid.PageSize = pageSize;
        return this;
    }

    public GridBuilder<TItem> Selection(GridSelectionMode mode)
    {
        _grid.SelectionMode = mode;
        return this;
    }

    public GridBuilder<TItem> Editing(GridEditMode mode)
    {
        _grid.EditMode = mode;
        return this;
    }

    public GridBuilder<TItem> SortBy(string field, ListSortDirection direction = ListSortDirection.Ascending)
    {
        if (_grid.CurrentRequest.Sorts.Count == 0)
        {
            _grid.SortBy(field, direction);
        }
        else
        {
            // Additional sort levels are added through the tri-state cycle: ascending first, then descending.
            _grid.CycleSort(field, allowMultiSort: true);
            if (direction == ListSortDirection.Descending) _grid.CycleSort(field, allowMultiSort: true);
        }

        return this;
    }

    public GridBuilder<TItem> GroupBy(string field)
    {
        _grid.GroupBy(field);
        return this;
    }

    public GridBuilder<TItem> Filter(string field, FilterOperator op, object? value)
    {
        _grid.FilterBy(field, op, value);
        return this;
    }

    public GridBuilder<TItem> QuickFilter(string field, string text)
    {
        _grid.SetQuickFilter(field, text);
        return this;
    }

    public GridBuilder<TItem> Search(string text)
    {
        _grid.SearchPanelText = text;
        return this;
    }

    /// <summary>Adds a column footer summary (for example a sum under a numeric column).</summary>
    public GridBuilder<TItem> Summary(string field, AggregateFunction function, string? format = null)
    {
        FindColumn(field).FooterSummaries.Add(new GridColumnSummary(function, format));
        return this;
    }

    public GridBuilder<TItem> GroupSummary(string field, AggregateFunction function, string format = "{0}")
    {
        _grid.GroupSummaries.Add(new GroupSummaryDescriptor(field, function, format));
        return this;
    }

    public GridBuilder<TItem> FormatRule(GridFormatRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _grid.FormatRules.Add(rule);
        return this;
    }

    public GridBuilder<TItem> Validate(string field, GridValidationRule rule)
    {
        FindColumn(field).ValidationRules.Add(rule);
        return this;
    }

    /// <summary>Runs arbitrary configuration against the control for settings without a dedicated method.</summary>
    public GridBuilder<TItem> Configure(Action<EnterpriseDataGrid<TItem>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_grid);
        return this;
    }

    public GridBuilder<TItem> DataSource(IEnumerable<TItem> data)
    {
        _data = data;
        return this;
    }

    /// <summary>Returns the configured grid; the data source is attached last so the first load already has the full query.</summary>
    public EnterpriseDataGrid<TItem> Build()
    {
        if (_data != null) _grid.DataSource = _data;
        return _grid;
    }

    private GridColumn FindColumn(string field) =>
        _grid.Columns.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Column '{field}' was not added yet.");

    private void Assign(GridColumnBand band, IEnumerable<string> fields)
    {
        foreach (var field in fields) FindColumn(field).BandId = band.Id;
    }

    private static GridColumnBand? FindBand(IEnumerable<GridColumnBand> bands, string title)
    {
        foreach (var band in bands)
        {
            if (string.Equals(band.Title, title, StringComparison.Ordinal)) return band;
            var nested = FindBand(band.ChildBands, title);
            if (nested != null) return nested;
        }
        return null;
    }
}
