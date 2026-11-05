using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseUI.Grids;

/// <summary>Inclusive rectangle of cells by display row and visible column index; normalized so From is not after To.</summary>
public readonly record struct GridCellRange(int RowFrom, int RowTo, int ColumnFrom, int ColumnTo)
{
    public static GridCellRange Between(int rowA, int columnA, int rowB, int columnB) =>
        new(Math.Min(rowA, rowB), Math.Max(rowA, rowB), Math.Min(columnA, columnB), Math.Max(columnA, columnB));

    public bool Contains(int row, int column) => row >= RowFrom && row <= RowTo && column >= ColumnFrom && column <= ColumnTo;
    public long CellCount => (long)(RowTo - RowFrom + 1) * (ColumnTo - ColumnFrom + 1);
}

/// <summary>
/// Selection and keyboard focus over display rows and visible columns. Implements the click, shift/ctrl, drag and arrow-key
/// gestures for single/multiple rows, checkbox rows, single cells, cell ranges and whole columns.
/// </summary>
public sealed class GridSelectionModel
{
    private readonly HashSet<int> _rows = new();
    private readonly HashSet<int> _columns = new();
    private readonly List<GridCellRange> _ranges = new();
    private GridSelectionMode _mode = GridSelectionMode.SingleRow;

    public event EventHandler? Changed;
    public event EventHandler? FocusChanged;

    public GridSelectionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            ClearSelectionCore();
            RaiseChanged();
        }
    }

    public int RowCount { get; private set; }
    public int ColumnCount { get; private set; }
    public int FocusedRow { get; private set; } = -1;
    public int FocusedColumn { get; private set; } = -1;
    public int AnchorRow { get; private set; } = -1;
    public int AnchorColumn { get; private set; } = -1;

    public IReadOnlyCollection<int> SelectedRows => _rows;
    public IReadOnlyCollection<int> SelectedColumns => _columns;
    public IReadOnlyList<GridCellRange> CellRanges => _ranges;

    public bool HasSelection => _rows.Count > 0 || _columns.Count > 0 || _ranges.Count > 0;

    /// <summary>Tells the model the current extent; focus beyond it is pulled back and selected rows beyond it are dropped.</summary>
    public void SetBounds(int rowCount, int columnCount)
    {
        RowCount = Math.Max(0, rowCount);
        ColumnCount = Math.Max(0, columnCount);

        var changed = _rows.RemoveWhere(r => r >= RowCount) > 0;
        changed |= _columns.RemoveWhere(c => c >= ColumnCount) > 0;
        if (changed) RaiseChanged();

        var row = FocusedRow >= RowCount ? RowCount - 1 : FocusedRow;
        var column = FocusedColumn >= ColumnCount ? ColumnCount - 1 : FocusedColumn;
        if (row != FocusedRow || column != FocusedColumn)
        {
            FocusedRow = row;
            FocusedColumn = column;
            FocusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsRowSelected(int row) => _rows.Contains(row);

    public bool IsColumnSelected(int column) => _columns.Contains(column);

    public bool IsCellSelected(int row, int column)
    {
        if (_rows.Contains(row) || _columns.Contains(column)) return true;
        foreach (var range in _ranges)
        {
            if (range.Contains(row, column)) return true;
        }
        return false;
    }

    public long SelectedCellCount
    {
        get
        {
            if (_columns.Count == 0 && _ranges.Count == 0) return (long)_rows.Count * Math.Max(1, ColumnCount);
            var cells = _columns.Count * (long)RowCount;
            cells += _ranges.Sum(r => r.CellCount);
            return cells;
        }
    }

    /// <summary>Enumerates selected cells (row, column), stopping after <paramref name="limit"/> to keep huge selections cheap.</summary>
    public IEnumerable<(int Row, int Column)> EnumerateSelectedCells(int limit = 1_000_000)
    {
        var seen = new HashSet<(int, int)>();
        var count = 0;
        foreach (var row in _rows.OrderBy(r => r))
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                if (count++ >= limit) yield break;
                yield return (row, column);
            }
        }

        foreach (var range in _ranges)
        {
            for (var row = range.RowFrom; row <= range.RowTo; row++)
            {
                for (var column = range.ColumnFrom; column <= range.ColumnTo; column++)
                {
                    if (_rows.Contains(row) || !seen.Add((row, column))) continue;
                    if (count++ >= limit) yield break;
                    yield return (row, column);
                }
            }
        }

        foreach (var column in _columns.OrderBy(c => c))
        {
            for (var row = 0; row < RowCount; row++)
            {
                if (_rows.Contains(row) || seen.Contains((row, column))) continue;
                if (count++ >= limit) yield break;
                yield return (row, column);
            }
        }
    }

    public void Clear()
    {
        if (!HasSelection) return;
        ClearSelectionCore();
        RaiseChanged();
    }

    public void SetFocus(int row, int column)
    {
        row = RowCount == 0 ? -1 : Math.Clamp(row, 0, RowCount - 1);
        column = ColumnCount == 0 ? -1 : Math.Clamp(column, 0, ColumnCount - 1);
        if (row == FocusedRow && column == FocusedColumn) return;
        FocusedRow = row;
        FocusedColumn = column;
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies a mouse click on a cell with the modifier keys held; returns true when the selection changed.</summary>
    public bool Click(int row, int column, bool ctrl = false, bool shift = false)
    {
        if (row < 0 || row >= RowCount) return false;
        column = ColumnCount == 0 ? -1 : Math.Clamp(column, 0, ColumnCount - 1);
        SetFocus(row, column);

        switch (_mode)
        {
            case GridSelectionMode.None:
                return false;

            case GridSelectionMode.SingleRow:
                return ReplaceRows(row, row);

            case GridSelectionMode.MultipleRows:
            case GridSelectionMode.CheckboxRow:
                if (shift && AnchorRow >= 0)
                {
                    return ctrl ? AddRowRange(AnchorRow, row) : ReplaceRows(AnchorRow, row, keepAnchor: true);
                }
                if (ctrl)
                {
                    return ToggleRow(row);
                }
                return ReplaceRows(row, row);

            case GridSelectionMode.SingleCell:
                return ReplaceRanges(GridCellRange.Between(row, column, row, column), row, column);

            case GridSelectionMode.CellRange:
                if (shift && AnchorRow >= 0)
                {
                    return ReplaceRanges(GridCellRange.Between(AnchorRow, AnchorColumn, row, column), AnchorRow, AnchorColumn, keepOthers: ctrl);
                }
                return ReplaceRanges(GridCellRange.Between(row, column, row, column), row, column, keepOthers: ctrl);

            case GridSelectionMode.Column:
                return ClickColumn(column, ctrl, shift);

            default:
                return false;
        }
    }

    /// <summary>Extends the selection from the anchor to the cell under the mouse during a drag.</summary>
    public bool Drag(int row, int column)
    {
        if (row < 0 || row >= RowCount || AnchorRow < 0) return false;
        column = ColumnCount == 0 ? -1 : Math.Clamp(column, 0, ColumnCount - 1);
        SetFocus(row, column);

        switch (_mode)
        {
            case GridSelectionMode.MultipleRows:
            case GridSelectionMode.CheckboxRow:
                return ReplaceRows(AnchorRow, row, keepAnchor: true);

            case GridSelectionMode.CellRange:
                if (_ranges.Count == 0) return false;
                var range = GridCellRange.Between(AnchorRow, AnchorColumn, row, column);
                if (_ranges[^1] == range) return false;
                _ranges[^1] = range;
                RaiseChanged();
                return true;

            case GridSelectionMode.Column:
                return ClickColumn(column, ctrl: false, shift: true);

            default:
                return false;
        }
    }

    /// <summary>Click on a column header; selects the whole column in column and cell modes.</summary>
    public bool ClickColumn(int column, bool ctrl = false, bool shift = false)
    {
        if (column < 0 || column >= ColumnCount) return false;
        if (_mode is not (GridSelectionMode.Column or GridSelectionMode.CellRange or GridSelectionMode.SingleCell)) return false;

        var before = _columns.ToArray();
        if (shift && AnchorColumn >= 0 && _mode == GridSelectionMode.Column)
        {
            if (!ctrl) _columns.Clear();
            for (var c = Math.Min(AnchorColumn, column); c <= Math.Max(AnchorColumn, column); c++) _columns.Add(c);
        }
        else if (ctrl && _mode != GridSelectionMode.SingleCell)
        {
            if (!_columns.Remove(column)) _columns.Add(column);
            AnchorColumn = column;
        }
        else
        {
            _columns.Clear();
            _columns.Add(column);
            AnchorColumn = column;
        }

        _rows.Clear();
        if (_mode != GridSelectionMode.Column) _ranges.Clear();
        AnchorRow = Math.Max(0, FocusedRow);
        return FinishChange(before.Length == _columns.Count && before.All(_columns.Contains) ? false : true);
    }

    public bool ToggleRow(int row)
    {
        if (row < 0 || row >= RowCount || _mode is not (GridSelectionMode.MultipleRows or GridSelectionMode.CheckboxRow or GridSelectionMode.SingleRow)) return false;

        if (_mode == GridSelectionMode.SingleRow)
        {
            return _rows.Contains(row) ? RemoveRow(row) : ReplaceRows(row, row);
        }

        if (!_rows.Remove(row)) _rows.Add(row);
        AnchorRow = row;
        SetFocus(row, FocusedColumn);
        RaiseChanged();
        return true;
    }

    public bool SelectAll()
    {
        switch (_mode)
        {
            case GridSelectionMode.MultipleRows:
            case GridSelectionMode.CheckboxRow:
                if (RowCount == 0) return false;
                _rows.Clear();
                for (var r = 0; r < RowCount; r++) _rows.Add(r);
                RaiseChanged();
                return true;

            case GridSelectionMode.CellRange:
                if (RowCount == 0 || ColumnCount == 0) return false;
                _ranges.Clear();
                _ranges.Add(new GridCellRange(0, RowCount - 1, 0, ColumnCount - 1));
                AnchorRow = 0;
                AnchorColumn = 0;
                RaiseChanged();
                return true;

            case GridSelectionMode.Column:
                _columns.Clear();
                for (var c = 0; c < ColumnCount; c++) _columns.Add(c);
                RaiseChanged();
                return ColumnCount > 0;

            default:
                return false;
        }
    }

    /// <summary>Arrow-key navigation; with <paramref name="extend"/> (Shift) the selection grows from the anchor.</summary>
    public bool Move(int rowDelta, int columnDelta, bool extend = false)
    {
        if (RowCount == 0) return false;
        var row = FocusedRow < 0 ? 0 : Math.Clamp(FocusedRow + rowDelta, 0, RowCount - 1);
        var column = ColumnCount == 0 ? -1 : FocusedColumn < 0 ? 0 : Math.Clamp(FocusedColumn + columnDelta, 0, ColumnCount - 1);
        return MoveTo(row, column, extend);
    }

    /// <summary>Jumps to an absolute cell (Home/End/Ctrl+Home/Ctrl+End/PageUp/PageDown).</summary>
    public bool MoveTo(int row, int column, bool extend = false)
    {
        if (RowCount == 0) return false;
        row = Math.Clamp(row, 0, RowCount - 1);
        column = ColumnCount == 0 ? -1 : Math.Clamp(column, 0, ColumnCount - 1);

        var changed = false;
        if (extend && AnchorRow >= 0)
        {
            SetFocus(row, column);
            switch (_mode)
            {
                case GridSelectionMode.MultipleRows:
                case GridSelectionMode.CheckboxRow:
                    changed = ReplaceRows(AnchorRow, row, keepAnchor: true);
                    break;
                case GridSelectionMode.CellRange:
                    changed = ReplaceRanges(GridCellRange.Between(AnchorRow, AnchorColumn, row, column), AnchorRow, AnchorColumn);
                    break;
                case GridSelectionMode.Column:
                    changed = ClickColumn(column, ctrl: false, shift: true);
                    break;
                default:
                    changed = Click(row, column);
                    break;
            }
            return changed;
        }

        return Click(row, column);
    }

    // ------------------------------------------------------------------

    private bool ReplaceRows(int from, int to, bool keepAnchor = false)
    {
        var low = Math.Min(from, to);
        var high = Math.Max(from, to);
        var unchanged = _rows.Count == high - low + 1 && _rows.All(r => r >= low && r <= high) && _ranges.Count == 0 && _columns.Count == 0;
        _rows.Clear();
        _ranges.Clear();
        _columns.Clear();
        for (var r = low; r <= high; r++) _rows.Add(r);
        if (!keepAnchor)
        {
            AnchorRow = from;
            AnchorColumn = Math.Max(0, FocusedColumn);
        }
        return FinishChange(!unchanged);
    }

    private bool AddRowRange(int from, int to)
    {
        var before = _rows.Count;
        for (var r = Math.Min(from, to); r <= Math.Max(from, to); r++) _rows.Add(r);
        return FinishChange(_rows.Count != before);
    }

    private bool RemoveRow(int row)
    {
        _rows.Remove(row);
        AnchorRow = row;
        RaiseChanged();
        return true;
    }

    private bool ReplaceRanges(GridCellRange range, int anchorRow, int anchorColumn, bool keepOthers = false)
    {
        var before = _ranges.ToArray();
        _rows.Clear();
        _columns.Clear();
        if (!keepOthers) _ranges.Clear();
        _ranges.Add(range);
        AnchorRow = anchorRow;
        AnchorColumn = anchorColumn;
        return FinishChange(!(before.Length == _ranges.Count && before.SequenceEqual(_ranges)));
    }

    private bool FinishChange(bool changed)
    {
        if (changed) RaiseChanged();
        return changed;
    }

    private void ClearSelectionCore()
    {
        _rows.Clear();
        _columns.Clear();
        _ranges.Clear();
        AnchorRow = -1;
        AnchorColumn = -1;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
