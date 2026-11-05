using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

[ToolboxItem(true)]
public class EnterpriseDataGrid : EnterpriseDataGrid<object>
{
}

/// <summary>
/// Windows Forms data grid with DPI-aware GDI+ rendering over <see cref="DataGrid{TItem}"/>: paging, sorting, filtering, grouping,
/// master/detail and tree hierarchy, row/cell/column selection, in-cell/popup editing with validation, summaries, search highlighting,
/// export, printing, drag and drop, theme-derived colors (including high contrast) and UI Automation support.
/// </summary>
[ToolboxItem(false)]
public partial class EnterpriseDataGrid<TItem> : Control, IThemeableControl, INotifyPropertyChanged
{
    private const int FilterListLimit = 1000;

    private enum DragMode { None, Resize, HeaderPress, HeaderDrag, Select, RowPress }
    private enum EditorMode { None, Cell, FilterRow }

    private readonly record struct RowSlot(int Index, GridRow<TItem>? Row, Rectangle Bounds);

    private sealed class Frame
    {
        public int SearchH, GroupH, BandRowH, BandDepth, HeaderH, FilterH, SummaryRows, SummaryH, FooterH, PagerH;
        public int Gutter, ExpanderW, CheckW, RowH, DetailH;
        public int GroupTop, BandTop, HeaderTop, FilterTop, BodyTop, BodyBottom, BodyWidth, FullRows, SummaryTop, StatusTop, PagerTop;
        public ColumnLayoutResult Layout = null!;
        public IReadOnlyList<BandSpan> Bands = Array.Empty<BandSpan>();
        public List<GridColumn> Ordered = new();
    }

    /// <summary>Grid colors resolved from the active theme, or from the system colors when Windows high contrast is on.</summary>
    private sealed class Colors
    {
        public Color Surface, Alt, Header, Border, Text, Muted, Primary, PrimaryText, Danger, Success, Warning;
        public Color SelectionBack, SelectionText, HoverBack, FocusRing, HighlightBack, HighlightText, ErrorBack;
        public bool HighContrast;
    }

    private sealed class FilterItem
    {
        public FilterItem(object value) => Value = value;
        public object Value { get; }
        public override string ToString() => Value.ToString() ?? string.Empty;
    }

    private readonly DataGrid<TItem> _engine = new();
    private readonly ContextMenuStrip _headerContextMenu = new();
    private readonly ContextMenuStrip _bodyContextMenu = new();
    private readonly ToolStripDropDown _filterDropDown = new() { AutoClose = true, Padding = Padding.Empty };
    private readonly ToolStripMenuItem _chooserMenu = new("Column Chooser");
    private readonly ToolStripMenuItem _pinLeftItem = new("Pin Left");
    private readonly ToolStripMenuItem _pinRightItem = new("Pin Right");
    private readonly ToolStripMenuItem _unpinItem = new("Unpin");
    private readonly ToolStripMenuItem _hideItem = new("Hide Column");
    private readonly ToolStripMenuItem _groupItem = new("Group By This Column");
    private readonly ToolTip _toolTip = new();
    private readonly VScrollBar _vScroll = new() { Visible = false, TabStop = false };
    private readonly HScrollBar _hScroll = new() { Visible = false, TabStop = false };
    private readonly EventHandler<ITheme> _themeHandler;

    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private Colors? _colors;
    private Font? _headerFont;
    private Font? _smallFont;
    private int _firstRow;
    private int _horizontalOffset;
    private bool _updatingScroll;
    private int _hoveredRow = -1;
    private int _hoveredHeader = -1;
    private string? _toolTipText;
    private bool _bestFitDone;

    private DragMode _drag;
    private GridColumn? _dragColumn;
    private Point _dragStart;
    private int _resizeStartWidth;
    private int _dropIndex = -1;
    private bool _resized;
    private GridColumn? _contextColumn;

    private bool _showFilterRow;
    private bool _showFooter = true;
    private bool _showBands = true;
    private bool _fillColumns = true;
    private GridAutoSizeMode _autoSizeMode = GridAutoSizeMode.Fill;

    public EnterpriseDataGrid()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick,
            true);

        DoubleBuffered = true;
        TabStop = true;
        Font = new Font("Segoe UI", 9f);
        Size = new Size(680, 440);
        AccessibleRole = AccessibleRole.Table;
        AccessibleName = _engine.AriaLabel;
        AllowDrop = false;

        // Editing is on by default in the control; set EditMode to None to make the grid read-only.
        _engine.EditMode = GridEditMode.InCell;

        _vScroll.ValueChanged += (_, _) => OnVerticalScroll();
        _hScroll.ValueChanged += (_, _) => OnHorizontalScroll();
        Controls.Add(_vScroll);
        Controls.Add(_hScroll);

        BuildHeaderContextMenu();
        BuildBodyContextMenu();
        InitializeFeatures();

        _engine.PropertyChanged += OnEngineChanged;
        _engine.QueryChanged += OnEngineQueryChanged;
        _engine.DataWindowChanged += OnEngineWindowChanged;
        _engine.PageChanged += OnEnginePageChanged;
        _engine.ScrollToRowRequested += OnEngineScrollRequested;
        _engine.Announcement += OnEngineAnnouncement;
        _engine.SearchResultChanged += OnEngineSearchChanged;
        _engine.ValidationChanged += OnEngineVisualChanged;
        _engine.RunningTotalsChanged += OnEngineVisualChanged;
        _engine.UndoStackChanged += OnEngineVisualChanged;
        _engine.DataLoadFailed += OnEngineLoadFailed;
        _engine.SelectionChanged += OnEngineSelectionChanged;
        _engine.Selection.FocusChanged += OnEngineFocusChanged;
        _themeHandler = (_, newTheme) => OnThemeChanged(newTheme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }

    /// <summary>The underlying engine retained for compatibility; grid features are also available directly on the control.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGrid<TItem> Engine => _engine;

    // ------------------------------------------------------------------
    // Direct Grid Properties, Collections, Events & Operations
    // ------------------------------------------------------------------

    public List<GridColumn> Columns => _engine.Columns;
    public List<GridColumnBand> Bands => _engine.Bands;
    public DataSourceRequest CurrentRequest => _engine.CurrentRequest;
    public GridSelectionModel Selection => _engine.Selection;
    public GridSelectionMode SelectionMode { get => _engine.SelectionMode; set { _engine.SelectionMode = value; RefreshLayout(); } }
    public GridEditMode EditMode { get => _engine.EditMode; set => _engine.EditMode = value; }
    public bool EnableVirtualization { get => _engine.EnableVirtualization; set => _engine.EnableVirtualization = value; }
    public bool EnableGrouping { get => _engine.EnableGrouping; set { _engine.EnableGrouping = value; RefreshLayout(); } }
    public bool EnableFiltering { get => _engine.EnableFiltering; set { _engine.EnableFiltering = value; RefreshLayout(); } }
    public bool EnableSorting { get => _engine.EnableSorting; set => _engine.EnableSorting = value; }
    public bool EnableMultiColumnSorting { get => _engine.EnableMultiColumnSorting; set => _engine.EnableMultiColumnSorting = value; }
    public bool EnableAlternateRows { get => _engine.EnableAlternateRows; set { _engine.EnableAlternateRows = value; Invalidate(); } }
    public bool EnableMasterDetail { get => _engine.EnableMasterDetail; set { _engine.EnableMasterDetail = value; RefreshLayout(); } }
    public bool ShowGroupPanel { get => _engine.ShowGroupPanel; set { _engine.ShowGroupPanel = value; RefreshLayout(); } }
    public bool EnableSearchPanel { get => _engine.EnableSearchPanel; set { _engine.EnableSearchPanel = value; if (!value) ShowSearchPanel = false; } }
    public bool EnableQuickFilterRow { get => _engine.EnableQuickFilterRow; set => _engine.EnableQuickFilterRow = value; }
    public object? DetailTemplate { get => _engine.DetailTemplate; set => _engine.DetailTemplate = value; }
    public bool GroupsExpandedByDefault { get => _engine.GroupsExpandedByDefault; set => _engine.GroupsExpandedByDefault = value; }
    public bool ShowGroupFooters { get => _engine.ShowGroupFooters; set => _engine.ShowGroupFooters = value; }
    public bool RejectInvalidEdits { get => _engine.RejectInvalidEdits; set => _engine.RejectInvalidEdits = value; }

    public Dictionary<string, string> QuickFilters => _engine.QuickFilters;
    public Dictionary<string, List<IFilterDescriptor>> SavedFilterPresets => _engine.SavedFilterPresets;
    public List<GroupSummaryDescriptor> GroupSummaries => _engine.GroupSummaries;
    public List<GridFormatRule> FormatRules => _engine.FormatRules;
    public List<GridHierarchyRelation> Relations => _engine.Relations;
    public GridClipboardCopyMode ClipboardCopyMode { get => _engine.ClipboardCopyMode; set => _engine.ClipboardCopyMode = value; }
    public string? SearchPanelText { get => _engine.SearchPanelText; set => _engine.SearchPanelText = value; }

    public GridPagingMode PagingMode { get => _engine.PagingMode; set { _engine.PagingMode = value; RefreshLayout(); } }
    public int PageSize { get => _engine.PageSize; set => _engine.PageSize = value; }
    public int PageIndex { get => _engine.PageIndex; set => _engine.PageIndex = value; }
    public int PageCount => _engine.PageCount;
    public bool GoToPage(int pageIndex) => _engine.GoToPage(pageIndex);

    public double ItemHeight { get => _engine.ItemHeight; set { _engine.ItemHeight = value; RefreshLayout(); } }
    public double ItemWidth { get => _engine.ItemWidth; set { _engine.ItemWidth = value; RefreshLayout(); } }
    public int TotalItemsCount => _engine.TotalItemsCount;
    public int TotalRecordCount => _engine.TotalRecordCount;
    public int RowBuffer { get => _engine.RowBuffer; set => _engine.RowBuffer = value; }
    public bool CanUndo => _engine.CanUndo;
    public bool CanRedo => _engine.CanRedo;
    public bool IsLoading => _engine.IsLoading;
    public int WindowStart => _engine.WindowStart;
    public string? AriaLabel { get => _engine.AriaLabel; set { _engine.AriaLabel = value; AccessibleName = value; } }

    public IEnumerable<TItem>? DataSource { get => _engine.DataSource; set => _engine.DataSource = value; }
    public IDataSource<TItem>? CustomDataSource { get => _engine.CustomDataSource; set => _engine.CustomDataSource = value; }
    public TItem? SelectedItem { get => _engine.SelectedItem; set => _engine.SelectedItem = value; }
    public IReadOnlyList<TItem> SelectedItems => _engine.SelectedItems;
    public IReadOnlyList<TItem> RenderedItems => _engine.RenderedItems;

    public event EventHandler? DataWindowChanged { add => _engine.DataWindowChanged += value; remove => _engine.DataWindowChanged -= value; }
    public event EventHandler? QueryChanged { add => _engine.QueryChanged += value; remove => _engine.QueryChanged -= value; }
    public event EventHandler? PageChanged { add => _engine.PageChanged += value; remove => _engine.PageChanged -= value; }
    public event EventHandler<GridValidationEventArgs>? ValidationFailed { add => _engine.ValidationFailed += value; remove => _engine.ValidationFailed -= value; }
    public event EventHandler<GridRowStyleEventArgs<TItem>>? RowStyleNeeded { add => _engine.RowStyleNeeded += value; remove => _engine.RowStyleNeeded -= value; }
    public event EventHandler<GridAnnouncementEventArgs>? Announcement { add => _engine.Announcement += value; remove => _engine.Announcement -= value; }

    public bool TryGetItemAt(int absoluteIndex, out TItem item) => _engine.TryGetItemAt(absoluteIndex, out item);
    public bool TryGetRowAt(int displayIndex, out GridRow<TItem> row) => _engine.TryGetRowAt(displayIndex, out row);
    public bool EnsureRowsLoaded(int firstRow, int rowCount) => _engine.EnsureRowsLoaded(firstRow, rowCount);
    public Task<IReadOnlyList<TItem>> ReadAllAsync(int? maxRows = null, CancellationToken cancellationToken = default) => _engine.ReadAllAsync(maxRows, cancellationToken);
    public Task RefreshDataAsync(CancellationToken cancellationToken = default) => _engine.RefreshDataAsync(cancellationToken);

    public void SortBy(string fieldName, ListSortDirection direction) => _engine.SortBy(fieldName, direction);
    public void CycleSort(string fieldName, bool allowMultiSort = false) => _engine.CycleSort(fieldName, allowMultiSort);
    public void ClearSort() => _engine.ClearSort();

    public void FilterBy(string fieldName, FilterOperator op, object? value) => _engine.FilterBy(fieldName, op, value);
    public void ApplyExcelFilter(string fieldName, IEnumerable<object> selectedValues) => _engine.ApplyExcelFilter(fieldName, selectedValues);
    public void ClearExcelFilter(string fieldName) => _engine.ClearExcelFilter(fieldName);
    public void ClearFilters() => _engine.ClearFilters();
    public IReadOnlyList<object> GetDistinctValues(string fieldName, string? contains = null, int maxCount = 1000, CancellationToken cancellationToken = default) => _engine.GetDistinctValues(fieldName, contains, maxCount, cancellationToken);
    public void SetQuickFilter(string fieldName, string text) => _engine.SetQuickFilter(fieldName, text);
    public void SaveFilterPreset(string presetName) => _engine.SaveFilterPreset(presetName);
    public bool ApplyFilterPreset(string presetName) => _engine.ApplyFilterPreset(presetName);

    public void GroupBy(string fieldName) => _engine.GroupBy(fieldName);
    public void RemoveGroup(string fieldName) => _engine.RemoveGroup(fieldName);
    public void ClearGroups() => _engine.ClearGroups();
    public void ToggleGroupExpansion(object groupKey) => _engine.ToggleGroupExpansion(groupKey);
    public void ExpandAllGroups() => _engine.ExpandAllGroups();
    public void CollapseAllGroups() => _engine.CollapseAllGroups();

    public bool EditCell(TItem item, string fieldName, object? newValue) => _engine.EditCell(item, fieldName, newValue);
    public bool UndoLastEdit() => _engine.UndoLastEdit();
    public bool RedoLastEdit() => _engine.RedoLastEdit();
    public GridCommitResult CommitChanges() => _engine.CommitChanges();
    public int RollbackChanges() => _engine.RollbackChanges();
    public bool ReorderRow(int fromIndex, int toIndex) => _engine.ReorderRow(fromIndex, toIndex);
    public bool AddRow(TItem item, int? index = null) => _engine.AddRow(item, index);
    public int RemoveRows(IEnumerable<TItem> items) => _engine.RemoveRows(items);
    public GridHierarchyRelation AddRelation(string name, string keyField, string foreignField, System.Collections.IEnumerable childSource, Type? childItemType = null) => _engine.AddRelation(name, keyField, foreignField, childSource, childItemType);
    public GridHierarchyRelation AddSelfReference(string keyField, string parentField) => _engine.AddSelfReference(keyField, parentField);
    public bool RemoveRelation(string name) => _engine.RemoveRelation(name);
    public void NotifyRelationsChanged() => _engine.NotifyRelationsChanged();
    public void ToggleDetailExpansion(TItem rowItem) => _engine.ToggleDetailExpansion(rowItem);
    public void ExpandAllDetails() => _engine.ExpandAllDetails();
    public void CollapseAllDetails() => _engine.CollapseAllDetails();
    public bool IsDetailExpanded(TItem rowItem) => _engine.IsDetailExpanded(rowItem);

    public void SetColumnVisibility(string fieldName, bool visible) => _engine.SetColumnVisibility(fieldName, visible);
    public void ReorderColumn(string fieldName, int newIndex) => _engine.ReorderColumn(fieldName, newIndex);
    public bool ResizeColumn(string fieldName, double width) => _engine.ResizeColumn(fieldName, width);
    public bool MoveColumnBefore(string fieldName, string? beforeFieldName) => _engine.MoveColumnBefore(fieldName, beforeFieldName);
    public void PinColumn(string fieldName, ColumnPinPosition position) => _engine.PinColumn(fieldName, position);
    public ColumnLayoutProfile CreateLayoutProfile(string profileName = "Default") => _engine.CreateLayoutProfile(profileName);
    public IReadOnlyList<string> ApplyLayoutProfile(ColumnLayoutProfile profile) => _engine.ApplyLayoutProfile(profile);

    /// <summary>Display row of the focused or first selected row, or -1.</summary>
    public int SelectedRowIndex
    {
        get
        {
            var selection = _engine.Selection;
            if (selection.SelectedRows.Count == 0) return -1;
            return selection.IsRowSelected(selection.FocusedRow) ? selection.FocusedRow : selection.SelectedRows.Min();
        }
        set
        {
            if (value < 0) _engine.Selection.Clear();
            else _engine.Selection.Click(value, Math.Max(0, _engine.Selection.FocusedColumn));
            Invalidate();
        }
    }

    /// <summary>Display rows (positions on the current page) of the selected rows.</summary>
    public IReadOnlySet<int> SelectedIndices => new HashSet<int>(_engine.Selection.SelectedRows);

    public bool ShowFilterRow { get => _showFilterRow; set { _showFilterRow = value; RefreshLayout(); } }
    public bool ShowSummaryFooter { get => _showFooter; set { _showFooter = value; RefreshLayout(); } }
    public bool ShowBandedHeaders { get => _showBands; set { _showBands = value; RefreshLayout(); } }
    public bool ShowConditionalDataBars { get; set; } = true;

    /// <summary>Stretches the last scrollable column so columns always fill the viewport.</summary>
    public bool FillColumnsToWidth { get => _fillColumns; set { _fillColumns = value; RefreshLayout(); } }

    /// <summary>Fill stretches the last column; BestFit sizes every column to its content when data first loads.</summary>
    public GridAutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            _autoSizeMode = value;
            _fillColumns = value == GridAutoSizeMode.Fill;
            _bestFitDone = false;
            if (value == GridAutoSizeMode.BestFit) BestFitAllColumns();
            RefreshLayout();
        }
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler<string>? CellEdited;

    // ------------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------------

    private float DpiScale => DeviceDpi / 96f;
    private int S(double logical) => (int)Math.Round(logical * DpiScale);

    private Frame ComputeFrame()
    {
        var ordered = GridColumnLayout.OrderVisible(_engine.Columns);
        var f = new Frame
        {
            SearchH = _searchPanel.Visible ? S(36) : 0,
            GroupH = _engine.ShowGroupPanel && _engine.EnableGrouping ? S(34) : 0,
            BandRowH = S(26),
            HeaderH = S(32),
            FilterH = _showFilterRow && _engine.EnableFiltering ? S(28) : 0,
            FooterH = _showFooter ? S(28) : 0,
            PagerH = PagerVisible ? S(36) : 0,
            ExpanderW = (_engine.EnableMasterDetail || _engine.MasterDetailRelations.Count > 0) && !_engine.IsTreeMode ? S(22) : 0,
            CheckW = _engine.SelectionMode == GridSelectionMode.CheckboxRow ? S(28) : 0,
            RowH = Math.Max(S(16), S(_engine.ItemHeight)),
            DetailH = S(56),
            Ordered = ordered
        };

        f.Gutter = f.ExpanderW + f.CheckW;
        f.SummaryRows = _showColumnSummaries ? ordered.Select(c => c.FooterSummaries.Count).DefaultIfEmpty(0).Max() : 0;
        f.SummaryH = f.SummaryRows * f.RowH;
        f.BandDepth = _showBands ? GridColumnLayout.BandDepth(_engine.Columns, _engine.Bands) : 0;
        f.GroupTop = f.SearchH;
        f.BandTop = f.GroupTop + f.GroupH;
        f.HeaderTop = f.BandTop + f.BandDepth * f.BandRowH;
        f.FilterTop = f.HeaderTop + f.HeaderH;
        f.BodyTop = f.FilterTop + f.FilterH;

        var vbar = _vScroll.Visible ? _vScroll.Width : 0;
        var hbar = _hScroll.Visible ? _hScroll.Height : 0;
        f.BodyWidth = Math.Max(0, ClientSize.Width - vbar);
        f.PagerTop = ClientSize.Height - f.PagerH;
        f.StatusTop = f.PagerTop - f.FooterH;
        f.SummaryTop = Math.Max(f.BodyTop, f.StatusTop - hbar - f.SummaryH);
        f.BodyBottom = Math.Max(f.BodyTop, f.SummaryTop);
        f.FullRows = Math.Max(1, (f.BodyBottom - f.BodyTop) / f.RowH);

        f.Layout = GridColumnLayout.Compute(ordered, Math.Max(0, f.BodyWidth - f.Gutter), _horizontalOffset, _fillColumns, DpiScale);
        f.Bands = f.BandDepth > 0 ? GridColumnLayout.ComputeBandSpans(f.Layout, _engine.Bands) : Array.Empty<BandSpan>();
        return f;
    }

    private int RowHeightOf(Frame f, GridRow<TItem>? row) => row?.Kind == GridRowKind.DetailSummary ? f.DetailH : f.RowH;

    private IEnumerable<RowSlot> EnumerateRows(Frame f)
    {
        var y = f.BodyTop;
        var total = _engine.TotalItemsCount;
        for (var i = _firstRow; i < total && y < f.BodyBottom; i++)
        {
            GridRow<TItem>? row = _engine.TryGetRowAt(i, out var r) ? r : null;
            var height = RowHeightOf(f, row);
            yield return new RowSlot(i, row, new Rectangle(0, y, f.BodyWidth, height));
            y += height;
        }
    }

    private void UpdateScrollBars()
    {
        if (!IsHandleCreated && Width == 0) return;

        _updatingScroll = true;
        try
        {
            bool needV = false, needH = false;
            Frame f = null!;
            for (var pass = 0; pass < 3; pass++)
            {
                _vScroll.Visible = needV;
                _hScroll.Visible = needH;
                f = ComputeFrame();

                var contentPx = f.Ordered.Sum(c => GridColumnLayout.WidthOf(c) * DpiScale);
                var nextV = _engine.TotalItemsCount > f.FullRows;
                var nextH = contentPx > Math.Max(0, f.BodyWidth - f.Gutter) + 0.5;
                if (nextV == needV && nextH == needH) break;
                needV = nextV;
                needH = nextH;
            }

            var maxFirst = Math.Max(0, _engine.TotalItemsCount - f.FullRows);
            _firstRow = Math.Clamp(_firstRow, 0, maxFirst);
            _horizontalOffset = (int)Math.Round(f.Layout.HorizontalOffset);

            _vScroll.SetBounds(ClientSize.Width - _vScroll.Width, f.BodyTop, _vScroll.Width, Math.Max(0, f.BodyBottom - f.BodyTop));
            _vScroll.Minimum = 0;
            _vScroll.Maximum = Math.Max(0, _engine.TotalItemsCount - 1);
            _vScroll.LargeChange = f.FullRows;
            _vScroll.SmallChange = 1;
            _vScroll.Value = Math.Clamp(_firstRow, _vScroll.Minimum, Math.Max(_vScroll.Minimum, _vScroll.Maximum));

            var region = Math.Max(1, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft));
            _hScroll.SetBounds(0, f.SummaryTop + f.SummaryH, Math.Max(0, ClientSize.Width - (_vScroll.Visible ? _vScroll.Width : 0)), _hScroll.Height);
            _hScroll.Minimum = 0;
            _hScroll.LargeChange = region;
            _hScroll.SmallChange = S(24);
            _hScroll.Maximum = (int)Math.Max(0, f.Layout.MaxHorizontalOffset) + region - 1;
            _hScroll.Value = Math.Clamp(_horizontalOffset, 0, Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1));

            LayoutSearchPanel(f);
        }
        finally
        {
            _updatingScroll = false;
        }

        RequestVisibleRows();
    }

    private void RequestVisibleRows()
    {
        var f = ComputeFrame();
        _engine.EnsureRowsLoaded(_firstRow, f.FullRows + 2);
    }

    private void RefreshLayout()
    {
        UpdateScrollBars();
        Invalidate();
    }

    private void Post(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(action); } catch (InvalidOperationException) { }
            return;
        }

        action();
    }

    private void OnEngineChanged(object? sender, PropertyChangedEventArgs e) => Post(RefreshLayout);
    private void OnEngineQueryChanged(object? sender, EventArgs e) => Post(ResetViewAfterQueryChange);
    private void OnEngineWindowChanged(object? sender, EventArgs e) => Post(() => { OnWindowChanged(); RefreshLayout(); });
    private void OnEnginePageChanged(object? sender, EventArgs e) => Post(() => { CloseEditor(commit: false); _firstRow = 0; RefreshLayout(); });
    private void OnEngineScrollRequested(object? sender, int row) => Post(() => ScrollToRow(row));
    private void OnEngineSearchChanged(object? sender, EventArgs e) => Post(() => { UpdateSearchCount(); Invalidate(); });
    private void OnEngineVisualChanged(object? sender, EventArgs e) => Post(Invalidate);
    private void OnEngineLoadFailed(object? sender, GridDataLoadFailedEventArgs e) => Post(Invalidate);

    private void OnEngineSelectionChanged(object? sender, EventArgs e) => Post(() =>
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    });

    private void OnEngineFocusChanged(object? sender, EventArgs e) => Post(() =>
    {
        var focused = _engine.Selection.FocusedRow;
        if (focused >= 0) ScrollToRow(focused);
        EnsureColumnVisible(_engine.Selection.FocusedColumn);
        NotifyFocusChanged();
        Invalidate();
    });

    private void ResetViewAfterQueryChange()
    {
        CloseEditor(commit: false);
        _firstRow = 0;
        _hoveredRow = -1;
        RefreshLayout();
    }

    private void OnWindowChanged()
    {
        SyncItemObservers();
        if (_autoSizeMode == GridAutoSizeMode.BestFit && !_bestFitDone && _engine.RenderedItems.Count > 0)
        {
            _bestFitDone = true;
            BestFitAllColumns();
        }
    }

    private void OnVerticalScroll()
    {
        if (_updatingScroll) return;
        CloseEditor(commit: true);
        _firstRow = _vScroll.Value;
        RequestVisibleRows();
        MaybeLoadMoreOnScroll();
        Invalidate();
    }

    private void OnHorizontalScroll()
    {
        if (_updatingScroll) return;
        CloseEditor(commit: true);
        _horizontalOffset = _hScroll.Value;
        Invalidate();
    }

    private void ScrollToRow(int row)
    {
        var f = ComputeFrame();
        row = Math.Clamp(row, 0, Math.Max(0, _engine.TotalItemsCount - 1));
        if (row < _firstRow)
        {
            _firstRow = row;
        }
        else if (row >= _firstRow + f.FullRows)
        {
            _firstRow = row - f.FullRows + 1;
        }

        UpdateScrollBars();
        Invalidate();
    }

    private void EnsureColumnVisible(int columnIndex)
    {
        var f = ComputeFrame();
        if (columnIndex < 0 || columnIndex >= f.Ordered.Count) return;
        var slot = f.Layout.Slots.FirstOrDefault(s => ReferenceEquals(s.Column, f.Ordered[columnIndex]));
        if (slot == null || slot.Zone != ColumnZone.Scrollable || !_hScroll.Visible) return;

        var viewLeft = f.Layout.ScrollableLeft;
        var viewRight = f.Layout.ScrollableRight;
        double delta = 0;
        if (slot.Left < viewLeft) delta = slot.Left - viewLeft;
        else if (slot.Right > viewRight) delta = slot.Right - viewRight;
        if (Math.Abs(delta) < 0.5) return;

        _hScroll.Value = Math.Clamp(_hScroll.Value + (int)Math.Round(delta), 0, Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CloseEditor(commit: false);
        UpdateScrollBars();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateScrollBars();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ResetFonts();
        UpdateScrollBars();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ResetFonts();
        Invalidate();
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        _colors = null;
        Invalidate();
    }

    private void ResetFonts()
    {
        _headerFont?.Dispose();
        _smallFont?.Dispose();
        _headerFont = null;
        _smallFont = null;
    }

    private Font HeaderFont => _headerFont ??= new Font(Font, FontStyle.Bold);
    private Font SmallFont => _smallFont ??= new Font(Font.FontFamily, Font.Size * 0.8f, FontStyle.Bold);

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        _colors = null;
        Post(() =>
        {
            ApplyThemeToChildren();
            Invalidate();
        });
    }

    private Colors GetPalette() => _colors ??= BuildColors();

    private Colors BuildColors()
    {
        if (SystemInformation.HighContrast)
        {
            return new Colors
            {
                Surface = SystemColors.Window, Alt = SystemColors.Window, Header = SystemColors.Control, Border = SystemColors.WindowText,
                Text = SystemColors.WindowText, Muted = SystemColors.GrayText, Primary = SystemColors.Highlight, PrimaryText = SystemColors.HighlightText,
                Danger = Color.Red, Success = SystemColors.WindowText, Warning = SystemColors.WindowText,
                SelectionBack = SystemColors.Highlight, SelectionText = SystemColors.HighlightText, HoverBack = SystemColors.Control,
                FocusRing = SystemColors.WindowText, HighlightBack = SystemColors.Highlight, HighlightText = SystemColors.HighlightText,
                ErrorBack = SystemColors.Window, HighContrast = true
            };
        }

        var p = GridPalette.FromTheme(_theme);
        return new Colors
        {
            Surface = Hex(p.Surface), Alt = Hex(p.Alt), Header = Hex(p.Header), Border = Hex(p.Border), Text = Hex(p.Text), Muted = Hex(p.Muted),
            Primary = Hex(p.Primary), PrimaryText = Hex(p.PrimaryText), Danger = Hex(p.Danger), Success = Hex(p.Success), Warning = Hex(p.Warning),
            SelectionBack = Hex(p.SelectionBack), SelectionText = Hex(p.SelectionText), HoverBack = Hex(p.HoverBack), FocusRing = Hex(p.FocusRing),
            HighlightBack = Hex(p.HighlightBack), HighlightText = Hex(p.HighlightText), ErrorBack = Hex(p.ErrorBack), HighContrast = p.HighContrast
        };
    }

    private static Color Hex(string value)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return Color.Magenta; }
    }

    private static Color Blend(Color start, Color end, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)(start.A + (end.A - start.A) * amount),
            (int)(start.R + (end.R - start.R) * amount),
            (int)(start.G + (end.G - start.G) * amount),
            (int)(start.B + (end.B - start.B) * amount));
    }

    private static Color GetRuleColor(string? colorHex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(colorHex)) return fallback;
        try
        {
            var color = ColorTranslator.FromHtml(colorHex);
            return color.IsEmpty ? fallback : color;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return fallback;
        }
    }

    // ------------------------------------------------------------------
    // Cell helpers
    // ------------------------------------------------------------------

    private object? CellValue(GridRow<TItem> row, GridColumn column) =>
        row.Kind == GridRowKind.Data ? _engine.GetRowCellValue(row, column) : column.GetValue(row.RowObject);

    private string CellText(GridRow<TItem> row, GridColumn column) => _engine.FormatCellValue(column, CellValue(row, column));

    private GridColumnType ResolveType(GridColumn column, object? value)
    {
        if (column is GridCommandColumn) return GridColumnType.Command;
        if (column.ColumnType != GridColumnType.Auto) return column.ColumnType;
        if (column.ComboItems != null) return GridColumnType.ComboBox;
        return value is bool ? GridColumnType.Boolean : GridColumnType.Text;
    }

    private static TextFormatFlags FlagsFor(ColumnTextAlignment alignment)
    {
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        return alignment switch
        {
            ColumnTextAlignment.Center => flags | TextFormatFlags.HorizontalCenter,
            ColumnTextAlignment.Right => flags | TextFormatFlags.Right,
            _ => flags | TextFormatFlags.Left
        };
    }

    private int ColumnIndexOf(Frame f, GridColumn column) => f.Ordered.IndexOf(column);

    private bool IsCellSelectionMode => _engine.SelectionMode is GridSelectionMode.CellRange or GridSelectionMode.SingleCell or GridSelectionMode.Column;

    private void ClearSelection() => _engine.Selection.Clear();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            _engine.PropertyChanged -= OnEngineChanged;
            _engine.QueryChanged -= OnEngineQueryChanged;
            _engine.DataWindowChanged -= OnEngineWindowChanged;
            _engine.PageChanged -= OnEnginePageChanged;
            _engine.ScrollToRowRequested -= OnEngineScrollRequested;
            _engine.Announcement -= OnEngineAnnouncement;
            _engine.SearchResultChanged -= OnEngineSearchChanged;
            _engine.ValidationChanged -= OnEngineVisualChanged;
            _engine.RunningTotalsChanged -= OnEngineVisualChanged;
            _engine.UndoStackChanged -= OnEngineVisualChanged;
            _engine.DataLoadFailed -= OnEngineLoadFailed;
            _engine.SelectionChanged -= OnEngineSelectionChanged;
            _engine.Selection.FocusChanged -= OnEngineFocusChanged;
            DisposeFeatures();
            ResetFonts();
            if (_filterDropDown.Visible) _filterDropDown.Close();
            while (_filterDropDown.Items.Count > 0)
            {
                var item = _filterDropDown.Items[0];
                _filterDropDown.Items.RemoveAt(0);
                if (item is ToolStripControlHost host) host.Control?.Dispose();
                item.Dispose();
            }
            _filterDropDown.Dispose();
            if (_headerContextMenu.Visible) _headerContextMenu.Close();
            _headerContextMenu.Dispose();
            _bodyContextMenu.Dispose();
            _toolTip.Dispose();
            _engine.Dispose();
        }

        base.Dispose(disposing);
    }
}
