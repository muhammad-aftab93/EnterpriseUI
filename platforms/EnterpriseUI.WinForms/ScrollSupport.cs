using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

/// <summary>Thin themed scroll bar: tinted track and a rounded thumb that darkens on hover and while dragged.</summary>
public sealed class EnterpriseScrollBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _largeChange = 10;
    private int _smallChange = 1;
    private int _value;
    private bool _thumbHover;
    private bool _dragging;
    private int _dragOffset;
    private ControlColors _colors = ControlColors.FromTheme(EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme);

    public EnterpriseScrollBar() : this(Orientation.Vertical)
    {
    }

    public EnterpriseScrollBar(Orientation orientation)
    {
        Orientation = orientation;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    public Orientation Orientation { get; }

    public int Minimum { get => _minimum; set { _minimum = value; Clamp(); Invalidate(); } }
    public int Maximum { get => _maximum; set { _maximum = value; Clamp(); Invalidate(); } }
    public int LargeChange { get => _largeChange; set { _largeChange = Math.Max(1, value); Clamp(); Invalidate(); } }
    public int SmallChange { get => _smallChange; set => _smallChange = Math.Max(1, value); }

    /// <summary>Largest reachable value, as for the standard scroll bar.</summary>
    public int MaxValue => Math.Max(_minimum, _maximum - _largeChange + 1);

    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, _minimum, MaxValue);
            if (value == _value) return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    public void ApplyColors(ControlColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    private bool Vertical => Orientation == Orientation.Vertical;
    private int TrackLength => Math.Max(1, Vertical ? Height : Width);

    private Rectangle ThumbRect()
    {
        var range = Math.Max(1, _maximum - _minimum + 1);
        var margin = 2;
        var track = TrackLength - margin * 2;
        var length = Math.Clamp((int)Math.Round(track * Math.Min(1d, _largeChange / (double)range)), Math.Min(24, track), Math.Max(24, track));
        var scrollable = Math.Max(1, MaxValue - _minimum);
        var offset = (int)Math.Round((track - length) * ((_value - _minimum) / (double)scrollable));
        var thickness = Math.Max(3, (Vertical ? Width : Height) - margin * 2);
        return Vertical ? new Rectangle(margin, margin + offset, thickness, length) : new Rectangle(margin + offset, margin, length, thickness);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Chrome.Blend(_colors.Surface, _colors.Alt, 0.7));
        if (MaxValue <= _minimum) return;

        var thumb = ThumbRect();
        var color = _dragging ? _colors.Primary : _thumbHover ? Chrome.Blend(_colors.Muted, _colors.Primary, 0.4) : Chrome.Blend(_colors.Surface, _colors.Muted, _colors.HighContrast ? 1 : 0.55);
        Chrome.FillRounded(g, thumb, color, Math.Min(thumb.Width, thumb.Height) / 2);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            var thumb = ThumbRect();
            var track = TrackLength - 4 - (Vertical ? thumb.Height : thumb.Width);
            var position = (Vertical ? e.Y : e.X) - _dragOffset - 2;
            if (track > 0) Value = _minimum + (int)Math.Round(Math.Clamp(position / (double)track, 0, 1) * (MaxValue - _minimum));
            return;
        }

        var hover = ThumbRect().Contains(e.Location);
        if (hover != _thumbHover)
        {
            _thumbHover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || MaxValue <= _minimum) return;

        var thumb = ThumbRect();
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = Vertical ? e.Y - thumb.Y : e.X - thumb.X;
            Capture = true;
            Invalidate();
            return;
        }

        var before = Vertical ? e.Y < thumb.Y : e.X < thumb.X;
        Value += before ? -_largeChange : _largeChange;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_thumbHover) { _thumbHover = false; Invalidate(); }
    }

    private void Clamp()
    {
        var clamped = Math.Clamp(_value, _minimum, MaxValue);
        if (clamped != _value)
        {
            _value = clamped;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Base of controls whose content can be larger than the view: themed scroll bars, wheel handling and scroll-into-view.</summary>
public abstract class EnterpriseScrollableControl : EnterpriseControlBase
{
    private readonly EnterpriseScrollBar _vertical = new(Orientation.Vertical) { Visible = false };
    private readonly EnterpriseScrollBar _horizontal = new(Orientation.Horizontal) { Visible = false };
    private bool _updating;

    protected EnterpriseScrollableControl()
    {
        _vertical.ValueChanged += (_, _) => { if (!_updating) OnScrolled(); };
        _horizontal.ValueChanged += (_, _) => { if (!_updating) OnScrolled(); };
        Controls.Add(_vertical);
        Controls.Add(_horizontal);
        _vertical.ApplyColors(Colors);
        _horizontal.ApplyColors(Colors);
    }

    /// <summary>Full size of the content in pixels.</summary>
    protected abstract Size ContentSize { get; }

    protected int ScrollBarSize => S(12);
    protected virtual int LineHeight => S(24);
    public int ScrollY => _vertical.Visible ? _vertical.Value : 0;
    public int ScrollX => _horizontal.Visible ? _horizontal.Value : 0;

    /// <summary>Area of the content view: inside the frame and left of / above the visible scroll bars.</summary>
    protected Rectangle ViewPort
    {
        get
        {
            var inner = InnerBounds;
            return new Rectangle(inner.X, inner.Y, Math.Max(0, inner.Width - (_vertical.Visible ? ScrollBarSize : 0)), Math.Max(0, inner.Height - (_horizontal.Visible ? ScrollBarSize : 0)));
        }
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _vertical.ApplyColors(Colors);
        _horizontal.ApplyColors(Colors);
    }

    protected virtual void OnScrolled() => Invalidate();

    /// <summary>Recomputes scroll ranges; call whenever the content size or the control size changes.</summary>
    protected void UpdateScrollBars()
    {
        if (Width <= 0 || Height <= 0) return;
        _updating = true;
        try
        {
            var content = ContentSize;
            var inner = InnerBounds;
            bool needV = false, needH = false;
            for (var pass = 0; pass < 2; pass++)
            {
                needV = content.Height > inner.Height - (needH ? ScrollBarSize : 0);
                needH = content.Width > inner.Width - (needV ? ScrollBarSize : 0);
            }

            _vertical.Visible = needV;
            _horizontal.Visible = needH;
            var view = ViewPort;

            if (needV)
            {
                _vertical.SetBounds(inner.Right - ScrollBarSize, inner.Y, ScrollBarSize, view.Height);
                _vertical.Minimum = 0;
                _vertical.Maximum = Math.Max(0, content.Height - 1);
                _vertical.LargeChange = Math.Max(1, view.Height);
                _vertical.SmallChange = LineHeight;
            }
            else
            {
                _vertical.Value = 0;
            }

            if (needH)
            {
                _horizontal.SetBounds(inner.X, inner.Bottom - ScrollBarSize, view.Width, ScrollBarSize);
                _horizontal.Minimum = 0;
                _horizontal.Maximum = Math.Max(0, content.Width - 1);
                _horizontal.LargeChange = Math.Max(1, view.Width);
                _horizontal.SmallChange = S(24);
            }
            else
            {
                _horizontal.Value = 0;
            }
        }
        finally
        {
            _updating = false;
        }

        Invalidate();
    }

    public void ScrollTo(int x, int y)
    {
        if (_horizontal.Visible) _horizontal.Value = x;
        if (_vertical.Visible) _vertical.Value = y;
    }

    /// <summary>Scrolls minimally so the rectangle (in content coordinates) is inside the view.</summary>
    protected void EnsureVisible(Rectangle content)
    {
        var view = ViewPort;
        if (_vertical.Visible)
        {
            if (content.Top < _vertical.Value) _vertical.Value = content.Top;
            else if (content.Bottom > _vertical.Value + view.Height) _vertical.Value = content.Bottom - view.Height;
        }

        if (_horizontal.Visible)
        {
            if (content.Left < _horizontal.Value) _horizontal.Value = content.Left;
            else if (content.Right > _horizontal.Value + view.Width) _horizontal.Value = content.Right - view.Width;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        var delta = -e.Delta / 120 * lines * LineHeight;
        if ((ModifierKeys & Keys.Shift) == Keys.Shift && _horizontal.Visible) _horizontal.Value += delta;
        else if (_vertical.Visible) _vertical.Value += delta;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
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
        UpdateScrollBars();
    }
}
