using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Automation;
using EnterpriseUI.Exporting;
using EnterpriseUI.Grids;
using EnterpriseUI.Printing;

namespace EnterpriseUI.WinForms;

public partial class EnterpriseDataGrid<TItem>
{
    // ------------------------------------------------------------------
    // Feature state
    // ------------------------------------------------------------------

    private enum PagerAction { First, Previous, Page, Next, Last, PageSize, LoadMore, None }

    private readonly record struct PagerItem(Rectangle Bounds, PagerAction Action, int Arg, string Text, bool Enabled, bool Current);

    private readonly Panel _searchPanel = new() { Visible = false };
    private readonly TextBox _searchBox = new();
    private readonly Label _searchCount = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _searchPrev = new() { Text = "\u25B2", AccessibleName = "Previous match", FlatStyle = FlatStyle.Flat };
    private readonly Button _searchNext = new() { Text = "\u25BC", AccessibleName = "Next match", FlatStyle = FlatStyle.Flat };
    private readonly Button _searchClose = new() { Text = "\u2715", AccessibleName = "Close search", FlatStyle = FlatStyle.Flat };
    private readonly CheckBox _searchFilter = new() { Text = "Filter rows", Checked = true, AutoSize = false };
    private readonly HashSet<object> _observed = new(ReferenceEqualityComparer.Instance);

    private bool _showPager = true;
    private bool _showColumnSummaries = true;
    private bool _syncingSearch;
    private bool _loadingMore;
    private int _dropRow = -1;
    private int _hoveredPager = -1;
    private PrintTemplate _printTemplate = PrintTemplate.Standard();

    private static string DragFormat => $"EnterpriseUI.GridRows.{typeof(TItem).FullName}";

    private sealed class RowDragPayload
    {
        public RowDragPayload(EnterpriseDataGrid<TItem> source, List<TItem> items)
        {
            Source = source;
            Items = items;
        }

        public EnterpriseDataGrid<TItem> Source { get; }
        public List<TItem> Items { get; }
    }

    private void InitializeFeatures()
    {
        _searchBox.AccessibleName = "Search grid";
        _searchBox.PlaceholderText = "Search all columns (Ctrl+F)";
        _searchFilter.AccessibleName = "Filter rows while searching";
        _searchCount.AccessibleName = "Search result count";
        foreach (var button in new[] { _searchPrev, _searchNext, _searchClose }) button.TabStop = true;

        _searchPanel.Controls.AddRange(new Control[] { _searchBox, _searchCount, _searchFilter, _searchPrev, _searchNext, _searchClose });
        _searchPanel.Resize += (_, _) => LayoutSearchChildren();
        _searchPanel.AccessibleName = "Search panel";
        Controls.Add(_searchPanel);

        _searchBox.TextChanged += (_, _) =>
        {
            if (_syncingSearch) return;
            _engine.SearchPanelText = string.IsNullOrWhiteSpace(_searchBox.Text) ? null : _searchBox.Text;
        };
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = e.SuppressKeyPress = true;
                if (e.Shift) _engine.PreviousMatch(); else _engine.NextMatch();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = e.SuppressKeyPress = true;
                ShowSearchPanel = false;
            }
        };
        _searchPrev.Click += (_, _) => _engine.PreviousMatch();
        _searchNext.Click += (_, _) => _engine.NextMatch();
        _searchClose.Click += (_, _) => ShowSearchPanel = false;
        _searchFilter.CheckedChanged += (_, _) =>
        {
            _engine.SearchMode = _searchFilter.Checked ? GridSearchMode.Filter : GridSearchMode.Highlight;
            var text = _engine.SearchPanelText;
            if (text == null) return;
            _engine.SearchPanelText = null;
            _engine.SearchPanelText = text;
        };

        _bodyContextMenu.Opening += (_, _) => RefreshBodyMenuState();
    }

    private void DisposeFeatures()
    {
        UnsubscribeAllItems();
        _searchPanel.Dispose();
    }

    // ------------------------------------------------------------------
    // Search panel
    // ------------------------------------------------------------------

    /// <summary>Shows the built-in search panel (Ctrl+F). Typing filters rows or highlights and counts matches.</summary>
    public bool ShowSearchPanel
    {
        get => _searchPanel.Visible;
        set
        {
            if (_searchPanel.Visible == value) return;
            _searchPanel.Visible = value;
            if (!value)
            {
                _syncingSearch = true;
                _searchBox.Text = string.Empty;
                _syncingSearch = false;
                _engine.SearchPanelText = null;
                UpdateSearchCount();
            }

            RefreshLayout();
            if (value)
            {
                ApplyThemeToChildren();
                _searchBox.Focus();
            }
            else if (IsHandleCreated)
            {
                Focus();
            }
        }
    }

    private void LayoutSearchPanel(Frame f)
    {
        if (!_searchPanel.Visible) return;
        _searchPanel.SetBounds(0, 0, ClientSize.Width, f.SearchH);
        LayoutSearchChildren();
    }

    private void LayoutSearchChildren()
    {
        var h = _searchPanel.Height;
        var pad = S(6);
        var item = Math.Max(S(22), h - pad * 2);
        var x = _searchPanel.Width - pad;

        x -= item; _searchClose.SetBounds(x, pad, item, item);
        x -= item + S(2); _searchNext.SetBounds(x, pad, item, item);
        x -= item + S(2); _searchPrev.SetBounds(x, pad, item, item);
        x -= S(96) + S(6); _searchCount.SetBounds(x, pad, S(96), item);
        x -= S(100) + S(6); _searchFilter.SetBounds(x, pad, S(100), item);
        _searchBox.SetBounds(pad, pad + 1, Math.Max(S(80), x - pad * 2), item);
    }

    private void UpdateSearchCount()
    {
        var result = _engine.SearchResult;
        var text = _engine.HighlightText;
        _searchCount.Text = text == null || result == null ? string.Empty
            : result.MatchCount == 0 ? "No matches"
            : _engine.CurrentMatchIndex >= 0 ? $"{_engine.CurrentMatchIndex + 1} of {result.MatchCount:N0}"
            : $"{result.MatchCount:N0} matches";
    }

    private void ApplyThemeToChildren()
    {
        var p = GetPalette();
        _searchPanel.BackColor = p.Header;
        _searchPanel.ForeColor = p.Text;
        _searchBox.BackColor = p.Surface;
        _searchBox.ForeColor = p.Text;
        _searchCount.ForeColor = p.Muted;
        _searchFilter.ForeColor = p.Text;
        foreach (var button in new[] { _searchPrev, _searchNext, _searchClose })
        {
            button.BackColor = p.Header;
            button.ForeColor = p.Text;
            button.FlatAppearance.BorderColor = p.Border;
        }
    }

    /// <summary>Shows column summaries (footers) between the rows and the status bar.</summary>
    public bool ShowColumnSummaries { get => _showColumnSummaries; set { _showColumnSummaries = value; RefreshLayout(); } }

    // ------------------------------------------------------------------
    // Pager
    // ------------------------------------------------------------------

    /// <summary>Shows the pager when <see cref="PagingMode"/> is Client, Server or Infinite.</summary>
    public bool ShowPager { get => _showPager; set { _showPager = value; RefreshLayout(); } }

    private bool PagerVisible => _showPager && _engine.PagingMode is GridPagingMode.Client or GridPagingMode.Server or GridPagingMode.Infinite;

    private List<PagerItem> LayoutPager(Frame f)
    {
        var items = new List<PagerItem>();
        if (f.PagerH == 0) return items;

        var h = S(26);
        var y = f.PagerTop + (f.PagerH - h) / 2;
        var x = S(8);
        var buttonWidth = S(30);

        void Add(string text, PagerAction action, int arg, bool enabled, bool current = false, int? width = null)
        {
            var w = width ?? Math.Max(buttonWidth, TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width + S(14));
            items.Add(new PagerItem(new Rectangle(x, y, w, h), action, arg, text, enabled, current));
            x += w + S(3);
        }

        if (_engine.PagingMode == GridPagingMode.Infinite)
        {
            Add("Load more", PagerAction.LoadMore, 0, _engine.HasMoreRows, width: S(96));
            return items;
        }

        Add("\u00AB", PagerAction.First, 0, _engine.HasPreviousPage);
        Add("\u2039", PagerAction.Previous, 0, _engine.HasPreviousPage);
        foreach (var page in GridPagingHelper.GetPageButtons(_engine.PageIndex, _engine.PageCount, 7))
        {
            if (page < 0) Add("\u2026", PagerAction.None, -1, false);
            else Add((page + 1).ToString(CultureInfo.CurrentCulture), PagerAction.Page, page, true, page == _engine.PageIndex);
        }
        Add("\u203A", PagerAction.Next, 0, _engine.HasNextPage);
        Add("\u00BB", PagerAction.Last, 0, _engine.HasNextPage);
        x += S(8);
        Add($"Rows per page: {_engine.PageSize} \u25BE", PagerAction.PageSize, 0, true);
        return items;
    }

    private void DrawPager(Graphics g, Frame f, Colors p)
    {
        if (f.PagerH == 0) return;

        using var bg = new SolidBrush(p.Header);
        using var line = new Pen(p.Border);
        g.FillRectangle(bg, 0, f.PagerTop, Width, f.PagerH);
        g.DrawLine(line, 0, f.PagerTop, Width, f.PagerTop);

        var items = LayoutPager(f);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var hover = i == _hoveredPager && item.Enabled && !item.Current;
            using var back = new SolidBrush(item.Current ? p.Primary : hover ? Blend(p.Header, p.Primary, 0.12) : p.Surface);
            g.FillRectangle(back, item.Bounds);
            if (item.Action != PagerAction.None) g.DrawRectangle(line, item.Bounds);
            var color = item.Current ? p.PrimaryText : item.Enabled ? p.Text : p.Muted;
            TextRenderer.DrawText(g, item.Text, item.Current ? HeaderFont : Font, item.Bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        var textLeft = items.Count == 0 ? S(8) : items[^1].Bounds.Right + S(12);
        TextRenderer.DrawText(g, _engine.PagerText, Font, new Rectangle(textLeft, f.PagerTop, Math.Max(0, Width - textLeft - S(10)), f.PagerH), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private void HandlePagerClick(Frame f, Point location)
    {
        var items = LayoutPager(f);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.Bounds.Contains(location) || !item.Enabled) continue;

            switch (item.Action)
            {
                case PagerAction.First: _engine.FirstPage(); break;
                case PagerAction.Previous: _engine.PreviousPage(); break;
                case PagerAction.Page: _engine.GoToPage(item.Arg); break;
                case PagerAction.Next: _engine.NextPage(); break;
                case PagerAction.Last: _engine.LastPage(); break;
                case PagerAction.LoadMore: _ = LoadMoreSafeAsync(); break;
                case PagerAction.PageSize: ShowPageSizeMenu(item.Bounds); break;
            }
            return;
        }
    }

    private void ShowPageSizeMenu(Rectangle anchor)
    {
        var menu = new ContextMenuStrip();
        foreach (var size in _engine.PageSizeOptions)
        {
            var captured = size;
            menu.Items.Add(new ToolStripMenuItem($"{size} rows", null, (_, _) => _engine.SetPageSize(captured)) { Checked = size == _engine.PageSize });
        }
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(this, new Point(anchor.X, anchor.Y - menu.PreferredSize.Height));
    }

    private void MaybeLoadMoreOnScroll()
    {
        if (_engine.PagingMode != GridPagingMode.Infinite || !_engine.HasMoreRows || _loadingMore) return;
        var f = ComputeFrame();
        if (_firstRow + f.FullRows >= _engine.TotalItemsCount - 2) _ = LoadMoreSafeAsync();
    }

    private async Task LoadMoreSafeAsync()
    {
        if (_loadingMore) return;
        _loadingMore = true;
        try
        {
            await _engine.LoadMoreAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _loadingMore = false;
            if (!IsDisposed) RefreshLayout();
        }
    }

    // ------------------------------------------------------------------
    // Row drag and drop
    // ------------------------------------------------------------------

    /// <summary>Lets selected rows be dragged out of the grid (reorder inside, move or Ctrl+copy to another grid).</summary>
    public bool AllowRowDrag { get; set; }

    /// <summary>Accepts rows dropped from this or another grid of the same item type.</summary>
    public bool AllowRowDrop { get => AllowDrop; set => AllowDrop = value; }

    private void BeginRowDrag()
    {
        var items = _engine.SelectedItems.ToList();
        if (items.Count == 0) return;

        var data = new DataObject();
        data.SetData(DragFormat, new RowDragPayload(this, items));
        data.SetText(string.Join(Environment.NewLine, items.Select(i => i?.ToString() ?? string.Empty)));
        DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
        _dropRow = -1;
        Invalidate();
    }

    private int DropRowAt(Point clientPoint)
    {
        var f = ComputeFrame();
        var raw = _firstRow + (int)Math.Round((clientPoint.Y - f.BodyTop) / (double)f.RowH);
        return Math.Clamp(raw, 0, _engine.TotalItemsCount);
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        base.OnDragEnter(drgevent);
        UpdateDragEffect(drgevent);
    }

    protected override void OnDragOver(DragEventArgs drgevent)
    {
        base.OnDragOver(drgevent);
        UpdateDragEffect(drgevent);
        if (drgevent.Effect == DragDropEffects.None) return;

        var point = PointToClient(new Point(drgevent.X, drgevent.Y));
        var f = ComputeFrame();
        if (point.Y < f.BodyTop + S(8)) ScrollBy(-1);
        else if (point.Y > f.BodyBottom - S(8)) ScrollBy(1);

        var row = DropRowAt(point);
        if (row != _dropRow)
        {
            _dropRow = row;
            Invalidate();
        }
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _dropRow = -1;
        Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        base.OnDragDrop(drgevent);
        var payload = drgevent.Data?.GetData(DragFormat) as RowDragPayload;
        var insertion = _dropRow >= 0 ? _dropRow : DropRowAt(PointToClient(new Point(drgevent.X, drgevent.Y)));
        _dropRow = -1;
        Invalidate();
        if (payload == null) return;

        TItem? before = default;
        if (insertion < _engine.TotalItemsCount && _engine.TryGetRowAt(insertion, out var row) && row.Kind == GridRowKind.Data) before = row.Item;

        var copy = (drgevent.KeyState & 8) == 8;
        if (ReferenceEquals(payload.Source, this))
        {
            if (_engine.MoveRows(payload.Items, before)) AccessibilityAnnounce($"Moved {payload.Items.Count} rows");
            return;
        }

        int? index = null;
        if (before != null && _engine.DataSource is IList<TItem> list)
        {
            var at = list.IndexOf(before);
            if (at >= 0) index = at;
        }

        var moved = payload.Source._engine.TransferRows(payload.Items, _engine, index, copy);
        AccessibilityAnnounce($"{(copy ? "Copied" : "Moved")} {moved} rows");
    }

    private void UpdateDragEffect(DragEventArgs e)
    {
        if (!AllowDrop || e.Data?.GetDataPresent(DragFormat) != true)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        var copy = (e.KeyState & 8) == 8;
        e.Effect = copy ? DragDropEffects.Copy : DragDropEffects.Move;
    }

    private void DrawRowDropIndicator(Graphics g, Frame f, Colors p)
    {
        if (_dropRow < 0) return;

        var y = f.BodyTop + (_dropRow - _firstRow) * f.RowH;
        if (y < f.BodyTop || y > f.BodyBottom) return;
        using var pen = new Pen(p.FocusRing, Math.Max(2, S(2)));
        g.DrawLine(pen, 0, y, f.BodyWidth, y);
        using var brush = new SolidBrush(p.FocusRing);
        g.FillPolygon(brush, new[] { new Point(0, y - S(4)), new Point(S(6), y), new Point(0, y + S(4)) });
    }

    // ------------------------------------------------------------------
    // Item change tracking (real-time updates)
    // ------------------------------------------------------------------

    /// <summary>Repaints and re-queries when items of the visible window raise PropertyChanged.</summary>
    public bool ObserveItemChanges { get; set; } = true;

    private void SyncItemObservers()
    {
        if (!ObserveItemChanges)
        {
            UnsubscribeAllItems();
            return;
        }

        var current = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var item in _engine.RenderedItems.Take(2000))
        {
            if (item is INotifyPropertyChanged notify) current.Add(notify);
        }

        foreach (var stale in _observed.Where(o => !current.Contains(o)).ToList())
        {
            ((INotifyPropertyChanged)stale).PropertyChanged -= OnObservedItemChanged;
            _observed.Remove(stale);
        }

        foreach (var fresh in current)
        {
            if (_observed.Add(fresh)) ((INotifyPropertyChanged)fresh).PropertyChanged += OnObservedItemChanged;
        }
    }

    private void UnsubscribeAllItems()
    {
        foreach (var item in _observed) ((INotifyPropertyChanged)item).PropertyChanged -= OnObservedItemChanged;
        _observed.Clear();
    }

    private void OnObservedItemChanged(object? sender, PropertyChangedEventArgs e) => Post(() =>
    {
        var request = _engine.CurrentRequest;
        var affectsQuery = request.Sorts.Count > 0 || request.Groups.Count > 0 || request.Filters.Count > 0 ||
            !string.IsNullOrEmpty(_engine.SearchPanelText) || _engine.Columns.Any(c => c.FooterSummaries.Count > 0 || c.IsCalculated);
        if (affectsQuery) _engine.NotifyDataChanged();
        else Invalidate();
    });

    // ------------------------------------------------------------------
    // Context menu, export
    // ------------------------------------------------------------------

    private ToolStripMenuItem? _copyItem, _undoItem, _redoItem, _saveItem, _discardItem, _deleteItem, _expandGroups, _collapseGroups, _expandDetails, _collapseDetails, _editDialogItem;

    private void BuildBodyContextMenu()
    {
        _copyItem = new ToolStripMenuItem("Copy", null, (_, _) => CopySelectionToClipboard()) { ShortcutKeyDisplayString = "Ctrl+C" };
        var selectAll = new ToolStripMenuItem("Select All", null, (_, _) => _engine.Selection.SelectAll()) { ShortcutKeyDisplayString = "Ctrl+A" };
        _editDialogItem = new ToolStripMenuItem("Edit Row...", null, (_, _) =>
        {
            if (_engine.TryGetRowAt(_contextRow, out var row) && row.Item != null) ShowRowEditDialog(row.Item);
        });
        _undoItem = new ToolStripMenuItem("Undo", null, (_, _) => _engine.UndoLastEdit()) { ShortcutKeyDisplayString = "Ctrl+Z" };
        _redoItem = new ToolStripMenuItem("Redo", null, (_, _) => _engine.RedoLastEdit()) { ShortcutKeyDisplayString = "Ctrl+Y" };
        _saveItem = new ToolStripMenuItem("Save Changes", null, (_, _) => _engine.CommitChanges()) { ShortcutKeyDisplayString = "Ctrl+S" };
        _discardItem = new ToolStripMenuItem("Discard Changes", null, (_, _) => _engine.RollbackChanges());
        _deleteItem = new ToolStripMenuItem("Delete Selected Rows", null, (_, _) => _engine.RemoveRows(_engine.SelectedItems.ToList())) { ShortcutKeyDisplayString = "Del" };
        _expandGroups = new ToolStripMenuItem("Expand All Groups", null, (_, _) => _engine.ExpandAllGroups());
        _collapseGroups = new ToolStripMenuItem("Collapse All Groups", null, (_, _) => _engine.CollapseAllGroups());
        _expandDetails = new ToolStripMenuItem("Expand All Details", null, (_, _) => _engine.ExpandAllDetails());
        _collapseDetails = new ToolStripMenuItem("Collapse All Details", null, (_, _) => _engine.CollapseAllDetails());

        var export = new ToolStripMenuItem("Export");
        foreach (var (title, id) in new[] { ("Excel Workbook (.xlsx)", "xlsx"), ("CSV (.csv)", "csv"), ("PDF (.pdf)", "pdf"), ("HTML (.html)", "html"), ("JSON (.json)", "json"), ("XML (.xml)", "xml") })
        {
            var format = id;
            export.DropDownItems.Add(new ToolStripMenuItem(title, null, (_, _) => _ = ExportWithDialogAsync(format)));
        }

        var print = new ToolStripMenuItem("Print Preview...", null, (_, _) => ShowPrintPreviewDialog());
        var setup = new ToolStripMenuItem("Page Setup...", null, (_, _) => ShowPageSetupDialog());
        var search = new ToolStripMenuItem("Find...", null, (_, _) => ShowSearchPanel = true) { ShortcutKeyDisplayString = "Ctrl+F" };

        _bodyContextMenu.Items.AddRange(new ToolStripItem[]
        {
            _copyItem, selectAll, search, _editDialogItem,
            new ToolStripSeparator(),
            _undoItem, _redoItem, _saveItem, _discardItem, _deleteItem,
            new ToolStripSeparator(),
            _expandGroups, _collapseGroups, _expandDetails, _collapseDetails,
            new ToolStripSeparator(),
            export, print, setup
        });
    }

    private void RefreshBodyMenuState()
    {
        var hasGroups = _engine.CurrentRequest.Groups.Count > 0;
        var hasDetails = _engine.EnableMasterDetail || _engine.Relations.Count > 0;
        _copyItem!.Enabled = _engine.Selection.HasSelection && _engine.ClipboardCopyMode != GridClipboardCopyMode.None;
        _editDialogItem!.Visible = _engine.EditMode is GridEditMode.PopupModal or GridEditMode.Form;
        _undoItem!.Enabled = _engine.CanUndo;
        _redoItem!.Enabled = _engine.CanRedo;
        _saveItem!.Visible = _discardItem!.Visible = _engine.IsBatchMode;
        _saveItem.Enabled = _discardItem.Enabled = _engine.HasPendingChanges;
        _deleteItem!.Visible = AllowRowDelete;
        _deleteItem.Enabled = _engine.SelectedItems.Count > 0;
        _expandGroups!.Visible = _collapseGroups!.Visible = hasGroups;
        _expandDetails!.Visible = _collapseDetails!.Visible = hasDetails && !_engine.IsTreeMode;
    }

    /// <summary>Exports every row matching the current query (not only the loaded window) to a file; the format comes from the extension.</summary>
    public Task<ExportResult> ExportToFileAsync(string path, GridExportOptions? gridOptions = null, ExportOptions? options = null, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        gridOptions ??= new GridExportOptions { Title = _engine.AriaLabel };
        return GridExporter.ExportToFileAsync(_engine, path, gridOptions, options, progress, null, cancellationToken);
    }

    /// <summary>Exports to a stream in the given provider format (csv, json, xml, html, xlsx, excelxml, pdf).</summary>
    public Task<ExportResult> ExportAsync(string formatId, Stream output, GridExportOptions? gridOptions = null, ExportOptions? options = null, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default) =>
        GridExporter.ExportAsync(_engine, formatId, output, gridOptions ?? new GridExportOptions { Title = _engine.AriaLabel }, options, progress, null, cancellationToken);

    public Task ExportToCsvAsync(string filePath) => ExportToFileAsync(EnsureExtension(filePath, ".csv"));
    public Task ExportToJsonAsync(string filePath) => ExportToFileAsync(EnsureExtension(filePath, ".json"));
    public Task ExportToXmlAsync(string filePath) => ExportToFileAsync(EnsureExtension(filePath, ".xml"));
    public Task ExportToPdfAsync(string filePath) => ExportToFileAsync(EnsureExtension(filePath, ".pdf"));
    public Task ExportToHtmlAsync(string filePath) => ExportToFileAsync(EnsureExtension(filePath, ".html"));

    /// <summary>Writes an Excel workbook (.xlsx); any other extension gets SpreadsheetML 2003 XML.</summary>
    public async Task ExportToExcelAsync(string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await ExportToFileAsync(filePath);
            return;
        }

        await using var stream = File.Create(filePath);
        await ExportAsync("excelxml", stream);
    }

    private static string EnsureExtension(string path, string extension) =>
        string.IsNullOrEmpty(Path.GetExtension(path)) ? path + extension : path;

    /// <summary>Asks for a file name, then exports with a progress dialog that can cancel the export.</summary>
    public async Task ExportWithDialogAsync(string formatId)
    {
        var (extension, filter) = formatId switch
        {
            "xlsx" => (".xlsx", "Excel Workbook (*.xlsx)|*.xlsx"),
            "csv" => (".csv", "CSV (*.csv)|*.csv"),
            "pdf" => (".pdf", "PDF (*.pdf)|*.pdf"),
            "html" => (".html", "HTML (*.html)|*.html"),
            "json" => (".json", "JSON (*.json)|*.json"),
            _ => (".xml", "XML (*.xml)|*.xml")
        };

        using var dialog = new SaveFileDialog { Filter = filter, DefaultExt = extension.TrimStart('.'), FileName = "grid-export", OverwritePrompt = true };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        using var cts = new CancellationTokenSource();
        using var progressForm = new Form
        {
            Text = "Exporting",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ControlBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(S(360), S(110)),
            Font = Font
        };
        var bar = new ProgressBar { Left = S(16), Top = S(20), Width = S(328), Height = S(20), Style = ProgressBarStyle.Marquee, AccessibleName = "Export progress" };
        var cancel = new Button { Text = "Cancel", Left = S(264), Top = S(60), Width = S(80) };
        cancel.Click += (_, _) => cts.Cancel();
        progressForm.Controls.AddRange(new Control[] { bar, cancel });
        progressForm.Show(FindForm());

        var progress = new Progress<ExportProgress>(update =>
        {
            if (update.Fraction is { } fraction)
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = Math.Clamp((int)(fraction * 100), 0, 100);
            }
        });

        try
        {
            var result = await ExportToFileAsync(dialog.FileName, null, null, progress, cts.Token);
            AccessibilityAnnounce($"Exported {result.Rows:N0} rows");
        }
        catch (OperationCanceledException)
        {
            AccessibilityAnnounce("Export cancelled");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(FindForm(), ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            progressForm.Close();
        }
    }

    // ------------------------------------------------------------------
    // Printing
    // ------------------------------------------------------------------

    /// <summary>Page setup, header/footer text and style used by print and print preview.</summary>
    public PrintTemplate PrintTemplate { get => _printTemplate; set => _printTemplate = value ?? PrintTemplate.Standard(); }

    /// <summary>Builds the page plan (what goes on which page) for the current query without opening a dialog.</summary>
    public async Task<PrintPlan> CreatePrintPlanAsync(PrintTemplate? template = null, CancellationToken cancellationToken = default)
    {
        var effective = (template ?? _printTemplate).Clone();
        var document = await GridExporter.CreateDocumentAsync(_engine, new GridExportOptions { Title = effective.Title ?? _engine.AriaLabel, MaxRows = effective.Settings.MaxRows }, cancellationToken);
        return PrintPaginator.Paginate(document, effective);
    }

    /// <summary>Opens a print preview of every row matching the current filters and sort order (up to the template's row limit).</summary>
    public async Task ShowPrintPreviewDialogAsync(PrintTemplate? template = null)
    {
        var plan = await CreatePrintPlanAsync(template);
        using var document = CreatePrintDocument(plan);
        using var preview = new PrintPreviewDialog { Document = document, Width = 1000, Height = 760, StartPosition = FormStartPosition.CenterParent };
        preview.ShowDialog(FindForm());
    }

    public void ShowPrintPreviewDialog() => _ = RunPrintSafeAsync(() => ShowPrintPreviewDialogAsync());

    /// <summary>Prints after showing the print dialog.</summary>
    public async Task PrintAsync(PrintTemplate? template = null)
    {
        var plan = await CreatePrintPlanAsync(template);
        using var document = CreatePrintDocument(plan);
        using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false, AllowSelection = false };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) document.Print();
    }

    private async Task RunPrintSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidPrinterException or Win32Exception)
        {
            MessageBox.Show(FindForm(), ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Shows the page setup dialog and stores paper size, orientation and margins in <see cref="PrintTemplate"/>.</summary>
    public bool ShowPageSetupDialog()
    {
        var settings = _printTemplate.Settings;
        var (width, height) = PrintPaperSizes.Get(settings.PaperSize);
        using var dialog = new PageSetupDialog
        {
            PageSettings = new PageSettings
            {
                Landscape = settings.IsLandscape,
                PaperSize = new PaperSize(settings.PaperSize, (int)Math.Round(width / 0.72), (int)Math.Round(height / 0.72)),
                Margins = new Margins((int)Math.Round(settings.MarginLeft / 0.72), (int)Math.Round(settings.MarginRight / 0.72), (int)Math.Round(settings.MarginTop / 0.72), (int)Math.Round(settings.MarginBottom / 0.72))
            },
            PrinterSettings = new PrinterSettings(),
            AllowPrinter = false
        };

        try
        {
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return false;
        }
        catch (InvalidPrinterException ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "Page Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var page = dialog.PageSettings;
        settings.IsLandscape = page.Landscape;
        settings.MarginLeft = page.Margins.Left * 0.72;
        settings.MarginRight = page.Margins.Right * 0.72;
        settings.MarginTop = page.Margins.Top * 0.72;
        settings.MarginBottom = page.Margins.Bottom * 0.72;

        var selected = (Width: page.PaperSize.Width * 0.72, Height: page.PaperSize.Height * 0.72);
        var best = PrintPaperSizes.Names
            .Select(name => (Name: name, Size: PrintPaperSizes.Get(name)))
            .OrderBy(candidate => Math.Abs(candidate.Size.Width - Math.Min(selected.Width, selected.Height)) + Math.Abs(candidate.Size.Height - Math.Max(selected.Width, selected.Height)))
            .First();
        settings.PaperSize = best.Name;
        return true;
    }

    private static PrintDocument CreatePrintDocument(PrintPlan plan)
    {
        var settings = plan.Template.Settings;
        var (width, height) = PrintPaperSizes.Get(settings.PaperSize);
        var document = new PrintDocument { DocumentName = plan.Template.Title ?? "EnterpriseUI DataGrid" };
        document.DefaultPageSettings.PaperSize = new PaperSize(settings.PaperSize, (int)Math.Round(width / 0.72), (int)Math.Round(height / 0.72));
        document.DefaultPageSettings.Landscape = settings.IsLandscape;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        var index = 0;
        document.BeginPrint += (_, _) => index = 0;
        document.PrintPage += (_, args) =>
        {
            if (args.Graphics == null || plan.Pages.Count == 0)
            {
                args.HasMorePages = false;
                return;
            }

            RenderPrintPage(args.Graphics, args.PageSettings, plan, plan.Pages[index]);
            index++;
            args.HasMorePages = index < plan.Pages.Count;
        };
        return document;
    }

    private static Color PrintColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return ColorTranslator.FromHtml(hex); }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return fallback; }
    }

    /// <summary>Draws one planned page. Geometry is in points from the paper corner; the plan decides what goes where.</summary>
    private static void RenderPrintPage(Graphics g, PageSettings pageSettings, PrintPlan plan, PrintPage page)
    {
        var template = plan.Template;
        var settings = template.Settings;
        var style = template.Style;
        var state = g.Save();
        g.TranslateTransform(-pageSettings.HardMarginX, -pageSettings.HardMarginY);
        g.ScaleTransform(100f / 72f, 100f / 72f);
        g.SmoothingMode = SmoothingMode.HighQuality;

        var scale = page.Scale;
        var fontSize = (float)(settings.FontSize * scale);
        using var font = new Font(style.FontFamily, fontSize, FontStyle.Regular, GraphicsUnit.World);
        using var bold = new Font(style.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.World);
        using var bandFont = new Font(style.FontFamily, (float)settings.FontSize, FontStyle.Regular, GraphicsUnit.World);
        using var textBrush = new SolidBrush(PrintColor(style.TextHex, Color.Black));
        using var linePen = new Pen(PrintColor(style.GridLineHex, Color.Gray), 0.5f);
        using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center };

        var left = (float)settings.MarginLeft;
        var contentWidth = (float)settings.PrintableWidth;
        var headerBand = (float)PrintPaginator.BandHeight(template, true);
        var footerBand = (float)PrintPaginator.BandHeight(template, false);

        void Band(float y, float h, string? l, string? c, string? r)
        {
            var area = new RectangleF(left, y, contentWidth, h);
            using var side = new StringFormat(StringFormatFlags.NoWrap) { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            side.Alignment = StringAlignment.Near;
            g.DrawString(template.Resolve(l, page.Number, plan.PageCount), bandFont, textBrush, area, side);
            side.Alignment = StringAlignment.Center;
            g.DrawString(template.Resolve(c, page.Number, plan.PageCount), bandFont, textBrush, area, side);
            side.Alignment = StringAlignment.Far;
            g.DrawString(template.Resolve(r, page.Number, plan.PageCount), bandFont, textBrush, area, side);
        }

        if (headerBand > 0) Band((float)settings.MarginTop, headerBand, template.HeaderLeft, template.HeaderCenter, template.HeaderRight);
        if (footerBand > 0) Band((float)(settings.PageHeight - settings.MarginBottom) - footerBand, footerBand, template.FooterLeft, template.FooterCenter, template.FooterRight);

        var top = (float)settings.MarginTop + headerBand;
        var widths = PrintPaginator.ColumnWidths(page.Sheet, scale).Skip(page.FirstColumn).Take(page.ColumnCount).Select(w => (float)w).ToList();
        var rowHeight = (float)PrintPaginator.RowHeight(settings, scale);
        var tableWidth = widths.Sum();
        var columns = page.Sheet.Columns.Skip(page.FirstColumn).Take(page.ColumnCount).ToList();

        void Cells(float y, IReadOnlyList<string> texts, IReadOnlyList<ExportAlignment> alignments, Font cellFont, Color? back, Color? fore)
        {
            var x = left;
            using var foreBrush = new SolidBrush(fore ?? PrintColor(style.TextHex, Color.Black));
            for (var i = 0; i < widths.Count; i++)
            {
                var cell = new RectangleF(x, y, widths[i], rowHeight);
                if (back is { } color)
                {
                    using var backBrush = new SolidBrush(color);
                    g.FillRectangle(backBrush, cell);
                }
                if (settings.ShowGridLines) g.DrawRectangle(linePen, cell.X, cell.Y, cell.Width, cell.Height);
                using var cellFormat = new StringFormat(format)
                {
                    Alignment = alignments[i] switch { ExportAlignment.Right => StringAlignment.Far, ExportAlignment.Center => StringAlignment.Center, _ => StringAlignment.Near }
                };
                g.DrawString(texts[i], cellFont, foreBrush, new RectangleF(cell.X + 2, cell.Y, Math.Max(0, cell.Width - 4), cell.Height), cellFormat);
                x += widths[i];
            }
        }

        var alignments = columns.Select(c => c.Alignment).ToList();
        if (page.ShowHeader)
        {
            var levels = page.Sheet.Columns.Count == 0 ? 0 : page.Sheet.Columns.Max(c => c.Bands.Count) + 1;
            for (var level = 0; level < levels; level++)
            {
                var texts = columns.Select(c => level < levels - 1 ? (level < c.Bands.Count ? c.Bands[level] : string.Empty) : c.Title).ToList();
                var back = PrintColor(level < levels - 1 ? style.BandBackHex : style.HeaderBackHex, Color.LightGray);
                Cells(top + level * rowHeight, texts, alignments, bold, back, PrintColor(style.HeaderForeHex, Color.Black));
            }
        }

        var culture = CultureInfo.CurrentCulture;
        var dataIndex = 0;
        foreach (var placed in page.Rows)
        {
            var y = top + (float)placed.Y;
            var row = placed.Row;
            switch (row.Kind)
            {
                case ExportRowKind.GroupHeader:
                case ExportRowKind.GroupFooter:
                case ExportRowKind.DetailHeader:
                {
                    using var back = new SolidBrush(PrintColor(row.Kind == ExportRowKind.GroupHeader ? style.GroupBackHex : style.SummaryBackHex, Color.WhiteSmoke));
                    g.FillRectangle(back, left, y, tableWidth, rowHeight);
                    if (settings.ShowGridLines) g.DrawRectangle(linePen, left, y, tableWidth, rowHeight);
                    var indent = (float)(row.Level * 10 * scale);
                    g.DrawString(row.Text ?? string.Join("  ", row.Cells.Where(c => c != null).Select(c => Convert.ToString(c, culture))), bold, textBrush, new RectangleF(left + 2 + indent, y, Math.Max(0, tableWidth - 4 - indent), rowHeight), format);
                    break;
                }

                default:
                {
                    var texts = new List<string>(widths.Count);
                    for (var i = 0; i < widths.Count; i++)
                    {
                        var cellIndex = page.FirstColumn + i;
                        var value = cellIndex < row.Cells.Length ? row.Cells[cellIndex] : null;
                        texts.Add(ExportFormatting.FormatCell(value, columns[i], culture));
                    }

                    var isSummary = row.Kind == ExportRowKind.Summary;
                    Color? back = isSummary ? PrintColor(style.SummaryBackHex, Color.WhiteSmoke)
                        : row.Style?.BackColorHex != null ? PrintColor(row.Style.BackColorHex, Color.White)
                        : style.AlternateRowHex != null && dataIndex % 2 == 1 ? PrintColor(style.AlternateRowHex, Color.WhiteSmoke) : null;
                    Cells(y, texts, alignments, isSummary || row.Style?.Bold == true ? bold : font, back, row.Style?.ForeColorHex != null ? PrintColor(row.Style.ForeColorHex, Color.Black) : null);
                    if (row.Kind == ExportRowKind.Data) dataIndex++;
                    break;
                }
            }
        }

        g.Restore(state);
    }

    // ------------------------------------------------------------------
    // Accessibility
    // ------------------------------------------------------------------

    private void OnEngineAnnouncement(object? sender, GridAnnouncementEventArgs e) => Post(() => AccessibilityAnnounce(e.Message, e.Assertive));

    private void AccessibilityAnnounce(string message, bool assertive = false)
    {
        if (string.IsNullOrWhiteSpace(message) || !IsHandleCreated || IsDisposed) return;
        AccessibleDescription = message;
        try
        {
            AccessibilityObject.RaiseAutomationNotification(
                AutomationNotificationKind.Other,
                assertive ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
                message);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void NotifyFocusChanged()
    {
        if (!IsHandleCreated || IsDisposed) return;
        var row = _engine.Selection.FocusedRow - _firstRow;
        if (row >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, row + 1);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new GridAccessibleObject(this);

    private int VisibleRowCount() => EnumerateRows(ComputeFrame()).Count();

    private Rectangle RowScreenBounds(int displayRow)
    {
        var f = ComputeFrame();
        if (displayRow < 0) return RectangleToScreen(new Rectangle(0, f.HeaderTop, f.BodyWidth, f.HeaderH));
        foreach (var slot in EnumerateRows(f))
        {
            if (slot.Index == displayRow) return RectangleToScreen(slot.Bounds);
        }
        return Rectangle.Empty;
    }

    private Rectangle CellScreenBounds(int displayRow, int columnIndex)
    {
        var f = ComputeFrame();
        if (columnIndex < 0 || columnIndex >= f.Ordered.Count) return Rectangle.Empty;
        var slot = f.Layout.Slots.FirstOrDefault(s => ReferenceEquals(s.Column, f.Ordered[columnIndex]));
        if (slot == null) return Rectangle.Empty;
        var x = (int)slot.Left + f.Gutter;
        var w = (int)slot.Width;

        if (displayRow < 0) return RectangleToScreen(new Rectangle(x, f.HeaderTop, w, f.HeaderH));
        foreach (var row in EnumerateRows(f))
        {
            if (row.Index == displayRow) return RectangleToScreen(new Rectangle(x, row.Bounds.Y, w, row.Bounds.Height));
        }
        return Rectangle.Empty;
    }

    private sealed class GridAccessibleObject : ControlAccessibleObject
    {
        private readonly EnterpriseDataGrid<TItem> _grid;

        public GridAccessibleObject(EnterpriseDataGrid<TItem> grid) : base(grid) => _grid = grid;

        public override string? Name
        {
            get => GridAccessibility.DescribeGrid(_grid._engine);
            set => base.Name = value;
        }

        public override string? Description =>
            _grid._engine.AriaDescription ?? "Use arrow keys to move between cells, Enter to edit, Space to select, Alt plus Right or Left to expand or collapse, Control plus F to search.";

        public override AccessibleRole Role => AccessibleRole.Table;

        public override int GetChildCount() => 1 + _grid.VisibleRowCount();

        public override AccessibleObject? GetChild(int index)
        {
            if (index < 0 || index >= GetChildCount()) return null;
            return new RowAccessibleObject(_grid, this, index == 0 ? -1 : _grid._firstRow + index - 1);
        }

        public override AccessibleObject? GetFocused()
        {
            var row = _grid._engine.Selection.FocusedRow;
            if (row < 0) return null;
            var column = Math.Max(0, _grid._engine.Selection.FocusedColumn);
            return new CellAccessibleObject(_grid, new RowAccessibleObject(_grid, this, row), row, column);
        }

        public override AccessibleObject? HitTest(int x, int y)
        {
            var point = _grid.PointToClient(new Point(x, y));
            var f = _grid.ComputeFrame();
            if (_grid.HitBody(f, point, out var slot, out var column))
            {
                var row = new RowAccessibleObject(_grid, this, slot.Index);
                return column == null ? row : new CellAccessibleObject(_grid, row, slot.Index, ColumnIndexOf(f, column.Column));
            }

            return base.HitTest(x, y);
        }
    }

    private sealed class RowAccessibleObject : AccessibleObject
    {
        private readonly EnterpriseDataGrid<TItem> _grid;
        private readonly AccessibleObject _parent;
        private readonly int _row;

        public RowAccessibleObject(EnterpriseDataGrid<TItem> grid, AccessibleObject parent, int row)
        {
            _grid = grid;
            _parent = parent;
            _row = row;
        }

        public override AccessibleObject? Parent => _parent;
        public override AccessibleRole Role => AccessibleRole.Row;
        public override Rectangle Bounds => _grid.RowScreenBounds(_row);

        public override string? Name
        {
            get
            {
                if (_row < 0) return "Column headers";
                return _grid._engine.TryGetRowAt(_row, out var row) ? GridAccessibility.DescribeRow(_grid._engine, row) : $"Row {_row + 1}";
            }
            set { }
        }

        public override AccessibleStates State
        {
            get
            {
                var state = AccessibleStates.Focusable;
                if (_row < 0) return state;
                var selection = _grid._engine.Selection;
                state |= AccessibleStates.Selectable;
                if (selection.IsRowSelected(_row)) state |= AccessibleStates.Selected;
                if (selection.FocusedRow == _row && _grid.Focused) state |= AccessibleStates.Focused;
                if (_grid._engine.TryGetRowAt(_row, out var row) && row.IsExpandable) state |= row.IsExpanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                return state;
            }
        }

        public override string? DefaultAction => _row >= 0 && _grid._engine.TryGetRowAt(_row, out var row) && row.IsExpandable ? (row.IsExpanded ? "Collapse" : "Expand") : null;

        public override void DoDefaultAction()
        {
            if (_row >= 0 && _grid._engine.TryGetRowAt(_row, out var row) && row.IsExpandable) _grid._engine.ToggleRowExpansion(_row);
        }

        public override void Select(AccessibleSelection flags)
        {
            if (_row < 0) return;
            if (flags.HasFlag(AccessibleSelection.TakeSelection) || flags.HasFlag(AccessibleSelection.AddSelection))
            {
                _grid._engine.Selection.Click(_row, Math.Max(0, _grid._engine.Selection.FocusedColumn), ctrl: flags.HasFlag(AccessibleSelection.AddSelection));
            }
            if (flags.HasFlag(AccessibleSelection.TakeFocus)) _grid.Focus();
        }

        public override int GetChildCount() => _grid.ComputeFrame().Ordered.Count;

        public override AccessibleObject? GetChild(int index) =>
            index < 0 || index >= GetChildCount() ? null : new CellAccessibleObject(_grid, this, _row, index);

        public override AccessibleObject? Navigate(AccessibleNavigation navdir) => navdir switch
        {
            AccessibleNavigation.FirstChild => GetChild(0),
            AccessibleNavigation.LastChild => GetChild(GetChildCount() - 1),
            AccessibleNavigation.Next => _parent.GetChild(Math.Max(0, _row - _grid._firstRow + 2)),
            AccessibleNavigation.Previous => _parent.GetChild(Math.Max(0, _row - _grid._firstRow)),
            _ => base.Navigate(navdir)
        };
    }

    private sealed class CellAccessibleObject : AccessibleObject
    {
        private readonly EnterpriseDataGrid<TItem> _grid;
        private readonly AccessibleObject _parent;
        private readonly int _row;
        private readonly int _column;

        public CellAccessibleObject(EnterpriseDataGrid<TItem> grid, AccessibleObject parent, int row, int column)
        {
            _grid = grid;
            _parent = parent;
            _row = row;
            _column = column;
        }

        private GridColumn? Column
        {
            get
            {
                var ordered = _grid.ComputeFrame().Ordered;
                return _column >= 0 && _column < ordered.Count ? ordered[_column] : null;
            }
        }

        public override AccessibleObject? Parent => _parent;
        public override AccessibleRole Role => _row < 0 ? AccessibleRole.ColumnHeader : AccessibleRole.Cell;
        public override Rectangle Bounds => _grid.CellScreenBounds(_row, _column);

        public override string? Name
        {
            get
            {
                var column = Column;
                if (column == null) return null;
                if (_row < 0) return GridAccessibility.DescribeColumnHeader(_grid._engine, column);
                var count = _grid.ComputeFrame().Ordered.Count;
                return _grid._engine.TryGetRowAt(_row, out var row) ? GridAccessibility.DescribeCell(_grid._engine, row, column, _column, count) : null;
            }
            set { }
        }

        public override string? Value
        {
            get
            {
                var column = Column;
                return column != null && _row >= 0 && _grid._engine.TryGetRowAt(_row, out var row) ? _grid.CellText(row, column) : null;
            }
            set { }
        }

        public override AccessibleStates State
        {
            get
            {
                var state = AccessibleStates.Focusable | AccessibleStates.Selectable;
                if (_row < 0) return state;
                var selection = _grid._engine.Selection;
                if (selection.IsCellSelected(_row, _column)) state |= AccessibleStates.Selected;
                if (selection.FocusedRow == _row && selection.FocusedColumn == _column && _grid.Focused) state |= AccessibleStates.Focused;
                var column = Column;
                if (column == null || !_grid.CanEdit(column)) state |= AccessibleStates.ReadOnly;
                return state;
            }
        }

        public override string? DefaultAction => Column != null && _row >= 0 && _grid.CanEdit(Column) ? "Edit" : null;

        public override void DoDefaultAction()
        {
            if (_row >= 0) _grid.BeginEdit(_row, _column);
        }

        public override void Select(AccessibleSelection flags)
        {
            if (_row < 0) return;
            if (flags.HasFlag(AccessibleSelection.TakeSelection) || flags.HasFlag(AccessibleSelection.AddSelection))
            {
                _grid._engine.Selection.Click(_row, _column, ctrl: flags.HasFlag(AccessibleSelection.AddSelection));
            }
            if (flags.HasFlag(AccessibleSelection.TakeFocus)) _grid.Focus();
        }
    }
}
