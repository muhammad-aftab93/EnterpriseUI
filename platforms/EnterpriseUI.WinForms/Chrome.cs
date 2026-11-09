using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum ChevronDirection { Up, Down, Left, Right }

/// <summary>Colors every data control paints with, derived from the active theme (or the system colors in Windows high contrast).</summary>
public sealed class ControlColors
{
    public Color Surface, Alt, Header, Border, Text, Muted, Primary, PrimaryText, Danger, Success, Warning;
    public Color SelectionBack, SelectionText, HoverBack, FocusRing, HighlightBack, HighlightText, ErrorBack, Disabled;
    public bool HighContrast;

    public static ControlColors FromTheme(ITheme theme)
    {
        if (SystemInformation.HighContrast)
        {
            return new ControlColors
            {
                Surface = SystemColors.Window, Alt = SystemColors.Window, Header = SystemColors.Control, Border = SystemColors.WindowText,
                Text = SystemColors.WindowText, Muted = SystemColors.GrayText, Primary = SystemColors.Highlight, PrimaryText = SystemColors.HighlightText,
                Danger = Color.Red, Success = SystemColors.WindowText, Warning = SystemColors.WindowText, SelectionBack = SystemColors.Highlight,
                SelectionText = SystemColors.HighlightText, HoverBack = SystemColors.Control, FocusRing = SystemColors.WindowText, HighlightBack = SystemColors.Highlight,
                HighlightText = SystemColors.HighlightText, ErrorBack = SystemColors.Window, Disabled = SystemColors.GrayText, HighContrast = true
            };
        }

        var p = GridPalette.FromTheme(theme);
        return new ControlColors
        {
            Surface = Hex(p.Surface), Alt = Hex(p.Alt), Header = Hex(p.Header), Border = Hex(p.Border), Text = Hex(p.Text), Muted = Hex(p.Muted),
            Primary = Hex(p.Primary), PrimaryText = Hex(p.PrimaryText), Danger = Hex(p.Danger), Success = Hex(p.Success), Warning = Hex(p.Warning),
            SelectionBack = Hex(p.SelectionBack), SelectionText = Hex(p.SelectionText), HoverBack = Hex(p.HoverBack), FocusRing = Hex(p.FocusRing),
            HighlightBack = Hex(p.HighlightBack), HighlightText = Hex(p.HighlightText), ErrorBack = Hex(p.ErrorBack), Disabled = Hex(p.Muted), HighContrast = p.HighContrast
        };
    }

    private static Color Hex(string value)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return Color.Magenta; }
    }
}

/// <summary>The shared visual language: 1 px borders, 4 px corners, a 2 px focus ring, tinted hover and selection with an accent bar.</summary>
public static class Chrome
{
    public const int CornerRadius = 4;
    public const float BorderWidth = 1f;
    public const float FocusWidth = 2f;

    public static Color Blend(Color start, Color end, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)(start.A + (end.A - start.A) * amount), (int)(start.R + (end.R - start.R) * amount),
            (int)(start.G + (end.G - start.G) * amount), (int)(start.B + (end.B - start.B) * amount));
    }

    public static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, radius * 2);
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Rectangle rect, Color color, int radius)
    {
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedBorder(Graphics g, Rectangle rect, Color color, int radius, float width = BorderWidth)
    {
        using var path = RoundedRect(new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>Surface with a border that turns into the accent when focused; the frame every input-like control starts with.</summary>
    public static void DrawFrame(Graphics g, Rectangle rect, ControlColors c, bool focused, bool hovered, int radius = CornerRadius, bool enabled = true)
    {
        FillRounded(g, rect, enabled ? c.Surface : Blend(c.Surface, c.Border, 0.12), radius);
        var border = !enabled ? c.Border : focused ? c.FocusRing : hovered ? Blend(c.Border, c.Primary, 0.55) : c.Border;
        DrawRoundedBorder(g, rect, border, radius, focused ? FocusWidth : BorderWidth);
    }

    public static void DrawFocusRing(Graphics g, Rectangle rect, ControlColors c, int radius = CornerRadius)
    {
        using var path = RoundedRect(Rectangle.Inflate(rect, -1, -1), radius);
        using var pen = new Pen(c.FocusRing, FocusWidth);
        g.DrawPath(pen, path);
    }

    /// <summary>Background of a list-like row: selection tint, hover tint or the surface; the accent bar is drawn by <see cref="DrawSelectionBar"/>.</summary>
    public static Color ItemBack(ControlColors c, bool selected, bool hovered, bool alternate = false)
    {
        if (selected) return c.SelectionBack;
        if (hovered) return c.HighContrast ? c.Header : c.HoverBack;
        return alternate && !c.HighContrast ? Blend(c.Surface, c.Alt, 0.5) : c.Surface;
    }

    public static void DrawSelectionBar(Graphics g, Rectangle row, ControlColors c, int width)
    {
        if (c.HighContrast) return;
        using var brush = new SolidBrush(c.Primary);
        g.FillRectangle(brush, row.X, row.Y + 2, width, Math.Max(1, row.Height - 4));
    }

    public static void DrawCheckBox(Graphics g, ControlColors c, Rectangle box, CheckState state, bool enabled = true, bool hovered = false)
    {
        var border = enabled ? (hovered ? c.Primary : c.Text) : c.Disabled;
        if (state != CheckState.Unchecked) FillRounded(g, box, enabled ? c.Primary : c.Disabled, 2);
        DrawRoundedBorder(g, new Rectangle(box.X, box.Y, box.Width + 1, box.Height + 1), state != CheckState.Unchecked && enabled ? c.Primary : border, 2, 1.25f);
        if (state == CheckState.Unchecked) return;

        using var mark = new Pen(c.PrimaryText, Math.Max(1.75f, box.Width / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (state == CheckState.Checked)
        {
            g.DrawLines(mark, new[]
            {
                new PointF(box.X + box.Width * 0.22f, box.Y + box.Height * 0.52f),
                new PointF(box.X + box.Width * 0.42f, box.Y + box.Height * 0.72f),
                new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.28f)
            });
        }
        else
        {
            g.DrawLine(mark, box.X + box.Width * 0.25f, box.Y + box.Height / 2f, box.Right - box.Width * 0.25f, box.Y + box.Height / 2f);
        }
    }

    public static void DrawChevron(Graphics g, Rectangle area, ChevronDirection direction, Color color, float width = 1.6f)
    {
        var size = Math.Max(3, Math.Min(area.Width, area.Height) / 4);
        var cx = area.X + area.Width / 2f;
        var cy = area.Y + area.Height / 2f;
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var points = direction switch
        {
            ChevronDirection.Down => new[] { new PointF(cx - size, cy - size / 2f), new PointF(cx, cy + size / 2f), new PointF(cx + size, cy - size / 2f) },
            ChevronDirection.Up => new[] { new PointF(cx - size, cy + size / 2f), new PointF(cx, cy - size / 2f), new PointF(cx + size, cy + size / 2f) },
            ChevronDirection.Left => new[] { new PointF(cx + size / 2f, cy - size), new PointF(cx - size / 2f, cy), new PointF(cx + size / 2f, cy + size) },
            _ => new[] { new PointF(cx - size / 2f, cy - size), new PointF(cx + size / 2f, cy), new PointF(cx - size / 2f, cy + size) }
        };
        g.DrawLines(pen, points);
    }

    public static void DrawFilledChevron(Graphics g, Rectangle area, bool expanded, Color color)
    {
        var s = Math.Max(3, area.Width / 4);
        var cx = area.X + area.Width / 2f;
        var cy = area.Y + area.Height / 2f;
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, expanded
            ? new[] { new PointF(cx - s, cy - s / 2f), new PointF(cx + s, cy - s / 2f), new PointF(cx, cy + s) }
            : new[] { new PointF(cx - s / 2f, cy - s), new PointF(cx + s, cy), new PointF(cx - s / 2f, cy + s) });
    }

    public static TextFormatFlags TextFlags(ContentAlignment alignment = ContentAlignment.MiddleLeft, bool wrap = false)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        flags |= alignment switch
        {
            ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
            ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
            ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
            ContentAlignment.MiddleCenter => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
            ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
            ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
            ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
            ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
            _ => TextFormatFlags.VerticalCenter | TextFormatFlags.Left
        };
        return wrap ? flags | TextFormatFlags.WordBreak : flags | TextFormatFlags.SingleLine;
    }

    public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, ContentAlignment alignment = ContentAlignment.MiddleLeft, bool wrap = false) =>
        TextRenderer.DrawText(g, text, font, bounds, color, TextFlags(alignment, wrap));

    public static int MeasureText(string text, Font font) =>
        TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
}

/// <summary>Base of the data controls: theme-aware colors, DPI scaling, hover tracking, a shared frame and focus handling.</summary>
public abstract class EnterpriseControlBase : Control, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private ControlColors? _colors;
    private Font? _boldFont;
    private bool _hovered;

    protected EnterpriseControlBase()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        DoubleBuffered = true;
        TabStop = true;
        Font = new Font("Segoe UI", 9f);
        _themeHandler = (_, theme) =>
        {
            if (!IsDisposed) OnThemeChanged(theme);
        };
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }

    protected ControlColors Colors => _colors ??= ControlColors.FromTheme(_theme);
    protected float DpiScale => DeviceDpi / 96f;
    protected int S(double logical) => (int)Math.Round(logical * DpiScale);
    protected bool IsHovered => _hovered;
    protected Font BoldFont => _boldFont ??= new Font(Font, FontStyle.Bold);

    /// <summary>Bounds inside the 1 px frame; content is painted and hit-tested here.</summary>
    protected Rectangle InnerBounds => new(1, 1, Math.Max(0, Width - 2), Math.Max(0, Height - 2));

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        _colors = null;
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(ApplyThemeChange)); } catch (InvalidOperationException) { }
            return;
        }
        ApplyThemeChange();
    }

    private void ApplyThemeChange()
    {
        BackColor = Colors.Surface;
        ForeColor = Colors.Text;
        OnThemeApplied();
        Invalidate();
    }

    /// <summary>Called after the palette changed, for controls that own child controls to recolor.</summary>
    protected virtual void OnThemeApplied()
    {
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        _colors = null;
        ApplyThemeChange();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BackColor = Colors.Surface;
        ForeColor = Colors.Text;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _boldFont?.Dispose();
        _boldFont = null;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    /// <summary>Fills the control with the surface and draws the frame; call first in OnPaint.</summary>
    protected void PaintFrame(Graphics g, bool drawFocus = true, int radius = Chrome.CornerRadius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        Chrome.DrawFrame(g, new Rectangle(0, 0, Width, Height), Colors, drawFocus && Focused, IsHovered, radius, Enabled);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            _boldFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
