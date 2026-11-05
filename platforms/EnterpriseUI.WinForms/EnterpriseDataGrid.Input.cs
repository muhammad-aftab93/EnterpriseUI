using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Arguments for clicks on cells; used by command and hyperlink columns.</summary>
public sealed class GridCellClickEventArgs<T> : EventArgs
{
    public GridCellClickEventArgs(T item, GridColumn column, int rowIndex)
    {
        Item = item;
        Column = column;
        RowIndex = rowIndex;
    }

    public T Item { get; }
    public GridColumn Column { get; }
    public int RowIndex { get; }
}

public partial class EnterpriseDataGrid<TItem>
{
    private (int Row, int Column)? _pendingClick;
    private string _typeAhead = string.Empty;
    private DateTime _typeAheadTime = DateTime.MinValue;
    private int _contextRow = -1;

    /// <summary>Raised when a cell of a command or hyperlink column is clicked.</summary>
    public event EventHandler<GridCellClickEventArgs<TItem>>? CellClick;

    /// <summary>Raised when a data row is double-clicked, after editing was considered.</summary>
    public event EventHandler<GridCellClickEventArgs<TItem>>? RowDoubleClick;

    /// <summary>Allows the Delete key to remove the selected rows from an in-memory list source.</summary>
    public bool AllowRowDelete { get; set; }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    private static bool InHeader(Frame f, int y) => y >= f.HeaderTop && y < f.HeaderTop + f.HeaderH;

    private Rectangle FilterIconRect(ColumnSlot slot, Frame f)
    {
        var size = S(12);
        var right = (int)slot.Right + f.Gutter - S(8);
        return new Rectangle(right - size, f.HeaderTop + (f.HeaderH - size) / 2, size, size);
    }

    private Rectangle HeaderCheckRect(Frame f)
    {
        var size = S(14);
        return new Rectangle(f.ExpanderW + (f.CheckW - size) / 2, f.HeaderTop + (f.HeaderH - size) / 2, size, size);
    }

    private Rectangle RowCheckRect(Frame f, Rectangle rowBounds)
    {
        var size = S(14);
        return new Rectangle(f.ExpanderW + (f.CheckW - size) / 2, rowBounds.Y + (rowBounds.Height - size) / 2, size, size);
    }

    private CheckState HeaderCheckState()
    {
        var count = _engine.Selection.SelectedRows.Count;
        if (count == 0) return CheckState.Unchecked;
        return count >= _engine.TotalItemsCount ? CheckState.Checked : CheckState.Indeterminate;
    }

    private IEnumerable<(string Member, Rectangle Bounds, Rectangle Close)> GetGroupChips(Frame f)
    {
        var x = S(10);
        foreach (var group in _engine.CurrentRequest.Groups.ToList())
        {
            var text = group.Member;
            var width = TextRenderer.MeasureText(text, HeaderFont, Size.Empty, TextFormatFlags.NoPadding).Width + S(36);
            var bounds = new Rectangle(x, f.GroupTop + (f.GroupH - S(22)) / 2, width, S(22));
            var close = new Rectangle(bounds.Right - S(20), bounds.Y, S(20), bounds.Height);
            yield return (group.Member, bounds, close);
            x += width + S(8);
        }
    }

    private bool HitBody(Frame f, Point location, out RowSlot slot, out ColumnSlot? column)
    {
        slot = default;
        column = null;
        if (location.Y < f.BodyTop || location.Y >= f.BodyBottom || location.X >= f.BodyWidth) return false;

        foreach (var candidate in EnumerateRows(f))
        {
            if (!candidate.Bounds.Contains(location.X, location.Y)) continue;
            slot = candidate;
            var hit = f.Layout.HitTest(location.X - f.Gutter);
            column = hit >= 0 ? f.Layout.Slots[hit] : null;
            return true;
        }

        return false;
    }

    private void ScrollBy(int rows)
    {
        var f = ComputeFrame();
        _firstRow = Math.Clamp(_firstRow + rows, 0, Math.Max(0, _engine.TotalItemsCount - f.FullRows));
        UpdateScrollBars();
        Invalidate();
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
        var autoSize = new ToolStripMenuItem("Best Fit", null, (_, _) => AutoSizeColumn(_contextColumn));
        var autoSizeAll = new ToolStripMenuItem("Best Fit All Columns", null, (_, _) => BestFitAllColumns());

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
            autoSize, autoSizeAll, _pinLeftItem, _pinRightItem, _unpinItem, _hideItem, _chooserMenu,
            new ToolStripSeparator(),
            _groupItem, clearGroup,
            new ToolStripSeparator(),
            toggleBands, print
        });

        _headerContextMenu.Opening += (_, _) =>
        {
            var col = _contextColumn;
            asc.Enabled = desc.Enabled = col?.Sortable == true && _engine.EnableSorting;
            clearFilter.Enabled = col != null && HasFilter(col);
            _pinLeftItem.Checked = col?.PinPosition == ColumnPinPosition.Left;
            _pinRightItem.Checked = col?.PinPosition == ColumnPinPosition.Right;
            _unpinItem.Enabled = col != null && col.PinPosition != ColumnPinPosition.None;
            _hideItem.Enabled = col?.CanHide == true;
            _groupItem.Enabled = col?.Groupable == true && _engine.EnableGrouping;
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
    }

    private void PinContext(ColumnPinPosition position)
    {
        if (_contextColumn != null) _engine.PinColumn(_contextColumn.Field, position);
    }

    private bool HasFilter(GridColumn column) =>
        _engine.HasFilter(column.Field) || _engine.QuickFilters.ContainsKey(column.Field) || column.ExcelFilterValues.Count > 0;

    private void ClearColumnFilter(GridColumn? column)
    {
        if (column == null) return;
        _engine.ClearExcelFilter(column.Field);
        _engine.SetQuickFilter(column.Field, string.Empty);
    }

    // ------------------------------------------------------------------
    // Keyboard
    // ------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Enter or Keys.Escape or Keys.F2 => true,
        _ => base.IsInputKey(keyData)
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_editor != null) return;

        var selection = _engine.Selection;
        var total = _engine.TotalItemsCount;
        var page = ComputeFrame().FullRows;
        var column = Math.Max(0, selection.FocusedColumn);
        e.Handled = true;

        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.Z:
                    if (_engine.UndoLastEdit()) CellEdited?.Invoke(this, "Undo Edit Executed");
                    return;
                case Keys.Y:
                    if (_engine.RedoLastEdit()) CellEdited?.Invoke(this, "Redo Edit Executed");
                    return;
                case Keys.C:
                    CopySelectionToClipboard();
                    return;
                case Keys.A:
                    selection.SelectAll();
                    return;
                case Keys.F:
                    if (_engine.EnableSearchPanel) ShowSearchPanel = true;
                    return;
                case Keys.S when _engine.HasPendingChanges:
                    _engine.CommitChanges();
                    return;
                case Keys.PageDown:
                    _engine.NextPage();
                    return;
                case Keys.PageUp:
                    _engine.PreviousPage();
                    return;
                case Keys.Home:
                    selection.MoveTo(0, column, e.Shift);
                    return;
                case Keys.End:
                    selection.MoveTo(total - 1, column, e.Shift);
                    return;
            }
        }

        switch (e.KeyCode)
        {
            case Keys.Down: selection.Move(1, 0, e.Shift); break;
            case Keys.Up: selection.Move(-1, 0, e.Shift); break;
            case Keys.Left when e.Alt: SetFocusedExpansion(false); break;
            case Keys.Right when e.Alt: SetFocusedExpansion(true); break;
            case Keys.Left: selection.Move(0, -1, e.Shift); break;
            case Keys.Right: selection.Move(0, 1, e.Shift); break;
            case Keys.PageDown: selection.Move(page, 0, e.Shift); break;
            case Keys.PageUp: selection.Move(-page, 0, e.Shift); break;
            case Keys.Home: selection.MoveTo(Math.Max(0, selection.FocusedRow), 0, e.Shift); break;
            case Keys.End: selection.MoveTo(Math.Max(0, selection.FocusedRow), _engine.Columns.Count(c => c.Visible) - 1, e.Shift); break;
            case Keys.Space: ActivateFocusedCell(spaceKey: true); break;
            case Keys.Enter:
            case Keys.F2: ActivateFocusedCell(spaceKey: false); break;
            case Keys.F3: if (e.Shift) _engine.PreviousMatch(); else _engine.NextMatch(); break;
            case Keys.Escape:
                if (_searchPanel.Visible) ShowSearchPanel = false;
                break;
            case Keys.Delete when AllowRowDelete && _engine.SelectedItems.Count > 0:
                _engine.RemoveRows(_engine.SelectedItems.ToList());
                break;
            case Keys.Apps:
                ShowBodyContextMenuAtFocus();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || _editor != null || char.IsControl(e.KeyChar)) return;

        e.Handled = true;
        _ = TypeAheadAsync(e.KeyChar);
    }

    private async Task TypeAheadAsync(char ch)
    {
        var now = DateTime.UtcNow;
        _typeAhead = now - _typeAheadTime > TimeSpan.FromSeconds(1) ? ch.ToString() : _typeAhead + ch;
        _typeAheadTime = now;

        var f = ComputeFrame();
        var selection = _engine.Selection;
        var index = Math.Clamp(selection.FocusedColumn, 0, Math.Max(0, f.Ordered.Count - 1));
        if (f.Ordered.Count == 0) return;

        var prefix = _typeAhead;
        try
        {
            var start = Math.Max(0, selection.FocusedRow) + (prefix.Length == 1 ? 1 : 0);
            var found = await _engine.FindRowByPrefixAsync(f.Ordered[index].Field, prefix, start);
            if (found < 0 && start > 0) found = await _engine.FindRowByPrefixAsync(f.Ordered[index].Field, prefix, 0);
            if (found >= 0 && !IsDisposed) _engine.FocusRow(found, index);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SetFocusedExpansion(bool expand)
    {
        var row = _engine.Selection.FocusedRow;
        if (row < 0 || !_engine.TryGetRowAt(row, out var gridRow) || !gridRow.IsExpandable || gridRow.IsExpanded == expand) return;
        _engine.ToggleRowExpansion(row);
    }

    private void ActivateFocusedCell(bool spaceKey)
    {
        var selection = _engine.Selection;
        var rowIndex = selection.FocusedRow;
        if (rowIndex < 0 || !_engine.TryGetRowAt(rowIndex, out var row)) return;

        if (row.Kind != GridRowKind.Data)
        {
            if (row.IsExpandable) _engine.ToggleRowExpansion(rowIndex);
            return;
        }

        var f = ComputeFrame();
        var columnIndex = Math.Clamp(selection.FocusedColumn, 0, Math.Max(0, f.Ordered.Count - 1));
        var column = f.Ordered.Count == 0 ? null : f.Ordered[columnIndex];

        if (spaceKey)
        {
            if (column != null && ResolveType(column, CellValue(row, column)) == GridColumnType.Boolean && CanEdit(column))
            {
                ToggleBoolean(row, column);
            }
            else
            {
                selection.ToggleRow(rowIndex);
            }
            return;
        }

        if (_engine.EditMode is GridEditMode.PopupModal or GridEditMode.Form)
        {
            ShowRowEditDialog(row.Item!);
        }
        else if (column != null)
        {
            BeginEditAt(f, rowIndex, columnIndex);
        }
    }

    /// <summary>Copies the selected rows, cells or columns as tab-separated text, honoring <see cref="ClipboardCopyMode"/>.</summary>
    public void CopySelectionToClipboard()
    {
        var selection = _engine.Selection;
        if (!selection.HasSelection || _engine.ClipboardCopyMode == GridClipboardCopyMode.None) return;

        var columns = GridColumnLayout.OrderVisible(_engine.Columns);
        var cells = new SortedDictionary<int, SortedSet<int>>();
        foreach (var (row, column) in selection.EnumerateSelectedCells(100_000))
        {
            if (!cells.TryGetValue(row, out var set)) cells[row] = set = new SortedSet<int>();
            set.Add(column);
        }

        if (cells.Count == 0) return;

        var usedColumns = cells.Values.SelectMany(c => c).Distinct().OrderBy(c => c).ToList();
        var text = new StringBuilder();
        if (_engine.ClipboardCopyMode == GridClipboardCopyMode.CellsWithHeaders)
        {
            text.AppendLine(string.Join('\t', usedColumns.Select(c => c < columns.Count ? (string.IsNullOrEmpty(columns[c].Title) ? columns[c].Field : columns[c].Title) : string.Empty)));
        }

        foreach (var (rowIndex, set) in cells)
        {
            if (!_engine.TryGetRowAt(rowIndex, out var row) || row.Kind != GridRowKind.Data) continue;
            text.AppendLine(string.Join('\t', usedColumns.Select(c => set.Contains(c) && c < columns.Count ? CellText(row, columns[c]).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ') : string.Empty)));
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        CloseEditor(commit: true);

        var f = ComputeFrame();
        var x = e.X - f.Gutter;

        if (e.Button == MouseButtons.Left && f.PagerH > 0 && e.Y >= f.PagerTop)
        {
            HandlePagerClick(f, e.Location);
            return;
        }

        if (e.Button == MouseButtons.Left && InHeader(f, e.Y))
        {
            if (f.CheckW > 0 && Rectangle.Inflate(HeaderCheckRect(f), S(4), S(4)).Contains(e.Location))
            {
                if (HeaderCheckState() == CheckState.Checked) _engine.Selection.Clear(); else _engine.Selection.SelectAll();
                return;
            }

            var handle = f.Layout.HitTestResizeHandle(x, S(4));
            if (handle >= 0)
            {
                var slot = f.Layout.Slots[handle];
                if (!slot.Column.Resizable) return;
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
            return;
        }

        if (HitBody(f, e.Location, out var rowSlot, out var colSlot) && rowSlot.Row != null)
        {
            if (e.Button == MouseButtons.Right)
            {
                BodyRightClick(f, rowSlot, colSlot);
                return;
            }

            if (e.Button == MouseButtons.Left) BodyMouseDown(f, e, rowSlot, colSlot);
        }
    }

    private void BodyMouseDown(Frame f, MouseEventArgs e, RowSlot slot, ColumnSlot? colSlot)
    {
        var row = slot.Row!;
        var selection = _engine.Selection;

        if (e.X < f.Gutter)
        {
            if (e.X < f.ExpanderW && row.IsExpandable && !_engine.IsTreeMode)
            {
                _engine.ToggleRowExpansion(slot.Index);
            }
            else if (f.CheckW > 0 && row.Kind == GridRowKind.Data)
            {
                selection.ToggleRow(slot.Index);
            }
            return;
        }

        var indentBase = f.Gutter + (row.Level + 1) * S(16);
        switch (row.Kind)
        {
            case GridRowKind.GroupHeader:
                _engine.ToggleRowExpansion(slot.Index);
                return;
            case GridRowKind.DetailData:
                if (row.IsExpandable && e.X < indentBase + S(4)) _engine.ToggleRowExpansion(slot.Index);
                return;
            case GridRowKind.Data:
                break;
            default:
                return;
        }

        if (colSlot == null) return;
        var columnIndex = ColumnIndexOf(f, colSlot.Column);
        var cellRect = new Rectangle((int)colSlot.Left + f.Gutter, slot.Bounds.Y, (int)colSlot.Width, slot.Bounds.Height);

        if (_engine.IsTreeMode && row.IsExpandable && columnIndex == 0)
        {
            var glyph = new Rectangle(cellRect.X + S(8) + row.Level * S(16), cellRect.Y, S(18), cellRect.Height);
            if (glyph.Contains(e.Location))
            {
                _engine.ToggleRowExpansion(slot.Index);
                return;
            }
        }

        var type = ResolveType(colSlot.Column, CellValue(row, colSlot.Column));
        if (type == GridColumnType.Boolean && CanEdit(colSlot.Column))
        {
            var box = new Rectangle(cellRect.X + (cellRect.Width - S(14)) / 2, cellRect.Y + (cellRect.Height - S(14)) / 2, S(14), S(14));
            if (Rectangle.Inflate(box, S(4), S(4)).Contains(e.Location))
            {
                selection.Click(slot.Index, columnIndex);
                ToggleBoolean(row, colSlot.Column);
                return;
            }
        }

        if (type is GridColumnType.Command or GridColumnType.Hyperlink)
        {
            selection.SetFocus(slot.Index, columnIndex);
            CellClick?.Invoke(this, new GridCellClickEventArgs<TItem>(row.Item!, colSlot.Column, slot.Index));
            if (type == GridColumnType.Command) return;
        }

        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
        if (AllowRowDrag && selection.IsRowSelected(slot.Index) && !ctrl && !shift && selection.SelectedRows.Count > 1)
        {
            _drag = DragMode.RowPress;
            _dragStart = e.Location;
            _pendingClick = (slot.Index, columnIndex);
            return;
        }

        selection.Click(slot.Index, columnIndex, ctrl, shift);
        _dragStart = e.Location;
        _drag = AllowRowDrag ? DragMode.RowPress : DragMode.Select;
        _pendingClick = null;
    }

    private void BodyRightClick(Frame f, RowSlot slot, ColumnSlot? colSlot)
    {
        var selection = _engine.Selection;
        var columnIndex = colSlot == null ? Math.Max(0, selection.FocusedColumn) : ColumnIndexOf(f, colSlot.Column);
        var covered = IsCellSelectionMode ? selection.IsCellSelected(slot.Index, columnIndex) : selection.IsRowSelected(slot.Index);
        if (!covered) selection.Click(slot.Index, columnIndex);
        _contextRow = slot.Index;
        _bodyContextMenu.Show(this, PointToClient(Cursor.Position));
    }

    private void ShowBodyContextMenuAtFocus()
    {
        var f = ComputeFrame();
        foreach (var slot in EnumerateRows(f))
        {
            if (slot.Index != _engine.Selection.FocusedRow) continue;
            _contextRow = slot.Index;
            _bodyContextMenu.Show(this, new Point(f.Gutter + S(24), slot.Bounds.Bottom));
            return;
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

            case DragMode.Select:
                ExtendSelection(f, e.Location);
                return;

            case DragMode.RowPress:
                var size = SystemInformation.DragSize;
                if (e.Button == MouseButtons.Left && (Math.Abs(e.X - _dragStart.X) >= size.Width || Math.Abs(e.Y - _dragStart.Y) >= size.Height))
                {
                    _drag = DragMode.None;
                    _pendingClick = null;
                    BeginRowDrag();
                }
                return;
        }

        UpdateHover(f, e.Location);
    }

    private void ExtendSelection(Frame f, Point location)
    {
        if (location.Y < f.BodyTop) ScrollBy(-1);
        else if (location.Y >= f.BodyBottom) ScrollBy(1);

        f = ComputeFrame();
        var clampedY = Math.Clamp(location.Y, f.BodyTop, Math.Max(f.BodyTop, f.BodyBottom - 1));
        if (!HitBody(f, new Point(Math.Clamp(location.X, 0, Math.Max(0, f.BodyWidth - 1)), clampedY), out var slot, out var colSlot)) return;

        var columnIndex = colSlot == null
            ? (location.X < f.Gutter ? 0 : f.Ordered.Count - 1)
            : ColumnIndexOf(f, colSlot.Column);
        _engine.Selection.Drag(slot.Index, columnIndex);
    }

    private void UpdateHover(Frame f, Point location)
    {
        var x = location.X - f.Gutter;
        var onHeader = InHeader(f, location.Y);
        var cursor = Cursors.Default;
        var header = -1;

        if (onHeader)
        {
            var handle = f.Layout.HitTestResizeHandle(x, S(4));
            if (handle >= 0 && f.Layout.Slots[handle].Column.Resizable) cursor = Cursors.VSplit;
            header = f.Layout.HitTest(x);
        }

        if (!ReferenceEquals(Cursor, cursor)) Cursor = cursor;

        string? tip = null;
        if (header >= 0 && header < f.Layout.Slots.Count)
        {
            var col = f.Layout.Slots[header].Column;
            tip = !string.IsNullOrEmpty(col.Tooltip) ? col.Tooltip : $"{(string.IsNullOrEmpty(col.Title) ? col.Field : col.Title)}: click to sort, Ctrl+click for multi-sort, drag to reorder";
        }

        var row = -1;
        if (HitBody(f, location, out var slot, out var colSlot))
        {
            if (slot.Row?.Kind == GridRowKind.Data) row = slot.Index;

            if (slot.Row?.Kind == GridRowKind.Data && colSlot != null)
            {
                var errors = _engine.GetCellErrors(slot.Row.Item!, colSlot.Column.Field);
                if (errors.Count > 0) tip = string.Join(Environment.NewLine, errors.Select(error => error.Message));
            }
        }

        if (tip != _toolTipText)
        {
            _toolTipText = tip;
            _toolTip.SetToolTip(this, tip);
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
                if (f.GroupH > 0 && e.Y >= f.GroupTop && e.Y < f.GroupTop + f.GroupH)
                {
                    _dropIndex = -1;
                    Invalidate();
                    if (column.Groupable) _engine.GroupBy(column.Field);
                    return;
                }
                CompleteColumnDrop(f, column);
                return;

            case DragMode.HeaderPress when column != null && e.Button == MouseButtons.Left:
                HandleHeaderClick(f, column);
                return;

            case DragMode.Select:
                return;

            case DragMode.RowPress:
                if (_pendingClick is { } pending)
                {
                    _pendingClick = null;
                    _engine.Selection.Click(pending.Row, pending.Column);
                }
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

        if (f.GroupH > 0 && e.Y >= f.GroupTop && e.Y < f.GroupTop + f.GroupH)
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

        if (f.FilterH > 0 && e.Y >= f.FilterTop && e.Y < f.FilterTop + f.FilterH)
        {
            var hit = f.Layout.HitTest(x);
            if (hit >= 0 && f.Layout.Slots[hit].Column.Filterable)
            {
                BeginFilterRowEdit(f, f.Layout.Slots[hit]);
            }
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
        if (_engine.Selection.FocusedRow < 0 && _engine.TotalItemsCount > 0 && MouseButtons == MouseButtons.None)
        {
            _engine.Selection.SetFocus(0, 0);
        }
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _drag != DragMode.None && _drag != DragMode.RowPress)
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

        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        if (_engine.EnableFiltering && column.Filterable && Rectangle.Inflate(FilterIconRect(slot, f), S(4), S(4)).Contains(PointToClient(Cursor.Position)))
        {
            ShowFilterPopup(column, slot, f);
            return;
        }

        if (_engine.SelectionMode == GridSelectionMode.Column)
        {
            _engine.Selection.ClickColumn(ColumnIndexOf(f, column), ctrl, (ModifierKeys & Keys.Shift) == Keys.Shift);
            return;
        }

        if (!column.Sortable || !_engine.EnableSorting) return;
        _engine.CycleSort(column.Field, ctrl);
    }

    private void CompleteColumnDrop(Frame f, GridColumn column)
    {
        var drop = _dropIndex;
        _dropIndex = -1;
        Invalidate();
        if (drop < 0) return;

        var slots = f.Layout.Slots;
        var before = drop < slots.Count ? slots[drop].Column.Field : null;
        if (before != null && before.Equals(column.Field, StringComparison.OrdinalIgnoreCase)) return;

        var moved = _engine.MoveColumnBefore(column.Field, before);
        if (moved && before == null && slots.Count > 0)
        {
            _engine.PinColumn(column.Field, slots[^1].Column.PinPosition);
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

        if (!HitBody(f, e.Location, out var slot, out var colSlot) || slot.Row is not { Kind: GridRowKind.Data } row || colSlot == null || e.X < f.Gutter) return;

        if (_engine.EditMode is GridEditMode.PopupModal or GridEditMode.Form)
        {
            ShowRowEditDialog(row.Item!);
        }
        else
        {
            BeginEditAt(f, slot.Index, ColumnIndexOf(f, colSlot.Column));
        }

        RowDoubleClick?.Invoke(this, new GridCellClickEventArgs<TItem>(row.Item!, colSlot.Column, slot.Index));
    }

    // ------------------------------------------------------------------
    // Column sizing
    // ------------------------------------------------------------------

    private double MeasureLogical(string text, bool header) =>
        TextRenderer.MeasureText(text, header ? HeaderFont : Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width / DpiScale;

    /// <summary>Fits a column to its header and the widest sampled value.</summary>
    public void AutoSizeColumn(GridColumn? column)
    {
        if (column == null || !column.Resizable) return;
        _engine.BestFitColumn(column.Field, MeasureLogical);
    }

    /// <summary>Fits every visible column to its content.</summary>
    public void BestFitAllColumns() => _engine.BestFitAllColumns(MeasureLogical);

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
        var search = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search values", BackColor = p.Surface, ForeColor = p.Text, Font = Font, AccessibleName = "Search filter values" };
        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = truncated ? S(32) : 0,
            Text = truncated ? $"Showing the first {FilterListLimit:N0} values. Use search to find others." : string.Empty,
            ForeColor = p.Muted,
            Font = Font
        };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None, BackColor = p.Surface, ForeColor = p.Text, Font = Font, AccessibleName = "Filter values" };
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

        if (_filterDropDown.Visible) _filterDropDown.Close();
        while (_filterDropDown.Items.Count > 0)
        {
            var oldItem = _filterDropDown.Items[0];
            _filterDropDown.Items.RemoveAt(0);
            if (oldItem is ToolStripControlHost oldHost) oldHost.Control?.Dispose();
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
        };

        Populate();
        _filterDropDown.Show(this, new Point(Math.Max(0, (int)slot.Right + f.Gutter - panel.Width), f.HeaderTop + f.HeaderH));
        search.Focus();
    }
}
