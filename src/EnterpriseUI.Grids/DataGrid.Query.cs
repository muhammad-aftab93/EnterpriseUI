using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public partial class DataGrid<TItem>
{
    private static readonly System.Text.Json.JsonSerializerOptions PresetJsonOptions = new() { WriteIndented = true };
    public GridColumn? FindColumn(string field) =>
        string.IsNullOrEmpty(field) ? null : Columns.FirstOrDefault(c => c.Field.Equals(field, StringComparison.OrdinalIgnoreCase));

    /// <summary>Cell value of a row object: the calculated value, the running total, or the property.</summary>
    public object? GetCellValue(object? item, GridColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return column.GetValue(item);
    }

    public string FormatCellValue(GridColumn column, object? value)
    {
        if (value == null) return string.Empty;
        if (!string.IsNullOrEmpty(column.FormatString) && value is IFormattable formattable)
        {
            try
            {
                return formattable.ToString(column.FormatString, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
            }
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    // ==========================================
    // SORTING
    // ==========================================

    private SortDescriptor CreateSortDescriptor(string fieldName, ListSortDirection direction)
    {
        var column = FindColumn(fieldName);
        return new SortDescriptor(fieldName, direction)
        {
            ValueSelector = column?.ValueSelector,
            Comparer = column?.CustomComparer == null ? null : Comparer<object?>.Create((a, b) => column.CustomComparer.Compare(a!, b!))
        };
    }

    private SortDescriptor? FindSort(string fieldName) =>
        CurrentRequest.Sorts.FirstOrDefault(s => s.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

    private void ResetSortState()
    {
        foreach (var col in Columns)
        {
            col.SortDirection = null;
            col.SortIndex = -1;
        }
    }

    private void SyncSortState()
    {
        ResetSortState();
        for (var i = 0; i < CurrentRequest.Sorts.Count; i++)
        {
            var sort = CurrentRequest.Sorts[i];
            var column = FindColumn(sort.Member);
            if (column != null)
            {
                column.SortDirection = sort.SortDirection;
                column.SortIndex = i;
            }
        }

        // A sort on a grouped column orders the groups.
        foreach (var group in CurrentRequest.Groups)
        {
            var sort = FindSort(group.Member);
            if (sort != null) group.SortDirection = sort.SortDirection;
        }
    }

    public void SortBy(string fieldName, ListSortDirection direction)
    {
        CurrentRequest.Sorts.Clear();
        CurrentRequest.Sorts.Add(CreateSortDescriptor(fieldName, direction));
        SyncSortState();
        Announce($"Sorted by {SortLabel(fieldName)} {(direction == ListSortDirection.Ascending ? "ascending" : "descending")}");
        ResetAndRefresh();
    }

    /// <summary>Tri-state header click: none, ascending, descending, none. Ctrl+click (<paramref name="allowMultiSort"/>) adds a column to the sort.</summary>
    public void CycleSort(string fieldName, bool allowMultiSort = false)
    {
        var column = FindColumn(fieldName);
        if (!EnableSorting || column == null || !column.Sortable) return;

        var multi = allowMultiSort && EnableMultiColumnSorting;
        var existing = FindSort(fieldName);
        var current = existing?.SortDirection;

        if (!multi)
        {
            CurrentRequest.Sorts.RemoveAll(s => !ReferenceEquals(s, existing));
        }

        ListSortDirection? next = current == null ? ListSortDirection.Ascending : current == ListSortDirection.Ascending ? ListSortDirection.Descending : null;
        if (next == null)
        {
            CurrentRequest.Sorts.Remove(existing!);
        }
        else if (existing == null)
        {
            CurrentRequest.Sorts.Add(CreateSortDescriptor(fieldName, next.Value));
        }
        else
        {
            existing.SortDirection = next.Value;
        }

        SyncSortState();
        Announce(next == null ? $"Sorting removed from {SortLabel(fieldName)}" : $"Sorted by {SortLabel(fieldName)} {(next == ListSortDirection.Ascending ? "ascending" : "descending")}");
        ResetAndRefresh();
    }

    private string SortLabel(string field)
    {
        var column = FindColumn(field);
        return column == null || string.IsNullOrEmpty(column.Title) ? field : column.Title;
    }

    public void ClearSort()
    {
        CurrentRequest.Sorts.Clear();
        SyncSortState();
        ResetAndRefresh();
    }

    // ==========================================
    // FILTERING, VALUE-LIST FILTERS & SEARCH
    // ==========================================

    /// <summary>Builds a filter for a column; calculated columns have no property to query, so they get an in-memory predicate.</summary>
    private IFilterDescriptor CreateFilter(string fieldName, FilterOperator op, object? value, object? value2 = null)
    {
        var column = FindColumn(fieldName);
        if (column?.ValueSelector is { } selector)
        {
            return new CustomFilterDescriptor(string.Empty, $"{fieldName} {op}", row => FilterEvaluator.Matches(selector(row!), op, value, value2));
        }

        return new FilterDescriptor(fieldName, op, value, value2);
    }

    private static bool TargetsField(IFilterDescriptor filter, string fieldName) => filter switch
    {
        FilterDescriptor fd => fd.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase),
        CustomFilterDescriptor custom => custom.OperatorName.StartsWith(fieldName + " ", StringComparison.OrdinalIgnoreCase) || custom.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    /// <summary>True when any active filter (column, value list, preset) restricts the given column.</summary>
    public bool HasFilter(string fieldName) =>
        QuickFilters.ContainsKey(fieldName) || _excelFilters.ContainsKey(fieldName) || CurrentRequest.Filters.Any(f => !ReferenceEquals(f, _searchFilter) && ContainsTarget(f, fieldName));

    private static bool ContainsTarget(IFilterDescriptor filter, string fieldName) =>
        filter is CompositeFilterDescriptor composite ? composite.FilterDescriptors.Any(f => ContainsTarget(f, fieldName)) : TargetsField(filter, fieldName);

    public void FilterBy(string fieldName, FilterOperator op, object? value) => FilterBy(fieldName, op, value, null);

    public void FilterBy(string fieldName, FilterOperator op, object? value, object? value2)
    {
        RemoveColumnFilters(fieldName);
        var needsValue = op is not (FilterOperator.IsNull or FilterOperator.IsNotNull or FilterOperator.IsEmpty or FilterOperator.IsNotEmpty);
        if (!needsValue || value != null)
        {
            CurrentRequest.Filters.Add(CreateFilter(fieldName, op, value, value2));
        }

        ResetAndRefresh();
        Announce(FilterSummary());
    }

    /// <summary>Adds an arbitrary filter tree (for example from a filter builder dialog) next to the column filters.</summary>
    public void AddFilter(IFilterDescriptor filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        CurrentRequest.Filters.Add(filter);
        ResetAndRefresh();
        Announce(FilterSummary());
    }

    public void RemoveFilter(IFilterDescriptor filter)
    {
        if (CurrentRequest.Filters.Remove(filter)) ResetAndRefresh();
    }

    /// <summary>Applies a custom operator registered in <see cref="FilterOperatorRegistry"/>, or a delegate, to a column.</summary>
    public void FilterByPredicate(string fieldName, string operatorName, Func<object?, bool> predicate)
    {
        RemoveColumnFilters(fieldName);
        var column = FindColumn(fieldName);
        CurrentRequest.Filters.Add(column?.ValueSelector is { } selector
            ? new CustomFilterDescriptor(string.Empty, $"{fieldName} {operatorName}", row => predicate(selector(row!)))
            : new CustomFilterDescriptor(fieldName, operatorName, predicate));
        ResetAndRefresh();
    }

    /// <summary>Removes every column, value-list, preset and builder filter; the search panel text is kept.</summary>
    public void ClearFilters()
    {
        CurrentRequest.Filters.RemoveAll(f => !ReferenceEquals(f, _searchFilter));
        _excelFilters.Clear();
        QuickFilters.Clear();
        foreach (var column in Columns) column.ExcelFilterValues.Clear();
        ResetAndRefresh();
    }

    private void RemoveColumnFilters(string fieldName)
    {
        if (_excelFilters.Remove(fieldName, out var previous))
        {
            CurrentRequest.Filters.Remove(previous);
        }

        FindColumn(fieldName)?.ExcelFilterValues.Clear();
        QuickFilters.Remove(fieldName);
        CurrentRequest.Filters.RemoveAll(f => !ReferenceEquals(f, _searchFilter) && TargetsField(f, fieldName));
    }

    /// <summary>Keeps only rows whose value is in <paramref name="selectedValues"/>; an empty selection removes the filter.</summary>
    public void ApplyExcelFilter(string fieldName, IEnumerable<object> selectedValues)
    {
        var column = FindColumn(fieldName);
        if (column == null) return;

        RemoveColumnFilters(column.Field);

        var values = selectedValues.ToList();
        foreach (var value in values) column.ExcelFilterValues.Add(value);

        if (values.Count > 0)
        {
            IFilterDescriptor filter = column.ValueSelector is { } selector
                ? new CustomFilterDescriptor(string.Empty, $"{column.Field} In", row => FilterEvaluator.Matches(selector(row!), FilterOperator.IsContainedIn, values, null))
                : new FilterDescriptor(column.Field, FilterOperator.IsContainedIn, values);
            CurrentRequest.Filters.Add(filter);
            _excelFilters[column.Field] = filter;
        }

        ResetAndRefresh();
        Announce(FilterSummary());
    }

    public void ClearExcelFilter(string fieldName)
    {
        if (_excelFilters.Remove(fieldName, out var previous))
        {
            CurrentRequest.Filters.Remove(previous);
        }

        FindColumn(fieldName)?.ExcelFilterValues.Clear();
        ResetAndRefresh();
    }

    /// <summary>Distinct non-null values of a column in the bound in-memory source, optionally text-filtered; stops after <paramref name="maxCount"/>.</summary>
    public IReadOnlyList<object> GetDistinctValues(string fieldName, string? contains = null, int maxCount = 1000, CancellationToken cancellationToken = default)
    {
        if (_dataSource == null || maxCount <= 0 || string.IsNullOrEmpty(fieldName))
        {
            return Array.Empty<object>();
        }

        var column = FindColumn(fieldName);
        var hasProperty = PropertyPath.GetPathType(typeof(TItem), fieldName) != null;
        if (column?.IsCalculated != true && !hasProperty)
        {
            return Array.Empty<object>();
        }

        var seen = new HashSet<object>(GridKeyComparer.Instance);
        var values = new List<object>();
        foreach (var item in _dataSource)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = column != null ? column.GetValue(item) : PropertyPath.GetValue(item, fieldName);
            if (value == null || !seen.Add(value))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(contains) && !(Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty).Contains(contains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Add(value);
            if (values.Count >= maxCount)
            {
                break;
            }
        }

        values.Sort(ValueComparer.Instance);
        return values;
    }

    /// <summary>Filter-row text for a column (contains); empty text removes the filter.</summary>
    public void SetQuickFilter(string fieldName, string text)
    {
        RemoveColumnFilters(fieldName);
        if (!string.IsNullOrWhiteSpace(text))
        {
            QuickFilters[fieldName] = text;
            CurrentRequest.Filters.Add(CreateFilter(fieldName, FilterOperator.Contains, text));
        }

        ResetAndRefresh();
        Announce(FilterSummary());
    }

    private string FilterSummary()
    {
        var count = CurrentRequest.Filters.Count;
        return count == 0 ? "Filters cleared" : $"{count} filter{(count == 1 ? string.Empty : "s")} applied";
    }

    private void ApplySearchPanelFilter(string? query)
    {
        if (_searchFilter != null)
        {
            CurrentRequest.Filters.Remove(_searchFilter);
            _searchFilter = null;
        }

        if (SearchMode == GridSearchMode.Filter && !string.IsNullOrWhiteSpace(query))
        {
            var searchComposite = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };
            foreach (var column in Columns.Where(c => c.Visible && c.Searchable))
            {
                searchComposite.FilterDescriptors.Add(CreateFilter(column.Field, FilterOperator.Contains, query));
            }

            if (searchComposite.FilterDescriptors.Count > 0)
            {
                CurrentRequest.Filters.Add(searchComposite);
                _searchFilter = searchComposite;
            }
        }

        ResetAndRefresh();
        if (SearchMode == GridSearchMode.Highlight)
        {
            _ = SearchSafeAsync(query);
        }
    }

    // ------------------------------------------------------------------
    // Saved filters
    // ------------------------------------------------------------------

    private static IFilterDescriptor CloneFilter(IFilterDescriptor filter)
    {
        switch (filter)
        {
            case FilterDescriptor fd:
                return new FilterDescriptor(fd.Member, fd.Operator, fd.Value, fd.Value2) { IgnoreCase = fd.IgnoreCase };
            case CustomFilterDescriptor custom:
                return new CustomFilterDescriptor(custom.Member, custom.OperatorName, custom.Predicate);
            case CompositeFilterDescriptor composite:
                var copy = new CompositeFilterDescriptor { LogicalOperator = composite.LogicalOperator };
                copy.FilterDescriptors.AddRange(composite.FilterDescriptors.Select(CloneFilter));
                return copy;
            default:
                return filter;
        }
    }

    /// <summary>Stores a copy of the current column filters under a name; the search panel text is not part of a preset.</summary>
    public void SaveFilterPreset(string presetName)
    {
        SavedFilterPresets[presetName] = CurrentRequest.Filters
            .Where(f => !ReferenceEquals(f, _searchFilter))
            .Select(CloneFilter)
            .ToList();
    }

    public bool ApplyFilterPreset(string presetName)
    {
        if (!SavedFilterPresets.TryGetValue(presetName, out var filters))
        {
            return false;
        }

        CurrentRequest.Filters.RemoveAll(f => !ReferenceEquals(f, _searchFilter));
        _excelFilters.Clear();
        QuickFilters.Clear();
        foreach (var column in Columns) column.ExcelFilterValues.Clear();
        CurrentRequest.Filters.AddRange(filters.Select(CloneFilter));
        ResetAndRefresh();
        Announce($"Filter {presetName} applied");
        return true;
    }

    public string ExportFilterPresets()
    {
        var presets = SavedFilterPresets.ToDictionary(p => p.Key, p => FilterSerializer.Serialize(p.Value), StringComparer.OrdinalIgnoreCase);
        return System.Text.Json.JsonSerializer.Serialize(presets, PresetJsonOptions);
    }

    /// <summary>Loads presets written by <see cref="ExportFilterPresets"/>; returns warnings for filters that could not be restored.</summary>
    public IReadOnlyList<string> ImportFilterPresets(string json)
    {
        var warnings = new List<string>();
        var presets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        foreach (var (name, filterJson) in presets)
        {
            var loaded = FilterSerializer.Deserialize(filterJson);
            SavedFilterPresets[name] = loaded.Filters.ToList();
            warnings.AddRange(loaded.Warnings.Select(w => $"{name}: {w}"));
        }
        return warnings;
    }

    // ==========================================
    // GROUPING
    // ==========================================

    public void GroupBy(string fieldName)
    {
        if (CurrentRequest.Groups.Any(g => g.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var column = FindColumn(fieldName);
        CurrentRequest.Groups.Add(new GroupDescriptor
        {
            Member = fieldName,
            SortDirection = FindSort(fieldName)?.SortDirection ?? ListSortDirection.Ascending,
            ValueSelector = column?.ValueSelector
        });
        Announce($"Grouped by {SortLabel(fieldName)}");
        ResetAndRefresh();
    }

    public void RemoveGroup(string fieldName)
    {
        if (CurrentRequest.Groups.RemoveAll(g => g.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            ResetAndRefresh();
        }
    }

    public void ClearGroups()
    {
        CurrentRequest.Groups.Clear();
        ResetAndRefresh();
    }

    /// <summary>Changes the nesting order of the groups, as when dragging chips in the group panel.</summary>
    public bool MoveGroup(string fieldName, int newIndex)
    {
        var group = CurrentRequest.Groups.FirstOrDefault(g => g.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (group == null) return false;

        newIndex = Math.Clamp(newIndex, 0, CurrentRequest.Groups.Count - 1);
        if (CurrentRequest.Groups.IndexOf(group) == newIndex) return false;
        CurrentRequest.Groups.Remove(group);
        CurrentRequest.Groups.Insert(newIndex, group);
        ResetAndRefresh();
        return true;
    }

    /// <summary>Groups numeric or date values into buckets, e.g. dates by year or amounts in steps of 1000.</summary>
    public void GroupBy(string fieldName, Func<object?, object?> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        RemoveGroup(fieldName);
        CurrentRequest.Groups.Add(new GroupDescriptor { Member = fieldName, KeySelector = keySelector, ValueSelector = FindColumn(fieldName)?.ValueSelector });
        ResetAndRefresh();
    }

    // ==========================================
    // COLUMNS
    // ==========================================

    public void SetColumnVisibility(string fieldName, bool visible)
    {
        var column = FindColumn(fieldName);
        if (column == null || (!visible && !column.CanHide) || column.Visible == visible)
        {
            return;
        }

        column.Visible = visible;
        OnPropertyChanged(nameof(Columns));
        Selection.SetBounds(TotalItemsCount, GridColumnLayout.OrderVisible(Columns).Count);

        // The search panel only searches visible columns.
        if (SearchMode == GridSearchMode.Filter && !string.IsNullOrWhiteSpace(_searchPanelText))
        {
            ApplySearchPanelFilter(_searchPanelText);
        }
    }

    public void ReorderColumn(string fieldName, int newIndex)
    {
        var column = FindColumn(fieldName);
        if (column != null && column.Reorderable && newIndex >= 0 && newIndex < Columns.Count)
        {
            Columns.Remove(column);
            Columns.Insert(newIndex, column);
            OnPropertyChanged(nameof(Columns));
        }
    }

    /// <summary>Sets a column width within its min/max limits; returns false for unknown columns or resizing disabled.</summary>
    public bool ResizeColumn(string fieldName, double width)
    {
        var column = FindColumn(fieldName);
        if (column == null || !column.Resizable)
        {
            return false;
        }

        column.Width = GridColumnLayout.ClampWidth(column, width);
        OnPropertyChanged(nameof(Columns));
        return true;
    }

    /// <summary>Widest value of a column among the sampled rows, as measured by <paramref name="measureText"/> (pixels), plus header and padding.</summary>
    public double CalculateBestFitWidth(GridColumn column, Func<string, bool, double> measureText, double padding = 24, int maxSamples = 500)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(measureText);

        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        var widest = measureText(title, true) + padding + 20;

        var rows = RenderedItems;
        var step = Math.Max(1, rows.Count / Math.Max(1, maxSamples));
        for (var i = 0; i < rows.Count; i += step)
        {
            widest = Math.Max(widest, measureText(FormatCellValue(column, column.GetValue(rows[i])), false) + padding);
        }

        return GridColumnLayout.ClampWidth(column, widest);
    }

    /// <summary>Resizes a column to fit its content.</summary>
    public bool BestFitColumn(string fieldName, Func<string, bool, double> measureText)
    {
        var column = FindColumn(fieldName);
        return column != null && ResizeColumn(fieldName, CalculateBestFitWidth(column, measureText));
    }

    public void BestFitAllColumns(Func<string, bool, double> measureText)
    {
        foreach (var column in Columns.Where(c => c.Visible && c.Resizable))
        {
            column.Width = CalculateBestFitWidth(column, measureText);
        }
        OnPropertyChanged(nameof(Columns));
    }

    /// <summary>Moves a column before another (or to the end when null); the column adopts the target's pin zone.</summary>
    public bool MoveColumnBefore(string fieldName, string? beforeFieldName)
    {
        var column = FindColumn(fieldName);
        if (column == null || !column.Reorderable)
        {
            return false;
        }

        GridColumn? target = null;
        if (beforeFieldName != null)
        {
            target = FindColumn(beforeFieldName);
            if (target == null || ReferenceEquals(target, column))
            {
                return false;
            }
        }

        Columns.Remove(column);
        Columns.Insert(target == null ? Columns.Count : Columns.IndexOf(target), column);
        if (target != null)
        {
            column.PinPosition = target.PinPosition;
        }

        OnPropertyChanged(nameof(Columns));
        return true;
    }

    public void PinColumn(string fieldName, ColumnPinPosition position)
    {
        var column = FindColumn(fieldName);
        if (column != null)
        {
            column.PinPosition = position;
            OnPropertyChanged(nameof(Columns));
        }
    }

    // ==========================================
    // LAYOUT PROFILES
    // ==========================================

    public ColumnLayoutProfile CreateLayoutProfile(string profileName = "Default")
    {
        var profile = new ColumnLayoutProfile { ProfileName = profileName, PageSize = _pageSize };
        for (var i = 0; i < Columns.Count; i++)
        {
            var c = Columns[i];
            profile.Columns.Add(new ColumnStateSnapshot
            {
                Field = c.Field,
                Visible = c.Visible,
                Width = c.Width,
                DisplayIndex = i,
                PinPosition = c.PinPosition,
                SortDirection = c.SortDirection,
                SortIndex = c.SortIndex
            });
        }

        profile.Sorts.AddRange(CurrentRequest.Sorts.Select(s => new ColumnSortSnapshot { Field = s.Member, Direction = s.SortDirection }));
        profile.Groups.AddRange(CurrentRequest.Groups.Select(g => new ColumnGroupSnapshot { Field = g.Member, Direction = g.SortDirection }));
        var filters = CurrentRequest.Filters.Where(f => !ReferenceEquals(f, _searchFilter)).ToList();
        if (filters.Count > 0) profile.FiltersJson = FilterSerializer.Serialize(filters);
        return profile;
    }

    /// <summary>Restores column state, sorting, grouping, filters and page size from a profile.</summary>
    public IReadOnlyList<string> ApplyLayoutProfile(ColumnLayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var warnings = new List<string>();
        var states = profile.Columns.GroupBy(c => c.Field, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var column in Columns)
        {
            if (states.TryGetValue(column.Field, out var state))
            {
                column.Visible = state.Visible || !column.CanHide;
                column.Width = state.Width;
                column.PinPosition = state.PinPosition;
            }
        }

        var ordered = Columns.OrderBy(c => states.TryGetValue(c.Field, out var s) ? s.DisplayIndex : int.MaxValue).ToList();
        Columns.Clear();
        Columns.AddRange(ordered);

        CurrentRequest.Sorts.Clear();
        var sortSnapshots = profile.Sorts.Count > 0
            ? profile.Sorts
            : profile.Columns.Where(c => c.SortDirection != null).OrderBy(c => c.SortIndex)
                .Select(c => new ColumnSortSnapshot { Field = c.Field, Direction = c.SortDirection!.Value }).ToList();
        foreach (var sort in sortSnapshots)
        {
            if (FindColumn(sort.Field) != null) CurrentRequest.Sorts.Add(CreateSortDescriptor(sort.Field, sort.Direction));
            else warnings.Add($"Sort on unknown column '{sort.Field}' was skipped.");
        }
        SyncSortState();

        CurrentRequest.Groups.Clear();
        foreach (var group in profile.Groups)
        {
            if (FindColumn(group.Field) is { } groupColumn)
            {
                CurrentRequest.Groups.Add(new GroupDescriptor { Member = group.Field, SortDirection = group.Direction, ValueSelector = groupColumn.ValueSelector });
            }
            else
            {
                warnings.Add($"Group on unknown column '{group.Field}' was skipped.");
            }
        }

        CurrentRequest.Filters.RemoveAll(f => !ReferenceEquals(f, _searchFilter));
        _excelFilters.Clear();
        QuickFilters.Clear();
        foreach (var column in Columns) column.ExcelFilterValues.Clear();
        if (!string.IsNullOrWhiteSpace(profile.FiltersJson))
        {
            var loaded = FilterSerializer.Deserialize(profile.FiltersJson);
            CurrentRequest.Filters.AddRange(loaded.Filters);
            warnings.AddRange(loaded.Warnings);
        }

        if (profile.PageSize is > 0)
        {
            _pageSize = profile.PageSize.Value;
            _infiniteLoaded = _pageSize;
            OnPagingChanged();
        }

        OnPropertyChanged(nameof(Columns));
        ResetAndRefresh();
        return warnings;
    }
}
