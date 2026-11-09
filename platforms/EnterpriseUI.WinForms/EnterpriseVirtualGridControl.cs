using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Grid for very large or computed data: it asks for the visible cells only, through <see cref="CellValueNeeded"/>, and reports edits through
/// <see cref="CellValuePushed"/>. Rows and columns are never materialized.
/// </summary>
public class EnterpriseVirtualGridControl : EnterpriseScrollableControl
{
    private static readonly string[] SampleTitles = { "Transaction ID", "Exchange Venue", "Notional Value", "Order Volume", "Metric C4", "Metric C5" };

    private readonly EnterpriseVirtualGrid _grid = new() { RowCount = 1_000_000, ColumnCount = 12 };
    private readonly TextBox _editor = new() { BorderStyle = BorderStyle.None, Visible = false };
    private readonly List<string> _titles = new();
    private int _columnWidth = 125;
    private int _selectedRow;
    private int _selectedColumn;
    private int _hotRow = -1;
    private bool _editing;
    private bool _committing;

    public EnterpriseVirtualGridControl()
    {
        Size = new Size(760, 280);
        AccessibleRole = AccessibleRole.Table;
        _grid.CellValueNeeded += (_, e) =>
        {
            if (!UseSampleData) return;
            e.Value = e.ColumnIndex switch
            {
                0 => $"TX-{e.RowIndex + 100000:D7}",
                1 => (e.RowIndex % 4) switch { 0 => "NYSE-CORE", 1 => "NASDAQ-OPT", 2 => "LSE-FX", _ => "TSE-DERIV" },
                2 => $"{(100.0 + ((e.RowIndex * 17) % 9000) / 10.0):C2}",
                3 => $"{((e.RowIndex * 31) % 500) + 10} lots",
                _ => $"Cell [{e.RowIndex:N0}, {e.ColumnIndex}]"
            };
        };
        _editor.KeyDown += OnEditorKeyDown;
        _editor.LostFocus += (_, _) => { if (_editing && !_committing) CommitEdit(); };
        Controls.Add(_editor);
        OnThemeApplied();
    }

    public event Action<int, int, object?>? CellSelected;
    public event EventHandler<VirtualCellValueNeededEventArgs>? CellValueNeeded { add => _grid.CellValueNeeded += value; remove => _grid.CellValueNeeded -= value; }
    public event EventHandler<VirtualCellValuePushedEventArgs>? CellValuePushed { add => _grid.CellValuePushed += value; remove => _grid.CellValuePushed -= value; }

    /// <summary>Shows generated sample data until a handler supplies values; turn it off when you provide your own.</summary>
    public bool UseSampleData { get; set; } = true;

    public bool AllowEdit { get; set; } = true;
    public int SelectedRow => _selectedRow;
    public int SelectedCol => _selectedColumn;

    public int RowCount
    {
        get => _grid.RowCount;
        set { _grid.RowCount = value; UpdateScrollBars(); Invalidate(); }
    }

    public int ColumnCount
    {
        get => _grid.ColumnCount;
        set { _grid.ColumnCount = value; UpdateScrollBars(); Invalidate(); }
    }

    public int ColumnWidth
    {
        get => _columnWidth;
        set { _columnWidth = Math.Max(40, value); UpdateScrollBars(); }
    }

    public int RowHeight { get; set; } = 28;

    /// <summary>Column captions; missing ones show "Column n".</summary>
    public List<string> ColumnTitles => _titles;

    public object? GetCellValue(int rowIndex, int columnIndex) => _grid.GetCellValue(rowIndex, columnIndex);

    public void SetCellValue(int rowIndex, int columnIndex, object? value)
    {
        _grid.SetCellValue(rowIndex, columnIndex, value);
        Invalidate();
    }

    public void SelectCell(int row, int column)
    {
        if (_grid.RowCount == 0 || _grid.ColumnCount == 0) return;
        EndEdit(false);
        _selectedRow = Math.Clamp(row, 0, _grid.RowCount - 1);
        _selectedColumn = Math.Clamp(column, 0, _grid.ColumnCount - 1);
        EnsureVisible(new Rectangle(RowHeaderPx + _selectedColumn * ColPx, (int)Math.Min(int.MaxValue / 2L, (long)_selectedRow * RowPx), ColPx, RowPx + HeaderPx));
        Invalidate();
        CellSelected?.Invoke(_selectedRow, _selectedColumn, _grid.GetCellValue(_selectedRow, _selectedColumn));
    }

    // ---- geometry -----------------------------------------------------------------------

    private int HeaderPx => S(34);
    private int RowPx => S(RowHeight);
    private int ColPx => S(_columnWidth);
    private int RowHeaderPx => S(76);
    protected override int LineHeight => RowPx;

    protected override Size ContentSize
    {
        get
        {
            var height = Math.Min(2_000_000_000L, HeaderPx + (long)_grid.RowCount * RowPx);
            var width = Math.Min(2_000_000_000L, RowHeaderPx + (long)_grid.ColumnCount * ColPx);
            return new Size((int)width, (int)height);
        }
    }

    private Rectangle CellsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X + RowHeaderPx, view.Y + HeaderPx, Math.Max(0, view.Width - RowHeaderPx), Math.Max(0, view.Height - HeaderPx));
        }
    }

    private Rectangle CellRect(int row, int column)
    {
        var view = ViewPort;
        return new Rectangle(view.X + RowHeaderPx + column * ColPx - ScrollX, view.Y + HeaderPx + (int)((long)row * RowPx - ScrollY), ColPx, RowPx);
    }

    private (int Row, int Column) CellAt(Point location)
    {
        var area = CellsArea;
        if (!area.Contains(location)) return (-1, -1);
        var row = (int)((location.Y - area.Y + (long)ScrollY) / RowPx);
        var column = (location.X - area.X + ScrollX) / ColPx;
        return row < _grid.RowCount && column < _grid.ColumnCount ? (row, column) : (-1, -1);
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _editor.BackColor = Colors.Surface;
        _editor.ForeColor = Colors.Text;
        _editor.Font = Font;
    }

    protected override void OnScrolled()
    {
        PositionEditor();
        base.OnScrolled();
    }

    // ---- painting -----------------------------------------------------------------------

    private string Title(int column) => column < _titles.Count ? _titles[column] : UseSampleData && column < SampleTitles.Length ? SampleTitles[column] : $"Column {column + 1}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        var area = CellsArea;
        if (view.Width <= 0 || view.Height <= 0) return;

        if (_grid.RowCount == 0 || _grid.ColumnCount == 0)
        {
            Chrome.DrawText(g, "No cells", Font, view, Colors.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var firstRow = (int)Math.Max(0, ScrollY / RowPx);
        var lastRow = (int)Math.Min(_grid.RowCount - 1, ((long)ScrollY + area.Height) / RowPx);
        var firstColumn = Math.Max(0, ScrollX / ColPx);
        var lastColumn = Math.Min(_grid.ColumnCount - 1, (ScrollX + area.Width) / ColPx);

        g.SetClip(area);
        using (var line = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.6)))
        {
            for (var r = firstRow; r <= lastRow; r++)
            {
                var rowRect = CellRect(r, 0);
                var hovered = r == _hotRow && Enabled;
                var back = hovered ? Colors.HoverBack : r % 2 == 1 && !Colors.HighContrast ? Chrome.Blend(Colors.Surface, Colors.Alt, 0.5) : Colors.Surface;
                using (var brush = new SolidBrush(back)) g.FillRectangle(brush, area.X, rowRect.Y, area.Width, rowRect.Height);
                g.DrawLine(line, area.X, rowRect.Bottom - 1, area.Right, rowRect.Bottom - 1);

                for (var c = firstColumn; c <= lastColumn; c++)
                {
                    var cell = CellRect(r, c);
                    var selected = r == _selectedRow && c == _selectedColumn;
                    if (selected)
                    {
                        using var tint = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.SelectionBack, Colors.HighContrast ? 1 : 0.75));
                        g.FillRectangle(tint, cell);
                        using var pen = new Pen(Colors.Primary, 2f);
                        g.DrawRectangle(pen, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
                    }

                    if (selected && _editing) continue;
                    var value = _grid.GetCellValue(r, c);
                    var numeric = value is IConvertible and not string and not bool and not DateTime;
                    Chrome.DrawText(g, Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty, Font, new Rectangle(cell.X + S(8), cell.Y, Math.Max(0, cell.Width - S(16)), cell.Height), selected && Colors.HighContrast ? Colors.SelectionText : Colors.Text, numeric ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft);
                }
            }

            for (var c = firstColumn; c <= lastColumn + 1; c++)
            {
                var x = CellRect(0, c).X - 1;
                g.DrawLine(line, x, area.Y, x, area.Bottom);
            }
        }

        g.SetClip(new Rectangle(view.X + RowHeaderPx, view.Y, Math.Max(0, view.Width - RowHeaderPx), HeaderPx));
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, view.X, view.Y, view.Width, HeaderPx);
        using (var divider = new Pen(Chrome.Blend(Colors.Header, Colors.Border, 0.9)))
        {
            for (var c = firstColumn; c <= lastColumn; c++)
            {
                var cell = CellRect(0, c);
                var header = new Rectangle(cell.X, view.Y, cell.Width, HeaderPx);
                var current = c == _selectedColumn;
                if (current)
                {
                    using var tint = new SolidBrush(Chrome.Blend(Colors.Header, Colors.Primary, 0.14));
                    g.FillRectangle(tint, header);
                }

                Chrome.DrawText(g, Title(c), BoldFont, new Rectangle(header.X + S(8), header.Y, Math.Max(0, header.Width - S(16)), header.Height), Colors.Text);
                g.DrawLine(divider, header.Right - 1, header.Y + S(8), header.Right - 1, header.Bottom - S(8));
            }
        }

        g.SetClip(new Rectangle(view.X, view.Y + HeaderPx, RowHeaderPx, Math.Max(0, view.Height - HeaderPx)));
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, view.X, view.Y + HeaderPx, RowHeaderPx, Math.Max(0, view.Height - HeaderPx));
        for (var r = firstRow; r <= lastRow; r++)
        {
            var rowRect = CellRect(r, 0);
            if (r == _selectedRow)
            {
                using var tint = new SolidBrush(Chrome.Blend(Colors.Header, Colors.Primary, 0.14));
                g.FillRectangle(tint, view.X, rowRect.Y, RowHeaderPx, rowRect.Height);
            }

            Chrome.DrawText(g, (r + 1).ToString("N0", CultureInfo.CurrentCulture), Font, new Rectangle(view.X + S(6), rowRect.Y, RowHeaderPx - S(14), rowRect.Height), r == _selectedRow ? Colors.Text : Colors.Muted, ContentAlignment.MiddleRight);
        }

        g.ResetClip();
        using (var cornerBrush = new SolidBrush(Colors.Header)) g.FillRectangle(cornerBrush, view.X, view.Y, RowHeaderPx, HeaderPx);
        Chrome.DrawText(g, "#", BoldFont, new Rectangle(view.X + S(6), view.Y, RowHeaderPx - S(14), HeaderPx), Colors.Muted, ContentAlignment.MiddleRight);
        using var border = new Pen(Colors.Border);
        g.DrawLine(border, view.X, view.Y + HeaderPx - 1, view.Right, view.Y + HeaderPx - 1);
        g.DrawLine(border, view.X + RowHeaderPx - 1, view.Y, view.X + RowHeaderPx - 1, view.Bottom);
    }

    // ---- editing ------------------------------------------------------------------------

    private void PositionEditor()
    {
        if (!_editing) return;
        var cell = CellRect(_selectedRow, _selectedColumn);
        var visible = CellsArea.IntersectsWith(cell);
        _editor.Visible = visible;
        if (!visible) return;
        var h = Math.Min(_editor.PreferredHeight, cell.Height - S(4));
        _editor.SetBounds(cell.X + S(8), cell.Y + (cell.Height - h) / 2, Math.Max(10, cell.Width - S(16)), h);
    }

    private void BeginEdit(string? initialText = null)
    {
        if (!AllowEdit || !Enabled || _grid.RowCount == 0) return;
        _editing = true;
        _editor.Text = initialText ?? Convert.ToString(_grid.GetCellValue(_selectedRow, _selectedColumn), CultureInfo.CurrentCulture) ?? string.Empty;
        PositionEditor();
        _editor.Visible = true;
        _editor.Focus();
        if (initialText is null) _editor.SelectAll();
        else _editor.SelectionStart = _editor.TextLength;
        Invalidate();
    }

    private void CommitEdit()
    {
        if (!_editing) return;
        _committing = true;
        try
        {
            var text = _editor.Text;
            EndEdit(false);
            _grid.SetCellValue(_selectedRow, _selectedColumn, text);
            Invalidate();
        }
        finally
        {
            _committing = false;
        }
    }

    private void EndEdit(bool refocus)
    {
        if (!_editing) return;
        _editing = false;
        _editor.Visible = false;
        if (refocus) Focus();
        Invalidate();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CommitEdit();
            Focus();
            SelectCell(_selectedRow + 1, _selectedColumn);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            EndEdit(true);
        }
    }

    // ---- input --------------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (row, _) = CellAt(e.Location);
        if (row != _hotRow)
        {
            _hotRow = row;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotRow = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var (row, column) = CellAt(e.Location);
        if (row >= 0) SelectCell(row, column);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && CellAt(e.Location).Row >= 0) BeginEdit();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.F2
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_grid.RowCount == 0 || _grid.ColumnCount == 0) return;
        var page = Math.Max(1, CellsArea.Height / RowPx - 1);
        switch (e.KeyCode)
        {
            case Keys.Down: SelectCell(_selectedRow + 1, _selectedColumn); break;
            case Keys.Up: SelectCell(_selectedRow - 1, _selectedColumn); break;
            case Keys.Right: SelectCell(_selectedRow, _selectedColumn + 1); break;
            case Keys.Left: SelectCell(_selectedRow, _selectedColumn - 1); break;
            case Keys.PageDown: SelectCell(_selectedRow + page, _selectedColumn); break;
            case Keys.PageUp: SelectCell(_selectedRow - page, _selectedColumn); break;
            case Keys.Home: SelectCell(e.Control ? 0 : _selectedRow, 0); break;
            case Keys.End: SelectCell(e.Control ? _grid.RowCount - 1 : _selectedRow, _grid.ColumnCount - 1); break;
            case Keys.F2 or Keys.Enter: BeginEdit(); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (char.IsControl(e.KeyChar) || !AllowEdit) return;
        BeginEdit(e.KeyChar.ToString());
        e.Handled = true;
    }
}
