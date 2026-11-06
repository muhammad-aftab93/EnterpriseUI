using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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

/// <summary>A computed footer value for one column summary.</summary>
public sealed record GridFooterValue(GridColumn Column, GridColumnSummary Summary, object? Value, string Text);

/// <summary>
/// Enterprise DataGrid engine: windowed or paged data access, multi-column sorting, filtering (column, value list, search),
/// grouping with aggregates, master/detail and tree hierarchies, selection, validated editing with undo/redo and transactions,
/// footer summaries and calculated columns, and layout profiles. Rendering is left to the platform controls.
/// </summary>
/// <remarks>
/// Property and method calls are expected on the UI thread. Data loads run asynchronously and raise
/// <see cref="DataWindowChanged"/> when rows are ready; failures raise <see cref="DataLoadFailed"/>.
/// </remarks>
public partial class DataGrid<TItem> : ObservableObject, IDataBoundControl<TItem>, IVirtualizedControl, IThemeableControl, IAccessibleControl
{
    [Flags]
    private enum LoadKind
    {
        /// <summary>Fetch rows for the visible window only.</summary>
        Window = 0,
        /// <summary>Sort, filter, group or paging changed; recompute summaries and drop cached orderings.</summary>
        Query = 1,
        /// <summary>Underlying data changed; also drop the source cache and hierarchy lookups.</summary>
        Data = 2 | Query
    }

    private readonly VirtualizationManager _virtualizer = new();
    private IEnumerable<TItem>? _dataSource;
    private IDataSource<TItem>? _boundDataSource;
    private INotifyCollectionChanged? _observedCollection;
    private int _totalRecords;
    private bool _hasResult;
    private IReadOnlyList<TItem> _window = Array.Empty<TItem>();
    private int _windowStart;
    private int _wantedFirstRow;
    private int _wantedRowCount = 50;
    private int _refreshVersion;
    private volatile bool _isLoading;
    private double _horizontalOffset;
    private double _verticalOffset;
    private string? _searchPanelText;
    private IFilterDescriptor? _searchFilter;
    private readonly Dictionary<string, IFilterDescriptor> _excelFilters = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<GridFooterValue> _footerValues = Array.Empty<GridFooterValue>();
    private Exception? _lastError;
    private int _updateDepth;
    private bool _pendingRefresh;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public DataGrid()
    {
        Selection.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedItem));
            OnPropertyChanged(nameof(SelectedItems));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    // Grid options & columns
    public List<GridColumn> Columns { get; } = new();
    public List<GridColumnBand> Bands { get; } = new();
    public DataSourceRequest CurrentRequest { get; } = new();
    public bool EnableVirtualization { get; set; } = true;
    public bool EnableGrouping { get; set; } = true;
    public bool EnableFiltering { get; set; } = true;
    public bool EnableSorting { get; set; } = true;
    public bool EnableMultiColumnSorting { get; set; } = true;
    public bool EnableMasterDetail { get; set; }
    public bool ShowGroupPanel { get; set; } = true;
    public bool EnableSearchPanel { get; set; } = true;
    public bool EnableQuickFilterRow { get; set; }
    public bool EnableAlternateRows { get; set; } = true;
    public object? DetailTemplate { get; set; }

    // Quick filters & presets
    public Dictionary<string, string> QuickFilters { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<IFilterDescriptor>> SavedFilterPresets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GroupSummaryDescriptor> GroupSummaries { get; } = new();
    public List<GridFormatRule> FormatRules { get; } = new();
    public GridClipboardCopyMode ClipboardCopyMode { get; set; } = GridClipboardCopyMode.CellsWithHeaders;

    /// <summary>Raised for each displayed row so applications can style rows at runtime.</summary>
    public event EventHandler<GridRowStyleEventArgs<TItem>>? RowStyleNeeded;

    public GridRowStyleEventArgs<TItem>? GetRowStyle(TItem item, int rowIndex)
    {
        var handler = RowStyleNeeded;
        if (handler == null) return null;
        var args = new GridRowStyleEventArgs<TItem>(item, rowIndex);
        handler(this, args);
        return args;
    }

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

    // Virtualization
    public double ViewportWidth { get; private set; } = 800;
    public double ViewportHeight { get; private set; } = 600;
    public double ItemHeight { get => _virtualizer.ItemHeight; set => _virtualizer.ItemHeight = value; }
    public double ItemWidth { get => _virtualizer.ItemWidth; set => _virtualizer.ItemWidth = value; }
    public IndexRange VisibleRowRange { get; private set; }
    public IndexRange VisibleColumnRange { get; private set; }

    /// <summary>Rows displayed on the current page (including group, detail and tree rows).</summary>
    public int TotalItemsCount => IsMaterialized ? PageRowCount(_rows.Length) : PageRowCount(_totalRecords);

    /// <summary>Data records matching the filters across all pages, excluding group headers and child rows.</summary>
    public int TotalRecordCount => _totalRecords;

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

    // Data binding
    public IEnumerable<TItem>? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value))
            {
                return;
            }

            if (_observedCollection != null)
            {
                _observedCollection.CollectionChanged -= OnSourceCollectionChanged;
                _observedCollection = null;
            }

            _dataSource = value;
            _boundDataSource = value != null ? new DataSource<TItem>(value) : null;
            if (value is INotifyCollectionChanged notify)
            {
                _observedCollection = notify;
                notify.CollectionChanged += OnSourceCollectionChanged;
            }

            OnPropertyChanged();
            ResetAndRefresh(LoadKind.Data);
        }
    }

    public IDataSource<TItem>? CustomDataSource
    {
        get => _boundDataSource;
        set
        {
            _boundDataSource = value;
            ResetAndRefresh(LoadKind.Data);
        }
    }

    /// <summary>Last data load failure, or null after a successful load.</summary>
    public Exception? LastError => _lastError;

    public bool IsLoading => _isLoading;

    /// <summary>Extra rows fetched before and after the requested range to make scrolling smooth.</summary>
    public int RowBuffer { get; set; } = 100;

    public IReadOnlyList<TItem> RenderedItems => IsMaterialized ? _materialized : _window;
    public int WindowStart => _windowStart;

    public event EventHandler? DataWindowChanged;
    public event EventHandler? QueryChanged;
    public event EventHandler<GridDataLoadFailedEventArgs>? DataLoadFailed;
    public event EventHandler<GridAnnouncementEventArgs>? Announcement;

    /// <summary>Raised when a bound change notification or <see cref="NotifyDataChanged"/> reloaded the grid.</summary>
    public event EventHandler? DataRefreshed;

    /// <summary>Returns the data item at a display row of the current page if it is a data row and cached.</summary>
    public bool TryGetItemAt(int absoluteIndex, out TItem item)
    {
        if (TryGetRowAt(absoluteIndex, out var row) && row.Kind == GridRowKind.Data && row.Item != null)
        {
            item = row.Item;
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

        if (UsesMaterializedRows)
        {
            if (!_materializedValid && !_isLoading)
            {
                _ = RefreshSafeAsync(LoadKind.Window);
            }

            return _materializedValid;
        }

        if (IsWindowSatisfied())
        {
            return true;
        }

        if (!_isLoading)
        {
            _ = RefreshSafeAsync(LoadKind.Window);
        }

        return false;
    }

    private bool IsWindowSatisfied()
    {
        if (!_hasResult)
        {
            return false;
        }

        var lastRow = Math.Min(TotalItemsCount, _wantedFirstRow + _wantedRowCount);
        var windowEnd = _windowStart + _window.Count;
        return lastRow <= _wantedFirstRow || (_wantedFirstRow >= _windowStart && lastRow <= windowEnd);
    }

    /// <summary>Reads every row matching the current filters and sort order from the start, for export and printing; null reads all.</summary>
    public async Task<IReadOnlyList<TItem>> ReadAllAsync(int? maxRows = null, CancellationToken cancellationToken = default)
    {
        if (_boundDataSource == null)
        {
            return Array.Empty<TItem>();
        }

        var request = CurrentRequest.Clone();
        request.Skip = 0;
        request.Take = maxRows ?? 0;
        request.Aggregates.Clear();
        var result = await _boundDataSource.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Data?.ToList() ?? new List<TItem>();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDataAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reloads from the source, discarding cached orderings; call after changing items outside the grid.</summary>
    public Task RefreshDataAsync(CancellationToken cancellationToken = default) => LoadAsync(LoadKind.Data, cancellationToken);

    /// <summary>Fire-and-forget load that reports failures through <see cref="DataLoadFailed"/> instead of throwing on a background task.</summary>
    private async Task RefreshSafeAsync(LoadKind kind)
    {
        try
        {
            await LoadAsync(kind, CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _lastError = ex;
            _isLoading = false;
            DataLoadFailed?.Invoke(this, new GridDataLoadFailedEventArgs(ex));
        }
    }

    private async Task LoadAsync(LoadKind kind, CancellationToken cancellationToken)
    {
        if (_updateDepth > 0)
        {
            _pendingRefresh = true;
            return;
        }

        var version = Interlocked.Increment(ref _refreshVersion);

        if (_boundDataSource == null)
        {
            ClearLoadedRows();
            RaiseDataWindowChanged();
            return;
        }

        _isLoading = true;
        try
        {
            if ((kind & LoadKind.Query) != 0 || !UsesMaterializedRows)
            {
                _materializedValid = false;
            }

            if ((kind & LoadKind.Query) != 0)
            {
                _clientCache = null;
            }

            if (kind == LoadKind.Data)
            {
                (_boundDataSource as IInvalidatableDataSource)?.Invalidate();
                _hierarchy.Clear();
                _runningTotals.Clear();
            }

            if (UsesMaterializedRows)
            {
                await LoadMaterializedAsync(version, kind, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                await LoadWindowAsync(version, kind, cancellationToken).ConfigureAwait(true);
            }

            if (version == _refreshVersion)
            {
                _lastError = null;
            }
        }
        finally
        {
            if (version == _refreshVersion)
            {
                _isLoading = false;
            }
        }
    }

    private void ClearLoadedRows()
    {
        _totalRecords = 0;
        _window = Array.Empty<TItem>();
        _windowStart = 0;
        _materialized = Array.Empty<TItem>();
        _rows = Array.Empty<RowRef>();
        _materializedValid = false;
        _footerValues = Array.Empty<GridFooterValue>();
        _hasResult = true;
    }

    private async Task LoadWindowAsync(int version, LoadKind kind, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var pageOffset = PageOffset;
            var start = Math.Max(0, _wantedFirstRow - RowBuffer);
            var take = _wantedRowCount + 2 * RowBuffer;
            IReadOnlyList<TItem> data;
            int total;

            if (PagingMode == GridPagingMode.Client)
            {
                if (_clientCache == null)
                {
                    var all = CurrentRequest.Clone();
                    all.Skip = 0;
                    all.Take = 0;
                    ApplyAggregates(all, kind);
                    var allResult = await _boundDataSource!.ReadAsync(all, cancellationToken).ConfigureAwait(true);
                    if (version != _refreshVersion) return;
                    _clientCache = allResult.Data?.ToArray() ?? Array.Empty<TItem>();
                    CaptureFooter(allResult, kind);
                }

                total = _clientCache.Length;
                var pageRows = Math.Clamp(total - pageOffset, 0, PageSize);
                start = Math.Min(start, pageRows);
                var count = Math.Clamp(take, 0, pageRows - start);
                data = new ReadOnlyListSlice<TItem>(_clientCache, pageOffset + start, count);
            }
            else
            {
                if (PagingMode == GridPagingMode.Virtual && PageSize > 0)
                {
                    var end = (int)Math.Ceiling((_wantedFirstRow + _wantedRowCount + RowBuffer) / (double)PageSize) * PageSize;
                    start = start / PageSize * PageSize;
                    take = Math.Max(PageSize, end - start);
                }
                else if (PagingMode == GridPagingMode.Server)
                {
                    take = Math.Max(1, Math.Min(take, PageSize - start));
                }
                else if (PagingMode == GridPagingMode.Infinite)
                {
                    take = Math.Max(1, Math.Min(take, Math.Max(1, _infiniteLoaded - start)));
                }

                var request = CurrentRequest.Clone();
                request.Skip = pageOffset + start;
                request.Take = take;
                ApplyAggregates(request, kind);

                var result = await _boundDataSource!.ReadAsync(request, cancellationToken).ConfigureAwait(true);
                if (version != _refreshVersion) return;

                total = result.Total;
                data = result.Data?.ToArray() ?? Array.Empty<TItem>();
                CaptureFooter(result, kind);
            }

            if (version != _refreshVersion) return;

            // A filter or page-size change can leave the requested page or scroll position past the end of the data.
            if (total > 0 && PagingMode is GridPagingMode.Client or GridPagingMode.Server && pageOffset >= total)
            {
                _pageIndex = Math.Max(0, (total - 1) / PageSize);
                continue;
            }

            var displayRows = PageRowCount(total);
            if (total > 0 && start >= displayRows && displayRows > 0 && PagingMode != GridPagingMode.Client)
            {
                _wantedFirstRow = Math.Max(0, displayRows - _wantedRowCount);
                continue;
            }

            _totalRecords = total;
            _window = data;
            _windowStart = total == 0 ? 0 : start;
            _hasResult = true;
            _isLoading = false;
            RaiseDataWindowChanged();

            if (!IsWindowSatisfied() && version == _refreshVersion)
            {
                // The viewport moved while this fetch was running.
                _ = RefreshSafeAsync(LoadKind.Window);
            }

            if ((kind & LoadKind.Query) != 0 && RunningTotalColumns().Any())
            {
                _ = ComputeRunningTotalsSafeAsync();
            }

            return;
        }
    }

    private void ApplyAggregates(DataSourceRequest request, LoadKind kind)
    {
        request.Aggregates.Clear();
        if ((kind & LoadKind.Query) != 0)
        {
            request.Aggregates.AddRange(BuildFooterAggregates());
        }
    }

    private void CaptureFooter(DataSourceResult<TItem> result, LoadKind kind)
    {
        if ((kind & LoadKind.Query) != 0)
        {
            _footerValues = BuildFooterValues(result.AggregateResults);
        }
    }

    /// <summary>Restarts from the first row because a sort, filter, group or source change invalidates the scroll position.</summary>
    private void ResetAndRefresh(LoadKind kind = LoadKind.Query)
    {
        _wantedFirstRow = 0;
        _verticalOffset = 0;
        _pageIndex = 0;
        _infiniteLoaded = PageSize;
        _detailExpansions.Clear();
        _treeToggles.Clear();
        _groupToggles.Clear();
        Selection.Clear();
        QueryChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshSafeAsync(kind);
    }

    private void RaiseDataWindowChanged()
    {
        Selection.SetBounds(TotalItemsCount, GridColumnLayout.OrderVisible(Columns).Count);
        UpdateViewport(_horizontalOffset, _verticalOffset, ViewportWidth, ViewportHeight);
        OnPropertyChanged(nameof(RenderedItems));
        OnPropertyChanged(nameof(TotalItemsCount));
        OnPropertyChanged(nameof(TotalRecordCount));
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

    // ------------------------------------------------------------------
    // Batched and real-time updates
    // ------------------------------------------------------------------

    /// <summary>Suspends reloads until <see cref="EndUpdate"/>; nesting is supported.</summary>
    public void BeginUpdate() => _updateDepth++;

    public void EndUpdate()
    {
        if (_updateDepth == 0) return;
        if (--_updateDepth == 0 && _pendingRefresh)
        {
            _pendingRefresh = false;
            _ = RefreshSafeAsync(LoadKind.Data);
        }
    }

    /// <summary>Delay used to coalesce bursts of collection change notifications into one reload.</summary>
    public TimeSpan AutoRefreshDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Reload automatically when the bound collection raises <see cref="INotifyCollectionChanged"/>.</summary>
    public bool AutoRefreshOnCollectionChange { get; set; } = true;

    private int _refreshRequests;

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AutoRefreshOnCollectionChange)
        {
            NotifyDataChanged();
        }
    }

    /// <summary>Tells the grid items were added, removed or modified outside it; bursts of calls produce a single reload.</summary>
    public void NotifyDataChanged()
    {
        var request = Interlocked.Increment(ref _refreshRequests);
        _ = DebouncedRefreshAsync(request);
    }

    private async Task DebouncedRefreshAsync(int request)
    {
        try
        {
            if (AutoRefreshDelay > TimeSpan.Zero)
            {
                await Task.Delay(AutoRefreshDelay).ConfigureAwait(true);
            }

            if (request != Volatile.Read(ref _refreshRequests))
            {
                return;
            }

            await RefreshSafeAsync(LoadKind.Data).ConfigureAwait(true);
            DataRefreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _lastError = ex;
            DataLoadFailed?.Invoke(this, new GridDataLoadFailedEventArgs(ex));
        }
    }

    public void OnThemeChanged(ITheme newTheme) { }

    public void Dispose()
    {
        if (_observedCollection != null)
        {
            _observedCollection.CollectionChanged -= OnSourceCollectionChanged;
            _observedCollection = null;
        }

        Interlocked.Increment(ref _refreshVersion);
        Selection.Clear();
        _detailExpansions.Clear();
        _treeToggles.Clear();
        _groupToggles.Clear();
        _hierarchy.Clear();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Read-only window over a list without copying it.</summary>
internal sealed class ReadOnlyListSlice<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _start;

    public ReadOnlyListSlice(IReadOnlyList<T> source, int start, int count)
    {
        _source = source;
        _start = start;
        Count = Math.Max(0, count);
    }

    public int Count { get; }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _source[_start + index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return _source[_start + i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
