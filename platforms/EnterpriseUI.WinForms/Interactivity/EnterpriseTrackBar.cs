using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum TrackSelectionMode { Single, Range }

public enum TrackTickStyle { None, TopLeft, BottomRight, Both }

/// <summary>Tooltip painted in the control palette, used for slider values and rating hints.</summary>
internal sealed class ThemedToolTip : IDisposable
{
    private readonly ToolTip _tip = new() { OwnerDraw = true, ShowAlways = true, UseAnimation = false, UseFading = false };
    private readonly Func<ControlColors> _colors;
    private readonly Func<Font> _font;
    private string _text = string.Empty;
    private Control? _shownOn;

    public ThemedToolTip(Func<ControlColors> colors, Func<Font> font)
    {
        _colors = colors;
        _font = font;
        _tip.Popup += (_, e) => e.ToolTipSize = TextRenderer.MeasureText(_text, _font(), Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix) + new Size(16, 10);
        _tip.Draw += (_, e) =>
        {
            var c = _colors();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var back = c.HighContrast ? SystemColors.Window : Chrome.Blend(c.Text, c.Surface, 0.08);
            var fore = c.HighContrast ? SystemColors.WindowText : c.Surface;
            using (var brush = new SolidBrush(back)) g.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(g, _text, _font(), e.Bounds, fore, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    public void Show(Control owner, string text, Point location)
    {
        if (!owner.IsHandleCreated || text.Length == 0) return;
        _text = text;
        _shownOn = owner;
        _tip.Show(text, owner, location);
    }

    public void Hide()
    {
        if (_shownOn is { IsDisposed: false } owner) _tip.Hide(owner);
        _shownOn = null;
    }

    public void Dispose() => _tip.Dispose();
}

/// <summary>Slider with a single thumb or a selectable range, discrete steps, tick marks and a value tooltip; horizontal or vertical (minimum at the bottom).</summary>
public class EnterpriseTrackBarControl : EnterpriseControlBase
{
    private readonly TrackScale _scale = new();
    private readonly ValueAnimator[] _thumbHover = { new(), new() };
    private readonly ThemedToolTip _tip;
    private readonly System.Windows.Forms.Timer _tipTimer = new() { Interval = 1200 };
    private double _low;
    private double _high = 100;
    private TrackSelectionMode _mode;
    private Orientation _orientation = Orientation.Horizontal;
    private TrackTickStyle _ticks = TrackTickStyle.None;
    private double _tickFrequency = 10;
    private double _minimumGap;
    private bool _showTooltip = true;
    private bool _showTickLabels;
    private string _valueFormat = "{0:0.##}";
    private int _activeThumb = 1;
    private int _dragThumb = -1;
    private int _hoverThumb = -1;
    private int _grabOffset;

    public EnterpriseTrackBarControl()
    {
        Size = new Size(260, 44);
        AccessibleRole = AccessibleRole.Slider;
        Cursor = Cursors.Hand;
        _tip = new ThemedToolTip(() => Colors, () => Font);
        _tipTimer.Tick += (_, _) => { _tipTimer.Stop(); _tip.Hide(); };
        for (var i = 0; i < 2; i++) _thumbHover[i].Updated += (_, _) => Invalidate();
        _scale.SmallChange = 1;
        _scale.LargeChange = 10;
        _high = 0;
    }

    [DefaultValue(0.0)]
    public double Minimum
    {
        get => _scale.Minimum;
        set
        {
            _scale.Minimum = value;
            if (_scale.Maximum < value) _scale.Maximum = value;
            Resnap();
        }
    }

    [DefaultValue(100.0)]
    public double Maximum
    {
        get => _scale.Maximum;
        set
        {
            _scale.Maximum = value;
            if (_scale.Minimum > value) _scale.Minimum = value;
            Resnap();
        }
    }

    [DefaultValue(1.0)]
    public double SmallChange { get => _scale.SmallChange; set => _scale.SmallChange = Math.Max(0.000001, value); }

    [DefaultValue(10.0)]
    public double LargeChange { get => _scale.LargeChange; set => _scale.LargeChange = Math.Max(0.000001, value); }

    /// <summary>Discrete step (values are multiples of it from the minimum); 0 is continuous.</summary>
    [DefaultValue(0.0)]
    public double Step
    {
        get => _scale.Step;
        set { _scale.Step = Math.Max(0, value); Resnap(); }
    }

    [DefaultValue(TrackSelectionMode.Single)]
    public TrackSelectionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            _activeThumb = 1;
            if (value == TrackSelectionMode.Range && _high <= _scale.Minimum) _high = _scale.Maximum;
            Resnap();
            Invalidate();
        }
    }

    /// <summary>Value of the single thumb (in range mode: the same as <see cref="ValueHigh"/>).</summary>
    [DefaultValue(0.0)]
    public double Value
    {
        get => _mode == TrackSelectionMode.Single ? _high : _low;
        set => SetThumb(_mode == TrackSelectionMode.Single ? 1 : 0, value);
    }

    [DefaultValue(0.0)]
    public double ValueLow
    {
        get => _low;
        set => SetThumb(0, value);
    }

    [DefaultValue(0.0)]
    public double ValueHigh
    {
        get => _high;
        set => SetThumb(1, value);
    }

    /// <summary>Smallest distance the two thumbs may have in range mode.</summary>
    [DefaultValue(0.0)]
    public double MinimumGap
    {
        get => _minimumGap;
        set { _minimumGap = Math.Max(0, value); Resnap(); }
    }

    [DefaultValue(Orientation.Horizontal)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value) return;
            _orientation = value;
            Size = new Size(Height, Width);
            Invalidate();
        }
    }

    [DefaultValue(TrackTickStyle.None)]
    public TrackTickStyle TickStyle
    {
        get => _ticks;
        set { _ticks = value; Invalidate(); }
    }

    [DefaultValue(10.0)]
    public double TickFrequency
    {
        get => _tickFrequency;
        set { _tickFrequency = Math.Max(0, value); Invalidate(); }
    }

    [DefaultValue(false)]
    public bool ShowTickLabels
    {
        get => _showTickLabels;
        set { _showTickLabels = value; Invalidate(); }
    }

    [DefaultValue(true)]
    public bool ShowTooltip
    {
        get => _showTooltip;
        set { _showTooltip = value; if (!value) _tip.Hide(); }
    }

    [DefaultValue("{0:0.##}")]
    public string ValueFormat
    {
        get => _valueFormat;
        set => _valueFormat = string.IsNullOrEmpty(value) ? "{0}" : value;
    }

    [Browsable(false)]
    public Func<double, string>? ValueFormatter { get; set; }

    public event EventHandler? ValueChanged;

    /// <summary>Raised when a drag ends or a keyboard step finishes; use it instead of <see cref="ValueChanged"/> for expensive work.</summary>
    public event EventHandler? ValueCommitted;

    public string Format(double value)
    {
        if (ValueFormatter != null) return ValueFormatter(value);
        try { return string.Format(CultureInfo.CurrentCulture, _valueFormat, value); }
        catch (FormatException) { return value.ToString(CultureInfo.CurrentCulture); }
    }

    private void Resnap()
    {
        var oldLow = _low;
        var oldHigh = _high;
        _high = _scale.Snap(_high);
        _low = _mode == TrackSelectionMode.Range ? Math.Min(_scale.Snap(_low), _high - _minimumGap) : _scale.Minimum;
        if (_mode == TrackSelectionMode.Range) _low = Math.Max(_scale.Minimum, _low);
        Invalidate();
        if (oldLow != _low || oldHigh != _high) RaiseChanged();
    }

    private void RaiseChanged()
    {
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetThumb(int index, double value)
    {
        value = _scale.Snap(value);
        if (_mode == TrackSelectionMode.Single) index = 1;
        if (index == 0) value = Math.Min(value, _high - _minimumGap);
        else if (_mode == TrackSelectionMode.Range) value = Math.Max(value, _low + _minimumGap);
        value = Math.Clamp(value, _scale.Minimum, _scale.Maximum);
        ref var target = ref (index == 0 ? ref _low : ref _high);
        if (target == value) return;
        target = value;
        Invalidate();
        RaiseChanged();
    }

    private bool Horizontal => _orientation == Orientation.Horizontal;
    private int ThumbSize => S(18);
    private int EdgeMargin => ThumbSize / 2 + S(4);
    private bool TicksAbove => _ticks is TrackTickStyle.TopLeft or TrackTickStyle.Both;
    private bool TicksBelow => _ticks is TrackTickStyle.BottomRight or TrackTickStyle.Both;

    private int LineStart => EdgeMargin;
    private int LineLength => Math.Max(1, (Horizontal ? Width : Height) - EdgeMargin * 2);

    private int CrossCenter
    {
        get
        {
            var extent = Horizontal ? Height : Width;
            var shift = (TicksBelow ? S(6) : 0) - (TicksAbove ? S(6) : 0);
            if (_showTickLabels && TicksBelow) shift += S(4);
            return extent / 2 - shift / 2;
        }
    }

    private PointF ThumbCenter(double value)
    {
        var fraction = _scale.ToFraction(value);
        var main = Horizontal ? LineStart + fraction * LineLength : LineStart + (1 - fraction) * LineLength;
        return Horizontal ? new PointF((float)main, CrossCenter) : new PointF(CrossCenter, (float)main);
    }

    private double ValueAtPoint(Point p, int grabOffset = 0)
    {
        var main = Horizontal ? p.X - grabOffset : p.Y - grabOffset;
        var fraction = (main - LineStart) / (double)LineLength;
        return _scale.FromFraction(Horizontal ? fraction : 1 - fraction);
    }

    private double ThumbValue(int index) => index == 0 ? _low : _high;

    private int NearestThumb(Point p)
    {
        if (_mode == TrackSelectionMode.Single) return 1;
        var pl = ThumbCenter(_low);
        var ph = ThumbCenter(_high);
        var dl = Horizontal ? Math.Abs(p.X - pl.X) : Math.Abs(p.Y - pl.Y);
        var dh = Horizontal ? Math.Abs(p.X - ph.X) : Math.Abs(p.Y - ph.Y);
        if (Math.Abs(dl - dh) < 0.5) return Horizontal ? (p.X > pl.X ? 1 : 0) : (p.Y < pl.Y ? 1 : 0);
        return dl < dh ? 0 : 1;
    }

    private bool OnThumb(Point p, int index)
    {
        var c = ThumbCenter(ThumbValue(index));
        var r = ThumbSize / 2f + S(3);
        return (p.X - c.X) * (p.X - c.X) + (p.Y - c.Y) * (p.Y - c.Y) <= r * r;
    }

    private void ShowTip(int index)
    {
        if (!_showTooltip) return;
        var c = ThumbCenter(ThumbValue(index));
        var text = Format(ThumbValue(index));
        var size = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var location = Horizontal ? new Point((int)c.X - (size.Width + 16) / 2, (int)c.Y - ThumbSize - size.Height - S(14)) : new Point((int)c.X + ThumbSize, (int)c.Y - (size.Height + 10) / 2);
        _tip.Show(this, text, location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        var thumb = NearestThumb(e.Location);
        _grabOffset = 0;
        if (OnThumb(e.Location, thumb))
        {
            var c = ThumbCenter(ThumbValue(thumb));
            _grabOffset = (int)((Horizontal ? e.X - c.X : e.Y - c.Y));
        }
        else SetThumb(thumb, ValueAtPoint(e.Location));

        _activeThumb = thumb;
        _dragThumb = thumb;
        _thumbHover[thumb].AnimateTo(1, 80);
        Capture = true;
        ShowTip(thumb);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragThumb >= 0)
        {
            SetThumb(_dragThumb, ValueAtPoint(e.Location, _grabOffset));
            ShowTip(_dragThumb);
            return;
        }

        var hover = -1;
        if (Enabled)
        {
            if (_mode == TrackSelectionMode.Range && OnThumb(e.Location, 0)) hover = 0;
            if (OnThumb(e.Location, 1)) hover = 1;
        }

        if (hover == _hoverThumb) return;
        if (_hoverThumb >= 0) _thumbHover[_hoverThumb].AnimateTo(0, 140);
        _hoverThumb = hover;
        if (hover >= 0)
        {
            _thumbHover[hover].AnimateTo(1, 120);
            ShowTip(hover);
        }
        else _tip.Hide();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragThumb < 0) return;
        _dragThumb = -1;
        Capture = false;
        if (_hoverThumb < 0) _tip.Hide();
        for (var i = 0; i < 2; i++) if (i != _hoverThumb) _thumbHover[i].AnimateTo(0, 140);
        ValueCommitted?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragThumb >= 0) return;
        if (_hoverThumb >= 0) _thumbHover[_hoverThumb].AnimateTo(0, 140);
        _hoverThumb = -1;
        _tip.Hide();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Enabled || !Focused && !ClientRectangle.Contains(PointToClient(MousePosition))) return;
        Nudge(e.Delta > 0 ? 1 : -1, large: false);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown => true,
        Keys.Tab => _mode == TrackSelectionMode.Range && ((keyData & Keys.Shift) == 0 ? _activeThumb == 0 : _activeThumb == 1),
        _ => base.IsInputKey(keyData)
    };

    private void Nudge(int direction, bool large)
    {
        var index = _mode == TrackSelectionMode.Single ? 1 : _activeThumb;
        var amount = large ? _scale.LargeChange : _scale.SmallChange;
        if (_scale.Step > 0) amount = Math.Max(amount, _scale.Step);
        SetThumb(index, ThumbValue(index) + direction * amount);
        ShowTip(index);
        _tipTimer.Stop();
        _tipTimer.Start();
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled) return;
        var index = _mode == TrackSelectionMode.Single ? 1 : _activeThumb;
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Nudge(-1, false); break;
            case Keys.Right or Keys.Up: Nudge(1, false); break;
            case Keys.PageDown: Nudge(-1, true); break;
            case Keys.PageUp: Nudge(1, true); break;
            case Keys.Home: SetThumb(index, _scale.Minimum); ValueCommitted?.Invoke(this, EventArgs.Empty); break;
            case Keys.End: SetThumb(index, _scale.Maximum); ValueCommitted?.Invoke(this, EventArgs.Empty); break;
            case Keys.Tab when _mode == TrackSelectionMode.Range:
                if (!e.Shift && _activeThumb == 0) _activeThumb = 1;
                else if (e.Shift && _activeThumb == 1) _activeThumb = 0;
                else return;
                Invalidate();
                break;
            default: return;
        }

        e.Handled = true;
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _tip.Hide();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this,
        () => _mode == TrackSelectionMode.Range ? $"{Format(_low)} to {Format(_high)}" : Format(_high),
        null,
        text =>
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)) Value = parsed;
        });

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var thickness = Math.Max(3, S(4));
        var cross = CrossCenter;

        var start = ThumbCenter(_mode == TrackSelectionMode.Range ? _low : _scale.Minimum);
        var end = ThumbCenter(_high);
        Rectangle Segment(PointF a, PointF b) => Horizontal
            ? new Rectangle((int)Math.Min(a.X, b.X), cross - thickness / 2, (int)Math.Abs(b.X - a.X), thickness)
            : new Rectangle(cross - thickness / 2, (int)Math.Min(a.Y, b.Y), thickness, (int)Math.Abs(b.Y - a.Y));

        var groove = Horizontal ? new Rectangle(LineStart, cross - thickness / 2, LineLength, thickness) : new Rectangle(cross - thickness / 2, LineStart, thickness, LineLength);
        Chrome.FillRounded(g, groove, Chrome.Blend(c.Border, c.Muted, c.HighContrast ? 0 : 0.35), thickness / 2);
        var selected = Segment(start, end);
        if (selected.Width > 0 && selected.Height > 0) Chrome.FillRounded(g, selected, Enabled ? c.Primary : c.Disabled, thickness / 2);

        PaintTicks(g, c, cross);

        for (var i = _mode == TrackSelectionMode.Range ? 0 : 1; i < 2; i++) PaintThumb(g, c, i);
    }

    private void PaintTicks(Graphics g, ControlColors c, int cross)
    {
        if (_ticks == TrackTickStyle.None) return;
        using var pen = new Pen(c.HighContrast ? c.Text : c.Muted, 1f);
        var values = _scale.Ticks(_tickFrequency);
        var length = S(5);
        var offset = ThumbSize / 2 + S(3);
        foreach (var v in values)
        {
            var p = ThumbCenter(v);
            if (TicksBelow) DrawTick(g, pen, p, offset, length, 1);
            if (TicksAbove) DrawTick(g, pen, p, offset, length, -1);
        }

        if (!_showTickLabels || !TicksBelow || values.Count == 0) return;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top;
        var step = 1;
        var sample = TextRenderer.MeasureText(Format(_scale.Maximum), Font, Size.Empty, flags).Width + S(6);
        var pixelsPerTick = values.Count > 1 ? LineLength / (double)(values.Count - 1) : LineLength;
        if (Horizontal) while (pixelsPerTick * step < sample) step++;
        for (var i = 0; i < values.Count; i += step)
        {
            var p = ThumbCenter(values[i]);
            var text = Format(values[i]);
            var size = TextRenderer.MeasureText(text, Font, Size.Empty, flags);
            var rect = Horizontal ? new Rectangle((int)p.X - size.Width / 2 - 2, (int)p.Y + offset + length + S(1), size.Width + 4, size.Height) : new Rectangle((int)p.X + offset + length + S(2), (int)p.Y - size.Height / 2, size.Width + 4, size.Height);
            TextRenderer.DrawText(g, text, Font, rect, c.Muted, flags);
        }
    }

    private void DrawTick(Graphics g, Pen pen, PointF p, int offset, int length, int side)
    {
        if (Horizontal) g.DrawLine(pen, p.X, p.Y + side * offset, p.X, p.Y + side * (offset + length));
        else g.DrawLine(pen, p.X + side * offset, p.Y, p.X + side * (offset + length), p.Y);
    }

    private void PaintThumb(Graphics g, ControlColors c, int index)
    {
        var center = ThumbCenter(ThumbValue(index));
        var grow = 1 + 0.15 * _thumbHover[index].Value;
        var size = (float)(ThumbSize * grow);
        var rect = new RectangleF(center.X - size / 2, center.Y - size / 2, size, size);
        using (var shadow = new SolidBrush(Color.FromArgb(c.HighContrast ? 0 : 38, 0, 0, 0))) g.FillEllipse(shadow, rect.X, rect.Y + 1.5f, rect.Width, rect.Height);
        var dragging = _dragThumb == index;
        using (var brush = new SolidBrush(!Enabled ? Chrome.Blend(c.Surface, c.Border, 0.4) : dragging ? c.Primary : c.Surface)) g.FillEllipse(brush, rect);
        using (var pen = new Pen(Enabled ? c.Primary : c.Disabled, S(2))) g.DrawEllipse(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
        if (Focused && Enabled && (_mode == TrackSelectionMode.Single || _activeThumb == index))
        {
            using var ring = new Pen(c.FocusRing, 2f);
            g.DrawEllipse(ring, rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tipTimer.Stop();
            _tipTimer.Dispose();
            _tip.Dispose();
            foreach (var a in _thumbHover) a.Dispose();
        }

        base.Dispose(disposing);
    }
}
