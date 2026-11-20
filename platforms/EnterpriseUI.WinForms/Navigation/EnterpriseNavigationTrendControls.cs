using System.Collections;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Charts;
using EnterpriseUI.Navigation;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseBreadcrumbEventArgs : EventArgs
{
    public EnterpriseBreadcrumbEventArgs(BreadcrumbItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public BreadcrumbItem Item { get; }
    public int Index { get; }
}

/// <summary>Path / hierarchy breadcrumb: clickable segments, chevron separators and automatic collapsing of the leading segments into an overflow menu.</summary>
[DefaultEvent(nameof(ItemClicked))]
[ToolboxItem(true)]
public class EnterpriseBreadcrumbControl : EnterpriseControlBase
{
    private const string Ellipsis = "\u2026";
    private readonly List<(int Index, Rectangle Bounds)> _segments = new();
    private Rectangle _overflowBounds;
    private int _firstVisible;
    private int _hotIndex = -1;
    private bool _hotOverflow;
    private int _focusIndex = -1;

    public EnterpriseBreadcrumbControl() : this(new EnterpriseBreadcrumb())
    {
    }

    public EnterpriseBreadcrumbControl(EnterpriseBreadcrumb model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Size = new Size(360, 32);
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = "Breadcrumb";
    }

    /// <summary>The headless model that owns the segments.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseBreadcrumb Model { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<BreadcrumbItem> Items => Model.Items;

    [DefaultValue(false)]
    public bool TruncateOnClick { get; set; }

    [DefaultValue(false)]
    public bool ShowFrame { get; set; }

    public event EventHandler<EnterpriseBreadcrumbEventArgs>? ItemClicked;

    public BreadcrumbItem Add(string text, object? value = null, string? icon = null)
    {
        var item = new BreadcrumbItem { Text = text, Value = value, Icon = icon };
        Model.Items.Add(item);
        RefreshSegments();
        return item;
    }

    /// <summary>Replaces the segments with the parts of a delimited path.</summary>
    public void SetPath(string path, char separator = '/')
    {
        Model.Items.Clear();
        var built = string.Empty;
        foreach (var part in (path ?? string.Empty).Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
        {
            built = built.Length == 0 ? part : built + separator + part;
            Model.Items.Add(new BreadcrumbItem { Text = part, Value = built });
        }

        RefreshSegments();
    }

    public void Clear()
    {
        Model.Items.Clear();
        RefreshSegments();
    }

    public void RefreshSegments()
    {
        Relayout();
        Invalidate();
    }

    private int SegmentWidth(string text) => Chrome.MeasureText(text, Font) + S(16);

    private void Relayout()
    {
        _segments.Clear();
        _overflowBounds = Rectangle.Empty;
        _firstVisible = 0;
        var items = Model.Items;
        if (items.Count == 0 || Width <= 0) return;

        var sep = S(18);
        var available = Width - S(8);
        var widths = items.Select(i => SegmentWidth(i.Text)).ToArray();
        var overflowWidth = SegmentWidth(Ellipsis) + sep;
        var first = 0;
        while (first < items.Count - 1)
        {
            var total = (first > 0 ? overflowWidth : 0);
            for (var i = first; i < items.Count; i++) total += widths[i] + (i < items.Count - 1 ? sep : 0);
            if (total <= available) break;
            first++;
        }

        _firstVisible = first;
        var x = S(4);
        var h = Height - S(6);
        var y = S(3);
        if (first > 0)
        {
            _overflowBounds = new Rectangle(x, y, SegmentWidth(Ellipsis), h);
            x += _overflowBounds.Width + sep;
        }

        for (var i = first; i < items.Count; i++)
        {
            var width = widths[i];
            if (i == items.Count - 1 && x + width > Width - S(4)) width = Math.Max(S(24), Width - S(4) - x);
            _segments.Add((i, new Rectangle(x, y, width, h)));
            x += width + sep;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Relayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Relayout();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = _segments.Where(s => s.Bounds.Contains(e.Location)).Select(s => s.Index).DefaultIfEmpty(-1).First();
        var overflow = _overflowBounds.Contains(e.Location);
        if (hot == _hotIndex && overflow == _hotOverflow) return;
        _hotIndex = hot;
        _hotOverflow = overflow;
        Cursor = hot >= 0 || overflow ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotIndex = -1;
        _hotOverflow = false;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (_overflowBounds.Contains(e.Location))
        {
            ShowOverflow();
            return;
        }

        foreach (var (index, bounds) in _segments)
        {
            if (!bounds.Contains(e.Location)) continue;
            Activate(index);
            return;
        }
    }

    private void ShowOverflow()
    {
        var menu = new ContextMenuStrip();
        EnterpriseMenuRenderer.Apply(menu);
        for (var i = 0; i < _firstVisible; i++)
        {
            var index = i;
            menu.Items.Add(new ToolStripMenuItem(Model.Items[i].Text, null, (_, _) => Activate(index)));
        }

        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(this, new Point(_overflowBounds.X, _overflowBounds.Bottom));
    }

    /// <summary>Activates a segment as if it was clicked: runs its command and raises <see cref="ItemClicked"/>.</summary>
    public void PerformItemClick(int index) => Activate(index);

    private void Activate(int index)
    {
        if (index < 0 || index >= Model.Items.Count) return;
        var item = Model.Items[index];
        _focusIndex = index;
        if (item.Command?.CanExecute(item.Value) == true) item.Command.Execute(item.Value);
        ItemClicked?.Invoke(this, new EnterpriseBreadcrumbEventArgs(item, index));
        if (TruncateOnClick && index < Model.Items.Count - 1)
        {
            Model.Items.RemoveRange(index + 1, Model.Items.Count - index - 1);
            RefreshSegments();
        }
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var last = Model.Items.Count - 1;
        if (last < 0) return;
        if (_focusIndex < 0) _focusIndex = last;
        switch (e.KeyCode)
        {
            case Keys.Left: _focusIndex = Math.Max(0, _focusIndex - 1); break;
            case Keys.Right: _focusIndex = Math.Min(last, _focusIndex + 1); break;
            case Keys.Home: _focusIndex = 0; break;
            case Keys.End: _focusIndex = last; break;
            case Keys.Enter or Keys.Space: Activate(_focusIndex); break;
            default: return;
        }

        if (_focusIndex < _firstVisible) _firstVisible = _focusIndex;
        e.Handled = true;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? c.Surface);
        if (ShowFrame) Chrome.DrawFrame(g, new Rectangle(0, 0, Width, Height), c, Focused, IsHovered, Chrome.CornerRadius, Enabled);
        else g.Clear(c.Surface);

        var sepWidth = S(18);
        if (_overflowBounds != Rectangle.Empty)
        {
            DrawSegment(g, _overflowBounds, Ellipsis, _hotOverflow, false, false);
            Chrome.DrawChevron(g, new Rectangle(_overflowBounds.Right, _overflowBounds.Y, sepWidth, _overflowBounds.Height), ChevronDirection.Right, c.Muted);
        }

        for (var i = 0; i < _segments.Count; i++)
        {
            var (index, bounds) = _segments[i];
            var last = index == Model.Items.Count - 1;
            DrawSegment(g, bounds, Model.Items[index].Text, _hotIndex == index, last, Focused && _focusIndex == index);
            if (!last) Chrome.DrawChevron(g, new Rectangle(bounds.Right, bounds.Y, sepWidth, bounds.Height), ChevronDirection.Right, c.Muted);
        }
    }

    private void DrawSegment(Graphics g, Rectangle bounds, string text, bool hot, bool current, bool focused)
    {
        var c = Colors;
        if (hot && Enabled) Chrome.FillRounded(g, bounds, c.HoverBack, Chrome.CornerRadius);
        if (focused) Chrome.DrawFocusRing(g, bounds, c);
        Chrome.DrawText(g, text, current ? BoldFont : Font, bounds, !Enabled ? c.Disabled : current ? c.Text : hot ? c.Primary : c.Muted, ContentAlignment.MiddleCenter);
    }
}

/// <summary>Tiny inline chart (line, area, column or win/loss) for dashboards, lists and grid cells.</summary>
[DefaultProperty(nameof(Kind))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseControlDesigner<EnterpriseUI.WinForms.Design.SparklineActions>))]
[ToolboxItem(true)]
public class EnterpriseSparklineControl : EnterpriseControlBase
{
    private int _hotIndex = -1;
    private Color? _accent;
    private readonly ToolTip _tip = new();

    public EnterpriseSparklineControl() : this(new EnterpriseSparkline())
    {
    }

    public EnterpriseSparklineControl(EnterpriseSparkline model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
        Size = new Size(120, 32);
        AccessibleRole = AccessibleRole.Chart;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseSparkline Model { get; }

    [DefaultValue(EnterpriseSparkline.SparklineKind.Line)]
    public EnterpriseSparkline.SparklineKind Kind { get => Model.Kind; set { Model.Kind = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowHighLowIndicators { get => Model.ShowHighLowIndicators; set { Model.ShowHighLowIndicators = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowFirstLastIndicators { get => Model.ShowFirstLastIndicators; set { Model.ShowFirstLastIndicators = value; Invalidate(); } }

    [DefaultValue(false)]
    public bool ShowFrame { get; set; }

    /// <summary>Series color; the theme accent when empty.</summary>
    public Color AccentColor
    {
        get => _accent ?? Colors.Primary;
        set { _accent = value; Invalidate(); }
    }

    public bool ShouldSerializeAccentColor() => _accent.HasValue;

    public void ResetAccentColor() => _accent = null;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<double> Values => Model.Values;

    public void SetValues(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Model.Values.Clear();
        Model.Values.AddRange(values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
        AccessibleDescription = Model.Values.Count == 0 ? "No data" : $"{Model.Values.Count} points, from {Model.Values[0]:0.##} to {Model.Values[^1]:0.##}";
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BackColor = Color.Transparent;
    }

    protected override void OnThemeApplied() => BackColor = Color.Transparent;

    private Rectangle Plot => new(S(3), S(4), Math.Max(1, Width - S(6)), Math.Max(1, Height - S(8)));

    private PointF PointAt(int index, double min, double max)
    {
        var plot = Plot;
        var count = Model.Values.Count;
        var x = count <= 1 ? plot.X + plot.Width / 2f : plot.X + plot.Width * index / (float)(count - 1);
        var range = max - min;
        var y = range <= 0 ? plot.Y + plot.Height / 2f : (float)(plot.Bottom - (Model.Values[index] - min) / range * plot.Height);
        return new PointF(x, y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var count = Model.Values.Count;
        if (count == 0) return;
        var plot = Plot;
        var index = count == 1 ? 0 : (int)Math.Round(Math.Clamp((e.X - plot.X) / (double)Math.Max(1, plot.Width), 0, 1) * (count - 1));
        if (Kind is EnterpriseSparkline.SparklineKind.Column or EnterpriseSparkline.SparklineKind.WinLoss)
            index = Math.Clamp((e.X - plot.X) * count / Math.Max(1, plot.Width), 0, count - 1);
        if (index == _hotIndex) return;
        _hotIndex = index;
        _tip.SetToolTip(this, Model.Values[index].ToString("0.##", System.Globalization.CultureInfo.CurrentCulture));
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotIndex = -1;
        _tip.SetToolTip(this, null);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (ShowFrame)
        {
            g.Clear(Parent?.BackColor ?? c.Surface);
            Chrome.DrawFrame(g, new Rectangle(0, 0, Width, Height), c, false, IsHovered, Chrome.CornerRadius, Enabled);
        }

        var values = Model.Values;
        if (values.Count == 0) return;
        var accent = Enabled ? AccentColor : c.Disabled;
        double min = values.Min(), max = values.Max();
        var plot = Plot;
        switch (Model.Kind)
        {
            case EnterpriseSparkline.SparklineKind.Column:
            case EnterpriseSparkline.SparklineKind.WinLoss:
                DrawBars(g, accent, min, max, plot);
                break;
            default:
                DrawLineOrArea(g, accent, min, max, plot);
                break;
        }
    }

    private void DrawBars(Graphics g, Color accent, double min, double max, Rectangle plot)
    {
        var c = Colors;
        var values = Model.Values;
        var slot = plot.Width / (float)values.Count;
        var width = Math.Max(1f, slot - Math.Max(1, S(1)));
        var winLoss = Model.Kind == EnterpriseSparkline.SparklineKind.WinLoss;
        var baseline = winLoss ? plot.Y + plot.Height / 2f : (min < 0 && max > 0 ? (float)(plot.Bottom - (0 - min) / (max - min) * plot.Height) : plot.Bottom);
        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            float top, bottom;
            if (winLoss)
            {
                var half = plot.Height / 2f - 1;
                if (v == 0) { top = baseline - 1; bottom = baseline + 1; }
                else if (v > 0) { top = baseline - half; bottom = baseline; }
                else { top = baseline; bottom = baseline + half; }
            }
            else
            {
                var y = max - min <= 0 ? plot.Y + plot.Height / 2f : (float)(plot.Bottom - (v - min) / (max - min) * plot.Height);
                top = Math.Min(y, baseline);
                bottom = Math.Max(y, baseline);
                if (bottom - top < 1) bottom = top + 1;
            }

            var color = v < 0 ? c.Danger : accent;
            if (i == _hotIndex) color = Chrome.Blend(color, c.Text, 0.35);
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, plot.X + i * slot, top, width, bottom - top);
        }
    }

    private void DrawLineOrArea(Graphics g, Color accent, double min, double max, Rectangle plot)
    {
        var c = Colors;
        var values = Model.Values;
        var points = Enumerable.Range(0, values.Count).Select(i => PointAt(i, min, max)).ToArray();
        if (points.Length == 1) points = new[] { new PointF(plot.X, points[0].Y), new PointF(plot.Right, points[0].Y) };
        if (Model.Kind == EnterpriseSparkline.SparklineKind.Area)
        {
            var area = points.Concat(new[] { new PointF(points[^1].X, plot.Bottom), new PointF(points[0].X, plot.Bottom) }).ToArray();
            using var brush = new SolidBrush(Color.FromArgb(70, accent));
            g.FillPolygon(brush, area);
        }

        using (var pen = new Pen(accent, Math.Max(1.5f, S(1.5))) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLines(pen, points);

        void Dot(int index, Color color)
        {
            var p = PointAt(index, min, max);
            var r = Math.Max(2.5f, S(2.5));
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, p.X - r, p.Y - r, r * 2, r * 2);
        }

        if (values.Count > 1 && Model.ShowFirstLastIndicators)
        {
            Dot(0, c.Muted);
            Dot(values.Count - 1, accent);
        }

        if (Model.ShowHighLowIndicators && values.Count > 1)
        {
            Dot(values.ToList().IndexOf(max), c.Success);
            Dot(values.ToList().IndexOf(min), c.Danger);
        }

        if (_hotIndex >= 0 && _hotIndex < values.Count) Dot(_hotIndex, c.Text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class EnterpriseNavigatorCancelEventArgs : CancelEventArgs
{
    public EnterpriseNavigatorCancelEventArgs(object? item) => Item = item;

    public object? Item { get; }
}

/// <summary>Record navigator for a bound list: first, previous, position, next, last, add and delete.</summary>
[DefaultEvent(nameof(PositionChanged))]
[ToolboxItem(true)]
public class EnterpriseBindingNavigatorControl : EnterpriseControlBase
{
    private enum Action { First, Previous, Next, Last, Add, Delete }

    private readonly Dictionary<Action, Rectangle> _buttons = new();
    private readonly BindingSource _ownedSource = new();
    private BindingSource? _source;
    private Action? _hot;
    private Action? _pressed;
    private Rectangle _positionBounds;
    private bool _showAddDelete = true;

    public EnterpriseBindingNavigatorControl()
    {
        Size = new Size(320, 34);
        AccessibleRole = AccessibleRole.ToolBar;
        AccessibleName = "Record navigator";
        BindingSource = _ownedSource;
    }

    /// <summary>List source the navigator moves through; assign a <see cref="BindingSource"/> shared with the bound controls.</summary>
    [DefaultValue(null)]
    public BindingSource? BindingSource
    {
        get => _source == _ownedSource ? null : _source;
        set
        {
            if (_source is not null)
            {
                _source.PositionChanged -= OnSourceChanged;
                _source.ListChanged -= OnSourceListChanged;
            }

            _source = value ?? _ownedSource;
            _source.PositionChanged += OnSourceChanged;
            _source.ListChanged += OnSourceListChanged;
            Relayout();
            Invalidate();
        }
    }

    /// <summary>Convenience for lists that are not wrapped in a binding source yet.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? DataSource
    {
        get => (_source ?? _ownedSource).DataSource;
        set
        {
            if (_source != _ownedSource) BindingSource = null;
            _ownedSource.DataSource = value;
            Relayout();
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool ShowAddDelete { get => _showAddDelete; set { _showAddDelete = value; Relayout(); Invalidate(); } }

    [DefaultValue(false)]
    public bool ConfirmDelete { get; set; }

    [Browsable(false)]
    public int Position => _source?.Position ?? -1;

    [Browsable(false)]
    public int Count => _source?.Count ?? 0;

    public event EventHandler? PositionChanged;
    public event EventHandler<EnterpriseNavigatorCancelEventArgs>? ItemAdding;
    public event EventHandler<EnterpriseNavigatorCancelEventArgs>? ItemDeleting;

    public void MoveFirst() => _source?.MoveFirst();
    public void MovePrevious() => _source?.MovePrevious();
    public void MoveNext() => _source?.MoveNext();
    public void MoveLast() => _source?.MoveLast();

    public bool AddNew()
    {
        if (_source is null) return false;
        var args = new EnterpriseNavigatorCancelEventArgs(null);
        ItemAdding?.Invoke(this, args);
        if (args.Cancel) return false;
        try
        {
            _source.AddNew();
            _source.EndEdit();
            _source.MoveLast();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public bool DeleteCurrent()
    {
        if (_source is null || _source.Position < 0 || _source.Count == 0) return false;
        var args = new EnterpriseNavigatorCancelEventArgs(_source.Current);
        ItemDeleting?.Invoke(this, args);
        if (args.Cancel) return false;
        if (ConfirmDelete && EnterpriseMessageBox.ShowMessage("Delete the current record?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question, null, FindForm()) != DialogResult.Yes) return false;
        try
        {
            _source.RemoveCurrent();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        Invalidate();
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSourceListChanged(object? sender, ListChangedEventArgs e)
    {
        Relayout();
        Invalidate();
    }

    private string PositionText => Count == 0 ? "0 of 0" : $"{Position + 1} of {Count}";

    private bool IsEnabledAction(Action action) => Enabled && action switch
    {
        Action.First or Action.Previous => Position > 0,
        Action.Next or Action.Last => Position >= 0 && Position < Count - 1,
        Action.Add => _source?.AllowNew != false && _source?.DataSource is not null,
        _ => Count > 0 && _source?.AllowRemove != false
    };

    private void Relayout()
    {
        _buttons.Clear();
        var size = Math.Min(Height - S(6), S(30));
        var y = (Height - size) / 2;
        var x = S(4);
        void Place(Action action)
        {
            _buttons[action] = new Rectangle(x, y, size, size);
            x += size + S(2);
        }

        Place(Action.First);
        Place(Action.Previous);
        var positionWidth = Math.Max(S(72), Chrome.MeasureText(PositionText, Font) + S(20));
        _positionBounds = new Rectangle(x, y, positionWidth, size);
        x += positionWidth + S(2);
        Place(Action.Next);
        Place(Action.Last);
        if (_showAddDelete)
        {
            x += S(8);
            Place(Action.Add);
            Place(Action.Delete);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    private Action? HitTest(Point p)
    {
        foreach (var pair in _buttons)
        {
            if (pair.Value.Contains(p)) return pair.Key;
        }

        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (hit == _hot) return;
        _hot = hit;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _pressed = e.Button == MouseButtons.Left ? HitTest(e.Location) : null;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var action = _pressed;
        _pressed = null;
        Invalidate();
        if (action is not null && action == HitTest(e.Location) && IsEnabledAction(action.Value)) Perform(action.Value);
    }

    private void Perform(Action action)
    {
        switch (action)
        {
            case Action.First: MoveFirst(); break;
            case Action.Previous: MovePrevious(); break;
            case Action.Next: MoveNext(); break;
            case Action.Last: MoveLast(); break;
            case Action.Add: AddNew(); break;
            default: DeleteCurrent(); break;
        }
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Home or Keys.End or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Home: MoveFirst(); break;
            case Keys.End: MoveLast(); break;
            case Keys.Left: MovePrevious(); break;
            case Keys.Right: MoveNext(); break;
            case Keys.Insert when _showAddDelete: AddNew(); break;
            case Keys.Delete when _showAddDelete: DeleteCurrent(); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        PaintFrame(g);
        foreach (var pair in _buttons)
        {
            var enabled = IsEnabledAction(pair.Key);
            var rect = pair.Value;
            if (enabled && (_hot == pair.Key || _pressed == pair.Key))
                Chrome.FillRounded(g, rect, _pressed == pair.Key ? c.SelectionBack : c.HoverBack, Chrome.CornerRadius);
            DrawGlyph(g, pair.Key, rect, !enabled ? c.Disabled : pair.Key == Action.Delete ? c.Danger : c.Text);
        }

        Chrome.DrawText(g, PositionText, Font, _positionBounds, Enabled ? c.Text : c.Disabled, ContentAlignment.MiddleCenter);
        if (_showAddDelete && _buttons.TryGetValue(Action.Add, out var add))
        {
            using var pen = new Pen(c.Border);
            g.DrawLine(pen, add.X - S(5), add.Y + S(4), add.X - S(5), add.Bottom - S(4));
        }
    }

    private void DrawGlyph(Graphics g, Action action, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.6f, S(1.6))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var cx = rect.X + rect.Width / 2f;
        var cy = rect.Y + rect.Height / 2f;
        var r = rect.Width / 5f;
        void Tri(float x, bool left)
        {
            var d = left ? -1 : 1;
            g.DrawLines(pen, new[] { new PointF(x - d * r * 0.5f, cy - r), new PointF(x + d * r * 0.5f, cy), new PointF(x - d * r * 0.5f, cy + r) });
        }

        switch (action)
        {
            case Action.First:
                g.DrawLine(pen, cx - r * 1.3f, cy - r, cx - r * 1.3f, cy + r);
                Tri(cx - r * 0.1f, true);
                Tri(cx + r * 0.9f, true);
                break;
            case Action.Previous:
                Tri(cx, true);
                break;
            case Action.Next:
                Tri(cx, false);
                break;
            case Action.Last:
                g.DrawLine(pen, cx + r * 1.3f, cy - r, cx + r * 1.3f, cy + r);
                Tri(cx + r * 0.1f, false);
                Tri(cx - r * 0.9f, false);
                break;
            case Action.Add:
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
                g.DrawLine(pen, cx, cy - r, cx, cy + r);
                break;
            default:
                g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_source is not null)
            {
                _source.PositionChanged -= OnSourceChanged;
                _source.ListChanged -= OnSourceListChanged;
            }

            _ownedSource.Dispose();
        }

        base.Dispose(disposing);
    }
}
