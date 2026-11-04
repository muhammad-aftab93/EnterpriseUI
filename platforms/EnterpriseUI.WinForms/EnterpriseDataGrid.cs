using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Data;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Windows Forms data grid with DPI-aware GDI+ rendering, row windowing over <see cref="DataGrid{TItem}"/>,
/// scrolling, column resize/reorder/pinning, banded headers, filter row, value-list filter popup,
/// conditional formatting, inline editing with undo/redo, and print preview.
/// </summary>
public class EnterpriseDataGrid<TItem> : Control, IThemeableControl
{
    private const int FilterListLimit = 1000;
    private const int PrintRowLimit = 5000;

    private enum DragMode { None, Resize, HeaderPress, HeaderDrag }
    private enum EditorMode { None, Cell, FilterRow }

    private readonly record struct RowSlot(int Index, bool IsDetail, Rectangle Bounds);

    private sealed class Frame
    {
        public int GroupH, BandRowH, BandDepth, HeaderH, FilterH, FooterH, Gutter, RowH, DetailH;
        public int BandTop, HeaderTop, FilterTop, BodyTop, BodyBottom, BodyWidth, FullRows;
        public ColumnLayoutResult Layout = null!;
        public IReadOnlyList<BandSpan> Bands = Array.Empty<BandSpan>();
    }

    private readonly record struct Palette(Color Surface, Color Alt, Color Header, Color Border, Color Text, Color Muted, Color Primary);

    private sealed class FilterItem
    {
        public FilterItem(object value) => Value = value;
        public object Value { get; }
        public override string ToString() => Value.ToString() ?? string.Empty;
    }

    private readonly DataGrid<TItem> _engine = new();
    private readonly Dictionary<string, PropertyInfo?> _props = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _selectedIndices = new();
    private readonly ContextMenuStrip _headerContextMenu = new();
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
    private Font? _headerFont;
    private Font? _smallFont;
    private int _firstRow;
    private int _horizontalOffset;
    private bool _updatingScroll;
    private int _hoveredRow = -1;
    private int _hoveredHeader = -1;
    private int _selectedRowIndex = -1;
    private int _anchorRowIndex = -1;
    private string? _toolTipText;

    private DragMode _drag;
    private GridColumn? _dragColumn;
    private Point _dragStart;
    private int _resizeStartWidth;
    private int _dropIndex = -1;
    private bool _resized;

    private TextBox? _editor;
    private EditorMode _editorMode;
    private int _editingRow = -1;
    private GridColumn? _editingColumn;
    private bool _closingEditor;
    private GridColumn? _contextColumn;

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

        _vScroll.ValueChanged += (_, _) => OnVerticalScroll();
        _hScroll.ValueChanged += (_, _) => OnHorizontalScroll();
        Controls.Add(_vScroll);
        Controls.Add(_hScroll);

        BuildHeaderContextMenu();

        _engine.PropertyChanged += OnEngineChanged;
        _engine.QueryChanged += OnEngineQueryChanged;
        _themeHandler = (_, newTheme) => OnThemeChanged(newTheme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }

    // ------------------------------------------------------------------
    // Direct Grid Properties, Collections, Events & Operations
    // ------------------------------------------------------------------

    public List<GridColumn> Columns => _engine.Columns;
    public List<GridColumnBand> Bands => _engine.Bands;
    public DataSourceRequest CurrentRequest => _engine.CurrentRequest;
    public GridSelectionMode SelectionMode { get => _engine.SelectionMode; set => _engine.SelectionMode = value; }
    public GridEditMode EditMode { get => _engine.EditMode; set => _engine.EditMode = value; }
    public bool EnableVirtualization { get => _engine.EnableVirtualization; set => _engine.EnableVirtualization = value; }
    public bool EnableGrouping { get => _engine.EnableGrouping; set => _engine.EnableGrouping = value; }
    public bool EnableFiltering { get => _engine.EnableFiltering; set => _engine.EnableFiltering = value; }
    public bool EnableSorting { get => _engine.EnableSorting; set => _engine.EnableSorting = value; }
    public bool EnableMultiColumnSorting { get => _engine.EnableMultiColumnSorting; set => _engine.EnableMultiColumnSorting = value; }
    public bool EnableMasterDetail { get => _engine.EnableMasterDetail; set { _engine.EnableMasterDetail = value; RefreshLayout(); } }
    public bool ShowGroupPanel { get => _engine.ShowGroupPanel; set { _engine.ShowGroupPanel = value; RefreshLayout(); } }
    public bool EnableSearchPanel { get => _engine.EnableSearchPanel; set => _engine.EnableSearchPanel = value; }
    public bool EnableQuickFilterRow { get => _engine.EnableQuickFilterRow; set => _engine.EnableQuickFilterRow = value; }
    public object? DetailTemplate { get => _engine.DetailTemplate; set => _engine.DetailTemplate = value; }

    public Dictionary<string, string> QuickFilters => _engine.QuickFilters;
    public Dictionary<string, List<IFilterDescriptor>> SavedFilterPresets => _engine.SavedFilterPresets;
    public List<GroupSummaryDescriptor> GroupSummaries => _engine.GroupSummaries;
    public List<GridFormatRule> FormatRules => _engine.FormatRules;
    public List<GridHierarchyRelation> Relations => _engine.Relations;
    public GridClipboardCopyMode ClipboardCopyMode { get => _engine.ClipboardCopyMode; set => _engine.ClipboardCopyMode = value; }
    public string? SearchPanelText { get => _engine.SearchPanelText; set => _engine.SearchPanelText = value; }

    public double ItemHeight { get => _engine.ItemHeight; set { _engine.ItemHeight = value; RefreshLayout(); } }
    public double ItemWidth { get => _engine.ItemWidth; set { _engine.ItemWidth = value; RefreshLayout(); } }
    public int TotalItemsCount => _engine.TotalItemsCount;
    public int RowBuffer { get => _engine.RowBuffer; set => _engine.RowBuffer = value; }
    public bool CanUndo => _engine.CanUndo;
    public bool CanRedo => _engine.CanRedo;
    public bool IsLoading => _engine.IsLoading;
    public int WindowStart => _engine.WindowStart;

    public IEnumerable<TItem>? DataSource { get => _engine.DataSource; set => _engine.DataSource = value; }
    public IDataSource<TItem>? CustomDataSource { get => _engine.CustomDataSource; set => _engine.CustomDataSource = value; }
    public TItem? SelectedItem { get => _engine.SelectedItem; set => _engine.SelectedItem = value; }
    public IReadOnlyList<TItem> SelectedItems => _engine.SelectedItems;
    public IReadOnlyList<TItem> RenderedItems => _engine.RenderedItems;

    public event EventHandler? DataWindowChanged { add => _engine.DataWindowChanged += value; remove => _engine.DataWindowChanged -= value; }
    public event EventHandler? QueryChanged { add => _engine.QueryChanged += value; remove => _engine.QueryChanged -= value; }

    public bool TryGetItemAt(int absoluteIndex, out TItem item) => _engine.TryGetItemAt(absoluteIndex, out item);
    public bool EnsureRowsLoaded(int firstRow, int rowCount) => _engine.EnsureRowsLoaded(firstRow, rowCount);
    public Task<IReadOnlyList<TItem>> ReadAllAsync(int? maxRows = null, CancellationToken cancellationToken = default) => _engine.ReadAllAsync(maxRows, cancellationToken);
    public Task RefreshDataAsync(CancellationToken cancellationToken = default) => _engine.RefreshDataAsync(cancellationToken);

    public void SortBy(string fieldName, ListSortDirection direction) => _engine.SortBy(fieldName, direction);
    public void CycleSort(string fieldName, bool allowMultiSort = false) => _engine.CycleSort(fieldName, allowMultiSort);
    public void ClearSort() => _engine.ClearSort();

    public void FilterBy(string fieldName, FilterOperator op, object? value) => _engine.FilterBy(fieldName, op, value);
    public void ApplyExcelFilter(string fieldName, IEnumerable<object> selectedValues) => _engine.ApplyExcelFilter(fieldName, selectedValues);
    public void ClearExcelFilter(string fieldName) => _engine.ClearExcelFilter(fieldName);
    public IReadOnlyList<object> GetDistinctValues(string fieldName, string? contains = null, int maxCount = 1000, CancellationToken cancellationToken = default) => _engine.GetDistinctValues(fieldName, contains, maxCount, cancellationToken);
    public void SetQuickFilter(string fieldName, string text) => _engine.SetQuickFilter(fieldName, text);
    public void SaveFilterPreset(string presetName) => _engine.SaveFilterPreset(presetName);
    public void ApplyFilterPreset(string presetName) => _engine.ApplyFilterPreset(presetName);

    public void GroupBy(string fieldName) => _engine.GroupBy(fieldName);
    public void RemoveGroup(string fieldName) => _engine.RemoveGroup(fieldName);
    public void ClearGroups() => _engine.ClearGroups();
    public void ToggleGroupExpansion(object groupKey) => _engine.ToggleGroupExpansion(groupKey);
    public void ExpandAllGroups() => _engine.ExpandAllGroups();
    public void CollapseAllGroups() => _engine.CollapseAllGroups();

    public bool EditCell(TItem item, string fieldName, object? newValue) => _engine.EditCell(item, fieldName, newValue);
    public bool UndoLastEdit() => _engine.UndoLastEdit();
    public bool RedoLastEdit() => _engine.RedoLastEdit();
    public bool ReorderRow(int fromIndex, int toIndex) => _engine.ReorderRow(fromIndex, toIndex);
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
    public void ApplyLayoutProfile(ColumnLayoutProfile profile) => _engine.ApplyLayoutProfile(profile);

    public int SelectedRowIndex
    {
        get => _selectedRowIndex;
        set
        {
            if (_selectedRowIndex != value)
            {
                _selectedRowIndex = value;
                _anchorRowIndex = value;
                _selectedIndices.Clear();
                if (value >= 0) _selectedIndices.Add(value);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    /// <summary>Absolute row indexes (positions in the filtered, sorted result) of the selected rows.</summary>
    public IReadOnlySet<int> SelectedIndices => _selectedIndices;

    public bool ShowFilterRow { get => _showFilterRow; set { _showFilterRow = value; RefreshLayout(); } }
    public bool ShowSummaryFooter { get => _showFooter; set { _showFooter = value; RefreshLayout(); } }
    public bool ShowBandedHeaders { get => _showBands; set { _showBands = value; RefreshLayout(); } }
    public bool ShowConditionalDataBars { get; set; } = true;

    /// <summary>Stretches the last scrollable column so columns always fill the viewport.</summary>
    public bool FillColumnsToWidth { get => _fillColumns; set { _fillColumns = value; RefreshLayout(); } }

    private bool _showFilterRow;
    private bool _showFooter = true;
    private bool _showBands = true;
    private bool _fillColumns = true;

    public event EventHandler? SelectionChanged;
    public event EventHandler<string>? CellEdited;

    // ------------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------------

    private float DpiScale => DeviceDpi / 96f;
    private int S(double logical) => (int)Math.Round(logical * DpiScale);

    private Frame ComputeFrame()
    {
        var f = new Frame
        {
            GroupH = _engine.ShowGroupPanel ? S(34) : 0,
            BandRowH = S(26),
            HeaderH = S(32),
            FilterH = _showFilterRow ? S(28) : 0,
            FooterH = _showFooter ? S(28) : 0,
            Gutter = _engine.EnableMasterDetail ? S(22) : 0,
            RowH = Math.Max(S(16), S(_engine.ItemHeight)),
            DetailH = S(56)
        };

        f.BandDepth = _showBands ? GridColumnLayout.BandDepth(_engine.Columns, _engine.Bands) : 0;
        f.BandTop = f.GroupH;
        f.HeaderTop = f.BandTop + f.BandDepth * f.BandRowH;
        f.FilterTop = f.HeaderTop + f.HeaderH;
        f.BodyTop = f.FilterTop + f.FilterH;

        var vbar = _vScroll.Visible ? _vScroll.Width : 0;
        var hbar = _hScroll.Visible ? _hScroll.Height : 0;
        f.BodyWidth = Math.Max(0, ClientSize.Width - vbar);
        f.BodyBottom = Math.Max(f.BodyTop, ClientSize.Height - f.FooterH - hbar);
        f.FullRows = Math.Max(1, (f.BodyBottom - f.BodyTop) / f.RowH);

        var ordered = GridColumnLayout.OrderVisible(_engine.Columns);
        f.Layout = GridColumnLayout.Compute(ordered, Math.Max(0, f.BodyWidth - f.Gutter), _horizontalOffset, _fillColumns, DpiScale);
        f.Bands = f.BandDepth > 0 ? GridColumnLayout.ComputeBandSpans(f.Layout, _engine.Bands) : Array.Empty<BandSpan>();
        return f;
    }

    private IEnumerable<RowSlot> EnumerateRows(Frame f)
    {
        var y = f.BodyTop;
        var total = _engine.TotalItemsCount;
        for (int i = _firstRow; i < total && y < f.BodyBottom; i++)
        {
            yield return new RowSlot(i, false, new Rectangle(0, y, f.BodyWidth, f.RowH));
            y += f.RowH;

            if (_engine.EnableMasterDetail && y < f.BodyBottom && _engine.TryGetItemAt(i, out var item) && _engine.IsDetailExpanded(item))
            {
                yield return new RowSlot(i, true, new Rectangle(0, y, f.BodyWidth, f.DetailH));
                y += f.DetailH;
            }
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
            for (int pass = 0; pass < 3; pass++)
            {
                _vScroll.Visible = needV;
                _hScroll.Visible = needH;
                f = ComputeFrame();

                var contentPx = GridColumnLayout.OrderVisible(_engine.Columns).Sum(c => GridColumnLayout.WidthOf(c) * DpiScale);
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
            _vScroll.Value = Math.Clamp(_firstRow, _vScroll.Minimum, _vScroll.Maximum);

            var region = Math.Max(1, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft));
            _hScroll.SetBounds(0, f.BodyBottom, Math.Max(0, ClientSize.Width - (_vScroll.Visible ? _vScroll.Width : 0)), _hScroll.Height);
            _hScroll.Minimum = 0;
            _hScroll.LargeChange = region;
            _hScroll.SmallChange = S(24);
            _hScroll.Maximum = (int)Math.Max(0, f.Layout.MaxHorizontalOffset) + region - 1;
            _hScroll.Value = Math.Clamp(_horizontalOffset, 0, Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1));
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

    private void OnEngineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(RefreshLayout)); } catch (InvalidOperationException) { }
            return;
        }

        RefreshLayout();
    }

    private void OnEngineQueryChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(ResetViewAfterQueryChange)); } catch (InvalidOperationException) { }
            return;
        }

        ResetViewAfterQueryChange();
    }

    private void ResetViewAfterQueryChange()
    {
        CloseEditor(commit: false);
        _firstRow = 0;
        _hoveredRow = -1;
        if (_selectedIndices.Count > 0 || _selectedRowIndex >= 0)
        {
            ClearSelection();
        }
    }

    private void OnVerticalScroll()
    {
        if (_updatingScroll) return;
        CloseEditor(commit: true);
        _firstRow = _vScroll.Value;
        RequestVisibleRows();
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
        Invalidate();
    }

    private Palette GetPalette() => new(
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(246, 248, 250)),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(246, 248, 250)),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 224, 230)),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextMuted, Color.FromArgb(110, 118, 128)),
        EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215)));

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
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return fallback;
        }

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
    // Data access helpers
    // ------------------------------------------------------------------

    private PropertyInfo? PropertyOf(string field)
    {
        if (!_props.TryGetValue(field, out var prop))
        {
            prop = typeof(TItem).GetProperty(field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            _props[field] = prop;
        }

        return prop;
    }

    private static string FormatValue(GridColumn column, object? value)
    {
        if (value == null) return string.Empty;
        if (!string.IsNullOrEmpty(column.FormatString) && value is IFormattable formattable)
        {
            return formattable.ToString(column.FormatString, CultureInfo.CurrentCulture);
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
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

    private void ClearSelection()
    {
        _selectedIndices.Clear();
        _selectedRowIndex = -1;
        _anchorRowIndex = -1;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------
    // Header context menu
    // ------------------------------------------------------------------

    private void BuildHeaderContextMenu()
    {
        var asc = new ToolStripMenuItem("Sort Ascending", null, (_, _) => SortContext(ListSortDirection.Ascending));
        var desc = new ToolStripMenuItem("Sort Descending", null, (_, _) => SortContext(ListSortDirection.Descending));
        var clearSort = new ToolStripMenuItem("Clear Sorting", null, (_, _) => { _engine.ClearSort(); ClearSelection(); });
        var clearFilter = new ToolStripMenuItem("Clear Filter", null, (_, _) => ClearColumnFilter(_contextColumn));
        var autoSize = new ToolStripMenuItem("Auto-Size Column", null, (_, _) => AutoSizeColumn(_contextColumn));

        _pinLeftItem.Click += (_, _) => PinContext(ColumnPinPosition.Left);
        _pinRightItem.Click += (_, _) => PinContext(ColumnPinPosition.Right);
        _unpinItem.Click += (_, _) => PinContext(ColumnPinPosition.None);
        _hideItem.Click += (_, _) =>
        {
            if (_contextColumn != null) _engine.SetColumnVisibility(_contextColumn.Field, false);
        };
        _groupItem.Click += (_, _) =>
        {
            if (_contextColumn != null) _engine.GroupBy(_contextColumn.Field);
        };

        var clearGroup = new ToolStripMenuItem("Clear All Grouping", null, (_, _) => _engine.ClearGroups());
        var toggleBands = new ToolStripMenuItem("Toggle Column Bands", null, (_, _) => ShowBandedHeaders = !ShowBandedHeaders);
        var print = new ToolStripMenuItem("Print Preview...", null, (_, _) => ShowPrintPreviewDialog());

        _headerContextMenu.Items.AddRange(new ToolStripItem[]
        {
            asc, desc, clearSort, clearFilter,
            new ToolStripSeparator(),
            autoSize, _pinLeftItem, _pinRightItem, _unpinItem, _hideItem, _chooserMenu,
            new ToolStripSeparator(),
            _groupItem, clearGroup,
            new ToolStripSeparator(),
            toggleBands, print
        });

        _headerContextMenu.Opening += (_, _) =>
        {
            var col = _contextColumn;
            asc.Enabled = desc.Enabled = col?.Sortable == true;
            clearFilter.Enabled = col != null && HasFilter(col);
            _pinLeftItem.Checked = col?.PinPosition == ColumnPinPosition.Left;
            _pinRightItem.Checked = col?.PinPosition == ColumnPinPosition.Right;
            _unpinItem.Enabled = col != null && col.PinPosition != ColumnPinPosition.None;
            _hideItem.Enabled = col?.CanHide == true;
            _groupItem.Enabled = col?.Groupable == true;
            BuildChooserMenu();
        };
    }

    private void BuildChooserMenu()
    {
        _chooserMenu.DropDownItems.Clear();
        foreach (var column in _engine.Columns.Where(c => c.AllowColumnChooser))
        {
            var captured = column;
            var item = new ToolStripMenuItem(string.IsNullOrEmpty(column.Title) ? column.Field : column.Title)
            {
                Checked = column.Visible,
                Enabled = column.CanHide || !column.Visible
            };
            item.Click += (_, _) => _engine.SetColumnVisibility(captured.Field, !captured.Visible);
            _chooserMenu.DropDownItems.Add(item);
        }
    }

    private void SortContext(ListSortDirection direction)
    {
        if (_contextColumn == null) return;
        _engine.SortBy(_contextColumn.Field, direction);
        ClearSelection();
    }

    private void PinContext(ColumnPinPosition position)
    {
        if (_contextColumn != null) _engine.PinColumn(_contextColumn.Field, position);
    }

    private bool HasFilter(GridColumn column) =>
        _engine.QuickFilters.ContainsKey(column.Field) || column.ExcelFilterValues.Count > 0;

    private void ClearColumnFilter(GridColumn? column)
    {
        if (column == null) return;
        _engine.ClearExcelFilter(column.Field);
        _engine.SetQuickFilter(column.Field, string.Empty);
        ClearSelection();
    }

    // ------------------------------------------------------------------
    // Keyboard
    // ------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown => true,
        _ => base.IsInputKey(keyData)
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Control && e.KeyCode == Keys.Z)
        {
            if (_engine.UndoLastEdit()) CellEdited?.Invoke(this, "Undo Edit Executed");
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.Y)
        {
            if (_engine.RedoLastEdit()) CellEdited?.Invoke(this, "Redo Edit Executed");
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.C)
        {
            CopySelectionToClipboard();
            e.Handled = true;
            return;
        }

        var total = _engine.TotalItemsCount;
        var page = ComputeFrame().FullRows;
        var current = _selectedRowIndex < 0 ? 0 : _selectedRowIndex;

        int? target = e.KeyCode switch
        {
            Keys.Down => Math.Min(total - 1, _selectedRowIndex < 0 ? 0 : current + 1),
            Keys.Up => Math.Max(0, current - 1),
            Keys.PageDown => Math.Min(total - 1, current + page),
            Keys.PageUp => Math.Max(0, current - page),
            Keys.Home when e.Control => 0,
            Keys.End when e.Control => total - 1,
            _ => null
        };

        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
        {
            var step = e.KeyCode == Keys.Left ? -S(48) : S(48);
            _hScroll.Value = Math.Clamp(_hScroll.Value + step, 0, Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1));
            e.Handled = true;
            return;
        }

        if (target is int row && total > 0)
        {
            if (e.Shift && _anchorRowIndex >= 0)
            {
                SelectRange(_anchorRowIndex, row);
                _selectedRowIndex = row;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SelectedRowIndex = row;
            }

            ScrollToRow(row);
            e.Handled = true;
        }
    }

    private void SelectRange(int from, int to)
    {
        _selectedIndices.Clear();
        for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++) _selectedIndices.Add(i);
    }

    /// <summary>Copies selected rows (that are currently cached) as tab-separated text.</summary>
    public void CopySelectionToClipboard()
    {
        if (_selectedIndices.Count == 0 || _engine.ClipboardCopyMode == GridClipboardCopyMode.None) return;

        var columns = GridColumnLayout.OrderVisible(_engine.Columns);
        var text = new StringBuilder();
        if (_engine.ClipboardCopyMode == GridClipboardCopyMode.CellsWithHeaders)
        {
            text.AppendLine(string.Join('\t', columns.Select(c => string.IsNullOrEmpty(c.Title) ? c.Field : c.Title)));
        }

        foreach (var index in _selectedIndices.OrderBy(i => i))
        {
            if (!_engine.TryGetItemAt(index, out var item)) continue;
            text.AppendLine(string.Join('\t', columns.Select(c => FormatValue(c, PropertyOf(c.Field)?.GetValue(item)).Replace('\t', ' '))));
        }

        if (text.Length > 0) Clipboard.SetText(text.ToString());
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var notches = e.Delta / 120;
        if (notches == 0) return;

        if ((ModifierKeys & Keys.Shift) == Keys.Shift && _hScroll.Visible)
        {
            _hScroll.Value = Math.Clamp(_hScroll.Value - notches * S(48), 0, Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1));
            return;
        }

        if (!_vScroll.Visible) return;
        var lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        _vScroll.Value = Math.Clamp(_vScroll.Value - notches * lines, 0, Math.Max(0, _vScroll.Maximum - _vScroll.LargeChange + 1));
    }

    // ------------------------------------------------------------------
    // Mouse
    // ------------------------------------------------------------------

    private static bool InHeader(Frame f, int y) => y >= f.HeaderTop && y < f.HeaderTop + f.HeaderH;

    private Rectangle FilterIconRect(ColumnSlot slot, Frame f)
    {
        var size = S(12);
        var right = (int)slot.Right + f.Gutter - S(8);
        return new Rectangle(right - size, f.HeaderTop + (f.HeaderH - size) / 2, size, size);
    }

    private IEnumerable<(string Member, Rectangle Bounds, Rectangle Close)> GetGroupChips(Frame f)
    {
        var x = S(10);
        foreach (var group in _engine.CurrentRequest.Groups.ToList())
        {
            var text = group.Member;
            var width = TextRenderer.MeasureText(text, HeaderFont, Size.Empty, TextFormatFlags.NoPadding).Width + S(36);
            var bounds = new Rectangle(x, (f.GroupH - S(22)) / 2, width, S(22));
            var close = new Rectangle(bounds.Right - S(20), bounds.Y, S(20), bounds.Height);
            yield return (group.Member, bounds, close);
            x += width + S(8);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        CloseEditor(commit: true);

        var f = ComputeFrame();
        var x = e.X - f.Gutter;

        if (e.Button == MouseButtons.Left && InHeader(f, e.Y))
        {
            var handle = f.Layout.HitTestResizeHandle(x, S(4));
            if (handle >= 0)
            {
                var slot = f.Layout.Slots[handle];
                _drag = DragMode.Resize;
                _dragColumn = slot.Column;
                _dragStart = e.Location;
                _resizeStartWidth = (int)Math.Round(slot.Width);
                _resized = false;
                return;
            }

            var hit = f.Layout.HitTest(x);
            if (hit >= 0)
            {
                _drag = DragMode.HeaderPress;
                _dragColumn = f.Layout.Slots[hit].Column;
                _dragStart = e.Location;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var f = ComputeFrame();
        var x = e.X - f.Gutter;

        switch (_drag)
        {
            case DragMode.Resize when _dragColumn != null:
                var widthPx = _resizeStartWidth + (e.X - _dragStart.X);
                _resized |= Math.Abs(e.X - _dragStart.X) > 0;
                _engine.ResizeColumn(_dragColumn.Field, widthPx / DpiScale);
                return;

            case DragMode.HeaderPress:
                var drag = SystemInformation.DragSize;
                if (_dragColumn?.Reorderable == true && (Math.Abs(e.X - _dragStart.X) >= drag.Width || Math.Abs(e.Y - _dragStart.Y) >= drag.Height))
                {
                    _drag = DragMode.HeaderDrag;
                    Cursor = Cursors.SizeAll;
                }
                return;

            case DragMode.HeaderDrag:
                var drop = f.Layout.GetInsertionIndex(x);
                if (drop != _dropIndex)
                {
                    _dropIndex = drop;
                    Invalidate();
                }
                return;
        }

        UpdateHover(f, e.Location);
    }

    private void UpdateHover(Frame f, Point location)
    {
        var x = location.X - f.Gutter;
        var onHeader = InHeader(f, location.Y);
        var cursor = Cursors.Default;
        var header = -1;

        if (onHeader)
        {
            if (f.Layout.HitTestResizeHandle(x, S(4)) >= 0)
            {
                cursor = Cursors.VSplit;
            }

            header = f.Layout.HitTest(x);
        }

        if (!ReferenceEquals(Cursor, cursor)) Cursor = cursor;

        string? tip = null;
        if (header >= 0 && header < f.Layout.Slots.Count)
        {
            var col = f.Layout.Slots[header].Column;
            tip = !string.IsNullOrEmpty(col.Tooltip) ? col.Tooltip : $"{(string.IsNullOrEmpty(col.Title) ? col.Field : col.Title)}: click to sort, Ctrl+click for multi-sort, drag to reorder";
        }

        if (tip != _toolTipText)
        {
            _toolTipText = tip;
            _toolTip.SetToolTip(this, tip);
        }

        var row = -1;
        if (location.Y >= f.BodyTop && location.Y < f.BodyBottom && location.X < f.BodyWidth)
        {
            foreach (var slot in EnumerateRows(f))
            {
                if (slot.Bounds.Contains(location) && !slot.IsDetail)
                {
                    row = slot.Index;
                    break;
                }
            }
        }

        if (row != _hoveredRow || header != _hoveredHeader)
        {
            _hoveredRow = row;
            _hoveredHeader = header;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var f = ComputeFrame();
        var x = e.X - f.Gutter;
        var mode = _drag;
        var column = _dragColumn;
        _drag = DragMode.None;
        _dragColumn = null;
        Cursor = Cursors.Default;

        switch (mode)
        {
            case DragMode.Resize:
                if (_resized) UpdateScrollBars();
                return;

            case DragMode.HeaderDrag when column != null:
                CompleteColumnDrop(f, column);
                return;

            case DragMode.HeaderPress when column != null && e.Button == MouseButtons.Left:
                HandleHeaderClick(f, column);
                return;
        }

        if (e.Button == MouseButtons.Right && InHeader(f, e.Y))
        {
            var hit = f.Layout.HitTest(x);
            if (hit >= 0)
            {
                _contextColumn = f.Layout.Slots[hit].Column;
                _headerContextMenu.Show(this, e.Location);
            }
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        if (_engine.ShowGroupPanel && e.Y < f.GroupH)
        {
            foreach (var chip in GetGroupChips(f))
            {
                if (chip.Close.Contains(e.Location))
                {
                    _engine.RemoveGroup(chip.Member);
                    return;
                }
            }
            return;
        }

        if (_showFilterRow && e.Y >= f.FilterTop && e.Y < f.FilterTop + f.FilterH)
        {
            var hit = f.Layout.HitTest(x);
            if (hit >= 0 && f.Layout.Slots[hit].Column.Filterable)
            {
                BeginFilterRowEdit(f, f.Layout.Slots[hit]);
            }
            return;
        }

        if (e.Y >= f.BodyTop && e.Y < f.BodyBottom && e.X < f.BodyWidth)
        {
            HandleBodyClick(f, e.Location);
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _drag != DragMode.None)
        {
            _drag = DragMode.None;
            _dragColumn = null;
            _dropIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredRow != -1 || _hoveredHeader != -1)
        {
            _hoveredRow = -1;
            _hoveredHeader = -1;
            Invalidate();
        }
    }

    private void HandleHeaderClick(Frame f, GridColumn column)
    {
        var slot = f.Layout.Slots.FirstOrDefault(s => ReferenceEquals(s.Column, column));
        if (slot == null) return;

        if (column.Filterable && Rectangle.Inflate(FilterIconRect(slot, f), S(4), S(4)).Contains(PointToClient(Cursor.Position)))
        {
            ShowFilterPopup(column, slot, f);
            return;
        }

        if (!column.Sortable) return;
        _engine.CycleSort(column.Field, (ModifierKeys & Keys.Control) == Keys.Control);
        ClearSelection();
    }

    private void CompleteColumnDrop(Frame f, GridColumn column)
    {
        var drop = _dropIndex;
        _dropIndex = -1;
        Invalidate();
        if (drop < 0) return;

        var slots = f.Layout.Slots;
        string? before = drop < slots.Count ? slots[drop].Column.Field : null;
        if (before != null && before.Equals(column.Field, StringComparison.OrdinalIgnoreCase)) return;

        var moved = _engine.MoveColumnBefore(column.Field, before);
        if (moved && before == null && slots.Count > 0)
        {
            _engine.PinColumn(column.Field, slots[^1].Column.PinPosition);
        }
    }

    private void HandleBodyClick(Frame f, Point location)
    {
        foreach (var slot in EnumerateRows(f))
        {
            if (!slot.Bounds.Contains(location) || slot.IsDetail) continue;

            if (_engine.EnableMasterDetail && location.X < f.Gutter && _engine.TryGetItemAt(slot.Index, out var item))
            {
                _engine.ToggleDetailExpansion(item);
                Invalidate();
                return;
            }

            if ((ModifierKeys & Keys.Shift) == Keys.Shift && _anchorRowIndex >= 0)
            {
                SelectRange(_anchorRowIndex, slot.Index);
                _selectedRowIndex = slot.Index;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            else if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (!_selectedIndices.Remove(slot.Index)) _selectedIndices.Add(slot.Index);
                _selectedRowIndex = slot.Index;
                _anchorRowIndex = slot.Index;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            else
            {
                SelectedRowIndex = slot.Index;
            }
            return;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;

        var f = ComputeFrame();
        var x = e.X - f.Gutter;

        if (InHeader(f, e.Y))
        {
            var handle = f.Layout.HitTestResizeHandle(x, S(4));
            if (handle >= 0) AutoSizeColumn(f.Layout.Slots[handle].Column);
            return;
        }

        if (e.Y < f.BodyTop || e.Y >= f.BodyBottom || e.X >= f.BodyWidth) return;

        var colIdx = f.Layout.HitTest(x);
        if (colIdx < 0) return;

        foreach (var slot in EnumerateRows(f))
        {
            if (slot.Bounds.Contains(e.Location) && !slot.IsDetail)
            {
                BeginCellEdit(f, slot, f.Layout.Slots[colIdx]);
                return;
            }
        }
    }

    // ------------------------------------------------------------------
    // Column sizing
    // ------------------------------------------------------------------

    /// <summary>Fits a column to its header and the cached rows' widest value.</summary>
    public void AutoSizeColumn(GridColumn? column)
    {
        if (column == null || !column.Resizable) return;

        var prop = PropertyOf(column.Field);
        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        var widest = TextRenderer.MeasureText(title, HeaderFont, Size.Empty, TextFormatFlags.NoPadding).Width + S(44);

        var rows = _engine.RenderedItems;
        var step = Math.Max(1, rows.Count / 300);
        for (int i = 0; i < rows.Count && prop != null; i += step)
        {
            var text = FormatValue(column, prop.GetValue(rows[i]));
            widest = Math.Max(widest, TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width + S(20));
        }

        _engine.ResizeColumn(column.Field, widest / DpiScale);
    }

    // ------------------------------------------------------------------
    // Editing (cell editor and filter row editor)
    // ------------------------------------------------------------------

    private void BeginCellEdit(Frame f, RowSlot row, ColumnSlot slot)
    {
        var prop = PropertyOf(slot.Column.Field);
        if (prop == null || !prop.CanWrite || !_engine.TryGetItemAt(row.Index, out var item)) return;

        OpenEditor(
            EditorMode.Cell,
            slot.Column,
            row.Index,
            new Rectangle((int)slot.Left + f.Gutter + 1, row.Bounds.Y + 1, (int)slot.Width - 2, row.Bounds.Height - 2),
            Convert.ToString(prop.GetValue(item), CultureInfo.CurrentCulture) ?? string.Empty);
    }

    private void BeginFilterRowEdit(Frame f, ColumnSlot slot)
    {
        _engine.QuickFilters.TryGetValue(slot.Column.Field, out var current);
        OpenEditor(
            EditorMode.FilterRow,
            slot.Column,
            -1,
            new Rectangle((int)slot.Left + f.Gutter + 1, f.FilterTop + 1, (int)slot.Width - 2, f.FilterH - 2),
            current ?? string.Empty);
    }

    private void OpenEditor(EditorMode mode, GridColumn column, int row, Rectangle bounds, string text)
    {
        CloseEditor(commit: true);

        var p = GetPalette();
        _editorMode = mode;
        _editingColumn = column;
        _editingRow = row;
        _editor = new TextBox
        {
            Text = text,
            Bounds = bounds,
            Font = Font,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = p.Surface,
            ForeColor = p.Text
        };

        _editor.KeyDown += (_, ke) =>
        {
            if (ke.KeyCode == Keys.Enter)
            {
                CloseEditor(commit: true);
                ke.Handled = ke.SuppressKeyPress = true;
            }
            else if (ke.KeyCode == Keys.Escape)
            {
                CloseEditor(commit: false);
                ke.Handled = ke.SuppressKeyPress = true;
            }
        };
        _editor.LostFocus += (_, _) => CloseEditor(commit: true);

        Controls.Add(_editor);
        _editor.Focus();
        _editor.SelectAll();
    }

    private void CloseEditor(bool commit)
    {
        if (_editor == null || _closingEditor) return;
        _closingEditor = true;

        var editor = _editor;
        var mode = _editorMode;
        var column = _editingColumn;
        var row = _editingRow;
        var text = editor.Text;
        _editor = null;
        _editingColumn = null;
        _editingRow = -1;
        _editorMode = EditorMode.None;

        try
        {
            Controls.Remove(editor);
            editor.Dispose();

            if (!commit || column == null) return;

            if (mode == EditorMode.FilterRow)
            {
                _engine.SetQuickFilter(column.Field, text.Trim());
                ClearSelection();
            }
            else if (mode == EditorMode.Cell && _engine.TryGetItemAt(row, out var item) && _engine.EditCell(item, column.Field, text))
            {
                CellEdited?.Invoke(this, $"Edited [{column.Field}] = '{text}' (Row {row})");
                Invalidate();
            }
        }
        finally
        {
            _closingEditor = false;
            if (IsHandleCreated && !IsDisposed) Focus();
        }
    }

    // ------------------------------------------------------------------
    // Value-list filter popup
    // ------------------------------------------------------------------

    private void ShowFilterPopup(GridColumn column, ColumnSlot slot, Frame f)
    {
        var p = GetPalette();
        var all = _engine.GetDistinctValues(column.Field, null, FilterListLimit);
        var truncated = all.Count >= FilterListLimit;
        var selected = column.ExcelFilterValues.Count > 0 ? new HashSet<object>(column.ExcelFilterValues) : new HashSet<object>(all);

        var panel = new Panel { Size = new Size(S(260), S(340)), BackColor = p.Surface, Padding = new Padding(S(8)) };
        var search = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search values", BackColor = p.Surface, ForeColor = p.Text, Font = Font };
        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = truncated ? S(32) : 0,
            Text = truncated ? $"Showing the first {FilterListLimit:N0} values. Use search to find others." : string.Empty,
            ForeColor = p.Muted,
            Font = Font
        };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None, BackColor = p.Surface, ForeColor = p.Text, Font = Font };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(36), FlowDirection = FlowDirection.RightToLeft, BackColor = p.Surface };
        var ok = new Button { Text = "OK", FlatStyle = FlatStyle.System, Width = S(64) };
        var cancel = new Button { Text = "Cancel", FlatStyle = FlatStyle.System, Width = S(64) };
        var selectAll = new Button { Text = "All", FlatStyle = FlatStyle.System, Width = S(48) };
        var selectNone = new Button { Text = "None", FlatStyle = FlatStyle.System, Width = S(48) };
        buttons.Controls.AddRange(new Control[] { ok, cancel, selectNone, selectAll });
        panel.Controls.Add(list);
        panel.Controls.Add(note);
        panel.Controls.Add(buttons);
        panel.Controls.Add(search);
        list.BringToFront();

        var populating = false;
        void Populate()
        {
            populating = true;
            list.BeginUpdate();
            list.Items.Clear();
            var filter = search.Text;
            foreach (var value in all)
            {
                if (filter.Length > 0 && !(value.ToString() ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                list.Items.Add(new FilterItem(value), selected.Contains(value));
            }
            list.EndUpdate();
            populating = false;
            ok.Enabled = selected.Count > 0;
        }

        list.ItemCheck += (_, e) =>
        {
            if (populating || list.Items[e.Index] is not FilterItem item) return;
            if (e.NewValue == CheckState.Checked) selected.Add(item.Value); else selected.Remove(item.Value);
            ok.Enabled = selected.Count > 0 || e.NewValue == CheckState.Checked;
        };
        search.TextChanged += (_, _) => Populate();

        if (_filterDropDown.Visible)
        {
            _filterDropDown.Close();
        }
        while (_filterDropDown.Items.Count > 0)
        {
            var oldItem = _filterDropDown.Items[0];
            _filterDropDown.Items.RemoveAt(0);
            if (oldItem is ToolStripControlHost oldHost)
            {
                oldHost.Control?.Dispose();
            }
            oldItem.Dispose();
        }
        _filterDropDown.Items.Add(new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = panel.Size });

        selectAll.Click += (_, _) =>
        {
            foreach (var v in all.Where(v => search.Text.Length == 0 || (v.ToString() ?? string.Empty).Contains(search.Text, StringComparison.OrdinalIgnoreCase))) selected.Add(v);
            Populate();
        };
        selectNone.Click += (_, _) =>
        {
            foreach (var v in all.Where(v => search.Text.Length == 0 || (v.ToString() ?? string.Empty).Contains(search.Text, StringComparison.OrdinalIgnoreCase))) selected.Remove(v);
            Populate();
        };
        cancel.Click += (_, _) => _filterDropDown.Close();
        ok.Click += (_, _) =>
        {
            _filterDropDown.Close();
            if (all.All(selected.Contains)) _engine.ClearExcelFilter(column.Field);
            else _engine.ApplyExcelFilter(column.Field, selected.ToList());
            ClearSelection();
        };

        Populate();
        _filterDropDown.Show(this, new Point(Math.Max(0, (int)slot.Right + f.Gutter - panel.Width), f.HeaderTop + f.HeaderH));
        search.Focus();
    }

    // ------------------------------------------------------------------
    // Export & print
    // ------------------------------------------------------------------

    public async Task ExportToCsvAsync(string filePath) => await ExportAsync(filePath, ExportFormat.Csv);
    public async Task ExportToExcelAsync(string filePath) => await ExportAsync(filePath, ExportFormat.ExcelXml);
    public async Task ExportToJsonAsync(string filePath) => await ExportAsync(filePath, ExportFormat.Json);
    public async Task ExportToXmlAsync(string filePath) => await ExportAsync(filePath, ExportFormat.Xml);
    public async Task ExportToPdfAsync(string filePath) => await ExportAsync(filePath, ExportFormat.Pdf);

    /// <summary>Exports every row matching the current filters and sort order, not only the cached window.</summary>
    private async Task ExportAsync(string filePath, ExportFormat format)
    {
        var rows = await _engine.ReadAllAsync();
        using var stream = File.Create(filePath);
        await ExportEngine.ExportAsync(rows, format, stream);
    }

    /// <summary>Opens a print preview of up to 5,000 rows matching the current filters and sort order.</summary>
    public async Task ShowPrintPreviewDialogAsync()
    {
        var rows = await _engine.ReadAllAsync(PrintRowLimit);
        var columns = GridColumnLayout.OrderVisible(_engine.Columns);
        if (columns.Count == 0) return;

        var totalWidth = columns.Sum(GridColumnLayout.WidthOf);
        var index = 0;

        using var printDoc = new PrintDocument { DocumentName = "EnterpriseUI DataGrid Report" };
        printDoc.PrintPage += (_, pe) =>
        {
            var g = pe.Graphics;
            if (g == null) return;

            var bounds = pe.MarginBounds;
            var y = bounds.Top;
            using var titleFont = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var headerFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var cellFont = new Font("Segoe UI", 8.5f);

            g.DrawString($"Data Grid Report - {DateTime.Now:yyyy-MM-dd HH:mm}", titleFont, Brushes.Black, bounds.Left, y);
            y += 32;

            var widths = columns.Select(c => (int)(bounds.Width * GridColumnLayout.WidthOf(c) / totalWidth)).ToArray();
            var x = bounds.Left;
            for (int c = 0; c < columns.Count; c++)
            {
                g.FillRectangle(Brushes.WhiteSmoke, x, y, widths[c], 24);
                g.DrawRectangle(Pens.Gray, x, y, widths[c], 24);
                g.DrawString(string.IsNullOrEmpty(columns[c].Title) ? columns[c].Field : columns[c].Title, headerFont, Brushes.Black, new RectangleF(x + 4, y + 4, widths[c] - 8, 18));
                x += widths[c];
            }
            y += 24;

            while (index < rows.Count && y + 22 <= bounds.Bottom)
            {
                x = bounds.Left;
                for (int c = 0; c < columns.Count; c++)
                {
                    g.DrawRectangle(Pens.LightGray, x, y, widths[c], 22);
                    g.DrawString(FormatValue(columns[c], PropertyOf(columns[c].Field)?.GetValue(rows[index])), cellFont, Brushes.Black, new RectangleF(x + 4, y + 3, widths[c] - 8, 18));
                    x += widths[c];
                }
                y += 22;
                index++;
            }

            pe.HasMorePages = index < rows.Count;
        };

        using var preview = new PrintPreviewDialog { Document = printDoc, Width = 960, Height = 720, StartPosition = FormStartPosition.CenterParent };
        preview.ShowDialog(FindForm());
    }

    public void ShowPrintPreviewDialog() => _ = ShowPrintPreviewDialogSafeAsync();

    private async Task ShowPrintPreviewDialogSafeAsync()
    {
        try
        {
            await ShowPrintPreviewDialogAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Drawing.Printing.InvalidPrinterException)
        {
            MessageBox.Show(FindForm(), ex.Message, "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------------
    // Painting
    // ------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var p = GetPalette();
        var f = ComputeFrame();
        g.Clear(p.Surface);

        DrawGroupPanel(g, f, p);
        DrawBands(g, f, p);
        DrawHeader(g, f, p);
        DrawFilterRow(g, f, p);
        DrawBody(g, f, p);
        DrawPinDividers(g, f, p);
        DrawFooter(g, f, p);
        DrawDragFeedback(g, f, p);

        using var outline = new Pen(Focused ? p.Primary : p.Border, Focused ? Math.Max(1, S(1.5)) : 1);
        g.DrawRectangle(outline, 0, 0, Width - 1, Height - 1);
    }

    private void DrawGroupPanel(Graphics g, Frame f, Palette p)
    {
        if (f.GroupH == 0) return;

        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, 0, Width, f.GroupH);
        g.DrawLine(line, 0, f.GroupH - 1, Width, f.GroupH - 1);

        if (_engine.CurrentRequest.Groups.Count == 0)
        {
            TextRenderer.DrawText(g, "Right-click a column header and choose Group By", Font, new Rectangle(S(10), 0, Width - S(20), f.GroupH), p.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            return;
        }

        foreach (var chip in GetGroupChips(f))
        {
            using var chipBrush = new SolidBrush(p.Primary);
            using var path = RoundedRect(chip.Bounds, S(4));
            g.FillPath(chipBrush, path);
            TextRenderer.DrawText(g, chip.Member, HeaderFont, new Rectangle(chip.Bounds.X + S(10), chip.Bounds.Y, chip.Bounds.Width - S(30), chip.Bounds.Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            using var cross = new Pen(Color.White, 1.5f);
            var cx = chip.Close.X + chip.Close.Width / 2;
            var cy = chip.Close.Y + chip.Close.Height / 2;
            var r = S(3);
            g.DrawLine(cross, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(cross, cx - r, cy + r, cx + r, cy - r);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawBands(Graphics g, Frame f, Palette p)
    {
        if (f.BandDepth == 0) return;

        using var bandBrush = new SolidBrush(Blend(p.Surface, p.Primary, 0.10));
        using var line = new Pen(p.Border);
        g.FillRectangle(bandBrush, 0, f.BandTop, Width, f.BandDepth * f.BandRowH);

        var scrollRegion = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.BandTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.BandDepth * f.BandRowH);
        foreach (var span in f.Bands)
        {
            var state = g.Save();
            if (span.Zone == ColumnZone.Scrollable) g.SetClip(scrollRegion);

            var rect = new Rectangle((int)span.Left + f.Gutter, f.BandTop + span.Level * f.BandRowH, (int)span.Width, f.BandRowH);
            if (span.Zone != ColumnZone.Scrollable) g.FillRectangle(bandBrush, rect);
            g.DrawRectangle(line, rect);

            var textRect = span.Zone == ColumnZone.Scrollable ? Rectangle.Intersect(Rectangle.Inflate(rect, -S(6), 0), scrollRegion) : Rectangle.Inflate(rect, -S(6), 0);
            TextRenderer.DrawText(g, span.Band.Title, HeaderFont, textRect, p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            g.Restore(state);
        }
    }

    private void DrawHeader(Graphics g, Frame f, Palette p)
    {
        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.HeaderTop, Width, f.HeaderH);
        g.DrawLine(line, 0, f.HeaderTop + f.HeaderH - 1, Width, f.HeaderTop + f.HeaderH - 1);

        if (f.Gutter > 0) g.DrawLine(line, f.Gutter, f.HeaderTop, f.Gutter, f.HeaderTop + f.HeaderH);

        var layout = f.Layout;
        for (int zonePass = 0; zonePass < 2; zonePass++)
        {
            var state = g.Save();
            if (zonePass == 0)
            {
                g.SetClip(new Rectangle((int)layout.ScrollableLeft + f.Gutter, f.HeaderTop, Math.Max(0, (int)(layout.ScrollableRight - layout.ScrollableLeft)), f.HeaderH));
            }

            for (int i = 0; i < layout.Slots.Count; i++)
            {
                var slot = layout.Slots[i];
                if ((slot.Zone == ColumnZone.Scrollable) != (zonePass == 0)) continue;
                DrawHeaderCell(g, f, p, slot, i);
            }
            g.Restore(state);
        }
    }

    private void DrawHeaderCell(Graphics g, Frame f, Palette p, ColumnSlot slot, int index)
    {
        var rect = new Rectangle((int)slot.Left + f.Gutter, f.HeaderTop, (int)slot.Width, f.HeaderH);
        var column = slot.Column;

        using var bg = new SolidBrush(index == _hoveredHeader && _drag == DragMode.None ? Blend(p.Header, p.Primary, 0.08) : p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, rect);
        g.DrawLine(line, rect.Right - 1, rect.Y + S(6), rect.Right - 1, rect.Bottom - S(6));

        var reserved = S(8);
        if (column.Filterable) reserved += S(20);
        if (column.SortDirection != null) reserved += S(column.SortIndex >= 0 && _engine.CurrentRequest.Sorts.Count > 1 ? 24 : 16);

        var title = string.IsNullOrEmpty(column.Title) ? column.Field : column.Title;
        TextRenderer.DrawText(g, title, HeaderFont, new Rectangle(rect.X + S(8), rect.Y, Math.Max(0, rect.Width - reserved - S(8)), rect.Height), p.Text, FlagsFor(column.Alignment == ColumnTextAlignment.Right ? ColumnTextAlignment.Right : ColumnTextAlignment.Left));

        var right = rect.Right - S(8);
        if (column.Filterable)
        {
            var icon = FilterIconRect(slot, f);
            DrawFunnel(g, icon, HasFilter(column) ? p.Primary : p.Muted);
            right = icon.X - S(4);
        }

        if (column.SortDirection != null)
        {
            var asc = column.SortDirection == ListSortDirection.Ascending;
            var size = S(6);
            var cy = rect.Y + rect.Height / 2;
            var showIndex = column.SortIndex >= 0 && _engine.CurrentRequest.Sorts.Count > 1;
            var arrowRight = right - (showIndex ? S(10) : 0);
            var pts = asc
                ? new[] { new Point(arrowRight - size * 2, cy + size / 2), new Point(arrowRight, cy + size / 2), new Point(arrowRight - size, cy - size / 2 - 1) }
                : new[] { new Point(arrowRight - size * 2, cy - size / 2), new Point(arrowRight, cy - size / 2), new Point(arrowRight - size, cy + size / 2 + 1) };
            using var arrow = new SolidBrush(p.Primary);
            g.FillPolygon(arrow, pts);

            if (showIndex)
            {
                TextRenderer.DrawText(g, (column.SortIndex + 1).ToString(CultureInfo.CurrentCulture), SmallFont, new Rectangle(arrowRight + S(2), rect.Y, S(10), rect.Height), p.Primary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }

    private static void DrawFunnel(Graphics g, Rectangle r, Color color)
    {
        using var brush = new SolidBrush(color);
        var pts = new[]
        {
            new PointF(r.X, r.Y + r.Height * 0.15f),
            new PointF(r.Right, r.Y + r.Height * 0.15f),
            new PointF(r.X + r.Width * 0.62f, r.Y + r.Height * 0.55f),
            new PointF(r.X + r.Width * 0.62f, r.Bottom - r.Height * 0.1f),
            new PointF(r.X + r.Width * 0.38f, r.Bottom - r.Height * 0.25f),
            new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.55f)
        };
        g.FillPolygon(brush, pts);
    }

    private void DrawFilterRow(Graphics g, Frame f, Palette p)
    {
        if (f.FilterH == 0) return;

        using var bg = new SolidBrush(Blend(p.Surface, p.Primary, 0.04));
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.FilterTop, Width, f.FilterH);
        g.DrawLine(line, 0, f.FilterTop + f.FilterH - 1, Width, f.FilterTop + f.FilterH - 1);

        var scrollRegion = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, f.FilterTop, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), f.FilterH);
        foreach (var slot in f.Layout.Slots)
        {
            var state = g.Save();
            if (slot.Zone == ColumnZone.Scrollable) g.SetClip(scrollRegion);

            var rect = new Rectangle((int)slot.Left + f.Gutter, f.FilterTop, (int)slot.Width, f.FilterH);
            if (slot.Zone != ColumnZone.Scrollable) g.FillRectangle(bg, rect);
            g.DrawLine(line, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);

            if (slot.Column.Filterable)
            {
                var has = _engine.QuickFilters.TryGetValue(slot.Column.Field, out var text);
                TextRenderer.DrawText(g, has ? text! : "Filter...", Font, Rectangle.Inflate(rect, -S(8), 0), has ? p.Text : p.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
            g.Restore(state);
        }
    }

    private void DrawBody(Graphics g, Frame f, Palette p)
    {
        var bodyRect = new Rectangle(0, f.BodyTop, f.BodyWidth, Math.Max(0, f.BodyBottom - f.BodyTop));
        var state = g.Save();
        g.SetClip(bodyRect);

        using var grid = new Pen(p.Border);
        var rowRules = _engine.FormatRules.Where(r => r.Enabled && r.ApplyToRow && r.RuleType == FormatRuleType.HighlightCell).ToArray();
        var cellRules = _engine.FormatRules
            .Where(r => r.Enabled && !r.ApplyToRow)
            .GroupBy(r => r.ColumnField, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var drewAny = false;
        foreach (var row in EnumerateRows(f))
        {
            drewAny = true;
            if (row.IsDetail)
            {
                DrawDetailRow(g, f, p, row);
                continue;
            }

            if (!_engine.TryGetItemAt(row.Index, out var item))
            {
                TextRenderer.DrawText(g, "Loading...", Font, new Rectangle(f.Gutter + S(8), row.Bounds.Y, row.Bounds.Width, row.Bounds.Height), p.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                continue;
            }

            var selected = _selectedIndices.Contains(row.Index);
            var rowColor = row.Index % 2 == 1 ? Blend(p.Surface, p.Primary, 0.03) : p.Surface;
            foreach (var rule in rowRules)
            {
                if (GridFormatRuleEvaluator.Matches(rule, PropertyOf(rule.ColumnField)?.GetValue(item)))
                {
                    rowColor = GetRuleColor(rule.BackgroundColorHex, Blend(p.Surface, p.Primary, 0.12));
                }
            }

            if (selected) rowColor = Blend(rowColor, p.Primary, 0.20);
            else if (row.Index == _hoveredRow) rowColor = Blend(rowColor, p.Primary, 0.08);

            using (var rowBrush = new SolidBrush(rowColor)) g.FillRectangle(rowBrush, row.Bounds);

            DrawRowGutter(g, f, p, row, item, rowColor);

            var scrollable = new Rectangle((int)f.Layout.ScrollableLeft + f.Gutter, row.Bounds.Y, Math.Max(0, (int)(f.Layout.ScrollableRight - f.Layout.ScrollableLeft)), row.Bounds.Height);
            var rowState = g.Save();
            g.SetClip(Rectangle.Intersect(bodyRect, scrollable));
            foreach (var slot in f.Layout.Slots.Where(s => s.Zone == ColumnZone.Scrollable))
            {
                DrawCell(g, f, p, slot, row, item, cellRules, grid);
            }
            g.Restore(rowState);

            using var pinBrush = new SolidBrush(rowColor);
            foreach (var slot in f.Layout.Slots.Where(s => s.Zone != ColumnZone.Scrollable))
            {
                g.FillRectangle(pinBrush, (int)slot.Left + f.Gutter, row.Bounds.Y, (int)slot.Width, row.Bounds.Height);
                DrawCell(g, f, p, slot, row, item, cellRules, grid);
            }

            g.DrawLine(grid, 0, row.Bounds.Bottom - 1, f.BodyWidth, row.Bounds.Bottom - 1);

            if (row.Index == _selectedRowIndex)
            {
                using var accent = new SolidBrush(p.Primary);
                g.FillRectangle(accent, 0, row.Bounds.Y, Math.Max(2, S(3)), row.Bounds.Height);
            }
        }

        if (!drewAny)
        {
            var message = _engine.IsLoading ? "Loading..." : "No records to display";
            TextRenderer.DrawText(g, message, Font, bodyRect, p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        g.Restore(state);
    }

    private void DrawRowGutter(Graphics g, Frame f, Palette p, RowSlot row, TItem item, Color rowColor)
    {
        if (f.Gutter == 0) return;

        var expanded = _engine.IsDetailExpanded(item);
        var box = new Rectangle(row.Bounds.X + (f.Gutter - S(12)) / 2, row.Bounds.Y + (row.Bounds.Height - S(12)) / 2, S(12), S(12));
        using var pen = new Pen(p.Muted);
        g.DrawRectangle(pen, box);
        var cx = box.X + box.Width / 2;
        var cy = box.Y + box.Height / 2;
        g.DrawLine(pen, box.X + S(3), cy, box.Right - S(3), cy);
        if (!expanded) g.DrawLine(pen, cx, box.Y + S(3), cx, box.Bottom - S(3));
    }

    private void DrawCell(Graphics g, Frame f, Palette p, ColumnSlot slot, RowSlot row, TItem item, Dictionary<string, GridFormatRule[]> cellRules, Pen grid)
    {
        var rect = new Rectangle((int)slot.Left + f.Gutter, row.Bounds.Y, (int)slot.Width, row.Bounds.Height);
        var column = slot.Column;
        var value = PropertyOf(column.Field)?.GetValue(item);
        Color textColor = Enabled ? p.Text : p.Muted;

        if (cellRules.TryGetValue(column.Field, out var rules))
        {
            foreach (var rule in rules)
            {
                if (rule.RuleType == FormatRuleType.HighlightCell && GridFormatRuleEvaluator.Matches(rule, value))
                {
                    using var highlight = new SolidBrush(GetRuleColor(rule.BackgroundColorHex, Blend(p.Surface, p.Primary, 0.18)));
                    g.FillRectangle(highlight, rect);
                    if (!string.IsNullOrWhiteSpace(rule.ForegroundColorHex)) textColor = GetRuleColor(rule.ForegroundColorHex, textColor);
                }
                else if (rule.RuleType == FormatRuleType.DataBar && ShowConditionalDataBars &&
                    GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var ratio))
                {
                    var barWidth = (int)Math.Round((rect.Width - S(8)) * ratio);
                    if (barWidth > 0)
                    {
                        using var bar = new SolidBrush(GetRuleColor(rule.BackgroundColorHex, Color.FromArgb(96, p.Primary)));
                        g.FillRectangle(bar, rect.X + S(4), rect.Y + S(4), barWidth, rect.Height - S(8));
                    }
                }
                else if (rule.RuleType == FormatRuleType.ColorScale &&
                    GridFormatRuleEvaluator.TryGetNormalizedValue(value, rule.Value1, rule.Value2, out var scale))
                {
                    var from = GetRuleColor(rule.BackgroundColorHex, p.Header);
                    var to = GetRuleColor(rule.ForegroundColorHex, p.Primary);
                    using var scaleBrush = new SolidBrush(Blend(from, to, scale));
                    g.FillRectangle(scaleBrush, rect);
                }
            }
        }

        g.DrawLine(grid, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);
        TextRenderer.DrawText(g, FormatValue(column, value), Font, Rectangle.Inflate(rect, -S(8), 0), textColor, FlagsFor(column.Alignment));
    }

    private void DrawDetailRow(Graphics g, Frame f, Palette p, RowSlot row)
    {
        if (!_engine.TryGetItemAt(row.Index, out var item)) return;

        using var bg = new SolidBrush(p.Header);
        using var border = new Pen(p.Border);
        var rect = Rectangle.Inflate(row.Bounds, -S(1), 0);
        g.FillRectangle(bg, rect);
        g.DrawLine(border, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);

        var summary = typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(pr => pr.GetIndexParameters().Length == 0)
            .Take(8)
            .Select(pr => $"{pr.Name}: {Convert.ToString(pr.GetValue(item), CultureInfo.CurrentCulture)}");
        TextRenderer.DrawText(g, string.Join("   |   ", summary), Font, new Rectangle(f.Gutter + S(12), rect.Y, Math.Max(0, rect.Width - f.Gutter - S(24)), rect.Height), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DrawPinDividers(Graphics g, Frame f, Palette p)
    {
        var layout = f.Layout;
        using var pen = new Pen(Blend(p.Border, p.Primary, 0.5), Math.Max(1, S(1.5)));
        var top = f.HeaderTop;
        var bottom = f.BodyBottom;

        if (layout.Slots.Any(s => s.Zone == ColumnZone.Left))
        {
            var x = (int)layout.ScrollableLeft + f.Gutter;
            g.DrawLine(pen, x, top, x, bottom);
        }

        if (layout.Slots.Any(s => s.Zone == ColumnZone.Right))
        {
            var x = (int)layout.ScrollableRight + f.Gutter;
            g.DrawLine(pen, x, top, x, bottom);
        }
    }

    private void DrawFooter(Graphics g, Frame f, Palette p)
    {
        if (f.FooterH == 0) return;

        var top = ClientSize.Height - f.FooterH;
        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, top, Width, f.FooterH);
        g.DrawLine(line, 0, top, Width, top);

        var text = new StringBuilder($"Rows: {_engine.TotalItemsCount:N0}");
        if (_selectedIndices.Count > 0) text.Append($"  |  Selected: {_selectedIndices.Count:N0}");
        if (_engine.CurrentRequest.Groups.Count > 0) text.Append($"  |  Groups: {_engine.CurrentRequest.Groups.Count}");
        if (_engine.CanUndo) text.Append("  |  Ctrl+Z to undo");
        if (_engine.IsLoading) text.Append("  |  Loading...");

        TextRenderer.DrawText(g, text.ToString(), HeaderFont, new Rectangle(S(10), top, Width - S(20), f.FooterH), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private void DrawDragFeedback(Graphics g, Frame f, Palette p)
    {
        if (_drag != DragMode.HeaderDrag || _dragColumn == null) return;

        var slots = f.Layout.Slots;
        var cursorX = PointToClient(Cursor.Position).X;
        var title = string.IsNullOrEmpty(_dragColumn.Title) ? _dragColumn.Field : _dragColumn.Title;
        var ghost = new Rectangle(cursorX - S(40), f.HeaderTop + S(2), S(96), f.HeaderH - S(4));
        using (var ghostBrush = new SolidBrush(Color.FromArgb(200, p.Primary)))
        {
            g.FillRectangle(ghostBrush, ghost);
        }
        TextRenderer.DrawText(g, title, HeaderFont, ghost, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (_dropIndex >= 0 && slots.Count > 0)
        {
            var x = _dropIndex < slots.Count ? (int)slots[_dropIndex].Left : (int)slots[^1].Right;
            x += f.Gutter;
            using var pen = new Pen(p.Primary, Math.Max(2, S(2)));
            g.DrawLine(pen, x, f.HeaderTop, x, f.BodyBottom);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            _engine.PropertyChanged -= OnEngineChanged;
            _engine.QueryChanged -= OnEngineQueryChanged;
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
            _toolTip.Dispose();
            _editor?.Dispose();
            _engine.Dispose();
        }

        base.Dispose(disposing);
    }
}
