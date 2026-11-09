using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

public enum HeatMapScale { Sequential, Diverging }

public enum HeatMapAggregate { Sum, Average, Count, Min, Max }

public sealed class HeatMapCellEventArgs : EventArgs
{
    public HeatMapCellEventArgs(int row, int column, string rowLabel, string columnLabel, double? value)
    {
        Row = row;
        Column = column;
        RowLabel = rowLabel;
        ColumnLabel = columnLabel;
        Value = value;
    }

    public int Row { get; }
    public int Column { get; }
    public string RowLabel { get; }
    public string ColumnLabel { get; }
    public double? Value { get; }
}

/// <summary>Matrix of colored cells for two categories: color scale, legend, value labels, tooltips, selection and sorting by header.</summary>
public class EnterpriseHeatMapControl : EnterpriseScrollableControl
{
    private readonly List<string> _rows = new();
    private readonly List<string> _columns = new();
    private double?[,] _values = new double?[0, 0];
    private readonly ToolTip _tip = new() { InitialDelay = 300, ReshowDelay = 100 };
    private HeatMapScale _scale = HeatMapScale.Sequential;
    private double? _minimum;
    private double? _maximum;
    private bool _showValues = true;
    private bool _showLegend = true;
    private string _valueFormat = "N0";
    private int _selectedRow = -1;
    private int _selectedColumn = -1;
    private int _hotRow = -1;
    private int _hotColumn = -1;
    private int _sortColumn = -1;
    private bool _sortDescending = true;

    public EnterpriseHeatMapControl()
    {
        Size = new Size(640, 380);
        AccessibleRole = AccessibleRole.Graphic;
    }

    public event EventHandler<HeatMapCellEventArgs>? CellClick;
    public event EventHandler? SelectionChanged;

    public IReadOnlyList<string> RowLabels => _rows;
    public IReadOnlyList<string> ColumnLabels => _columns;
    public string Title { get; set; } = string.Empty;
    public int MinimumCellWidth { get; set; } = 56;
    public int MinimumCellHeight { get; set; } = 28;

    public HeatMapScale Scale { get => _scale; set { _scale = value; Invalidate(); } }
    public double? Minimum { get => _minimum; set { _minimum = value; Invalidate(); } }
    public double? Maximum { get => _maximum; set { _maximum = value; Invalidate(); } }
    public bool ShowValues { get => _showValues; set { _showValues = value; Invalidate(); } }
    public bool ShowLegend { get => _showLegend; set { _showLegend = value; UpdateScrollBars(); } }
    public string ValueFormat { get => _valueFormat; set { _valueFormat = value; Invalidate(); } }
    public int SelectedRow => _selectedRow;
    public int SelectedColumn => _selectedColumn;

    public double? SelectedValue => _selectedRow >= 0 && _selectedColumn >= 0 && _selectedRow < _rows.Count && _selectedColumn < _columns.Count ? _values[_selectedRow, _selectedColumn] : null;

    public double? ValueAt(int row, int column) => row >= 0 && row < _rows.Count && column >= 0 && column < _columns.Count ? _values[row, column] : null;

    public void SetMatrix(IEnumerable<string> rowLabels, IEnumerable<string> columnLabels, double?[,] values)
    {
        var rows = rowLabels.ToList();
        var columns = columnLabels.ToList();
        if (values.GetLength(0) != rows.Count || values.GetLength(1) != columns.Count) throw new ArgumentException("The matrix size must match the number of row and column labels.", nameof(values));
        _rows.Clear();
        _rows.AddRange(rows);
        _columns.Clear();
        _columns.AddRange(columns);
        _values = (double?[,])values.Clone();
        _selectedRow = _selectedColumn = -1;
        _sortColumn = -1;
        InvalidateMetrics(labels: true);
        UpdateScrollBars();
        Invalidate();
    }

    /// <summary>Aggregates objects into the matrix: one row per <paramref name="rowField"/> value, one column per <paramref name="columnField"/> value.</summary>
    public void Bind(IEnumerable data, string rowField, string columnField, string? valueField = null, HeatMapAggregate aggregate = HeatMapAggregate.Sum)
    {
        var items = data.Cast<object>().Where(o => o is not null).ToList();
        var rows = items.Select(i => MemberAccess.Text(i, rowField)).Distinct().OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        var columns = items.Select(i => MemberAccess.Text(i, columnField)).Distinct().OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        var buckets = new Dictionary<(string, string), List<double>>();
        foreach (var item in items)
        {
            var key = (MemberAccess.Text(item, rowField), MemberAccess.Text(item, columnField));
            var value = valueField is null ? 1d : Convert.ToDouble(MemberAccess.Read(item, valueField) ?? 0d, CultureInfo.CurrentCulture);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<double>();
            list.Add(value);
        }

        var matrix = new double?[rows.Count, columns.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                if (!buckets.TryGetValue((rows[r], columns[c]), out var list)) continue;
                matrix[r, c] = aggregate switch
                {
                    HeatMapAggregate.Average => list.Average(),
                    HeatMapAggregate.Count => list.Count,
                    HeatMapAggregate.Min => list.Min(),
                    HeatMapAggregate.Max => list.Max(),
                    _ => list.Sum()
                };
            }
        }

        SetMatrix(rows, columns, matrix);
    }

    /// <summary>Orders the rows by the values of a column.</summary>
    public void SortRowsByColumn(int column, bool descending)
    {
        if (column < 0 || column >= _columns.Count) return;
        var order = Enumerable.Range(0, _rows.Count)
            .OrderBy(r => _values[r, column] is null ? 1 : 0)
            .ThenBy(r => descending ? -(_values[r, column] ?? 0) : _values[r, column] ?? 0)
            .ToList();
        var labels = order.Select(r => _rows[r]).ToList();
        var matrix = new double?[_rows.Count, _columns.Count];
        for (var i = 0; i < order.Count; i++)
        {
            for (var c = 0; c < _columns.Count; c++) matrix[i, c] = _values[order[i], c];
        }

        _rows.Clear();
        _rows.AddRange(labels);
        _values = matrix;
        _sortColumn = column;
        _sortDescending = descending;
        _selectedRow = _selectedColumn = -1;
        Invalidate();
    }

    // ---- scale --------------------------------------------------------------------------

    private (double Min, double Max) Range()
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var v in _values)
        {
            if (v is not { } value) continue;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (min > max) return (0, 1);
        return (_minimum ?? min, _maximum ?? max);
    }

    private Color ColorFor(double value, double min, double max)
    {
        if (Colors.HighContrast) return value >= (min + max) / 2 ? SystemColors.Highlight : SystemColors.Window;
        if (_scale == HeatMapScale.Diverging)
        {
            var span = Math.Max(Math.Abs(min), Math.Abs(max));
            var t = span <= 0 ? 0 : Math.Clamp(value / span, -1, 1);
            var neutral = Chrome.Blend(Colors.Surface, Colors.Muted, 0.18);
            return t >= 0 ? Chrome.Blend(neutral, Colors.Success, t) : Chrome.Blend(neutral, Colors.Danger, -t);
        }

        var u = max <= min ? 1 : Math.Clamp((value - min) / (max - min), 0, 1);
        return Chrome.Blend(Chrome.Blend(Colors.Surface, Colors.Primary, 0.12), Colors.Primary, u);
    }

    private static Color TextOn(Color back) => (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) > 150 ? Color.FromArgb(32, 32, 32) : Color.White;

    // ---- geometry -----------------------------------------------------------------------

    private int LegendPx => _showLegend ? S(40) : 0;

    private int _rowLabelWidth = -1;
    private int _columnLabelWidth = -1;
    private (Size Inner, int Dpi, bool Legend, int MinW, int MinH, int Rows, int Columns) _metricsKey;
    private (int CellW, int CellH, int RowHeader, int ColHeader) _metrics;
    private bool _metricsValid;

    private void InvalidateMetrics(bool labels)
    {
        _metricsValid = false;
        if (labels) _rowLabelWidth = _columnLabelWidth = -1;
    }

    private int RowHeaderPx
    {
        get
        {
            if (_rowLabelWidth < 0) _rowLabelWidth = _rows.Count == 0 ? 0 : _rows.Max(r => Chrome.MeasureText(r, Font));
            return Math.Clamp(_rowLabelWidth + S(24), S(60), S(220));
        }
    }

    private int LongestColumnLabel
    {
        get
        {
            if (_columnLabelWidth < 0) _columnLabelWidth = _columns.Count == 0 ? 0 : _columns.Max(c => Chrome.MeasureText(c, Font));
            return _columnLabelWidth;
        }
    }

    private bool Angled(int cellWidth) => _columns.Count > 0 && LongestColumnLabel + S(10) > cellWidth;

    private int ColumnHeaderPx(int cellWidth)
    {
        if (!Angled(cellWidth)) return S(34);
        return Math.Min(S(140), (int)(LongestColumnLabel * 0.72) + S(18));
    }

    private (int CellW, int CellH, int RowHeader, int ColHeader) Metrics()
    {
        var inner = InnerBounds;
        var key = (inner.Size, DeviceDpi, _showLegend, MinimumCellWidth, MinimumCellHeight, _rows.Count, _columns.Count);
        if (_metricsValid && key.Equals(_metricsKey)) return _metrics;

        var rowHeader = RowHeaderPx;
        var cols = Math.Max(1, _columns.Count);
        var rows = Math.Max(1, _rows.Count);
        var fitW = (inner.Width - rowHeader) / cols;
        var minW = S(MinimumCellWidth);
        var cellW = Math.Max(minW, fitW);
        var colHeader = ColumnHeaderPx(cellW);
        var fitH = (inner.Height - LegendPx - colHeader) / rows;
        var cellH = Math.Max(S(MinimumCellHeight), fitH);

        // A vertical scroll bar takes width from the cells.
        if (cellH * rows + colHeader + LegendPx > inner.Height)
        {
            cellW = Math.Max(minW, (inner.Width - ScrollBarSize - rowHeader) / cols);
        }

        _metrics = (cellW, cellH, rowHeader, ColumnHeaderPx(cellW));
        _metricsKey = key;
        _metricsValid = true;
        return _metrics;
    }

    protected override int LineHeight => S(MinimumCellHeight);

    protected override Size ContentSize
    {
        get
        {
            var (cw, ch, rh, hh) = Metrics();
            return new Size(rh + cw * _columns.Count, LegendPx + hh + ch * _rows.Count);
        }
    }

    private Rectangle CellRect(int row, int column)
    {
        var (cw, ch, rh, hh) = Metrics();
        var view = ViewPort;
        return new Rectangle(view.X + rh + column * cw - ScrollX, view.Y + LegendPx + hh + row * ch - ScrollY, cw, ch);
    }

    private (int Row, int Column) CellAt(Point location)
    {
        var (cw, ch, rh, hh) = Metrics();
        var view = ViewPort;
        var x = location.X - view.X - rh + ScrollX;
        var y = location.Y - view.Y - LegendPx - hh + ScrollY;
        if (location.X < view.X + rh || location.Y < view.Y + LegendPx + hh) return (-1, -1);
        var column = x / Math.Max(1, cw);
        var row = y / Math.Max(1, ch);
        return row >= 0 && row < _rows.Count && column >= 0 && column < _columns.Count ? (row, column) : (-1, -1);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        InvalidateMetrics(labels: true);
        base.OnFontChanged(e);
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;

        if (_rows.Count == 0 || _columns.Count == 0)
        {
            Chrome.DrawText(g, "No data", Font, view, Colors.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var (cw, ch, rh, hh) = Metrics();
        var (min, max) = Range();
        var cellsArea = new Rectangle(view.X + rh, view.Y + LegendPx + hh, Math.Max(0, view.Width - rh), Math.Max(0, view.Height - LegendPx - hh));

        g.SetClip(cellsArea);
        var firstRow = Math.Max(0, ScrollY / ch);
        var lastRow = Math.Min(_rows.Count - 1, (ScrollY + cellsArea.Height) / ch);
        var firstColumn = Math.Max(0, ScrollX / cw);
        var lastColumn = Math.Min(_columns.Count - 1, (ScrollX + cellsArea.Width) / cw);
        for (var r = firstRow; r <= lastRow; r++)
        {
            for (var c = firstColumn; c <= lastColumn; c++) PaintCell(g, r, c, min, max);
        }

        g.SetClip(new Rectangle(view.X, cellsArea.Y, Math.Min(rh, view.Width), cellsArea.Height));
        for (var r = firstRow; r <= lastRow; r++)
        {
            var rect = CellRect(r, 0);
            var back = r == _hotRow && Enabled ? Chrome.Blend(Colors.Header, Colors.Primary, 0.12) : Colors.Header;
            using (var brush = new SolidBrush(back)) g.FillRectangle(brush, view.X, rect.Y, rh, rect.Height);
            Chrome.DrawText(g, _rows[r], r == _selectedRow ? BoldFont : Font, new Rectangle(view.X + S(10), rect.Y, rh - S(16), rect.Height), Colors.Text);
        }

        g.SetClip(new Rectangle(cellsArea.X, view.Y + LegendPx, cellsArea.Width, hh));
        PaintColumnHeaders(g, cw, hh);
        g.SetClip(new Rectangle(view.X, view.Y + LegendPx, Math.Min(rh, view.Width), hh));
        using (var corner = new SolidBrush(Colors.Header)) g.FillRectangle(corner, view.X, view.Y + LegendPx, rh, hh);
        g.ResetClip();
        using (var pen = new Pen(Colors.Border))
        {
            g.DrawLine(pen, view.X, view.Y + LegendPx + hh - 1, view.Right, view.Y + LegendPx + hh - 1);
            g.DrawLine(pen, view.X + rh - 1, view.Y + LegendPx, view.X + rh - 1, view.Bottom);
        }

        if (_showLegend) PaintLegend(g, view, min, max);
    }

    private void PaintCell(Graphics g, int row, int column, double min, double max)
    {
        var rect = CellRect(row, column);
        var value = _values[row, column];
        var inner = Rectangle.Inflate(rect, -1, -1);
        if (value is { } v)
        {
            var fill = ColorFor(v, min, max);
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, inner);
            if (_showValues && inner.Width > S(30) && inner.Height > S(16))
            {
                Chrome.DrawText(g, v.ToString(_valueFormat, CultureInfo.CurrentCulture), Font, inner, TextOn(fill), ContentAlignment.MiddleCenter);
            }
        }
        else
        {
            using var brush = new HatchBrush(HatchStyle.LightUpwardDiagonal, Chrome.Blend(Colors.Surface, Colors.Border, 0.5), Colors.Surface);
            g.FillRectangle(brush, inner);
        }

        var hot = row == _hotRow && column == _hotColumn && Enabled;
        var selected = row == _selectedRow && column == _selectedColumn;
        if (hot || selected)
        {
            using var pen = new Pen(selected ? Colors.FocusRing : Colors.Text, selected ? 3f : 2f);
            g.DrawRectangle(pen, inner.X + 1, inner.Y + 1, inner.Width - 3, inner.Height - 3);
        }
    }

    private void PaintColumnHeaders(Graphics g, int cw, int hh)
    {
        var view = ViewPort;
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, view.X, view.Y + LegendPx, view.Width, hh);
        var angled = Angled(cw);
        for (var c = 0; c < _columns.Count; c++)
        {
            var rect = CellRect(0, c);
            var cell = new Rectangle(rect.X, view.Y + LegendPx, cw, hh);
            if (cell.Right < view.X || cell.Left > view.Right) continue;
            if (c == _hotColumn && Enabled)
            {
                using var hover = new SolidBrush(Chrome.Blend(Colors.Header, Colors.Primary, 0.12));
                g.FillRectangle(hover, cell);
            }

            var label = _columns[c];
            if (!angled)
            {
                var arrow = c == _sortColumn ? S(18) : 0;
                Chrome.DrawText(g, label, c == _selectedColumn ? BoldFont : Font, new Rectangle(cell.X + S(4), cell.Y, cell.Width - S(8) - arrow, cell.Height), Colors.Text, ContentAlignment.MiddleCenter);
                if (c == _sortColumn) Chrome.DrawChevron(g, new Rectangle(cell.Right - S(22), cell.Y, S(18), cell.Height), _sortDescending ? ChevronDirection.Down : ChevronDirection.Up, Colors.Primary, 1.6f);
            }
            else
            {
                var state = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.TranslateTransform(cell.X + cw / 2f, cell.Bottom - S(6));
                g.RotateTransform(-45);
                using var brush = new SolidBrush(Colors.Text);
                g.DrawString(label, c == _selectedColumn ? BoldFont : Font, brush, 0, -Font.Height / 2f, StringFormat.GenericTypographic);
                g.Restore(state);
            }

            using var divider = new Pen(Chrome.Blend(Colors.Header, Colors.Border, 0.9));
            g.DrawLine(divider, cell.Right - 1, cell.Y + S(6), cell.Right - 1, cell.Bottom - S(6));
        }
    }

    private void PaintLegend(Graphics g, Rectangle view, double min, double max)
    {
        var band = new Rectangle(view.X, view.Y, view.Width, LegendPx);
        using (var brush = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Header, 0.4))) g.FillRectangle(brush, band);
        using (var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.7))) g.DrawLine(pen, band.X, band.Bottom - 1, band.Right, band.Bottom - 1);

        if (Title.Length > 0) Chrome.DrawText(g, Title, BoldFont, new Rectangle(band.X + S(12), band.Y, Math.Max(0, band.Width / 2), band.Height), Colors.Text);

        var barWidth = Math.Min(S(220), Math.Max(S(60), band.Width / 3));
        var minText = min.ToString(_valueFormat, CultureInfo.CurrentCulture);
        var maxText = max.ToString(_valueFormat, CultureInfo.CurrentCulture);
        var maxWidth = Chrome.MeasureText(maxText, Font) + S(8);
        var minWidth = Chrome.MeasureText(minText, Font) + S(8);
        var bar = new Rectangle(band.Right - S(12) - maxWidth - barWidth, band.Y + (band.Height - S(12)) / 2, barWidth, S(12));
        if (bar.X < band.X) return;

        for (var x = 0; x < bar.Width; x++)
        {
            var value = min + (max - min) * x / Math.Max(1, bar.Width - 1);
            using var pen = new Pen(ColorFor(value, min, max));
            g.DrawLine(pen, bar.X + x, bar.Y, bar.X + x, bar.Bottom - 1);
        }

        using var edge = new Pen(Colors.Border);
        g.DrawRectangle(edge, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);
        Chrome.DrawText(g, minText, Font, new Rectangle(bar.X - minWidth - S(4), band.Y, minWidth, band.Height), Colors.Muted, ContentAlignment.MiddleRight);
        Chrome.DrawText(g, maxText, Font, new Rectangle(bar.Right + S(4), band.Y, maxWidth, band.Height), Colors.Muted);
    }

    // ---- input --------------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (row, column) = CellAt(e.Location);
        var (cw, _, rh, hh) = Metrics();
        var view = ViewPort;
        var overHeader = e.Y >= view.Y + LegendPx && e.Y < view.Y + LegendPx + hh && e.X >= view.X + rh;
        var headerColumn = overHeader ? Math.Clamp((e.X - view.X - rh + ScrollX) / Math.Max(1, cw), 0, Math.Max(0, _columns.Count - 1)) : -1;
        var hotColumn = column >= 0 ? column : headerColumn;
        if (row != _hotRow || hotColumn != _hotColumn)
        {
            _hotRow = row;
            _hotColumn = hotColumn;
            Cursor = overHeader ? Cursors.Hand : Cursors.Default;
            Invalidate();
            if (row >= 0 && column >= 0)
            {
                var value = _values[row, column];
                _tip.SetToolTip(this, $"{_rows[row]} · {_columns[column]}\n{(value.HasValue ? value.Value.ToString(_valueFormat, CultureInfo.CurrentCulture) : "no data")}");
            }
            else
            {
                _tip.SetToolTip(this, null);
            }
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotRow = _hotColumn = -1;
        _tip.SetToolTip(this, null);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!Enabled || e.Button != MouseButtons.Left) return;
        var (row, column) = CellAt(e.Location);
        if (row >= 0)
        {
            Select(row, column);
            CellClick?.Invoke(this, new HeatMapCellEventArgs(row, column, _rows[row], _columns[column], _values[row, column]));
            return;
        }

        var (cw, _, rh, hh) = Metrics();
        var view = ViewPort;
        if (_columns.Count > 0 && e.Y >= view.Y + LegendPx && e.Y < view.Y + LegendPx + hh && e.X >= view.X + rh)
        {
            var header = Math.Clamp((e.X - view.X - rh + ScrollX) / Math.Max(1, cw), 0, _columns.Count - 1);
            SortRowsByColumn(header, _sortColumn == header ? !_sortDescending : true);
        }
    }

    private void Select(int row, int column)
    {
        if (_rows.Count == 0 || _columns.Count == 0) return;
        row = Math.Clamp(row, 0, _rows.Count - 1);
        column = Math.Clamp(column, 0, _columns.Count - 1);
        var changed = row != _selectedRow || column != _selectedColumn;
        _selectedRow = row;
        _selectedColumn = column;
        var (cw, ch, rh, hh) = Metrics();
        EnsureVisible(new Rectangle(rh + column * cw, row * ch, cw, ch + LegendPx + hh));
        Invalidate();
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0 || _columns.Count == 0) return;
        var row = Math.Max(0, _selectedRow);
        var column = Math.Max(0, _selectedColumn);
        var page = Math.Max(1, ViewPort.Height / Math.Max(1, S(MinimumCellHeight)) - 1);
        switch (e.KeyCode)
        {
            case Keys.Down: Select(_selectedRow < 0 ? 0 : row + 1, column); break;
            case Keys.Up: Select(row - 1, column); break;
            case Keys.Right: Select(row, _selectedColumn < 0 ? 0 : column + 1); break;
            case Keys.Left: Select(row, column - 1); break;
            case Keys.PageDown: Select(row + page, column); break;
            case Keys.PageUp: Select(row - page, column); break;
            case Keys.Home: Select(e.Control ? 0 : row, 0); break;
            case Keys.End: Select(e.Control ? _rows.Count - 1 : row, _columns.Count - 1); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
