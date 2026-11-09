using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Cross-tab over any list: row and column fields, aggregated values, subtotals, grand totals and collapsible groups with frozen headers.</summary>
public class EnterprisePivotGridControl : EnterpriseScrollableControl
{
    private readonly PivotEngine<object> _engine = new();
    private IReadOnlyList<object> _items = Array.Empty<object>();
    private object? _dataSource;
    private PivotResult? _result;
    private int _columnWidth = 120;
    private int _labelWidth = 150;
    private int _selectedRow = -1;
    private int _selectedColumn = -1;
    private int _hotRow = -1;

    public EnterprisePivotGridControl()
    {
        Size = new Size(720, 380);
        AccessibleRole = AccessibleRole.Table;
    }

    public event EventHandler<(int Row, int Column)>? CellClick;
    public event EventHandler? GroupToggled;
    public event EventHandler? SelectionChanged;

    public IList<string> RowFields => _engine.RowFields;
    public IList<string> ColumnFields => _engine.ColumnFields;
    public IList<PivotValueField> Values => _engine.Values;
    public PivotResult? Result => _result;

    public object? DataSource
    {
        get => _dataSource;
        set
        {
            _dataSource = value;
            _items = value is IEnumerable enumerable ? enumerable.Cast<object>().Where(i => i is not null).ToList() : Array.Empty<object>();
            Recompute();
        }
    }

    public bool ShowSubtotals
    {
        get => _engine.ShowSubtotals;
        set { _engine.ShowSubtotals = value; Recompute(); }
    }

    public bool ShowGrandTotals
    {
        get => _engine.ShowGrandTotals;
        set { _engine.ShowGrandTotals = value; Recompute(); }
    }

    public int ColumnWidth
    {
        get => _columnWidth;
        set { _columnWidth = Math.Max(60, value); UpdateScrollBars(); }
    }

    /// <summary>Width of each row field column in logical pixels.</summary>
    public int LabelWidth
    {
        get => _labelWidth;
        set { _labelWidth = Math.Max(80, value); UpdateScrollBars(); }
    }

    public int RowHeight { get; set; } = 30;
    public int SelectedRow => _selectedRow;
    public int SelectedColumn => _selectedColumn;

    public object? SelectedValue => _result is not null && _selectedRow >= 0 && _selectedRow < _result.Rows.Count && _selectedColumn >= 0 && _selectedColumn < _result.ColumnCount
        ? _result.Rows[_selectedRow].Values[_selectedColumn]
        : null;

    /// <summary>Recomputes the cross-tab after the fields, values or data changed.</summary>
    public void Recompute()
    {
        _result = _engine.RowFields.Count + _engine.ColumnFields.Count + _engine.Values.Count == 0 ? null : _engine.Compute(_items);
        _selectedRow = Math.Min(_selectedRow, (_result?.Rows.Count ?? 0) - 1);
        _selectedColumn = Math.Min(_selectedColumn, (_result?.ColumnCount ?? 0) - 1);
        UpdateScrollBars();
        Invalidate();
    }

    public void ToggleGroup(string key)
    {
        if (!_engine.CollapsedGroups.Remove(key)) _engine.CollapsedGroups.Add(key);
        Recompute();
        GroupToggled?.Invoke(this, EventArgs.Empty);
    }

    public void ExpandAll()
    {
        _engine.CollapsedGroups.Clear();
        Recompute();
        GroupToggled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Collapses the outermost row groups; expand them again one by one or with <see cref="ExpandAll"/>.</summary>
    public void CollapseAll()
    {
        if (_result is null) return;
        foreach (var row in _result.Rows)
        {
            if (row.GroupKeys.Length > 0 && row.GroupKeys[0] is { } key) _engine.CollapsedGroups.Add(key);
        }

        Recompute();
        GroupToggled?.Invoke(this, EventArgs.Empty);
    }

    // ---- geometry -----------------------------------------------------------------------

    private int LabelColumns => Math.Max(1, _engine.RowFields.Count);
    private int LabelsPx => LabelColumns * S(_labelWidth);
    private int ColPx => S(_columnWidth);
    private int RowPx => S(RowHeight);
    private int HeaderRowPx => S(34);
    private int HeaderLevels => _result is null ? 0 : Math.Max(1, _result.HeaderRows.Count);
    private int HeaderPx => HeaderLevels * HeaderRowPx;

    protected override int LineHeight => RowPx;

    protected override Size ContentSize => _result is null
        ? Size.Empty
        : new Size(LabelsPx + _result.ColumnCount * ColPx, HeaderPx + _result.Rows.Count * RowPx);

    private Rectangle RowsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X, view.Y + HeaderPx, view.Width, Math.Max(0, view.Height - HeaderPx));
        }
    }

    private Rectangle RowRect(int index)
    {
        var view = ViewPort;
        return new Rectangle(view.X, view.Y + HeaderPx + index * RowPx - ScrollY, view.Width, RowPx);
    }

    private int ColumnX(int column) => ViewPort.X + LabelsPx + column * ColPx - ScrollX;

    private int RowAt(int y)
    {
        if (_result is null) return -1;
        var index = (y - ViewPort.Y - HeaderPx + ScrollY) / RowPx;
        return y >= ViewPort.Y + HeaderPx && index >= 0 && index < _result.Rows.Count ? index : -1;
    }

    private int ColumnAt(int x)
    {
        if (_result is null || x < ViewPort.X + LabelsPx) return -1;
        var column = (x - ViewPort.X - LabelsPx + ScrollX) / ColPx;
        return column >= 0 && column < _result.ColumnCount ? column : -1;
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;

        if (_result is null)
        {
            Chrome.DrawText(g, "Add row, column and value fields to build the pivot", Font, view, Colors.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var rows = RowsArea;
        var valuesArea = new Rectangle(view.X + LabelsPx, rows.Y, Math.Max(0, view.Width - LabelsPx), rows.Height);
        var first = Math.Max(0, ScrollY / RowPx);
        var last = Math.Min(_result.Rows.Count - 1, (ScrollY + rows.Height) / RowPx);

        g.SetClip(valuesArea);
        for (var i = first; i <= last; i++) PaintValues(g, i);

        g.SetClip(new Rectangle(view.X, rows.Y, Math.Min(LabelsPx, view.Width), rows.Height));
        for (var i = first; i <= last; i++) PaintLabels(g, i);

        g.SetClip(new Rectangle(view.X + LabelsPx, view.Y, Math.Max(0, view.Width - LabelsPx), HeaderPx));
        PaintColumnHeaders(g);
        g.SetClip(new Rectangle(view.X, view.Y, Math.Min(LabelsPx, view.Width), HeaderPx));
        PaintCorner(g);
        g.ResetClip();

        using var pen = new Pen(Colors.Border);
        g.DrawLine(pen, view.X + LabelsPx - 1, view.Y, view.X + LabelsPx - 1, view.Bottom);
    }

    private Color RowBack(PivotRow row, int index, bool hovered)
    {
        var baseColor = row.Kind switch
        {
            PivotRowKind.GrandTotal => Chrome.Blend(Colors.Surface, Colors.Header, 1),
            PivotRowKind.Subtotal => Chrome.Blend(Colors.Surface, Colors.Header, 0.55),
            _ => index % 2 == 1 && !Colors.HighContrast ? Chrome.Blend(Colors.Surface, Colors.Alt, 0.5) : Colors.Surface
        };
        return hovered ? Chrome.Blend(baseColor, Colors.Primary, 0.08) : baseColor;
    }

    private void PaintValues(Graphics g, int index)
    {
        var row = _result!.Rows[index];
        var rect = RowRect(index);
        var hovered = index == _hotRow && Enabled;
        using (var brush = new SolidBrush(RowBack(row, index, hovered))) g.FillRectangle(brush, rect);

        var bold = row.Kind != PivotRowKind.Detail;
        var totals = _result.LeafHeaders;
        var firstColumn = Math.Max(0, ScrollX / ColPx);
        var lastColumn = Math.Min(_result.ColumnCount - 1, (ScrollX + rect.Width) / ColPx);
        for (var c = firstColumn; c <= lastColumn; c++)
        {
            var cell = new Rectangle(ColumnX(c), rect.Y, ColPx, rect.Height);
            if (totals[c].IsTotal && row.Kind == PivotRowKind.Detail)
            {
                using var tint = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Header, 0.5));
                g.FillRectangle(tint, cell);
            }

            var selected = index == _selectedRow && c == _selectedColumn;
            if (selected)
            {
                using var back = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.SelectionBack, Colors.HighContrast ? 1 : 0.7));
                g.FillRectangle(back, cell);
                using var pen = new Pen(Colors.Primary, 2f);
                g.DrawRectangle(pen, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }

            var text = _result.FormatValue(c, row.Values[c]);
            Chrome.DrawText(g, text, bold || totals[c].IsTotal ? BoldFont : Font, new Rectangle(cell.X + S(8), cell.Y, Math.Max(0, cell.Width - S(18)), cell.Height), selected && Colors.HighContrast ? Colors.SelectionText : row.Values[c] is null ? Colors.Muted : Colors.Text, ContentAlignment.MiddleRight);
        }

        using var line = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.6));
        g.DrawLine(line, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
        if (row.Kind == PivotRowKind.GrandTotal)
        {
            using var top = new Pen(Colors.Border, 2f);
            g.DrawLine(top, rect.X, rect.Y, rect.Right, rect.Y);
        }
    }

    private void PaintLabels(Graphics g, int index)
    {
        var row = _result!.Rows[index];
        var rect = RowRect(index);
        var hovered = index == _hotRow && Enabled;
        var labelRect = new Rectangle(rect.X, rect.Y, LabelsPx, rect.Height);
        using (var brush = new SolidBrush(RowBack(row, index, hovered))) g.FillRectangle(brush, labelRect);

        var width = S(_labelWidth);
        for (var slot = 0; slot < LabelColumns && slot < row.Labels.Length; slot++)
        {
            var cell = new Rectangle(rect.X + slot * width, rect.Y, width, rect.Height);
            var key = slot < row.GroupKeys.Length ? row.GroupKeys[slot] : null;
            var text = row.Labels[slot];
            var textX = cell.X + S(10);
            if (key is not null)
            {
                Chrome.DrawChevron(g, new Rectangle(cell.X + S(4), cell.Y, S(20), cell.Height), row.IsCollapsed && IsLastGroupSlot(row, slot) ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
                textX = cell.X + S(26);
            }

            if (text.Length > 0) Chrome.DrawText(g, text, row.Kind != PivotRowKind.Detail || key is not null ? BoldFont : Font, new Rectangle(textX, cell.Y, Math.Max(0, cell.Right - textX - S(6)), cell.Height), Colors.Text);
        }

        using var line = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.6));
        g.DrawLine(line, labelRect.X, labelRect.Bottom - 1, labelRect.Right, labelRect.Bottom - 1);
        if (row.Kind == PivotRowKind.GrandTotal)
        {
            using var top = new Pen(Colors.Border, 2f);
            g.DrawLine(top, labelRect.X, labelRect.Y, labelRect.Right, labelRect.Y);
        }
    }

    private static bool IsLastGroupSlot(PivotRow row, int slot) => row.GroupKeys.Skip(slot + 1).All(k => k is null);

    private void PaintColumnHeaders(Graphics g)
    {
        var view = ViewPort;
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, view.X, view.Y, view.Width, HeaderPx);
        using var divider = new Pen(Chrome.Blend(Colors.Header, Colors.Border, 0.9));
        foreach (var level in _result!.HeaderRows)
        {
            foreach (var cell in level)
            {
                var rect = new Rectangle(ColumnX(cell.Start), view.Y + cell.Level * HeaderRowPx, cell.Span * ColPx, HeaderRowPx);
                if (rect.Right < view.X || rect.Left > view.Right) continue;
                if (cell.IsTotal)
                {
                    using var tint = new SolidBrush(Chrome.Blend(Colors.Header, Colors.Primary, 0.08));
                    g.FillRectangle(tint, rect);
                }

                var textBounds = rect;
                // Keep the caption of a wide spanning cell visible while its start is scrolled out.
                if (rect.Left < view.X + LabelsPx && rect.Right > view.X + LabelsPx)
                {
                    textBounds = new Rectangle(view.X + LabelsPx, rect.Y, rect.Right - (view.X + LabelsPx), rect.Height);
                }

                Chrome.DrawText(g, cell.Text, BoldFont, new Rectangle(textBounds.X + S(8), textBounds.Y, Math.Max(0, textBounds.Width - S(16)), textBounds.Height), Colors.Text, cell.Level == _result.HeaderRows.Count - 1 ? ContentAlignment.MiddleRight : ContentAlignment.MiddleCenter);
                g.DrawLine(divider, rect.Right - 1, rect.Y + S(6), rect.Right - 1, rect.Bottom - S(6));
                g.DrawLine(divider, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            }
        }

        using var border = new Pen(Colors.Border);
        g.DrawLine(border, view.X, view.Y + HeaderPx - 1, view.Right, view.Y + HeaderPx - 1);
    }

    private void PaintCorner(Graphics g)
    {
        var view = ViewPort;
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, view.X, view.Y, LabelsPx, HeaderPx);
        var width = S(_labelWidth);
        var captions = _result!.RowCaptions;
        for (var slot = 0; slot < LabelColumns; slot++)
        {
            var text = slot < captions.Count ? captions[slot] : string.Empty;
            Chrome.DrawText(g, text, BoldFont, new Rectangle(view.X + slot * width + S(10), view.Y + HeaderPx - HeaderRowPx, Math.Max(0, width - S(16)), HeaderRowPx), Colors.Text);
        }

        using var border = new Pen(Colors.Border);
        g.DrawLine(border, view.X, view.Y + HeaderPx - 1, view.X + LabelsPx, view.Y + HeaderPx - 1);
    }

    // ---- input --------------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var row = RowAt(e.Y);
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
        if (_result is null || !Enabled) return;
        var row = RowAt(e.Y);
        if (row < 0) return;

        if (e.X < ViewPort.X + LabelsPx)
        {
            var slot = (e.X - ViewPort.X) / S(_labelWidth);
            var keys = _result.Rows[row].GroupKeys;
            if (e.Button == MouseButtons.Left && slot >= 0 && slot < keys.Length && keys[slot] is { } key)
            {
                ToggleGroup(key);
                return;
            }
        }

        var column = ColumnAt(e.X);
        if (column >= 0) Select(row, column);
        else if (_selectedRow != row) Select(row, Math.Max(0, _selectedColumn));
        if (column >= 0) CellClick?.Invoke(this, (row, column));
    }

    private void Select(int row, int column)
    {
        if (_result is null || _result.Rows.Count == 0 || _result.ColumnCount == 0) return;
        row = Math.Clamp(row, 0, _result.Rows.Count - 1);
        column = Math.Clamp(column, 0, _result.ColumnCount - 1);
        var changed = row != _selectedRow || column != _selectedColumn;
        _selectedRow = row;
        _selectedColumn = column;
        EnsureVisible(new Rectangle(LabelsPx + column * ColPx, row * RowPx, ColPx, RowPx + HeaderPx));
        Invalidate();
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Space or Keys.Enter
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_result is null || _result.Rows.Count == 0) return;
        var row = Math.Max(0, _selectedRow);
        var column = Math.Max(0, _selectedColumn);
        var page = Math.Max(1, RowsArea.Height / RowPx - 1);
        switch (e.KeyCode)
        {
            case Keys.Down: Select(_selectedRow < 0 ? 0 : row + 1, column); break;
            case Keys.Up: Select(row - 1, column); break;
            case Keys.Right: Select(row, _selectedColumn < 0 ? 0 : column + 1); break;
            case Keys.Left: Select(row, column - 1); break;
            case Keys.PageDown: Select(row + page, column); break;
            case Keys.PageUp: Select(row - page, column); break;
            case Keys.Home: Select(e.Control ? 0 : row, 0); break;
            case Keys.End: Select(e.Control ? _result.Rows.Count - 1 : row, _result.ColumnCount - 1); break;
            case Keys.Space or Keys.Enter:
                var keys = _result.Rows[row].GroupKeys;
                var key = keys.LastOrDefault(k => k is not null);
                if (key is null) return;
                ToggleGroup(key);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
