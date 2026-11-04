using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Abstractions.Virtualization;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Data;

namespace EnterpriseUI.Grids;

public sealed record GroupSummaryDescriptor(string Member, AggregateFunction Function, string FormatString = "{0}");

public sealed record GridEditRecord<TItem>(TItem Item, string FieldName, object? OldValue, object? NewValue);

/// <summary>
/// Superset Enterprise DataGrid combining Telerik UI for .NET Grid and DevExpress GridControl.
/// Features: Virtual scrolling, grouping, hierarchy, sorting, multi-column sorting, Excel-style filtering,
/// column bands, column chooser, presets, row/cell editing, master-detail, column pinning.
/// </summary>
public class DataGrid<TItem> : ObservableObject, IDataBoundControl<TItem>, IVirtualizedControl, IThemeableControl, IAccessibleControl
{
    private readonly VirtualizationManager _virtualizer = new();
    private IEnumerable<TItem>? _dataSource;
    private IDataSource<TItem>? _boundDataSource;
    private DataSourceResult<TItem>? _currentResult;
    private TItem? _selectedItem;
    private readonly HashSet<TItem> _selectedItems = new();
    private readonly HashSet<object> _expandedGroupKeys = new();
    private readonly HashSet<TItem> _expandedDetailRows = new();
    private readonly Stack<GridEditRecord<TItem>> _undoStack = new();
    private readonly Stack<GridEditRecord<TItem>> _redoStack = new();
    private string? _searchPanelText;
    private CompositeFilterDescriptor? _searchFilter;
    private readonly Dictionary<string, CompositeFilterDescriptor> _excelFilters = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TItem> _window = Array.Empty<TItem>();
    private int _windowStart;
    private int _wantedFirstRow;
    private int _wantedRowCount = 50;
    private int _refreshVersion;
    private volatile bool _isLoading;
    private double _horizontalOffset;
    private double _verticalOffset;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    // Grid Options & Columns
    public List<GridColumn> Columns { get; } = new();
    public List<GridColumnBand> Bands { get; } = new();
    public DataSourceRequest CurrentRequest { get; } = new();
    public GridSelectionMode SelectionMode { get; set; } = GridSelectionMode.SingleRow;
    public GridEditMode EditMode { get; set; } = GridEditMode.None;
    public bool EnableVirtualization { get; set; } = true;
    public bool EnableGrouping { get; set; } = true;
    public bool EnableFiltering { get; set; } = true;
    public bool EnableSorting { get; set; } = true;
    public bool EnableMultiColumnSorting { get; set; } = true;
    public bool EnableMasterDetail { get; set; } = false;
    public bool ShowGroupPanel { get; set; } = true;
    public bool EnableSearchPanel { get; set; } = true;
    public bool EnableQuickFilterRow { get; set; } = false;
    public object? DetailTemplate { get; set; }

    // Quick Filters & Presets
    public Dictionary<string, string> QuickFilters { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<IFilterDescriptor>> SavedFilterPresets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GroupSummaryDescriptor> GroupSummaries { get; } = new();
    public List<GridFormatRule> FormatRules { get; } = new();
    public List<GridHierarchyRelation> Relations { get; } = new();
    public GridClipboardCopyMode ClipboardCopyMode { get; set; } = GridClipboardCopyMode.CellsWithHeaders;

    public string? SearchPanelText
    {
        get => _searchPanelText;
        set
        {
            if (SetProperty(ref _searchPanelText, value))
            {
                ApplySearchPanelFilter(value);
            }
        }
    }

    // Virtualization properties
    public double ViewportWidth { get; private set; } = 800;
    public double ViewportHeight { get; private set; } = 600;
    public double ItemHeight { get => _virtualizer.ItemHeight; set => _virtualizer.ItemHeight = value; }
    public double ItemWidth { get => _virtualizer.ItemWidth; set => _virtualizer.ItemWidth = value; }
    public int TotalItemsCount => _currentResult?.Total ?? 0;
    public IndexRange VisibleRowRange { get; private set; }
    public IndexRange VisibleColumnRange { get; private set; }

    // Accessibility
    public AriaRole Role { get; set; } = AriaRole.Grid;
    public string? AriaLabel { get; set; } = "Data Grid";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }

    // Theming
    public string? ThemeOverride { get; set; }

    // Data Binding
    public IEnumerable<TItem>? DataSource
    {
        get => _dataSource;
        set
        {
            if (SetProperty(ref _dataSource, value))
            {
                _boundDataSource = value != null ? new DataSource<TItem>(value) : null;
                ResetAndRefresh();
            }
        }
    }

    public IDataSource<TItem>? CustomDataSource
    {
        get => _boundDataSource;
        set
        {
            _boundDataSource = value;
            ResetAndRefresh();
        }
    }

    public TItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                _selectedItems.Clear();
                if (value != null) _selectedItems.Add(value);
                OnPropertyChanged(nameof(SelectedItems));
            }
        }
    }

    public IReadOnlyList<TItem> SelectedItems => _selectedItems.ToList();

    /// <summary>Rows currently cached in memory; the first entry is row <see cref="WindowStart"/> of the filtered, sorted result.</summary>
    public IReadOnlyList<TItem> RenderedItems => _window;

    public int WindowStart => _windowStart;
    public bool IsLoading => _isLoading;

    /// <summary>Extra rows fetched before and after the requested range to make scrolling smooth.</summary>
    public int RowBuffer { get; set; } = 100;

    public event EventHandler? DataWindowChanged;
    public event EventHandler? QueryChanged;

    /// <summary>Returns the row at an absolute index of the filtered, sorted result if it is cached.</summary>
    public bool TryGetItemAt(int absoluteIndex, out TItem item)
    {
        var local = absoluteIndex - _windowStart;
        if (local >= 0 && local < _window.Count)
        {
            item = _window[local];
            return true;
        }

        item = default!;
        return false;
    }

    /// <summary>Declares the rows needed on screen; starts a fetch and returns false when they are not cached yet.</summary>
    public bool EnsureRowsLoaded(int firstRow, int rowCount)
    {
        _wantedFirstRow = Math.Max(0, firstRow);
        _wantedRowCount = Math.Max(1, rowCount);

        if (_boundDataSource == null)
        {
            return true;
        }

        var lastRow = Math.Min(TotalItemsCount, _wantedFirstRow + _wantedRowCount);
        var windowEnd = _windowStart + _window.Count;
        if (_currentResult != null && _wantedFirstRow >= _windowStart && lastRow <= windowEnd)
        {
            return true;
        }

        if (!_isLoading)
        {
            _ = RefreshDataAsync();
        }
        return false;
    }

    /// <summary>Reads rows matching the current filters and sort order from the start, for export and printing; null reads all.</summary>
    public async Task<IReadOnlyList<TItem>> ReadAllAsync(int? maxRows = null, CancellationToken cancellationToken = default)
    {
        if (_boundDataSource == null)
        {
            return Array.Empty<TItem>();
        }

        var request = CurrentRequest.Clone();
        request.Skip = 0;
        request.Take = maxRows ?? 0;
        var result = await _boundDataSource.ReadAsync(request, cancellationToken);
        return result.Data?.ToList() ?? new List<TItem>();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDataAsync(cancellationToken);
    }

    public async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _refreshVersion);

        if (_boundDataSource == null)
        {
            _currentResult = new DataSourceResult<TItem> { Data = Array.Empty<TItem>(), Total = 0 };
            _window = Array.Empty<TItem>();
            _windowStart = 0;
            RaiseDataWindowChanged();
            return;
        }

        _isLoading = true;
        try
        {
            var start = Math.Max(0, _wantedFirstRow - RowBuffer);
            var request = CurrentRequest.Clone();
            request.Skip = start;
            request.Take = _wantedRowCount + 2 * RowBuffer;

            var result = await _boundDataSource.ReadAsync(request, cancellationToken);
            if (version != _refreshVersion) return;

            if (result.Total > 0 && start >= result.Total)
            {
                _wantedFirstRow = Math.Max(0, result.Total - _wantedRowCount);
                start = Math.Max(0, _wantedFirstRow - RowBuffer);
                request.Skip = start;
                result = await _boundDataSource.ReadAsync(request, cancellationToken);
                if (version != _refreshVersion) return;
            }

            _currentResult = result;
            _window = result.Data?.ToArray() ?? Array.Empty<TItem>();
            _windowStart = result.Total == 0 ? 0 : start;
            _isLoading = false;
            RaiseDataWindowChanged();
        }
        finally
        {
            if (version == _refreshVersion)
            {
                _isLoading = false;
            }
        }
    }

    /// <summary>Restarts from the first row because a sort, filter, group or source change invalidates the scroll position.</summary>
    private void ResetAndRefresh()
    {
        _wantedFirstRow = 0;
        _verticalOffset = 0;
        QueryChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshDataAsync();
    }

    private void RaiseDataWindowChanged()
    {
        UpdateViewport(_horizontalOffset, _verticalOffset, ViewportWidth, ViewportHeight);
        OnPropertyChanged(nameof(RenderedItems));
        OnPropertyChanged(nameof(TotalItemsCount));
        DataWindowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateViewport(double horizontalOffset, double verticalOffset, double viewportWidth, double viewportHeight)
    {
        _horizontalOffset = horizontalOffset;
        _verticalOffset = verticalOffset;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;

        if (EnableVirtualization)
        {
            VisibleRowRange = _virtualizer.CalculateVisibleRows(verticalOffset, viewportHeight, TotalItemsCount);
            VisibleColumnRange = _virtualizer.CalculateVisibleColumns(horizontalOffset, viewportWidth, Columns.Count);
        }
        else
        {
            VisibleRowRange = new IndexRange(0, TotalItemsCount - 1);
            VisibleColumnRange = new IndexRange(0, Columns.Count - 1);
        }
    }

    // ==========================================
    // MULTI-COLUMN SORTING & SEQUENCES
    // ==========================================

    public void SortBy(string fieldName, ListSortDirection direction)
    {
        CurrentRequest.Sorts.Clear();
        foreach (var col in Columns)
        {
            col.SortDirection = null;
            col.SortIndex = -1;
        }

        var targetCol = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (targetCol != null)
        {
            targetCol.SortDirection = direction;
            targetCol.SortIndex = 0;
        }

        CurrentRequest.Sorts.Add(new SortDescriptor(fieldName, direction));
        ResetAndRefresh();
    }

    public void CycleSort(string fieldName, bool allowMultiSort = false)
    {
        var targetCol = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (targetCol == null || !targetCol.Sortable) return;

        if (!allowMultiSort || !EnableMultiColumnSorting)
        {
            foreach (var col in Columns.Where(c => c != targetCol))
            {
                col.SortDirection = null;
                col.SortIndex = -1;
            }
            CurrentRequest.Sorts.Clear();
        }

        // Tri-state cycle: null -> Ascending -> Descending -> null
        if (targetCol.SortDirection == null)
        {
            targetCol.SortDirection = ListSortDirection.Ascending;
            targetCol.SortIndex = CurrentRequest.Sorts.Count;
            CurrentRequest.Sorts.Add(new SortDescriptor(fieldName, ListSortDirection.Ascending));
        }
        else if (targetCol.SortDirection == ListSortDirection.Ascending)
        {
            targetCol.SortDirection = ListSortDirection.Descending;
            var existing = CurrentRequest.Sorts.FirstOrDefault(s => s.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (existing != null) existing.SortDirection = ListSortDirection.Descending;
        }
        else
        {
            targetCol.SortDirection = null;
            targetCol.SortIndex = -1;
            CurrentRequest.Sorts.RemoveAll(s => s.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            ReindexSortSequences();
        }

        ResetAndRefresh();
    }

    public void ClearSort()
    {
        CurrentRequest.Sorts.Clear();
        foreach (var col in Columns)
        {
            col.SortDirection = null;
            col.SortIndex = -1;
        }
        ResetAndRefresh();
    }

    private void ReindexSortSequences()
    {
        for (int i = 0; i < CurrentRequest.Sorts.Count; i++)
        {
            var sort = CurrentRequest.Sorts[i];
            var col = Columns.FirstOrDefault(c => c.Field.Equals(sort.Member, StringComparison.OrdinalIgnoreCase));
            if (col != null) col.SortIndex = i;
        }
    }

    // ==========================================
    // FILTERING, EXCEL FILTERS & SEARCH PANEL
    // ==========================================

    public void FilterBy(string fieldName, FilterOperator op, object? value)
    {
        RemoveColumnFilters(fieldName);
        if (value != null || op is FilterOperator.IsNull or FilterOperator.IsNotNull)
        {
            CurrentRequest.Filters.Add(new FilterDescriptor(fieldName, op, value));
        }
        ResetAndRefresh();
    }

    private void RemoveColumnFilters(string fieldName)
    {
        if (_excelFilters.Remove(fieldName, out var previous))
        {
            CurrentRequest.Filters.Remove(previous);
        }

        var column = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        column?.ExcelFilterValues.Clear();
        QuickFilters.Remove(fieldName);
        CurrentRequest.Filters.RemoveAll(f => f is FilterDescriptor fd && fd.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyExcelFilter(string fieldName, IEnumerable<object> selectedValues)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col == null) return;

        RemoveColumnFilters(col.Field);

        var composite = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };

        foreach (var val in selectedValues)
        {
            col.ExcelFilterValues.Add(val);
            composite.FilterDescriptors.Add(new FilterDescriptor(fieldName, FilterOperator.IsEqualTo, val));
        }

        if (composite.FilterDescriptors.Count > 0)
        {
            CurrentRequest.Filters.Add(composite);
            _excelFilters[col.Field] = composite;
        }

        ResetAndRefresh();
    }

    public void ClearExcelFilter(string fieldName)
    {
        if (_excelFilters.Remove(fieldName, out var previous))
        {
            CurrentRequest.Filters.Remove(previous);
        }

        var column = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        column?.ExcelFilterValues.Clear();
        ResetAndRefresh();
    }

    /// <summary>Distinct non-null values of a column in the bound in-memory source, optionally text-filtered; stops after <paramref name="maxCount"/>.</summary>
    public IReadOnlyList<object> GetDistinctValues(string fieldName, string? contains = null, int maxCount = 1000, CancellationToken cancellationToken = default)
    {
        var prop = typeof(TItem).GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null || _dataSource == null || maxCount <= 0)
        {
            return Array.Empty<object>();
        }

        var seen = new HashSet<object>();
        var values = new List<object>();
        foreach (var item in _dataSource)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = prop.GetValue(item);
            if (value == null || !seen.Add(value))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(contains) && !(value.ToString() ?? string.Empty).Contains(contains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Add(value);
            if (values.Count >= maxCount)
            {
                break;
            }
        }

        return values.OrderBy(v => v, Comparer<object>.Create((a, b) => a is IComparable ca && a.GetType() == b.GetType() ? ca.CompareTo(b) : string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public void SetQuickFilter(string fieldName, string text)
    {
        RemoveColumnFilters(fieldName);
        if (string.IsNullOrWhiteSpace(text))
        {
        }
        else
        {
            QuickFilters[fieldName] = text;
            CurrentRequest.Filters.Add(new FilterDescriptor(fieldName, FilterOperator.Contains, text));
        }
        ResetAndRefresh();
    }

    private void ApplySearchPanelFilter(string? query)
    {
        if (_searchFilter != null)
        {
            CurrentRequest.Filters.Remove(_searchFilter);
            _searchFilter = null;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchComposite = new CompositeFilterDescriptor { LogicalOperator = FilterLogicalOperator.Or };
            foreach (var col in Columns.Where(c => c.Visible && c.Searchable))
            {
                searchComposite.FilterDescriptors.Add(new FilterDescriptor(col.Field, FilterOperator.Contains, query));
            }

            if (searchComposite.FilterDescriptors.Count > 0)
            {
                CurrentRequest.Filters.Add(searchComposite);
                _searchFilter = searchComposite;
            }
        }

        ResetAndRefresh();
    }

    public void SaveFilterPreset(string presetName)
    {
        SavedFilterPresets[presetName] = new List<IFilterDescriptor>(CurrentRequest.Filters);
    }

    public void ApplyFilterPreset(string presetName)
    {
        if (SavedFilterPresets.TryGetValue(presetName, out var filters))
        {
            CurrentRequest.Filters.Clear();
            CurrentRequest.Filters.AddRange(filters);
            ResetAndRefresh();
        }
    }

    // ==========================================
    // GROUPING & DRAG-TO-GROUP
    // ==========================================

    public void GroupBy(string fieldName)
    {
        if (!CurrentRequest.Groups.Any(g => g.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentRequest.Groups.Add(new GroupDescriptor { Member = fieldName });
            ResetAndRefresh();
        }
    }

    public void RemoveGroup(string fieldName)
    {
        CurrentRequest.Groups.RemoveAll(g => g.Member.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        ResetAndRefresh();
    }

    public void ClearGroups()
    {
        CurrentRequest.Groups.Clear();
        ResetAndRefresh();
    }

    public void ToggleGroupExpansion(object groupKey)
    {
        if (!_expandedGroupKeys.Add(groupKey))
        {
            _expandedGroupKeys.Remove(groupKey);
        }
        OnPropertyChanged(nameof(RenderedItems));
    }

    public void ExpandAllGroups()
    {
        foreach (var grp in CurrentRequest.Groups)
        {
            _expandedGroupKeys.Add(grp.Member);
        }
        OnPropertyChanged(nameof(RenderedItems));
    }

    public void CollapseAllGroups()
    {
        _expandedGroupKeys.Clear();
        OnPropertyChanged(nameof(RenderedItems));
    }

    // ==========================================
    // EDITING, BATCH EDITING & UNDO/REDO
    // ==========================================

    public bool EditCell(TItem item, string fieldName, object? newValue)
    {
        if (item == null) return false;
        var prop = typeof(TItem).GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null || !prop.CanWrite) return false;

        var oldValue = prop.GetValue(item);
        try
        {
            var converted = newValue != null && newValue.GetType() != prop.PropertyType
                ? Convert.ChangeType(newValue, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType)
                : newValue;

            prop.SetValue(item, converted);
            _undoStack.Push(new GridEditRecord<TItem>(item, fieldName, oldValue, converted));
            _redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(RenderedItems));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool UndoLastEdit()
    {
        if (_undoStack.Count == 0) return false;
        var record = _undoStack.Pop();
        var prop = typeof(TItem).GetProperty(record.FieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(record.Item, record.OldValue);
            _redoStack.Push(record);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(RenderedItems));
            return true;
        }
        return false;
    }

    public bool RedoLastEdit()
    {
        if (_redoStack.Count == 0) return false;
        var record = _redoStack.Pop();
        var prop = typeof(TItem).GetProperty(record.FieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(record.Item, record.NewValue);
            _undoStack.Push(record);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(RenderedItems));
            return true;
        }
        return false;
    }

    public bool ReorderRow(int fromIndex, int toIndex)
    {
        if (_dataSource is IList<TItem> list && fromIndex >= 0 && fromIndex < list.Count && toIndex >= 0 && toIndex < list.Count && fromIndex != toIndex)
        {
            var item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
            _ = RefreshDataAsync();
            return true;
        }
        return false;
    }

    public void ToggleDetailExpansion(TItem rowItem)
    {
        if (!_expandedDetailRows.Add(rowItem))
        {
            _expandedDetailRows.Remove(rowItem);
        }
        OnPropertyChanged(nameof(RenderedItems));
    }

    public void ExpandAllDetails()
    {
        foreach (var item in RenderedItems)
        {
            _expandedDetailRows.Add(item);
        }
        OnPropertyChanged(nameof(RenderedItems));
    }

    public void CollapseAllDetails()
    {
        _expandedDetailRows.Clear();
        OnPropertyChanged(nameof(RenderedItems));
    }

    public bool IsDetailExpanded(TItem rowItem) => _expandedDetailRows.Contains(rowItem);

    // ==========================================
    // COLUMN CHOOSER, PRESETS & REORDERING
    // ==========================================

    public void SetColumnVisibility(string fieldName, bool visible)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col != null && col.CanHide)
        {
            col.Visible = visible;
            OnPropertyChanged(nameof(Columns));
            _ = RefreshDataAsync();
        }
    }

    public void ReorderColumn(string fieldName, int newIndex)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col != null && newIndex >= 0 && newIndex < Columns.Count)
        {
            Columns.Remove(col);
            Columns.Insert(newIndex, col);
            OnPropertyChanged(nameof(Columns));
        }
    }

    /// <summary>Sets a column width within its min/max limits; returns false for unknown columns or resizing disabled.</summary>
    public bool ResizeColumn(string fieldName, double width)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col == null || !col.Resizable)
        {
            return false;
        }

        col.Width = GridColumnLayout.ClampWidth(col, width);
        OnPropertyChanged(nameof(Columns));
        return true;
    }

    /// <summary>Moves a column before another (or to the end when null); the column adopts the target's pin zone.</summary>
    public bool MoveColumnBefore(string fieldName, string? beforeFieldName)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col == null || !col.Reorderable)
        {
            return false;
        }

        GridColumn? target = null;
        if (beforeFieldName != null)
        {
            target = Columns.FirstOrDefault(c => c.Field.Equals(beforeFieldName, StringComparison.OrdinalIgnoreCase));
            if (target == null || ReferenceEquals(target, col))
            {
                return false;
            }
        }

        Columns.Remove(col);
        Columns.Insert(target == null ? Columns.Count : Columns.IndexOf(target), col);
        if (target != null)
        {
            col.PinPosition = target.PinPosition;
        }

        OnPropertyChanged(nameof(Columns));
        return true;
    }

    public void PinColumn(string fieldName, ColumnPinPosition position)
    {
        var col = Columns.FirstOrDefault(c => c.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (col != null)
        {
            col.PinPosition = position;
            OnPropertyChanged(nameof(Columns));
        }
    }

    public ColumnLayoutProfile CreateLayoutProfile(string profileName = "Default")
    {
        var profile = new ColumnLayoutProfile { ProfileName = profileName };
        for (int i = 0; i < Columns.Count; i++)
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
        return profile;
    }

    public void ApplyLayoutProfile(ColumnLayoutProfile profile)
    {
        var dict = profile.Columns.ToDictionary(c => c.Field, StringComparer.OrdinalIgnoreCase);
        foreach (var col in Columns)
        {
            if (dict.TryGetValue(col.Field, out var state))
            {
                col.Visible = state.Visible;
                col.Width = state.Width;
                col.PinPosition = state.PinPosition;
                col.SortDirection = state.SortDirection;
                col.SortIndex = state.SortIndex;
            }
        }

        // Reorder
        var sorted = Columns.OrderBy(c => dict.TryGetValue(c.Field, out var s) ? s.DisplayIndex : int.MaxValue).ToList();
        Columns.Clear();
        Columns.AddRange(sorted);
        ResetAndRefresh();
    }

    public void OnThemeChanged(ITheme newTheme) { }

    public void Dispose()
    {
        _selectedItems.Clear();
        _expandedGroupKeys.Clear();
        _expandedDetailRows.Clear();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
