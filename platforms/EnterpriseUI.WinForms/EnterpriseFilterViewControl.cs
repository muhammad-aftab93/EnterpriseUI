using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Data;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

/// <summary>Faceted filter panel: checklists with live counts and numeric/date ranges; the selection narrows the data and raises <c>FilterChanged</c>.</summary>
public class EnterpriseFilterViewControl : EnterpriseScrollableControl
{
    private enum RowKind { Header, Value, More, Range }

    private sealed class Row
    {
        public RowKind Kind;
        public FacetField Facet = null!;
        public FacetValue? Value;
        public Rectangle Bounds;
    }

    private sealed class RangeInputs
    {
        public EnterpriseInputControl From = null!;
        public EnterpriseInputControl To = null!;
    }

    private readonly FacetModel<object> _model = new();
    private readonly List<Row> _rows = new();
    private readonly Dictionary<string, RangeInputs> _ranges = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _showAll = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string, string), int> _counts = new();
    private IReadOnlyList<object> _data = Array.Empty<object>();
    private object? _dataSource;
    private int _contentHeight;
    private int _current = -1;
    private int _hot = -1;
    private bool _hotClear;
    private bool _showCounts = true;
    private int _collapsedLimit = 8;
    private bool _loadingInputs;

    public EnterpriseFilterViewControl()
    {
        Size = new Size(280, 460);
        AccessibleRole = AccessibleRole.Grouping;
        _model.Changed += (_, _) =>
        {
            SyncRangeInputs();
            RebuildCounts();
            LayoutRows();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? FilterChanged;

    public FacetModel<object> Model => _model;
    public string Title { get; set; } = "Filters";

    public object? DataSource
    {
        get => _dataSource;
        set
        {
            _dataSource = value;
            _data = value is IEnumerable enumerable ? enumerable.Cast<object>().Where(i => i is not null).ToList() : Array.Empty<object>();
            _model.Load(_data);
        }
    }

    public bool ShowCounts
    {
        get => _showCounts;
        set { _showCounts = value; LayoutRows(); }
    }

    /// <summary>Values shown before a "Show more" link in a checklist.</summary>
    public int CollapsedLimit
    {
        get => _collapsedLimit;
        set { _collapsedLimit = Math.Max(1, value); LayoutRows(); }
    }

    public bool HasSelection => _model.HasSelection;
    public int ActiveCount => _model.Fields.Count(f => f.IsActive);

    public FacetField AddListFacet(string field, string? title = null, int maxValues = 50)
    {
        var facet = _model.AddList(field, title, maxValues);
        if (_data.Count > 0) _model.Load(_data);
        else LayoutRows();
        return facet;
    }

    public FacetField AddRangeFacet(string field, string? title = null)
    {
        var facet = _model.AddRange(field, title);
        var inputs = new RangeInputs { From = new EnterpriseInputControl { PlaceholderText = "From" }, To = new EnterpriseInputControl { PlaceholderText = "To" } };
        foreach (var input in new[] { inputs.From, inputs.To })
        {
            input.InnerTextBox.Leave += (_, _) => CommitRange(facet, inputs);
            input.InnerTextBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                CommitRange(facet, inputs);
            };
            Controls.Add(input);
        }

        _ranges[facet.Field] = inputs;
        if (_data.Count > 0) _model.Load(_data);
        else LayoutRows();
        return facet;
    }

    public bool Matches(object item) => _model.Matches(item);
    public IEnumerable<object> Apply(IEnumerable<object> items) => _model.Apply(items);
    public IReadOnlyList<IFilterDescriptor> ToDescriptors() => _model.ToDescriptors();

    public void ClearAll() => _model.ClearAll();

    public void ToggleValue(string field, object? value, bool selected) => _model.Toggle(field, value, selected);

    public void SetRange(string field, IComparable? from, IComparable? to) => _model.SetRange(field, from, to);

    public void ToggleSection(string field)
    {
        if (!_collapsed.Remove(field)) _collapsed.Add(field);
        LayoutRows();
    }

    private void CommitRange(FacetField facet, RangeInputs inputs)
    {
        if (_loadingInputs) return;
        var from = Parse(facet, inputs.From, out var fromOk);
        var to = Parse(facet, inputs.To, out var toOk);
        inputs.From.HasError = !fromOk;
        inputs.To.HasError = !toOk;
        if (!fromOk || !toOk) return;
        if (Equals(from, facet.From) && Equals(to, facet.To)) return;
        _model.SetRange(facet.Field, from, to);
    }

    private static IComparable? Parse(FacetField facet, EnterpriseInputControl input, out bool ok)
    {
        ok = true;
        var text = input.Text.Trim();
        if (text.Length == 0) return null;
        var type = (facet.Minimum ?? facet.Maximum)?.GetType() ?? typeof(decimal);
        try
        {
            return (IComparable)Convert.ChangeType(text, type, CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            ok = false;
            return null;
        }
    }

    private void SyncRangeInputs()
    {
        _loadingInputs = true;
        try
        {
            foreach (var facet in _model.Fields.Where(f => f.Kind == FacetKind.Range))
            {
                if (!_ranges.TryGetValue(facet.Field, out var inputs)) continue;
                string Show(IComparable? v) => v is null ? string.Empty : Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty;
                if (!inputs.From.InnerTextBox.Focused) inputs.From.Text = Show(facet.From);
                if (!inputs.To.InnerTextBox.Focused) inputs.To.Text = Show(facet.To);
                inputs.From.PlaceholderText = facet.Minimum is null ? "From" : Show(facet.Minimum);
                inputs.To.PlaceholderText = facet.Maximum is null ? "To" : Show(facet.Maximum);
            }
        }
        finally
        {
            _loadingInputs = false;
        }
    }

    private void RebuildCounts()
    {
        _counts.Clear();
        if (!_showCounts) return;
        foreach (var facet in _model.Fields.Where(f => f.Kind == FacetKind.List))
        {
            foreach (var value in facet.Values) _counts[(facet.Field, FacetKey(value.Value))] = facet.IsActive || _model.HasSelection ? _model.CountWith(facet, value.Value) : value.Count;
        }
    }

    private static string FacetKey(object? value) => value is null ? "\0" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    // ---- layout -------------------------------------------------------------------------

    private int TitlePx => S(48);
    private int RowPx => S(30);
    private int HeaderPx => S(38);

    protected override int LineHeight => RowPx;
    protected override Size ContentSize => new(0, TitlePx + _contentHeight);

    private void LayoutRows()
    {
        _rows.Clear();
        var width = Math.Max(1, ViewPort.Width);
        var y = 0;
        foreach (var facet in _model.Fields)
        {
            _rows.Add(new Row { Kind = RowKind.Header, Facet = facet, Bounds = new Rectangle(0, y, width, HeaderPx) });
            y += HeaderPx;
            if (_collapsed.Contains(facet.Field)) continue;

            if (facet.Kind == FacetKind.List)
            {
                var all = _showAll.Contains(facet.Field);
                var shown = all ? facet.Values : facet.Values.Take(_collapsedLimit).ToList();
                foreach (var value in shown)
                {
                    _rows.Add(new Row { Kind = RowKind.Value, Facet = facet, Value = value, Bounds = new Rectangle(0, y, width, RowPx) });
                    y += RowPx;
                }

                if (facet.Values.Count > _collapsedLimit)
                {
                    _rows.Add(new Row { Kind = RowKind.More, Facet = facet, Bounds = new Rectangle(0, y, width, RowPx) });
                    y += RowPx;
                }
            }
            else
            {
                _rows.Add(new Row { Kind = RowKind.Range, Facet = facet, Bounds = new Rectangle(0, y, width, S(52)) });
                y += S(52);
            }
        }

        _contentHeight = y + S(8);
        UpdateScrollBars();
        PositionInputs();
        Invalidate();
    }

    private Rectangle ToControl(Rectangle content)
    {
        var view = ViewPort;
        return new Rectangle(view.X + content.X, view.Y + TitlePx + content.Y - ScrollY, content.Width, content.Height);
    }

    private Rectangle RowsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X, view.Y + TitlePx, view.Width, Math.Max(0, view.Height - TitlePx));
        }
    }

    private void PositionInputs()
    {
        var area = RowsArea;
        foreach (var inputs in _ranges.Values)
        {
            inputs.From.Visible = false;
            inputs.To.Visible = false;
        }

        foreach (var row in _rows.Where(r => r.Kind == RowKind.Range))
        {
            var b = ToControl(row.Bounds);
            var pad = S(14);
            var gap = S(8);
            var w = (b.Width - pad * 2 - gap) / 2;
            var h = S(34);
            var inputs = _ranges[row.Facet.Field];
            Place(inputs.From, new Rectangle(b.X + pad, b.Y + S(8), w, h), area);
            Place(inputs.To, new Rectangle(b.X + pad + w + gap, b.Y + S(8), w, h), area);
        }
    }

    private static void Place(Control control, Rectangle bounds, Rectangle area)
    {
        control.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var visible = Rectangle.Intersect(bounds, area);
        control.Visible = visible.Width > 0 && visible.Height > 0;
        if (!control.Visible) return;
        if (visible == bounds)
        {
            control.Region = null;
            return;
        }

        visible.Offset(-bounds.X, -bounds.Y);
        control.Region = new Region(visible);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutRows();
    }

    protected override void OnScrolled()
    {
        PositionInputs();
        base.OnScrolled();
    }

    private Rectangle ClearRect()
    {
        var view = ViewPort;
        var w = S(76);
        return new Rectangle(view.Right - w - S(10), view.Y + (TitlePx - S(28)) / 2, w, S(28));
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;

        var area = RowsArea;
        g.SetClip(area);
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var b = ToControl(row.Bounds);
            if (b.Bottom < area.Top || b.Top > area.Bottom) continue;
            switch (row.Kind)
            {
                case RowKind.Header: PaintHeader(g, row, b, index); break;
                case RowKind.Value: PaintValue(g, row, b, index); break;
                case RowKind.More: PaintMore(g, row, b, index); break;
                case RowKind.Range: PaintRangeCaption(g, row, b); break;
            }
        }

        g.ResetClip();
        PaintTitle(g, view);
    }

    private void PaintTitle(Graphics g, Rectangle view)
    {
        var band = new Rectangle(view.X, view.Y, view.Width, TitlePx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, band);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, band.X, band.Bottom - 1, band.Right, band.Bottom - 1);
        Chrome.DrawText(g, Title, BoldFont, new Rectangle(band.X + S(14), band.Y, S(100), band.Height), Colors.Text);

        var active = ActiveCount;
        if (active > 0)
        {
            var x = band.X + S(14) + Chrome.MeasureText(Title, BoldFont) + S(8);
            var pill = new Rectangle(x, band.Y + (band.Height - S(20)) / 2, S(24), S(20));
            Chrome.FillRounded(g, pill, Colors.Primary, S(10));
            Chrome.DrawText(g, active.ToString(CultureInfo.CurrentCulture), Font, pill, Colors.PrimaryText, ContentAlignment.MiddleCenter);
            var clear = ClearRect();
            if (_hotClear) Chrome.FillRounded(g, clear, Chrome.Blend(Colors.Header, Colors.Primary, 0.14), S(4));
            Chrome.DrawText(g, "Clear all", Font, clear, Colors.Primary, ContentAlignment.MiddleCenter);
        }
    }

    private void PaintHeader(Graphics g, Row row, Rectangle b, int index)
    {
        using (var brush = new SolidBrush(index == _hot ? Colors.HoverBack : Chrome.Blend(Colors.Surface, Colors.Header, 0.4))) g.FillRectangle(brush, b);
        using (var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.7))) g.DrawLine(pen, b.X, b.Bottom - 1, b.Right, b.Bottom - 1);
        Chrome.DrawChevron(g, new Rectangle(b.X + S(8), b.Y, S(20), b.Height), _collapsed.Contains(row.Facet.Field) ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
        Chrome.DrawText(g, row.Facet.Title, BoldFont, new Rectangle(b.X + S(32), b.Y, Math.Max(0, b.Width - S(80)), b.Height), Colors.Text);
        var selected = row.Facet.Kind == FacetKind.List ? row.Facet.Selected.Count : row.Facet.IsActive ? 1 : 0;
        if (selected > 0)
        {
            var pill = new Rectangle(b.Right - S(38), b.Y + (b.Height - S(18)) / 2, S(26), S(18));
            Chrome.FillRounded(g, pill, Chrome.Blend(Colors.Surface, Colors.Primary, 0.2), S(9));
            Chrome.DrawText(g, selected.ToString(CultureInfo.CurrentCulture), Font, pill, Colors.Primary, ContentAlignment.MiddleCenter);
        }

        PaintCurrent(g, b, index);
    }

    private void PaintValue(Graphics g, Row row, Rectangle b, int index)
    {
        var value = row.Value!;
        var selected = row.Facet.Selected.Contains(value.Value);
        var hovered = index == _hot && Enabled;
        var cell = Rectangle.Inflate(b, -S(6), -S(1));
        if (hovered || selected) Chrome.FillRounded(g, cell, selected ? Chrome.Blend(Colors.Surface, Colors.SelectionBack, Colors.HighContrast ? 1 : 0.5) : Colors.HoverBack, S(3));

        var box = new Rectangle(cell.X + S(8), cell.Y + (cell.Height - S(16)) / 2, S(16), S(16));
        Chrome.DrawCheckBox(g, Colors, box, selected ? CheckState.Checked : CheckState.Unchecked, Enabled, hovered);

        var count = _showCounts ? (_counts.TryGetValue((row.Facet.Field, FacetKey(value.Value)), out var c) ? c : value.Count) : -1;
        var countWidth = count >= 0 ? S(46) : 0;
        var muted = count == 0 && !selected;
        var color = muted ? Colors.Muted : selected && Colors.HighContrast ? Colors.SelectionText : Colors.Text;
        Chrome.DrawText(g, value.Text, Font, new Rectangle(box.Right + S(10), cell.Y, Math.Max(0, cell.Width - S(40) - countWidth), cell.Height), color);
        if (count >= 0) Chrome.DrawText(g, count.ToString(CultureInfo.CurrentCulture), Font, new Rectangle(cell.Right - countWidth - S(8), cell.Y, countWidth, cell.Height), Colors.Muted, ContentAlignment.MiddleRight);
        PaintCurrent(g, cell, index);
    }

    private void PaintMore(Graphics g, Row row, Rectangle b, int index)
    {
        var all = _showAll.Contains(row.Facet.Field);
        var hidden = row.Facet.Values.Count - _collapsedLimit;
        Chrome.DrawText(g, all ? "Show less" : $"Show {hidden} more", Font, new Rectangle(b.X + S(38), b.Y, Math.Max(0, b.Width - S(48)), b.Height), Colors.Primary);
        PaintCurrent(g, b, index);
    }

    private void PaintRangeCaption(Graphics g, Row row, Rectangle b)
    {
        if (row.Facet.Minimum is null) return;
        var text = $"{Convert.ToString(row.Facet.Minimum, CultureInfo.CurrentCulture)} – {Convert.ToString(row.Facet.Maximum, CultureInfo.CurrentCulture)}";
        Chrome.DrawText(g, text, Font, new Rectangle(b.X + S(16), b.Bottom - S(14), Math.Max(0, b.Width - S(32)), S(14)), Colors.Muted, ContentAlignment.TopLeft);
    }

    private void PaintCurrent(Graphics g, Rectangle b, int index)
    {
        if (index != _current || !Focused) return;
        using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        g.DrawRectangle(pen, b.X + 1, b.Y + 1, b.Width - 3, b.Height - 3);
    }

    // ---- input --------------------------------------------------------------------------

    private int RowIndexAt(Point location)
    {
        if (!RowsArea.Contains(location)) return -1;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Kind != RowKind.Range && ToControl(_rows[i].Bounds).Contains(location)) return i;
        }

        return -1;
    }

    private void Activate(int index)
    {
        if (index < 0 || index >= _rows.Count) return;
        var row = _rows[index];
        switch (row.Kind)
        {
            case RowKind.Header:
                ToggleSection(row.Facet.Field);
                break;
            case RowKind.Value:
                _model.Toggle(row.Facet.Field, row.Value!.Value, !row.Facet.Selected.Contains(row.Value.Value));
                break;
            case RowKind.More:
                if (!_showAll.Remove(row.Facet.Field)) _showAll.Add(row.Facet.Field);
                LayoutRows();
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = RowIndexAt(e.Location);
        var clear = ActiveCount > 0 && ClearRect().Contains(e.Location);
        if (index != _hot || clear != _hotClear)
        {
            _hot = index;
            _hotClear = clear;
            Cursor = index >= 0 || clear ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = -1;
        _hotClear = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!Enabled || e.Button != MouseButtons.Left) return;
        if (ActiveCount > 0 && ClearRect().Contains(e.Location))
        {
            ClearAll();
            return;
        }

        var index = RowIndexAt(e.Location);
        if (index < 0) return;
        _current = index;
        Activate(index);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Space or Keys.Enter or Keys.Delete
        || base.IsInputKey(keyData);

    private int NextFocusable(int from, int step)
    {
        for (var i = from + step; i >= 0 && i < _rows.Count; i += step)
        {
            if (_rows[i].Kind != RowKind.Range) return i;
        }

        return from;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0) return;
        switch (e.KeyCode)
        {
            case Keys.Down: _current = _current < 0 ? NextFocusable(-1, 1) : NextFocusable(_current, 1); break;
            case Keys.Up: _current = _current < 0 ? NextFocusable(-1, 1) : NextFocusable(_current, -1); break;
            case Keys.Home: _current = NextFocusable(-1, 1); break;
            case Keys.End: _current = NextFocusable(_rows.Count, -1); break;
            case Keys.Space or Keys.Enter when _current >= 0: Activate(_current); e.Handled = true; return;
            case Keys.Right when _current >= 0 && _rows[_current].Kind == RowKind.Header && _collapsed.Contains(_rows[_current].Facet.Field):
            case Keys.Left when _current >= 0 && _rows[_current].Kind == RowKind.Header && !_collapsed.Contains(_rows[_current].Facet.Field):
                Activate(_current);
                e.Handled = true;
                return;
            case Keys.Delete: ClearAll(); e.Handled = true; return;
            default: return;
        }

        if (_current >= 0) EnsureVisible(new Rectangle(0, _rows[_current].Bounds.Y, 1, _rows[_current].Bounds.Height + TitlePx));
        Invalidate();
        e.Handled = true;
    }
}
