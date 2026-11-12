using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum CarouselLayout { Flat, Coverflow, Ring }

/// <summary>Ready-made carousel item; any other object is shown with its ToString text.</summary>
public sealed class CarouselItem
{
    public CarouselItem(string title, string? subtitle = null, Image? image = null)
    {
        Title = title;
        Subtitle = subtitle;
        Image = image;
    }

    public string Title { get; set; }
    public string? Subtitle { get; set; }
    public Image? Image { get; set; }
    public Color Accent { get; set; } = Color.Empty;
    public object? Tag { get; set; }

    public override string ToString() => Title;
}

/// <summary>Custom item template: draw the item face into <see cref="Bounds"/> and set <see cref="Handled"/>.</summary>
public sealed class CarouselItemPaintEventArgs : EventArgs
{
    public CarouselItemPaintEventArgs(Graphics graphics, Rectangle bounds, object item, int index, Font font, ControlColors colors)
    {
        Graphics = graphics;
        Bounds = bounds;
        Item = item;
        Index = index;
        Font = font;
        Colors = colors;
    }

    public Graphics Graphics { get; }
    public Rectangle Bounds { get; }
    public object Item { get; }
    public int Index { get; }
    public Font Font { get; }
    public ControlColors Colors { get; }
    public bool Handled { get; set; }
}

/// <summary>Item carousel with flat, cover-flow (perspective) or elliptical ring layouts, looping, swipe, keyboard navigation, indicators, autoplay and custom item painting.</summary>
public class EnterpriseCarouselControl : EnterpriseControlBase
{
    private sealed record Placement(int Index, Rectangle Bounds, double Depth);

    private readonly ObservableCollection<object> _items = new();
    private readonly ValueAnimator _position = new();
    private readonly Dictionary<int, Bitmap> _cards = new();
    private readonly List<Placement> _hit = new();
    private readonly System.Windows.Forms.Timer _autoTimer = new();
    private readonly ValueAnimator _chrome = new();
    private CarouselLayout _layout = CarouselLayout.Coverflow;
    private Size _itemSize = new(210, 130);
    private long _target;
    private bool _loop = true;
    private bool _showIndicators = true;
    private bool _showNavigation = true;
    private bool _showReflection = true;
    private bool _autoPlay;
    private bool _pauseOnHover = true;
    private int _animationMs = 450;
    private bool _dragging;
    private Point _dragStart;
    private double _dragFrom;
    private bool _moved;
    private int _hotNav = -1;

    public EnterpriseCarouselControl()
    {
        Size = new Size(720, 260);
        AccessibleRole = AccessibleRole.List;
        _items.CollectionChanged += OnItemsChanged;
        _position.Updated += (_, _) => Invalidate();
        _chrome.Updated += (_, _) => Invalidate();
        _autoTimer.Interval = 3500;
        _autoTimer.Tick += (_, _) => AutoAdvance();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<object> Items => _items;

    [DefaultValue(CarouselLayout.Coverflow)]
    public CarouselLayout Layout
    {
        get => _layout;
        set { _layout = value; Invalidate(); }
    }

    public Size ItemSize
    {
        get => _itemSize;
        set { _itemSize = new Size(Math.Max(40, value.Width), Math.Max(30, value.Height)); InvalidateItems(); }
    }

    private bool ShouldSerializeItemSize() => _itemSize != new Size(210, 130);
    private void ResetItemSize() => ItemSize = new Size(210, 130);

    [DefaultValue(true)]
    public bool InfiniteLoop
    {
        get => _loop;
        set { _loop = value; if (!value) SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _items.Count - 1)); Invalidate(); }
    }

    [DefaultValue(true)]
    public bool ShowIndicators { get => _showIndicators; set { _showIndicators = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowNavigationButtons { get => _showNavigation; set { _showNavigation = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowReflection { get => _showReflection; set { _showReflection = value; Invalidate(); } }

    [DefaultValue(false)]
    public bool AutoPlay
    {
        get => _autoPlay;
        set { _autoPlay = value; UpdateAutoTimer(); }
    }

    [DefaultValue(3500)]
    public int AutoPlayInterval
    {
        get => _autoTimer.Interval;
        set => _autoTimer.Interval = Math.Max(200, value);
    }

    [DefaultValue(true)]
    public bool PauseOnHover { get => _pauseOnHover; set => _pauseOnHover = value; }

    [DefaultValue(450)]
    public int AnimationDuration { get => _animationMs; set => _animationMs = Math.Clamp(value, 0, 5000); }

    [DefaultValue(0)]
    public int SelectedIndex
    {
        get => _items.Count == 0 ? -1 : Mod(_target, _items.Count);
        set
        {
            if (_items.Count == 0) return;
            var n = _items.Count;
            var from = Mod(_target, n);
            value = Math.Clamp(value, 0, n - 1);
            if (value == from) return;
            var delta = value - from;
            if (_loop)
            {
                if (delta > n / 2) delta -= n;
                else if (delta < -n / 2) delta += n;
            }

            MoveTo(_target + delta);
        }
    }

    [Browsable(false)]
    public object? SelectedItem => _items.Count == 0 ? null : _items[SelectedIndex];

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler<int>? ItemActivated;

    /// <summary>Template hook; runs once per item when its face is cached (call <see cref="InvalidateItems"/> after the data changes).</summary>
    public event EventHandler<CarouselItemPaintEventArgs>? PaintItem;

    public void MoveNext() => MoveBy(1);
    public void MovePrevious() => MoveBy(-1);

    public void InvalidateItems()
    {
        DisposeCards();
        Invalidate();
    }

    private static int Mod(long value, int n) => (int)(((value % n) + n) % n);

    private void MoveBy(int delta)
    {
        if (_items.Count == 0) return;
        MoveTo(_target + delta);
    }

    private void MoveTo(long target)
    {
        if (_items.Count == 0) return;
        if (!_loop) target = Math.Clamp(target, 0, _items.Count - 1);
        if (target == _target && !_position.IsRunning) return;
        var changed = target != _target;
        _target = target;
        _position.AnimateTo(target, _animationMs, EasingKind.EaseInOut);
        if (!changed) return;
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DisposeCards();
        if (_items.Count == 0) { _target = 0; _position.Jump(0); }
        else if (!_loop) _target = Math.Clamp(_target, 0, _items.Count - 1);
        Invalidate();
        UpdateAutoTimer();
    }

    private void DisposeCards()
    {
        foreach (var b in _cards.Values) b.Dispose();
        _cards.Clear();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        DisposeCards();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        DisposeCards();
    }

    private void UpdateAutoTimer() => _autoTimer.Enabled = _autoPlay && !DesignMode && IsHandleCreated && _items.Count > 1;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateAutoTimer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _autoTimer.Stop();
        base.OnHandleDestroyed(e);
    }

    private void AutoAdvance()
    {
        if (_dragging || _position.IsRunning || (_pauseOnHover && (IsHovered || Focused))) return;
        if (!_loop && _target >= _items.Count - 1) MoveTo(0); else MoveNext();
    }

    private Size Card => new(S(_itemSize.Width), S(_itemSize.Height));

    private Bitmap CardFor(int index)
    {
        if (_cards.TryGetValue(index, out var cached)) return cached;
        var size = Card;
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, size.Width, size.Height);
            var args = new CarouselItemPaintEventArgs(g, bounds, _items[index], index, Font, Colors);
            g.Clear(Colors.Surface);
            PaintItem?.Invoke(this, args);
            if (!args.Handled) PaintDefaultCard(g, bounds, _items[index], index);
        }

        _cards[index] = bitmap;
        return bitmap;
    }

    private void PaintDefaultCard(Graphics g, Rectangle bounds, object item, int index)
    {
        var c = Colors;
        var accent = item is CarouselItem { Accent.IsEmpty: false } ci ? ci.Accent : Chrome.Blend(c.Primary, c.Warning, (index % 6) / 6.0);
        g.Clear(Chrome.Blend(c.Surface, accent, 0.07));
        using (var bar = new SolidBrush(accent)) g.FillRectangle(bar, 0, 0, bounds.Width, S(6));
        using (var pen = new Pen(c.Border)) g.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);

        var pad = S(14);
        var content = new Rectangle(pad, S(6) + pad, bounds.Width - pad * 2, bounds.Height - S(6) - pad * 2);
        var ci2 = item as CarouselItem;
        if (ci2?.Image != null)
        {
            var side = Math.Min(content.Height, S(56));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(ci2.Image, new Rectangle(content.X, content.Y, side, side));
            content = new Rectangle(content.X + side + S(10), content.Y, content.Width - side - S(10), content.Height);
        }

        var title = ci2?.Title ?? item.ToString() ?? string.Empty;
        var titleHeight = BoldFont.Height * 2 + 2;
        TextRenderer.DrawText(g, title, BoldFont, new Rectangle(content.X, content.Y, content.Width, Math.Min(content.Height, titleHeight)), c.Text, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top);
        if (!string.IsNullOrEmpty(ci2?.Subtitle) && content.Height > titleHeight)
            TextRenderer.DrawText(g, ci2!.Subtitle, Font, new Rectangle(content.X, content.Y + titleHeight, content.Width, content.Height - titleHeight), c.Muted, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top);
    }

    private double Offset(int index)
    {
        var n = _items.Count;
        var p = _position.Value;
        if (!_loop) return index - p;
        var d = (index - p) % n;
        if (d > n / 2.0) d -= n;
        else if (d < -n / 2.0) d += n;
        return d;
    }

    private Rectangle Stage => new(0, 0, Width, Height - (_showIndicators ? S(26) : 0));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        _hit.Clear();
        var c = Colors;
        if (_items.Count == 0)
        {
            Chrome.DrawText(g, "No items", Font, new Rectangle(0, 0, Width, Height), c.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var stage = Stage;
        var card = Card;
        var order = new List<(int Index, double D)>();
        var range = _layout == CarouselLayout.Ring ? _items.Count : 3.2;
        for (var i = 0; i < _items.Count; i++)
        {
            var d = Offset(i);
            if (Math.Abs(d) <= range) order.Add((i, d));
        }

        foreach (var (index, d) in order.OrderByDescending(o => Math.Abs(o.D))) PaintItemAt(g, stage, card, index, d);
        PaintChrome(g, c, stage);
    }

    private void PaintItemAt(Graphics g, Rectangle stage, Size card, int index, double d)
    {
        var bmp = CardFor(index);
        var cx = stage.Width / 2.0;
        var cy = stage.Height / 2.0 - (_showReflection && _layout == CarouselLayout.Coverflow ? card.Height * 0.08 : 0);
        var abs = Math.Abs(d);
        var sign = Math.Sign(d);
        double x, scale, opacity, depth;
        switch (_layout)
        {
            case CarouselLayout.Flat:
                x = cx + d * card.Width * 0.82;
                scale = 1 - Math.Min(0.4, 0.17 * abs);
                opacity = 1 - Math.Min(0.65, 0.24 * abs);
                depth = 1 - abs;
                break;
            case CarouselLayout.Ring:
            {
                var theta = d * 2 * Math.PI / _items.Count;
                depth = (Math.Cos(theta) + 1) / 2;
                x = cx + Math.Sin(theta) * stage.Width * 0.36;
                cy -= (1 - depth) * stage.Height * 0.16;
                scale = 0.55 + 0.45 * depth;
                opacity = 0.35 + 0.65 * depth;
                break;
            }
            default:
            {
                var near = Math.Min(1, abs);
                x = cx + sign * (near * card.Width * 0.7 + Math.Max(0, abs - 1) * card.Width * 0.3);
                scale = 1 - 0.1 * Math.Min(2, abs);
                opacity = 1 - Math.Min(0.6, 0.18 * abs);
                depth = 1 - abs;
                break;
            }
        }

        var width = card.Width * scale;
        var height = card.Height * scale;
        using var attributes = opacity < 0.99 ? Fade((float)opacity) : null;
        Rectangle bounds;
        if (_layout == CarouselLayout.Coverflow && abs > 0.02)
        {
            var near = Math.Min(1, abs);
            var squeeze = 1 - 0.46 * near;
            var w = width * squeeze;
            var left = x - w / 2;
            var nearHeight = height * (1 - 0.04 * near);
            var farHeight = height * (1 - 0.34 * near);
            var (h0, h1) = sign > 0 ? (nearHeight, farHeight) : (farHeight, nearHeight);
            DrawStrips(g, bmp, left, w, h0, h1, cy, attributes, _showReflection);
            bounds = new Rectangle((int)left, (int)(cy - Math.Max(h0, h1) / 2), (int)w, (int)Math.Max(h0, h1));
        }
        else
        {
            bounds = new Rectangle((int)(x - width / 2), (int)(cy - height / 2), (int)width, (int)height);
            if (_layout == CarouselLayout.Coverflow && _showReflection) DrawStrips(g, bmp, bounds.X, bounds.Width, height, height, cy, attributes, true, drawCard: false);
            DrawFace(g, bmp, bounds, attributes, index == SelectedIndex && Math.Abs(d) < 0.5);
        }

        _hit.Add(new Placement(index, bounds, depth));
    }

    private void DrawFace(Graphics g, Bitmap bmp, Rectangle bounds, ImageAttributes? attributes, bool selected)
    {
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (var clip = Chrome.RoundedRect(bounds, Chrome.CornerRadius + 2))
        {
            g.SetClip(clip);
            if (attributes == null) g.DrawImage(bmp, bounds);
            else g.DrawImage(bmp, bounds, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
        }

        g.Restore(state);
        if (selected)
        {
            using var ring = new Pen(Focused ? Colors.FocusRing : Colors.Primary, Focused ? 3f : 2f);
            using var path = Chrome.RoundedRect(Rectangle.Inflate(bounds, 1, 1), Chrome.CornerRadius + 2);
            g.DrawPath(ring, path);
        }
    }

    private static ImageAttributes Fade(float opacity)
    {
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity }, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        return attributes;
    }

    private void DrawStrips(Graphics g, Bitmap bmp, double left, double width, double h0, double h1, double cy, ImageAttributes? attributes, bool reflect, bool drawCard = true)
    {
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var strip = 3;
        var count = Math.Max(1, (int)Math.Ceiling(width / strip));
        using var reflection = reflect ? Fade(0.2f) : null;
        for (var k = 0; k < count; k++)
        {
            var t0 = k / (double)count;
            var t1 = (k + 1) / (double)count;
            var tm = (t0 + t1) / 2;
            var h = h0 + (h1 - h0) * tm;
            var dx = (float)(left + t0 * width);
            var dw = (float)((t1 - t0) * width) + 0.6f;
            var src = new Rectangle((int)(t0 * bmp.Width), 0, Math.Max(1, (int)Math.Ceiling((t1 - t0) * bmp.Width)), bmp.Height);
            if (drawCard)
            {
                var dest = new RectangleF(dx, (float)(cy - h / 2), dw, (float)h);
                if (attributes == null) g.DrawImage(bmp, new[] { new PointF(dest.X, dest.Y), new PointF(dest.Right, dest.Y), new PointF(dest.X, dest.Bottom) }, src, GraphicsUnit.Pixel);
                else g.DrawImage(bmp, new[] { new PointF(dest.X, dest.Y), new PointF(dest.Right, dest.Y), new PointF(dest.X, dest.Bottom) }, src, GraphicsUnit.Pixel, attributes);
            }

            if (reflect)
            {
                var band = (int)(bmp.Height * 0.32);
                var rsrc = new Rectangle(src.X, bmp.Height - band, src.Width, band);
                var rh = (float)(h * 0.32);
                var top = (float)(cy + h / 2 + 3);
                g.DrawImage(bmp, new[] { new PointF(dx, top + rh), new PointF(dx + dw, top + rh), new PointF(dx, top) }, rsrc, GraphicsUnit.Pixel, reflection);
            }
        }

        g.Restore(state);
    }

    private Rectangle NavRect(int side)
    {
        var size = S(36);
        var stage = Stage;
        return new Rectangle(side == 0 ? S(8) : Width - S(8) - size, stage.Height / 2 - size / 2, size, size);
    }

    private bool NavEnabled(int side) => _loop ? _items.Count > 1 : side == 0 ? _target > 0 : _target < _items.Count - 1;

    private void PaintChrome(Graphics g, ControlColors c, Rectangle stage)
    {
        var show = _chrome.Value;
        if (_showNavigation && _items.Count > 1 && (show > 0.02 || Focused))
        {
            for (var side = 0; side < 2; side++)
            {
                var r = NavRect(side);
                var enabled = NavEnabled(side);
                var alpha = enabled ? (int)(230 * Math.Max(show, Focused ? 1 : 0)) : 0;
                if (alpha <= 0) continue;
                var back = _hotNav == side ? c.Primary : c.Surface;
                using (var brush = new SolidBrush(Color.FromArgb(alpha, back))) g.FillEllipse(brush, r);
                using (var pen = new Pen(Color.FromArgb(alpha, _hotNav == side ? c.Primary : c.Border), 1f)) g.DrawEllipse(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                Chrome.DrawChevron(g, r, side == 0 ? ChevronDirection.Left : ChevronDirection.Right, Color.FromArgb(Math.Min(255, alpha + 25), _hotNav == side ? c.PrimaryText : c.Text), 2f);
            }
        }

        if (!_showIndicators || _items.Count < 2 || _items.Count > 24) return;
        var dot = S(8);
        var gap = S(8);
        var total = _items.Count * dot + (_items.Count - 1) * gap;
        var x = (Width - total) / 2;
        var y = Height - S(18);
        var selected = SelectedIndex;
        for (var i = 0; i < _items.Count; i++)
        {
            using var brush = new SolidBrush(i == selected ? c.Primary : Chrome.Blend(c.Surface, c.Muted, 0.5));
            var r = new Rectangle(x + i * (dot + gap), y, dot, dot);
            if (i == selected) r = Rectangle.Inflate(r, S(4), 0);
            Chrome.FillRounded(g, r, brush.Color, dot / 2);
        }
    }

    private Rectangle IndicatorRect(int i)
    {
        var dot = S(8);
        var gap = S(8);
        var total = _items.Count * dot + (_items.Count - 1) * gap;
        return new Rectangle((Width - total) / 2 + i * (dot + gap) - S(3), Height - S(22), dot + S(6), dot + S(8));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _chrome.AnimateTo(1, 140);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotNav = -1;
        if (!Focused) _chrome.AnimateTo(0, 200);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled || _items.Count == 0) return;
        Focus();
        _dragging = true;
        _moved = false;
        _dragStart = e.Location;
        _dragFrom = _position.Value;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            var dx = e.X - _dragStart.X;
            if (!_moved && Math.Abs(dx) > S(6)) _moved = true;
            if (_moved)
            {
                var next = _dragFrom - dx / (Card.Width * 0.7);
                if (!_loop) next = Math.Clamp(next, 0, _items.Count - 1);
                _position.Jump(next);
            }

            return;
        }

        var hot = -1;
        if (_showNavigation && _items.Count > 1) for (var s = 0; s < 2; s++) if (NavEnabled(s) && NavRect(s).Contains(e.Location)) hot = s;
        Cursor = hot >= 0 || HitIndex(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        if (hot == _hotNav) return;
        _hotNav = hot;
        Invalidate();
    }

    private int HitIndex(Point p)
    {
        for (var i = _hit.Count - 1; i >= 0; i--) if (_hit[i].Bounds.Contains(p)) return _hit[i].Index;
        return -1;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        if (_moved)
        {
            _target = (long)Math.Round(_position.Value);
            if (!_loop) _target = Math.Clamp(_target, 0, _items.Count - 1);
            _position.AnimateTo(_target, 260, EasingKind.EaseOut);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_showNavigation && _items.Count > 1)
        {
            for (var s = 0; s < 2; s++)
            {
                if (!NavEnabled(s) || !NavRect(s).Contains(e.Location)) continue;
                MoveBy(s == 0 ? -1 : 1);
                return;
            }
        }

        if (_showIndicators && _items.Count is > 1 and <= 24)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (!IndicatorRect(i).Contains(e.Location)) continue;
                SelectedIndex = i;
                return;
            }
        }

        var hit = HitIndex(e.Location);
        if (hit < 0) return;
        if (hit == SelectedIndex) ItemActivated?.Invoke(this, hit);
        else SelectedIndex = hit;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_items.Count > 1) MoveBy(e.Delta > 0 ? -1 : 1);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0) return;
        var rtl = RightToLeft == RightToLeft.Yes ? -1 : 1;
        switch (e.KeyCode)
        {
            case Keys.Right: MoveBy(rtl); break;
            case Keys.Left: MoveBy(-rtl); break;
            case Keys.Home: SelectedIndex = 0; break;
            case Keys.End: SelectedIndex = _items.Count - 1; break;
            case Keys.Enter or Keys.Space: ItemActivated?.Invoke(this, SelectedIndex); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _chrome.AnimateTo(1, 120);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsHovered) _chrome.AnimateTo(0, 200);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () =>
    {
        if (_items.Count == 0) return "Empty";
        var item = _items[SelectedIndex];
        return $"Item {SelectedIndex + 1} of {_items.Count}: {item}";
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoTimer.Stop();
            _autoTimer.Dispose();
            _position.Dispose();
            _chrome.Dispose();
            DisposeCards();
        }

        base.Dispose(disposing);
    }
}
