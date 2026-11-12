using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

/// <summary>Multi-step indicator with completed, active, upcoming, skipped and validation-status markers and click or keyboard navigation.</summary>
public class EnterpriseStepProgressBarControl : EnterpriseControlBase
{
    private readonly StepFlow _flow = new();
    private readonly ValueAnimator _progress = new();
    private Orientation _orientation = Orientation.Horizontal;
    private bool _clickable = true;
    private bool _showDescriptions = true;
    private int _focusIndex;
    private int _hotIndex = -1;

    public EnterpriseStepProgressBarControl()
    {
        Size = new Size(560, 96);
        AccessibleRole = AccessibleRole.List;
        _flow.Changed += (_, _) =>
        {
            _progress.AnimateTo(_flow.Progress, 350, EasingKind.EaseInOut);
            _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, _flow.Steps.Count - 1));
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            Invalidate();
            StepChanged?.Invoke(this, EventArgs.Empty);
        };
        _flow.Changing += (_, e) => StepChanging?.Invoke(this, e);
        _progress.Updated += (_, _) => Invalidate();
    }

    /// <summary>Steps and navigation rules; use it for status changes and custom validation.</summary>
    [Browsable(false)]
    public StepFlow Flow => _flow;

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

    /// <summary>Steps can be activated by click or Enter; navigation still follows <see cref="AllowJumpAhead"/>.</summary>
    [DefaultValue(true)]
    public bool ClickableSteps { get => _clickable; set { _clickable = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowDescriptions { get => _showDescriptions; set { _showDescriptions = value; Invalidate(); } }

    [DefaultValue(false)]
    public bool AllowJumpAhead { get => _flow.AllowJumpAhead; set => _flow.AllowJumpAhead = value; }

    [DefaultValue(0)]
    public int CurrentStep
    {
        get => _flow.Current;
        set => _flow.TryNavigateTo(value);
    }

    [Browsable(false)]
    public int StepCount => _flow.Steps.Count;

    /// <summary>Raised before moving forward; set <see cref="StepChangingEventArgs.Cancel"/> to reject and mark the step as invalid.</summary>
    public event EventHandler<StepChangingEventArgs>? StepChanging;
    public event EventHandler? StepChanged;
    public event EventHandler<int>? StepClicked;

    public void AddStep(string title, string? description = null, string? glyph = null, bool optional = false)
    {
        _flow.Add(title, description, glyph, optional);
        Invalidate();
    }

    public void ClearSteps()
    {
        _flow.Steps.Clear();
        _flow.Reset();
        Invalidate();
    }

    public bool Next() => _flow.Next();
    public bool Previous() => _flow.Previous();
    public bool Finish() => _flow.Finish();
    public void Reset() => _flow.Reset();
    public void SetStatus(int index, StepStatus status, string? message = null) => _flow.SetStatus(index, status, message);

    private bool Horizontal => _orientation == Orientation.Horizontal;
    private int Marker => S(28);

    private Rectangle StepBounds(int i)
    {
        var n = Math.Max(1, _flow.Steps.Count);
        if (Horizontal)
        {
            var slot = Width / (double)n;
            return new Rectangle((int)(i * slot), 0, (int)slot, Height);
        }

        var row = Math.Max(Marker + S(8), Height / n);
        return new Rectangle(0, i * row, Width, row);
    }

    private Point MarkerCenter(int i)
    {
        var b = StepBounds(i);
        return Horizontal ? new Point(b.X + b.Width / 2, S(6) + Marker / 2) : new Point(S(6) + Marker / 2, b.Y + S(4) + Marker / 2);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _flow.Steps.Count; i++) if (StepBounds(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        var hot = _clickable && hit >= 0 && _flow.CanNavigateTo(hit) ? hit : -1;
        if (hot == _hotIndex) return;
        _hotIndex = hot;
        Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotIndex < 0) return;
        _hotIndex = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        var hit = HitTest(e.Location);
        if (hit < 0) return;
        _focusIndex = hit;
        Activate(hit);
    }

    private void Activate(int index)
    {
        if (!_clickable) return;
        StepClicked?.Invoke(this, index);
        _flow.TryNavigateTo(index);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_flow.Steps.Count == 0 || !Enabled) return;
        var rtl = RightToLeft == RightToLeft.Yes && Horizontal ? -1 : 1;
        switch (e.KeyCode)
        {
            case Keys.Right when Horizontal: case Keys.Down when !Horizontal: _focusIndex = Math.Clamp(_focusIndex + rtl, 0, _flow.Steps.Count - 1); break;
            case Keys.Left when Horizontal: case Keys.Up when !Horizontal: _focusIndex = Math.Clamp(_focusIndex - rtl, 0, _flow.Steps.Count - 1); break;
            case Keys.Home: _focusIndex = 0; break;
            case Keys.End: _focusIndex = _flow.Steps.Count - 1; break;
            case Keys.Enter or Keys.Space: Activate(_focusIndex); break;
            default: return;
        }

        Invalidate();
        e.Handled = true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () =>
    {
        if (_flow.Steps.Count == 0) return string.Empty;
        var step = _flow.Steps[_flow.Current];
        return _flow.IsFinished ? "All steps completed" : $"Step {_flow.Current + 1} of {_flow.Steps.Count}: {step.Title} ({step.Status})";
    });

    private Color StatusColor(StepStatus status)
    {
        var c = Colors;
        return status switch
        {
            StepStatus.Completed => c.Success,
            StepStatus.Error => c.Danger,
            StepStatus.Warning => c.Warning,
            StepStatus.Active => c.Primary,
            _ => c.Muted
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var steps = _flow.Steps;
        if (steps.Count == 0) return;
        var half = Marker / 2;
        var thickness = Math.Max(2, S(3));

        for (var i = 0; i < steps.Count - 1; i++)
        {
            var a = MarkerCenter(i);
            var b = MarkerCenter(i + 1);
            var fraction = Math.Clamp(_progress.Value * (steps.Count - 1) - i, 0, 1);
            var from = Horizontal ? new Point(a.X + half + S(4), a.Y) : new Point(a.X, a.Y + half + S(4));
            var to = Horizontal ? new Point(b.X - half - S(4), b.Y) : new Point(b.X, b.Y - half - S(4));
            using var track = new Pen(Chrome.Blend(c.Border, c.Muted, c.HighContrast ? 0 : 0.35), thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(track, from, to);
            if (fraction > 0)
            {
                using var done = new Pen(Enabled ? c.Success : c.Disabled, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(done, from, new Point(from.X + (int)((to.X - from.X) * fraction), from.Y + (int)((to.Y - from.Y) * fraction)));
            }
        }

        for (var i = 0; i < steps.Count; i++) PaintStep(g, c, i, steps[i]);
    }

    private void PaintStep(Graphics g, ControlColors c, int i, StepEntry step)
    {
        var center = MarkerCenter(i);
        var half = Marker / 2;
        var rect = new Rectangle(center.X - half, center.Y - half, Marker, Marker);
        var color = Enabled ? StatusColor(step.Status) : c.Disabled;
        var hot = i == _hotIndex;
        var bounds = StepBounds(i);

        if (hot)
        {
            using var halo = new SolidBrush(Color.FromArgb(40, color));
            g.FillEllipse(halo, Rectangle.Inflate(rect, S(4), S(4)));
        }

        var filled = step.Status is StepStatus.Completed or StepStatus.Error or StepStatus.Warning;
        if (filled)
        {
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, rect);
        }
        else
        {
            using var back = new SolidBrush(c.Surface);
            g.FillEllipse(back, rect);
            using var pen = new Pen(color, step.Status == StepStatus.Active ? S(3) : S(2));
            g.DrawEllipse(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            if (step.Status == StepStatus.Active)
            {
                using var dot = new SolidBrush(Color.FromArgb(40, color));
                g.FillEllipse(dot, Rectangle.Inflate(rect, -S(4), -S(4)));
            }
        }

        var markContent = filled ? ColorUtil.Readable(color) : color;
        switch (step.Status)
        {
            case StepStatus.Completed:
                using (var pen = new Pen(markContent, Math.Max(2f, S(2.5))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] { new PointF(rect.X + rect.Width * 0.28f, rect.Y + rect.Height * 0.52f), new PointF(rect.X + rect.Width * 0.44f, rect.Y + rect.Height * 0.68f), new PointF(rect.X + rect.Width * 0.74f, rect.Y + rect.Height * 0.34f) });
                break;
            case StepStatus.Error or StepStatus.Warning:
                Chrome.DrawText(g, "!", BoldFont, rect, markContent, ContentAlignment.MiddleCenter);
                break;
            case StepStatus.Skipped:
                using (var pen = new Pen(markContent, S(2))) g.DrawLine(pen, rect.X + rect.Width * 0.3f, rect.Y + rect.Height / 2f, rect.Right - rect.Width * 0.3f, rect.Y + rect.Height / 2f);
                break;
            default:
                if (step.Glyph != null) TextRenderer.DrawText(g, step.Glyph, GlyphFonts.Get(Font.SizeInPoints), rect, markContent, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                else Chrome.DrawText(g, (i + 1).ToString(), BoldFont, rect, markContent, ContentAlignment.MiddleCenter);
                break;
        }

        var titleColor = !Enabled ? c.Disabled : step.Status == StepStatus.Pending || step.Status == StepStatus.Skipped ? c.Muted : c.Text;
        var textBounds = Horizontal
            ? new Rectangle(bounds.X + S(4), rect.Bottom + S(6), bounds.Width - S(8), Math.Max(0, bounds.Bottom - rect.Bottom - S(8)))
            : new Rectangle(rect.Right + S(12), bounds.Y + S(4), Math.Max(0, bounds.Width - rect.Right - S(16)), Math.Max(0, bounds.Height - S(8)));
        var align = Horizontal ? ContentAlignment.TopCenter : ContentAlignment.TopLeft;
        var titleHeight = BoldFont.Height + S(2);
        var title = step.IsOptional ? step.Title + " (optional)" : step.Title;
        var titleRect = new Rectangle(textBounds.X, textBounds.Y, textBounds.Width, Math.Min(textBounds.Height, titleHeight));
        Chrome.DrawText(g, title, step.Status == StepStatus.Active ? BoldFont : Font, titleRect, titleColor, align);

        var detail = step.Message ?? (_showDescriptions ? step.Description : null);
        if (!string.IsNullOrEmpty(detail) && textBounds.Height > titleHeight)
        {
            var color2 = step.Message != null && step.Status == StepStatus.Error ? c.Danger : c.Muted;
            Chrome.DrawText(g, detail, Font, new Rectangle(textBounds.X, textBounds.Y + titleHeight, textBounds.Width, textBounds.Height - titleHeight), color2, align, true);
        }

        if (Focused && i == _focusIndex && Enabled)
        {
            using var ring = new Pen(c.FocusRing, 2f);
            g.DrawEllipse(ring, rect.X - S(4), rect.Y - S(4), rect.Width + S(8), rect.Height + S(8));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _progress.Dispose();
        base.Dispose(disposing);
    }
}

public enum WaitingBarAnimation { SpinningDots, Ring, WaveBars, BouncingBlocks, Marquee }

public enum WaitingTextPosition { None, Right, Below }

/// <summary>Busy indicator with several looping animation modes and an optional caption.</summary>
public class EnterpriseWaitingBarControl : EnterpriseControlBase
{
    private readonly FrameClock _clock = new();
    private WaitingBarAnimation _animation = WaitingBarAnimation.SpinningDots;
    private WaitingTextPosition _textPosition = WaitingTextPosition.Right;
    private bool _isWaiting = true;
    private bool _hideWhenIdle;
    private bool _reversed;
    private double _speed = 1;
    private int _indicatorSize = 32;
    private int _elementCount;
    private Color _indicatorColor = Color.Empty;
    private Color _secondaryColor = Color.Empty;

    public EnterpriseWaitingBarControl()
    {
        Size = new Size(220, 44);
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.ProgressBar;
        _clock.Frame += (_, _) => Invalidate();
    }

    [DefaultValue(WaitingBarAnimation.SpinningDots)]
    public WaitingBarAnimation Animation
    {
        get => _animation;
        set { _animation = value; Invalidate(); }
    }

    [DefaultValue(true)]
    public bool IsWaiting
    {
        get => _isWaiting;
        set
        {
            if (_isWaiting == value) return;
            _isWaiting = value;
            UpdateClock();
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            Invalidate();
            WaitingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue(WaitingTextPosition.Right)]
    public WaitingTextPosition TextPosition
    {
        get => _textPosition;
        set { _textPosition = value; Invalidate(); }
    }

    /// <summary>Hide the indicator (not the caption) while idle.</summary>
    [DefaultValue(false)]
    public bool HideWhenIdle { get => _hideWhenIdle; set { _hideWhenIdle = value; Invalidate(); } }

    [DefaultValue(false)]
    public bool Reversed { get => _reversed; set { _reversed = value; Invalidate(); } }

    [DefaultValue(1.0)]
    public double AnimationSpeed
    {
        get => _speed;
        set { _speed = Math.Clamp(value, 0.1, 10); Invalidate(); }
    }

    [DefaultValue(32)]
    public int IndicatorSize { get => _indicatorSize; set { _indicatorSize = Math.Clamp(value, 12, 200); Invalidate(); } }

    /// <summary>Dots, bars or blocks; 0 uses the default for the animation.</summary>
    [DefaultValue(0)]
    public int ElementCount { get => _elementCount; set { _elementCount = Math.Clamp(value, 0, 24); Invalidate(); } }

    public Color IndicatorColor { get => _indicatorColor; set { _indicatorColor = value; Invalidate(); } }
    public Color SecondaryColor { get => _secondaryColor; set { _secondaryColor = value; Invalidate(); } }

    private bool ShouldSerializeIndicatorColor() => !_indicatorColor.IsEmpty;
    private void ResetIndicatorColor() => IndicatorColor = Color.Empty;
    private bool ShouldSerializeSecondaryColor() => !_secondaryColor.IsEmpty;
    private void ResetSecondaryColor() => SecondaryColor = Color.Empty;

    public event EventHandler? WaitingChanged;

    public void StartWaiting() => IsWaiting = true;
    public void StopWaiting() => IsWaiting = false;

    private void UpdateClock() => _clock.Enabled = IsHandleCreated && !DesignMode && Visible && _isWaiting;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateClock();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _clock.Enabled = false;
        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateClock();
    }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new ValueAccessibleObject(this, () => _isWaiting ? (string.IsNullOrEmpty(Text) ? "Busy" : Text) : "Idle", () => _isWaiting ? AccessibleStates.Busy : AccessibleStates.None);

    private int Count => _elementCount > 0 ? _elementCount : _animation switch { WaitingBarAnimation.WaveBars => 5, WaitingBarAnimation.BouncingBlocks => 3, _ => 10 };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var color = !Enabled ? c.Disabled : _indicatorColor.IsEmpty ? c.Primary : _indicatorColor;
        var track = _secondaryColor.IsEmpty ? Chrome.Blend(c.Surface, c.Border, 0.6) : _secondaryColor;
        var t = _isWaiting && _speed > 0 ? _clock.Seconds * _speed : 0;
        if (_reversed) t = -t;
        var text = Text;
        var size = S(_indicatorSize);

        Rectangle indicator, textRect = Rectangle.Empty;
        if (_animation == WaitingBarAnimation.Marquee)
        {
            var bar = Math.Max(S(4), size / 5);
            var hasText = text.Length > 0 && _textPosition != WaitingTextPosition.None;
            var total = bar + (hasText ? Font.Height + S(6) : 0);
            var top = Math.Max(0, (Height - total) / 2);
            indicator = new Rectangle(S(4), top, Math.Max(1, Width - S(8)), bar);
            if (hasText) textRect = new Rectangle(0, indicator.Bottom + S(4), Width, Font.Height + S(2));
        }
        else if (text.Length > 0 && _textPosition == WaitingTextPosition.Below)
        {
            var total = size + S(6) + Font.Height;
            var top = Math.Max(0, (Height - total) / 2);
            indicator = new Rectangle((Width - size) / 2, top, size, size);
            textRect = new Rectangle(0, indicator.Bottom + S(4), Width, Font.Height + S(2));
        }
        else
        {
            var textWidth = text.Length > 0 && _textPosition == WaitingTextPosition.Right ? Chrome.MeasureText(text, Font) + S(10) : 0;
            var left = text.Length > 0 && _textPosition == WaitingTextPosition.Right ? Math.Max(S(4), (Width - size - textWidth) / 2) : (Width - size) / 2;
            indicator = new Rectangle(left, (Height - size) / 2, size, size);
            if (textWidth > 0) textRect = new Rectangle(indicator.Right + S(8), 0, Math.Max(0, Width - indicator.Right - S(10)), Height);
        }

        if (_isWaiting || !_hideWhenIdle) PaintIndicator(g, indicator, color, track, t);
        if (!textRect.IsEmpty) Chrome.DrawText(g, text, Font, textRect, Enabled ? c.Text : c.Disabled, _textPosition == WaitingTextPosition.Right && _animation != WaitingBarAnimation.Marquee ? ContentAlignment.MiddleLeft : ContentAlignment.TopCenter);
    }

    private void PaintIndicator(Graphics g, Rectangle area, Color color, Color track, double t)
    {
        var n = Count;
        switch (_animation)
        {
            case WaitingBarAnimation.SpinningDots:
            {
                var radius = Math.Min(area.Width, area.Height) / 2f;
                var dot = Math.Max(3f, radius * 0.28f);
                var head = (t * 1.1 % 1 + 1) % 1 * n;
                for (var i = 0; i < n; i++)
                {
                    var angle = i * 2 * Math.PI / n - Math.PI / 2;
                    var behind = ((head - i) % n + n) % n / n;
                    var strength = Math.Pow(1 - behind, 2.2);
                    var r = dot * (0.55f + 0.45f * (float)strength);
                    var cx = area.X + radius + (float)Math.Cos(angle) * (radius - dot);
                    var cy = area.Y + radius + (float)Math.Sin(angle) * (radius - dot);
                    using var brush = new SolidBrush(Chrome.Blend(track, color, strength));
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                }

                break;
            }
            case WaitingBarAnimation.Ring:
            {
                var pen = Math.Max(3f, area.Width / 8f);
                var rect = new RectangleF(area.X + pen / 2, area.Y + pen / 2, area.Width - pen, area.Height - pen);
                using var back = new Pen(track, pen);
                g.DrawEllipse(back, rect);
                var sweep = 70 + 200 * (0.5 + 0.5 * Math.Sin(t * 2.4));
                using var arc = new Pen(color, pen) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(arc, rect, (float)(t * 300 % 360 - 90), (float)sweep);
                break;
            }
            case WaitingBarAnimation.WaveBars:
            {
                var gap = Math.Max(2, area.Width / (n * 4));
                var bar = Math.Max(3, (area.Width - gap * (n - 1)) / n);
                var total = bar * n + gap * (n - 1);
                var x0 = area.X + (area.Width - total) / 2;
                for (var i = 0; i < n; i++)
                {
                    var wave = 0.5 + 0.5 * Math.Sin(t * 2 * Math.PI * 0.9 - i * 0.7);
                    var h = (int)(area.Height * (0.28 + 0.72 * wave));
                    var rect = new Rectangle(x0 + i * (bar + gap), area.Y + (area.Height - h) / 2, bar, h);
                    Chrome.FillRounded(g, rect, Chrome.Blend(track, color, 0.45 + 0.55 * wave), Math.Max(1, bar / 2));
                }

                break;
            }
            case WaitingBarAnimation.BouncingBlocks:
            {
                var block = Math.Max(4, area.Width / (n * 2));
                var gap = Math.Max(3, block / 2);
                var total = block * n + gap * (n - 1);
                var x0 = area.X + (area.Width - total) / 2;
                var travel = area.Height - block;
                for (var i = 0; i < n; i++)
                {
                    var phase = ((t * 1.1 - i * 0.16) % 1 + 1) % 1;
                    var bounce = Math.Abs(Math.Sin(phase * Math.PI));
                    var y = area.Bottom - block - (int)(travel * Easing.Apply(EasingKind.EaseOut, bounce));
                    Chrome.FillRounded(g, new Rectangle(x0 + i * (block + gap), y, block, block), Chrome.Blend(track, color, 0.5 + 0.5 * bounce), Math.Max(1, block / 4));
                }

                break;
            }
            default:
            {
                Chrome.FillRounded(g, area, track, area.Height / 2);
                var chunk = Math.Max(area.Height * 2, area.Width / 4);
                var phase = (t * 0.6 % 1 + 1) % 1;
                var x = area.X - chunk + (int)(Easing.Apply(EasingKind.EaseInOut, phase) * (area.Width + chunk));
                var state = g.Save();
                using var clip = Chrome.RoundedRect(area, area.Height / 2);
                g.SetClip(clip);
                using var brush = new LinearGradientBrush(new Rectangle(x, area.Y, chunk, area.Height), Color.FromArgb(0, color), color, LinearGradientMode.Horizontal) { WrapMode = WrapMode.TileFlipX };
                g.FillRectangle(brush, new Rectangle(x, area.Y, chunk, area.Height));
                g.Restore(state);
                break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }
}
