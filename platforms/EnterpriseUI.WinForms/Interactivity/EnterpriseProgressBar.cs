using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum EnterpriseProgressStyle { Determinate, Indeterminate }

public enum EnterpriseProgressFill { Solid, Gradient, Striped, Segmented }

public enum EnterpriseProgressState { Normal, Paused, Error }

/// <summary>Determinate or indeterminate progress bar with solid, gradient, striped or segmented fill, a text overlay and tunable animation speed.</summary>
public class EnterpriseProgressBarControl : EnterpriseControlBase
{
    private readonly ValueAnimator _display = new();
    private readonly FrameClock _clock = new() { Interval = 16 };
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private EnterpriseProgressStyle _style;
    private EnterpriseProgressFill _fill = EnterpriseProgressFill.Gradient;
    private EnterpriseProgressState _state;
    private Color _startColor = Color.Empty;
    private Color _endColor = Color.Empty;
    private Color _trackColor = Color.Empty;
    private double _speed = 1;
    private bool _showText = true;
    private string _textFormat = "{0:0}%";
    private Orientation _orientation = Orientation.Horizontal;
    private int _cornerRadius = -1;
    private bool _completedRaised;

    public EnterpriseProgressBarControl()
    {
        Size = new Size(280, 24);
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.ProgressBar;
        _display.Updated += (_, _) => Invalidate();
        _clock.Frame += (_, _) => Invalidate();
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum) _maximum = _minimum;
            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > _maximum) _minimum = _maximum;
            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, _minimum, _maximum);
            if (value == _value) return;
            _value = value;
            UpdateClock();
            if (_speed > 0) _display.AnimateTo(Fraction(value), (int)(320 / _speed), EasingKind.EaseOut); else _display.Jump(Fraction(value));
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
            if (value >= _maximum && !_completedRaised)
            {
                _completedRaised = true;
                Completed?.Invoke(this, EventArgs.Empty);
            }
            else if (value < _maximum) _completedRaised = false;
        }
    }

    [DefaultValue(EnterpriseProgressStyle.Determinate)]
    public EnterpriseProgressStyle Style
    {
        get => _style;
        set { _style = value; UpdateClock(); Invalidate(); }
    }

    [DefaultValue(EnterpriseProgressFill.Gradient)]
    public EnterpriseProgressFill Fill
    {
        get => _fill;
        set { _fill = value; UpdateClock(); Invalidate(); }
    }

    [DefaultValue(EnterpriseProgressState.Normal)]
    public EnterpriseProgressState State
    {
        get => _state;
        set { _state = value; Invalidate(); }
    }

    /// <summary>Gradient start (or the solid color); empty follows the theme accent.</summary>
    public Color FillStartColor
    {
        get => _startColor;
        set { _startColor = value; Invalidate(); }
    }

    public Color FillEndColor
    {
        get => _endColor;
        set { _endColor = value; Invalidate(); }
    }

    public Color TrackColor
    {
        get => _trackColor;
        set { _trackColor = value; Invalidate(); }
    }

    private bool ShouldSerializeFillStartColor() => !_startColor.IsEmpty;
    private void ResetFillStartColor() => FillStartColor = Color.Empty;
    private bool ShouldSerializeFillEndColor() => !_endColor.IsEmpty;
    private void ResetFillEndColor() => FillEndColor = Color.Empty;
    private bool ShouldSerializeTrackColor() => !_trackColor.IsEmpty;
    private void ResetTrackColor() => TrackColor = Color.Empty;

    /// <summary>Multiplier for marquee, stripe and value-change animation; 0 turns animation off.</summary>
    [DefaultValue(1.0)]
    public double AnimationSpeed
    {
        get => _speed;
        set { _speed = Math.Max(0, value); UpdateClock(); Invalidate(); }
    }

    [DefaultValue(true)]
    public bool ShowText
    {
        get => _showText;
        set { _showText = value; Invalidate(); }
    }

    /// <summary>Format of the overlay; {0} is the percentage. A non-empty <see cref="Control.Text"/> replaces it.</summary>
    [DefaultValue("{0:0}%")]
    public string TextFormat
    {
        get => _textFormat;
        set { _textFormat = value ?? string.Empty; Invalidate(); }
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

    /// <summary>-1 rounds the ends fully.</summary>
    [DefaultValue(-1)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    /// <summary>Progress 0..1 of the current value.</summary>
    [Browsable(false)]
    public double Percentage => Fraction(_value);

    public event EventHandler? ValueChanged;
    public event EventHandler? Completed;

    private double Fraction(int value) => _maximum > _minimum ? (value - _minimum) / (double)(_maximum - _minimum) : 0;

    private void UpdateClock() => _clock.Enabled = IsHandleCreated && Visible && _speed > 0 && (_style == EnterpriseProgressStyle.Indeterminate || (_fill == EnterpriseProgressFill.Striped && _value > _minimum));

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
        new ValueAccessibleObject(this, () => _style == EnterpriseProgressStyle.Indeterminate ? "Busy" : string.Format(CultureInfo.CurrentCulture, "{0:0}%", Fraction(_value) * 100),
            () => _style == EnterpriseProgressStyle.Indeterminate ? AccessibleStates.Busy : AccessibleStates.None);

    private string OverlayText()
    {
        if (!string.IsNullOrEmpty(Text)) return Text;
        if (_style == EnterpriseProgressStyle.Indeterminate) return string.Empty;
        try { return string.Format(CultureInfo.CurrentCulture, _textFormat, Fraction(_value) * 100); }
        catch (FormatException) { return _textFormat; }
    }

    private (Color Start, Color End) FillColors()
    {
        var c = Colors;
        var baseColor = _state switch { EnterpriseProgressState.Error => c.Danger, EnterpriseProgressState.Paused => c.Warning, _ => _startColor.IsEmpty ? c.Primary : _startColor };
        var end = _state != EnterpriseProgressState.Normal ? Chrome.Blend(baseColor, Color.White, 0.25) : _endColor.IsEmpty ? Chrome.Blend(baseColor, Color.White, 0.3) : _endColor;
        if (!Enabled) return (c.Disabled, c.Disabled);
        return (baseColor, c.HighContrast ? baseColor : end);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var track = new Rectangle(0, 0, Width, Height);
        var radius = _cornerRadius >= 0 ? _cornerRadius : Math.Min(Width, Height) / 2;
        var horizontal = _orientation == Orientation.Horizontal;
        Chrome.FillRounded(g, new Rectangle(0, 0, Width, Height), _trackColor.IsEmpty ? Chrome.Blend(c.Surface, c.Border, 0.35) : _trackColor, radius);

        var (start, end) = FillColors();
        Rectangle fillRect;
        if (_style == EnterpriseProgressStyle.Indeterminate)
        {
            var length = (int)((horizontal ? Width : Height) * 0.35);
            var span = (horizontal ? Width : Height) + length;
            var phase = _speed > 0 ? _clock.Seconds * _speed / 1.5 % 1 : 0.5;
            var offset = (int)(Easing.Apply(EasingKind.EaseInOut, phase) * span) - length;
            fillRect = horizontal ? new Rectangle(offset, 0, length, Height) : new Rectangle(0, Height - offset - length, Width, length);
        }
        else
        {
            var amount = Math.Clamp(_display.Value, 0, 1);
            fillRect = horizontal
                ? new Rectangle(0, 0, (int)Math.Round(Width * amount), Height)
                : new Rectangle(0, Height - (int)Math.Round(Height * amount), Width, (int)Math.Round(Height * amount));
        }

        var text = _showText ? OverlayText() : string.Empty;
        if (text.Length > 0) Chrome.DrawText(g, text, BoldFont, track, Enabled ? c.Text : c.Disabled, ContentAlignment.MiddleCenter);

        if (fillRect.Width > 0 && fillRect.Height > 0)
        {
            var state = g.Save();
            using var clip = Chrome.RoundedRect(track, radius);
            g.SetClip(clip);
            g.IntersectClip(fillRect);
            PaintFill(g, fillRect, track, start, end, horizontal, radius);
            if (text.Length > 0) Chrome.DrawText(g, text, BoldFont, track, ColorUtil.Readable(Chrome.Blend(start, end, 0.5)), ContentAlignment.MiddleCenter);
            g.Restore(state);
        }

        Chrome.DrawRoundedBorder(g, track, c.HighContrast ? c.Text : c.Border, radius);
    }

    private void PaintFill(Graphics g, Rectangle fillRect, Rectangle track, Color start, Color end, bool horizontal, int radius)
    {
        var bounds = _style == EnterpriseProgressStyle.Indeterminate ? fillRect : track;
        switch (_fill)
        {
            case EnterpriseProgressFill.Solid:
                using (var brush = new SolidBrush(start)) g.FillRectangle(brush, fillRect);
                break;
            case EnterpriseProgressFill.Segmented:
                var segment = Math.Max(S(8), (horizontal ? Height : Width) / 2);
                var gap = Math.Max(2, S(2));
                using (var brush = new SolidBrush(start))
                {
                    var length = horizontal ? Width : Height;
                    for (var p = 0; p < length; p += segment + gap)
                    {
                        var block = horizontal ? new Rectangle(p, 0, segment, Height) : new Rectangle(0, Height - p - segment, Width, segment);
                        if (block.IntersectsWith(fillRect)) g.FillRectangle(brush, block);
                    }
                }

                break;
            default:
                if (bounds.Width <= 0 || bounds.Height <= 0) break;
                using (var brush = new LinearGradientBrush(bounds, start, end, horizontal ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical)) g.FillRectangle(brush, fillRect);
                if (_fill == EnterpriseProgressFill.Striped) PaintStripes(g, fillRect, horizontal);
                break;
        }
    }

    private void PaintStripes(Graphics g, Rectangle area, bool horizontal)
    {
        var width = Math.Max(S(10), 8);
        var shift = _speed > 0 ? (float)(_clock.Seconds * _speed * width * 2 % (width * 2)) : 0;
        using var brush = new SolidBrush(Color.FromArgb(55, Color.White));
        var extent = Math.Max(area.Width, area.Height) + Height + Width;
        for (var x = -width * 2 + shift; x < extent; x += width * 2)
        {
            var points = horizontal
                ? new[] { new PointF(area.X + x, area.Bottom), new PointF(area.X + x + width, area.Bottom), new PointF(area.X + x + width + area.Height, area.Y), new PointF(area.X + x + area.Height, area.Y) }
                : new[] { new PointF(area.X, area.Y + x), new PointF(area.X, area.Y + x + width), new PointF(area.Right, area.Y + x + width - area.Width), new PointF(area.Right, area.Y + x - area.Width) };
            g.FillPolygon(brush, points);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _display.Dispose();
        }

        base.Dispose(disposing);
    }
}
