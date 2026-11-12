using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum RatingSymbolShape { Star, Heart, Circle, Diamond }

public sealed class RatingSymbolPaintEventArgs : EventArgs
{
    public RatingSymbolPaintEventArgs(Graphics graphics, RectangleF bounds, int index, double fill, bool isHot, Color fillColor, Color emptyColor)
    {
        Graphics = graphics;
        Bounds = bounds;
        Index = index;
        Fill = fill;
        IsHot = isHot;
        FillColor = fillColor;
        EmptyColor = emptyColor;
    }

    public Graphics Graphics { get; }
    public RectangleF Bounds { get; }
    public int Index { get; }

    /// <summary>0..1: how much of this symbol is filled.</summary>
    public double Fill { get; }
    public bool IsHot { get; }
    public Color FillColor { get; }
    public Color EmptyColor { get; }
    public bool Handled { get; set; }
}

/// <summary>Interactive rating with whole, half or free precision, hover preview and tooltip, read-only mode and custom symbol shapes.</summary>
public class EnterpriseStarRatingControl : EnterpriseControlBase
{
    private readonly ThemedToolTip _tip;
    private int _maximum = 5;
    private double _value;
    private double? _hot;
    private RatingPrecision _precision = RatingPrecision.Full;
    private RatingSymbolShape _shape = RatingSymbolShape.Star;
    private int _symbolSize = 26;
    private int _spacing = 4;
    private bool _readOnly;
    private bool _allowClear = true;
    private bool _showValueText;
    private Color _fillColor = Color.Empty;
    private Color _emptyColor = Color.Empty;

    public EnterpriseStarRatingControl()
    {
        Size = new Size(190, 36);
        AccessibleRole = AccessibleRole.Slider;
        Cursor = Cursors.Hand;
        _tip = new ThemedToolTip(() => Colors, () => Font);
    }

    [DefaultValue(5)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Clamp(value, 1, 20);
            _value = RatingMath.Snap(_value, _maximum, _precision);
            Invalidate();
        }
    }

    [DefaultValue(0.0)]
    public double Value
    {
        get => _value;
        set
        {
            value = RatingMath.Snap(value, _maximum, _precision);
            if (Math.Abs(value - _value) < 1e-9) return;
            _value = value;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue(RatingPrecision.Full)]
    public RatingPrecision Precision
    {
        get => _precision;
        set { _precision = value; _value = RatingMath.Snap(_value, _maximum, value); Invalidate(); }
    }

    [DefaultValue(RatingSymbolShape.Star)]
    public RatingSymbolShape SymbolShape
    {
        get => _shape;
        set { _shape = value; Invalidate(); }
    }

    /// <summary>Supplies the outline of a custom symbol for a bounding box; partial fill is applied by clipping.</summary>
    [Browsable(false)]
    public Func<RectangleF, GraphicsPath>? SymbolPathFactory { get; set; }

    [DefaultValue(26)]
    public int SymbolSize
    {
        get => _symbolSize;
        set { _symbolSize = Math.Clamp(value, 8, 128); Invalidate(); }
    }

    [DefaultValue(4)]
    public int SymbolSpacing
    {
        get => _spacing;
        set { _spacing = Math.Clamp(value, 0, 32); Invalidate(); }
    }

    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            Cursor = value ? Cursors.Default : Cursors.Hand;
            _hot = null;
            _tip.Hide();
            Invalidate();
        }
    }

    /// <summary>Choosing the current value again (or pressing Delete) resets the rating to zero.</summary>
    [DefaultValue(true)]
    public bool AllowClear { get => _allowClear; set => _allowClear = value; }

    [DefaultValue(false)]
    public bool ShowValueText
    {
        get => _showValueText;
        set { _showValueText = value; Invalidate(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public Color EmptyColor
    {
        get => _emptyColor;
        set { _emptyColor = value; Invalidate(); }
    }

    private bool ShouldSerializeFillColor() => !_fillColor.IsEmpty;
    private void ResetFillColor() => FillColor = Color.Empty;
    private bool ShouldSerializeEmptyColor() => !_emptyColor.IsEmpty;
    private void ResetEmptyColor() => EmptyColor = Color.Empty;

    /// <summary>One description per whole value, shown in the hover tooltip (for example Poor, Fair, Good, Great, Excellent).</summary>
    [Browsable(false)]
    public string[]? Descriptions { get; set; }

    [Browsable(false)]
    public Func<double, string>? TooltipFormatter { get; set; }

    public event EventHandler? ValueChanged;
    public event EventHandler<double>? HoverValueChanged;
    public event EventHandler<RatingSymbolPaintEventArgs>? PaintSymbol;

    private int Cell => S(_symbolSize + _spacing);
    private int Left => S(4);
    private int Top => (Height - S(_symbolSize)) / 2;

    public override Size GetPreferredSize(Size proposedSize) =>
        new(Left * 2 + Cell * _maximum - S(_spacing) + (_showValueText ? S(44) : 0), S(_symbolSize) + S(10));

    private double? ValueAtMouse(Point p)
    {
        if (p.X < Left - S(2)) return 0;
        var raw = RatingMath.ValueAt(p.X - Left, Cell * (double)_maximum, _maximum, _precision, RightToLeft == RightToLeft.Yes);
        return raw;
    }

    public string DescribeValue(double value)
    {
        if (TooltipFormatter != null) return TooltipFormatter(value);
        var whole = (int)Math.Ceiling(value - 1e-9);
        if (Descriptions != null && whole >= 1 && whole <= Descriptions.Length) return $"{value.ToString("0.##", CultureInfo.CurrentCulture)} — {Descriptions[whole - 1]}";
        return $"{value.ToString("0.##", CultureInfo.CurrentCulture)} of {_maximum}";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_readOnly || !Enabled) return;
        var hot = ValueAtMouse(e.Location);
        if (hot is <= 0) hot = null;
        if (hot == _hot) return;
        _hot = hot;
        Invalidate();
        if (hot is { } v)
        {
            HoverValueChanged?.Invoke(this, v);
            _tip.Show(this, DescribeValue(v), new Point(Math.Max(0, e.X - S(24)), Height + S(2)));
        }
        else _tip.Hide();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot == null) return;
        _hot = null;
        _tip.Hide();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _readOnly || !Enabled) return;
        Focus();
        var chosen = ValueAtMouse(e.Location);
        if (chosen is not { } v || v <= 0) return;
        Value = _allowClear && Math.Abs(v - _value) < 1e-9 ? 0 : v;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_readOnly || !Enabled) return;
        var rtl = RightToLeft == RightToLeft.Yes ? -1 : 1;
        switch (e.KeyCode)
        {
            case Keys.Right or Keys.Up: Value = RatingMath.Step(_value, rtl, _maximum, _precision); break;
            case Keys.Left or Keys.Down: Value = RatingMath.Step(_value, -rtl, _maximum, _precision); break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = _maximum; break;
            case Keys.Delete or Keys.Back when _allowClear: Value = 0; break;
            case >= Keys.D0 and <= Keys.D9: Value = Math.Min(_maximum, e.KeyCode - Keys.D0); break;
            case >= Keys.NumPad0 and <= Keys.NumPad9: Value = Math.Min(_maximum, e.KeyCode - Keys.NumPad0); break;
            default: return;
        }

        e.Handled = true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this,
        () => $"{_value.ToString("0.##", CultureInfo.CurrentCulture)} out of {_maximum}",
        () => _readOnly ? AccessibleStates.ReadOnly : AccessibleStates.None,
        text =>
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) && !_readOnly) Value = parsed;
        });

    private GraphicsPath SymbolPath(RectangleF bounds) => SymbolPathFactory?.Invoke(bounds) ?? _shape switch
    {
        RatingSymbolShape.Heart => Shapes.Heart(bounds),
        RatingSymbolShape.Circle => Shapes.Circle(bounds),
        RatingSymbolShape.Diamond => Shapes.Diamond(bounds),
        _ => Shapes.Star(bounds)
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var shown = _hot ?? _value;
        var fillColor = !Enabled ? c.Disabled : _fillColor.IsEmpty ? (c.HighContrast ? c.Primary : c.Warning) : _fillColor;
        if (_hot != null && !c.HighContrast) fillColor = Chrome.Blend(fillColor, c.Primary, 0.2);
        var emptyColor = _emptyColor.IsEmpty ? Chrome.Blend(c.Surface, c.Muted, 0.35) : _emptyColor;
        var size = S(_symbolSize);
        var rtl = RightToLeft == RightToLeft.Yes;

        for (var i = 0; i < _maximum; i++)
        {
            var slot = rtl ? _maximum - 1 - i : i;
            var bounds = new RectangleF(Left + slot * Cell, Top, size, size);
            var fill = RatingMath.FillOf(i, shown);
            var args = new RatingSymbolPaintEventArgs(g, bounds, i, fill, _hot != null, fillColor, emptyColor);
            PaintSymbol?.Invoke(this, args);
            if (args.Handled) continue;

            using var path = SymbolPath(bounds);
            using (var empty = new SolidBrush(emptyColor)) g.FillPath(empty, path);
            if (fill > 0)
            {
                var state = g.Save();
                var width = (float)(bounds.Width * fill);
                g.SetClip(rtl ? new RectangleF(bounds.Right - width, bounds.Y - 2, width, bounds.Height + 4) : new RectangleF(bounds.X, bounds.Y - 2, width, bounds.Height + 4));
                using var brush = new SolidBrush(fillColor);
                g.FillPath(brush, path);
                g.Restore(state);
            }

            using var pen = new Pen(fill > 0 ? Chrome.Blend(fillColor, Color.Black, 0.12) : Chrome.Blend(emptyColor, c.Muted, 0.4), 1f) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }

        if (_showValueText)
        {
            var text = _value.ToString("0.0#", CultureInfo.CurrentCulture);
            Chrome.DrawText(g, text, BoldFont, new Rectangle(Left + Cell * _maximum, 0, Width - Left - Cell * _maximum, Height), c.Text, ContentAlignment.MiddleLeft);
        }

        if (Focused && Enabled)
        {
            using var ring = new Pen(c.FocusRing, 2f) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(ring, 1, 1, Width - 3, Height - 3);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
