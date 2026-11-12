using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum RotatorEffect { None, Fade, SlideLeft, SlideRight, SlideUp, SlideDown, FlipHorizontal, FlipVertical }

/// <summary>One page of a rotator: formatted text (HTML or Markdown subset) with an optional title and image.</summary>
public sealed class RotatorPanel
{
    public RotatorPanel(string content, string? title = null, LabelMarkupMode mode = LabelMarkupMode.Html)
    {
        Content = content;
        Title = title;
        Mode = mode;
    }

    public string? Title { get; set; }
    public string Content { get; set; }
    public LabelMarkupMode Mode { get; set; }
    public Image? Image { get; set; }
    public Color Accent { get; set; } = Color.Empty;
    public object? Tag { get; set; }
}

/// <summary>Content ticker that cycles through formatted panels automatically or on demand with fade, slide or flip transitions.</summary>
public class EnterpriseRotatorControl : EnterpriseControlBase
{
    private sealed class Face : IDisposable
    {
        public Face(Bitmap bitmap, MarkupLayout? layout, Point origin)
        {
            Bitmap = bitmap;
            Layout = layout;
            Origin = origin;
        }

        public Bitmap Bitmap { get; }
        public MarkupLayout? Layout { get; }
        public Point Origin { get; }

        public void Dispose()
        {
            Bitmap.Dispose();
            Layout?.Dispose();
        }
    }

    private readonly ObservableCollection<RotatorPanel> _panels = new();
    private readonly Dictionary<int, Face> _faces = new();
    private readonly ValueAnimator _progress = new();
    private readonly ValueAnimator _chrome = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 4000 };
    private readonly FrameClock _barClock = new();
    private readonly Stopwatch _sinceChange = new();
    private RotatorEffect _effect = RotatorEffect.Fade;
    private int _index;
    private int _from = -1;
    private int _direction = 1;
    private int _transitionMs = 600;
    private bool _autoRotate = true;
    private bool _pauseOnHover = true;
    private bool _loop = true;
    private bool _showIndicators = true;
    private bool _showNavigation = true;
    private bool _showTimerBar;
    private bool _userPaused;
    private HorizontalAlignment _alignment = HorizontalAlignment.Center;
    private int _hotNav = -1;

    public EnterpriseRotatorControl()
    {
        Size = new Size(520, 150);
        AccessibleRole = AccessibleRole.Pane;
        _panels.CollectionChanged += OnPanelsChanged;
        _progress.Updated += (_, _) =>
        {
            if (_progress.Value >= 1) _from = -1;
            Invalidate();
        };
        _chrome.Updated += (_, _) => Invalidate();
        _timer.Tick += (_, _) => AutoAdvance();
        _barClock.Frame += (_, _) => Invalidate();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<RotatorPanel> Panels => _panels;

    [DefaultValue(RotatorEffect.Fade)]
    public RotatorEffect Effect { get => _effect; set => _effect = value; }

    /// <summary>Time a panel stays before the next one appears.</summary>
    [DefaultValue(4000)]
    public int Interval
    {
        get => _timer.Interval;
        set { _timer.Interval = Math.Max(300, value); RestartTimer(); }
    }

    [DefaultValue(600)]
    public int TransitionDuration { get => _transitionMs; set => _transitionMs = Math.Clamp(value, 0, 5000); }

    [DefaultValue(true)]
    public bool AutoRotate { get => _autoRotate; set { _autoRotate = value; RestartTimer(); } }

    [DefaultValue(true)]
    public bool PauseOnHover { get => _pauseOnHover; set { _pauseOnHover = value; RestartTimer(); } }

    [DefaultValue(true)]
    public bool Loop { get => _loop; set { _loop = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowIndicators { get => _showIndicators; set { _showIndicators = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowNavigationButtons { get => _showNavigation; set { _showNavigation = value; Invalidate(); } }

    /// <summary>Thin bar at the bottom that fills while waiting for the next panel.</summary>
    [DefaultValue(false)]
    public bool ShowTimerBar { get => _showTimerBar; set { _showTimerBar = value; RestartTimer(); } }

    [DefaultValue(HorizontalAlignment.Center)]
    public HorizontalAlignment TextAlignment { get => _alignment; set { _alignment = value; DisposeFaces(); Invalidate(); } }

    [DefaultValue(0)]
    public int SelectedIndex
    {
        get => _index;
        set => GoTo(value);
    }

    [Browsable(false)]
    public RotatorPanel? SelectedPanel => _index >= 0 && _index < _panels.Count ? _panels[_index] : null;

    [Browsable(false)]
    public bool IsPaused => _userPaused;

    public event EventHandler<RotatorChangingEventArgs>? PanelChanging;
    public event EventHandler? PanelChanged;
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    public RotatorPanel AddPanel(string content, string? title = null, LabelMarkupMode mode = LabelMarkupMode.Html)
    {
        var panel = new RotatorPanel(content, title, mode);
        _panels.Add(panel);
        return panel;
    }

    public void Next() => Move(1);
    public void Previous() => Move(-1);

    private void Move(int delta)
    {
        var n = _panels.Count;
        if (n < 2) return;
        var target = _index + delta;
        if (target < 0 || target >= n)
        {
            if (!_loop) return;
            target = (target + n) % n;
        }

        GoTo(target, delta);
    }

    public void GoTo(int index, int direction = 0)
    {
        if (_panels.Count == 0) return;
        index = Math.Clamp(index, 0, _panels.Count - 1);
        if (index == _index) return;
        var args = new RotatorChangingEventArgs(_index, index);
        PanelChanging?.Invoke(this, args);
        if (args.Cancel) return;

        _direction = direction != 0 ? Math.Sign(direction) : index > _index ? 1 : -1;
        _from = _index;
        _index = index;
        if (_effect == RotatorEffect.None || _transitionMs == 0 || !IsHandleCreated) { _from = -1; Invalidate(); }
        else
        {
            _progress.Jump(0);
            _progress.AnimateTo(1, _transitionMs, EasingKind.EaseInOut);
        }

        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        PanelChanged?.Invoke(this, EventArgs.Empty);
        RestartTimer();
    }

    /// <summary>Pauses or resumes automatic rotation (the Space key does the same).</summary>
    public void TogglePause()
    {
        _userPaused = !_userPaused;
        RestartTimer();
        Invalidate();
    }

    private void OnPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DisposeFaces();
        _index = _panels.Count == 0 ? 0 : Math.Clamp(_index, 0, _panels.Count - 1);
        _from = -1;
        RestartTimer();
        Invalidate();
    }

    private bool IsRunning => _autoRotate && !_userPaused && !DesignMode && IsHandleCreated && Visible && _panels.Count > 1 && !(_pauseOnHover && (IsHovered || Focused));

    private void RestartTimer()
    {
        _timer.Stop();
        _barClock.Enabled = false;
        if (!IsRunning) return;
        _timer.Start();
        _sinceChange.Restart();
        _barClock.Enabled = _showTimerBar;
    }

    private void AutoAdvance()
    {
        if (!IsRunning) return;
        if (!_loop && _index >= _panels.Count - 1) return;
        Next();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RestartTimer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        _barClock.Enabled = false;
        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        RestartTimer();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        DisposeFaces();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        DisposeFaces();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        DisposeFaces();
    }

    private void DisposeFaces()
    {
        foreach (var f in _faces.Values) f.Dispose();
        _faces.Clear();
    }

    private Face FaceOf(int index)
    {
        if (_faces.TryGetValue(index, out var cached)) return cached;
        var panel = _panels[index];
        var c = Colors;
        var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format24bppRgb);
        MarkupLayout? layout = null;
        var origin = Point.Empty;
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var accent = panel.Accent.IsEmpty ? c.Primary : panel.Accent;
            g.Clear(Chrome.Blend(c.Surface, accent, 0.06));
            using (var bar = new SolidBrush(accent)) g.FillRectangle(bar, 0, 0, S(4), Height);
            using (var pen = new Pen(c.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            var pad = S(22);
            var area = new Rectangle(pad, S(14), Math.Max(10, Width - pad * 2), Math.Max(10, Height - S(14) - S(36)));
            if (panel.Image != null)
            {
                var side = Math.Min(area.Height, S(72));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(panel.Image, new Rectangle(area.X, area.Y + (area.Height - side) / 2, side, side));
                area = new Rectangle(area.X + side + S(16), area.Y, Math.Max(10, area.Width - side - S(16)), area.Height);
            }

            using var titleFont = new Font(Font.FontFamily, Font.Size * 1.3f, FontStyle.Bold);
            var titleHeight = string.IsNullOrEmpty(panel.Title) ? 0 : titleFont.Height + S(6);
            layout = MarkupLayout.Create(LabelMarkup.Parse(panel.Content, panel.Mode), Font, c, area.Width, true, _alignment, 0, true, c.Text);
            var total = titleHeight + layout.Size.Height;
            var top = area.Y + Math.Max(0, (area.Height - total) / 2);
            if (titleHeight > 0)
                TextRenderer.DrawText(g, panel.Title, titleFont, new Rectangle(area.X, top, area.Width, titleFont.Height + 2), accent == c.Primary ? c.Text : accent, ToFlags(_alignment));
            origin = new Point(area.X, top + titleHeight);
            layout.Draw(g, origin);
        }

        return _faces[index] = new Face(bitmap, layout, origin);
    }

    private static TextFormatFlags ToFlags(HorizontalAlignment alignment) => TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.Top
        | (alignment == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : alignment == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);

    private static ImageAttributes Fade(float opacity)
    {
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity }, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        return attributes;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Colors.Surface);
        var c = Colors;
        if (_panels.Count == 0)
        {
            Chrome.DrawText(g, "No panels", Font, new Rectangle(0, 0, Width, Height), c.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var bounds = new Rectangle(0, 0, Width, Height);
        if (_from >= 0 && _from < _panels.Count && _progress.IsRunning) PaintTransition(g, bounds);
        else g.DrawImageUnscaled(FaceOf(_index).Bitmap, 0, 0);

        PaintChrome(g, c);
        if (Focused)
        {
            using var ring = new Pen(c.FocusRing, 2f);
            g.DrawRectangle(ring, 1, 1, Width - 3, Height - 3);
        }
    }

    private void PaintTransition(Graphics g, Rectangle b)
    {
        var from = FaceOf(_from).Bitmap;
        var to = FaceOf(_index).Bitmap;
        var p = Math.Clamp(_progress.Value, 0, 1);
        var effect = _effect;
        if (_direction < 0) effect = effect switch { RotatorEffect.SlideLeft => RotatorEffect.SlideRight, RotatorEffect.SlideRight => RotatorEffect.SlideLeft, RotatorEffect.SlideUp => RotatorEffect.SlideDown, RotatorEffect.SlideDown => RotatorEffect.SlideUp, _ => effect };
        var w = b.Width;
        var h = b.Height;
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.SetClip(b);
        switch (effect)
        {
            case RotatorEffect.SlideLeft:
                g.DrawImageUnscaled(from, (int)(-p * w), 0);
                g.DrawImageUnscaled(to, (int)(w - p * w), 0);
                break;
            case RotatorEffect.SlideRight:
                g.DrawImageUnscaled(from, (int)(p * w), 0);
                g.DrawImageUnscaled(to, (int)(-w + p * w), 0);
                break;
            case RotatorEffect.SlideUp:
                g.DrawImageUnscaled(from, 0, (int)(-p * h));
                g.DrawImageUnscaled(to, 0, (int)(h - p * h));
                break;
            case RotatorEffect.SlideDown:
                g.DrawImageUnscaled(from, 0, (int)(p * h));
                g.DrawImageUnscaled(to, 0, (int)(-h + p * h));
                break;
            case RotatorEffect.FlipHorizontal:
            {
                var face = p < 0.5 ? from : to;
                var scale = Math.Abs(1 - 2 * p);
                var width = Math.Max(1, (int)(w * scale));
                using var fill = new SolidBrush(Colors.Surface);
                g.FillRectangle(fill, b);
                g.DrawImage(face, new Rectangle((w - width) / 2, (int)(h * 0.03 * (1 - scale)), width, (int)(h * (1 - 0.06 * (1 - scale)))));
                break;
            }
            case RotatorEffect.FlipVertical:
            {
                var face = p < 0.5 ? from : to;
                var scale = Math.Abs(1 - 2 * p);
                var height = Math.Max(1, (int)(h * scale));
                using var fill = new SolidBrush(Colors.Surface);
                g.FillRectangle(fill, b);
                g.DrawImage(face, new Rectangle((int)(w * 0.03 * (1 - scale)), (h - height) / 2, (int)(w * (1 - 0.06 * (1 - scale))), height));
                break;
            }
            default:
            {
                g.DrawImageUnscaled(from, 0, 0);
                using var attributes = Fade((float)p);
                g.DrawImage(to, b, 0, 0, to.Width, to.Height, GraphicsUnit.Pixel, attributes);
                break;
            }
        }

        g.Restore(state);
    }

    private Rectangle NavRect(int side)
    {
        var size = S(32);
        return new Rectangle(side == 0 ? S(6) : Width - S(6) - size, (Height - size) / 2, size, size);
    }

    private bool NavEnabled(int side) => _loop ? _panels.Count > 1 : side == 0 ? _index > 0 : _index < _panels.Count - 1;

    private Rectangle DotRect(int i)
    {
        var dot = S(8);
        var gap = S(8);
        var total = _panels.Count * dot + (_panels.Count - 1) * gap;
        return new Rectangle((Width - total) / 2 + i * (dot + gap), Height - S(20), dot, dot);
    }

    private void PaintChrome(Graphics g, ControlColors c)
    {
        var show = Math.Max(_chrome.Value, Focused ? 1 : 0);
        if (_showNavigation && _panels.Count > 1 && show > 0.02)
        {
            for (var side = 0; side < 2; side++)
            {
                if (!NavEnabled(side)) continue;
                var r = NavRect(side);
                var alpha = (int)(225 * show);
                using (var brush = new SolidBrush(Color.FromArgb(alpha, _hotNav == side ? c.Primary : c.Surface))) g.FillEllipse(brush, r);
                using (var pen = new Pen(Color.FromArgb(alpha, c.Border))) g.DrawEllipse(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                Chrome.DrawChevron(g, r, side == 0 ? ChevronDirection.Left : ChevronDirection.Right, Color.FromArgb(Math.Min(255, alpha + 30), _hotNav == side ? c.PrimaryText : c.Text), 2f);
            }
        }

        if (_showIndicators && _panels.Count is > 1 and <= 24)
        {
            for (var i = 0; i < _panels.Count; i++)
            {
                var r = DotRect(i);
                if (i == _index) r = Rectangle.Inflate(r, S(4), 0);
                Chrome.FillRounded(g, r, i == _index ? c.Primary : Chrome.Blend(c.Surface, c.Muted, 0.5), r.Height / 2);
            }
        }

        if (_showTimerBar && IsRunning && _barClock.Enabled)
        {
            var fraction = Math.Clamp(_sinceChange.ElapsedMilliseconds / (double)_timer.Interval, 0, 1);
            using var brush = new SolidBrush(Color.FromArgb(170, c.Primary));
            g.FillRectangle(brush, 0, Height - S(3), (int)(Width * fraction), S(3));
        }

        if (_userPaused)
        {
            var r = new Rectangle(Width - S(26), S(8), S(18), S(18));
            using var brush = new SolidBrush(Color.FromArgb(200, c.Muted));
            g.FillRectangle(brush, r.X + S(3), r.Y + S(2), S(4), r.Height - S(4));
            g.FillRectangle(brush, r.X + S(11), r.Y + S(2), S(4), r.Height - S(4));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _chrome.AnimateTo(1, 140);
        RestartTimer();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotNav = -1;
        if (!Focused) _chrome.AnimateTo(0, 220);
        RestartTimer();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        RestartTimer();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        RestartTimer();
        Invalidate();
    }

    private string? LinkAt(Point p)
    {
        if (_panels.Count == 0 || _progress.IsRunning && _from >= 0) return null;
        var face = FaceOf(_index);
        return face.Layout?.LinkAt(p, face.Origin);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = -1;
        if (_showNavigation && _panels.Count > 1) for (var s = 0; s < 2; s++) if (NavEnabled(s) && NavRect(s).Contains(e.Location)) hot = s;
        Cursor = hot >= 0 || LinkAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
        if (hot == _hotNav) return;
        _hotNav = hot;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        for (var s = 0; s < 2; s++)
        {
            if (!_showNavigation || _panels.Count < 2 || !NavEnabled(s) || !NavRect(s).Contains(e.Location)) continue;
            Move(s == 0 ? -1 : 1);
            return;
        }

        if (_showIndicators && _panels.Count is > 1 and <= 24)
        {
            for (var i = 0; i < _panels.Count; i++)
            {
                if (!Rectangle.Inflate(DotRect(i), S(5), S(6)).Contains(e.Location)) continue;
                GoTo(i);
                return;
            }
        }

        if (LinkAt(e.Location) is { } href) LinkClicked?.Invoke(this, new LinkClickedEventArgs(href));
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_panels.Count == 0) return;
        var rtl = RightToLeft == RightToLeft.Yes ? -1 : 1;
        switch (e.KeyCode)
        {
            case Keys.Right: Move(rtl); break;
            case Keys.Left: Move(-rtl); break;
            case Keys.Home: GoTo(0, -1); break;
            case Keys.End: GoTo(_panels.Count - 1, 1); break;
            case Keys.Space: TogglePause(); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () =>
    {
        if (_panels.Count == 0) return "Empty";
        var panel = _panels[_index];
        var title = panel.Title ?? LabelMarkup.ToPlainText(LabelMarkup.Parse(panel.Content, panel.Mode));
        return $"Panel {_index + 1} of {_panels.Count}: {title}";
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _barClock.Dispose();
            _progress.Dispose();
            _chrome.Dispose();
            DisposeFaces();
        }

        base.Dispose(disposing);
    }
}

public sealed class RotatorChangingEventArgs : CancelEventArgs
{
    public RotatorChangingEventArgs(int from, int to)
    {
        From = from;
        To = to;
    }

    public int From { get; }
    public int To { get; }
}
