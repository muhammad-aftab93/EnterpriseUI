using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>Base of the themed container controls: palette, theme subscription and DPI helpers; children are added normally and docking respects <see cref="Control.DisplayRectangle"/>.</summary>
public abstract class EnterpriseContainerBase : Panel, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);

    protected EnterpriseContainerBase()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Font = new Font("Segoe UI", 10f);
        _themeHandler = (_, theme) =>
        {
            if (!IsDisposed) OnThemeChanged(theme);
        };
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        BackColor = _colors.Surface;
        ForeColor = _colors.Text;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeOverride { get; set; }

    protected ControlColors Colors => _colors;
    protected float DpiScale => DeviceDpi / 96f;
    protected int S(double logical) => (int)Math.Round(logical * DpiScale);

    private Font? _emphasis;

    /// <summary>Bold variant of the control font, recreated when the font changes.</summary>
    protected Font EmphasisFont => _emphasis is not null && Math.Abs(_emphasis.Size - Font.Size) < 0.01f && _emphasis.FontFamily.Name == Font.FontFamily.Name ? _emphasis : (_emphasis = new Font(Font, FontStyle.Bold));

    public void OnThemeChanged(ITheme newTheme)
    {
        _colors = ControlColors.FromTheme(newTheme);
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(ApplyTheme)); } catch (InvalidOperationException) { }
            return;
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        BackColor = _colors.Surface;
        ForeColor = _colors.Text;
        OnThemeApplied();
        Invalidate();
    }

    protected virtual void OnThemeApplied()
    {
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        ApplyTheme();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        if (disposing) _emphasis?.Dispose();
        base.Dispose(disposing);
    }
}

public enum EnterpriseDividerStyle { Solid, Dashed, Fade }

/// <summary>Thin rule that separates content, horizontal or vertical, with an optional caption.</summary>
[DefaultProperty(nameof(Text))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseControlDesigner<EnterpriseUI.WinForms.Design.DividerActions>))]
[ToolboxItem(true)]
public class EnterpriseDivider : EnterpriseControlBase
{
    private Orientation _orientation = Orientation.Horizontal;
    private EnterpriseDividerStyle _style = EnterpriseDividerStyle.Solid;
    private HorizontalAlignment _textAlignment = HorizontalAlignment.Left;
    private int _thickness = 1;
    private int _inset;

    public EnterpriseDivider()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
        Size = new Size(200, 20);
        AccessibleRole = AccessibleRole.Separator;
    }

    [DefaultValue(Orientation.Horizontal)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value) return;
            _orientation = value;
            if (_orientation == Orientation.Vertical && Width > Height) Size = new Size(Height, Width);
            else if (_orientation == Orientation.Horizontal && Height > Width) Size = new Size(Height, Width);
            Invalidate();
        }
    }

    [DefaultValue(EnterpriseDividerStyle.Solid)]
    public EnterpriseDividerStyle LineStyle { get => _style; set { _style = value; Invalidate(); } }

    [DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment TextAlignment { get => _textAlignment; set { _textAlignment = value; Invalidate(); } }

    [DefaultValue(1)]
    public int Thickness { get => _thickness; set { _thickness = Math.Clamp(value, 1, 8); Invalidate(); } }

    /// <summary>Space left blank at both ends of the rule, in logical pixels.</summary>
    [DefaultValue(0)]
    public int Inset { get => _inset; set { _inset = Math.Max(0, value); Invalidate(); } }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue("")]
    public override string Text { get => base.Text; set { base.Text = value; Invalidate(); } }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BackColor = Color.Transparent;
    }

    protected override void OnThemeApplied() => BackColor = Color.Transparent;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        var inset = S(_inset);
        var t = Math.Max(1, S(_thickness));
        var lineColor = Colors.Border;
        if (_orientation == Orientation.Vertical)
        {
            DrawLine(g, lineColor, new Rectangle((Width - t) / 2, inset, t, Math.Max(0, Height - inset * 2)), true);
            return;
        }

        var y = (Height - t) / 2;
        var area = new Rectangle(inset, y, Math.Max(0, Width - inset * 2), t);
        if (string.IsNullOrEmpty(Text))
        {
            DrawLine(g, lineColor, area, false);
            return;
        }

        var gap = S(8);
        var textWidth = Math.Min(Chrome.MeasureText(Text, Font), Math.Max(0, area.Width - gap * 4));
        var textX = _textAlignment switch
        {
            HorizontalAlignment.Left => area.X + gap * 2,
            HorizontalAlignment.Right => area.Right - gap * 2 - textWidth,
            _ => area.X + (area.Width - textWidth) / 2
        };
        Chrome.DrawText(g, Text, Font, new Rectangle(textX, 0, textWidth, Height), Colors.Muted, ContentAlignment.MiddleLeft);
        var left = new Rectangle(area.X, y, Math.Max(0, textX - gap - area.X), t);
        var right = new Rectangle(textX + textWidth + gap, y, Math.Max(0, area.Right - (textX + textWidth + gap)), t);
        DrawLine(g, lineColor, left, false);
        DrawLine(g, lineColor, right, false);
    }

    private void DrawLine(Graphics g, Color color, Rectangle bounds, bool vertical)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        switch (_style)
        {
            case EnterpriseDividerStyle.Dashed:
                using (var pen = new Pen(color, vertical ? bounds.Width : bounds.Height) { DashStyle = DashStyle.Dash })
                {
                    if (vertical) g.DrawLine(pen, bounds.X + bounds.Width / 2f, bounds.Y, bounds.X + bounds.Width / 2f, bounds.Bottom);
                    else g.DrawLine(pen, bounds.X, bounds.Y + bounds.Height / 2f, bounds.Right, bounds.Y + bounds.Height / 2f);
                }

                break;
            case EnterpriseDividerStyle.Fade:
                using (var brush = new LinearGradientBrush(bounds, Color.FromArgb(0, color), Color.FromArgb(0, color), vertical ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal))
                {
                    brush.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(0, color), color, color, Color.FromArgb(0, color) },
                        Positions = new[] { 0f, 0.2f, 0.8f, 1f }
                    };
                    g.FillRectangle(brush, bounds);
                }

                break;
            default:
                using (var brush = new SolidBrush(color)) g.FillRectangle(brush, bounds);
                break;
        }
    }
}

public enum EnterpriseSurfaceStyle
{
    /// <summary>Plain surface without border.</summary>
    Flat,
    /// <summary>Surface with a 1 px border.</summary>
    Outlined,
    /// <summary>Header tinted surface (cards, side panes).</summary>
    Filled,
    /// <summary>Surface with a soft shadow along the lower edges.</summary>
    Raised
}

/// <summary>Container with a themed surface: flat, outlined, filled or raised, with rounded corners and automatic inner padding.</summary>
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseContainerDesigner<EnterpriseUI.WinForms.Design.SurfacePanelActions>))]
[ToolboxItem(true)]
public class EnterpriseSurfacePanel : EnterpriseContainerBase
{
    private EnterpriseSurfaceStyle _surfaceStyle = EnterpriseSurfaceStyle.Outlined;
    private int _cornerRadius = Chrome.CornerRadius;
    private int _shadowDepth = 3;

    public EnterpriseSurfacePanel()
    {
        Size = new Size(240, 120);
        Padding = new Padding(8);
    }

    [DefaultValue(EnterpriseSurfaceStyle.Outlined)]
    public EnterpriseSurfaceStyle SurfaceStyle
    {
        get => _surfaceStyle;
        set { _surfaceStyle = value; PerformLayout(); Invalidate(); }
    }

    [DefaultValue(Chrome.CornerRadius)]
    public int CornerRadius { get => _cornerRadius; set { _cornerRadius = Math.Clamp(value, 0, 32); Invalidate(); } }

    /// <summary>Depth of the shadow of <see cref="EnterpriseSurfaceStyle.Raised"/> in logical pixels.</summary>
    [DefaultValue(3)]
    public int ShadowDepth { get => _shadowDepth; set { _shadowDepth = Math.Clamp(value, 1, 12); PerformLayout(); Invalidate(); } }

    private int BorderInset => _surfaceStyle switch
    {
        EnterpriseSurfaceStyle.Flat => 0,
        EnterpriseSurfaceStyle.Raised => 1 + S(_shadowDepth),
        _ => 1
    };

    public override Rectangle DisplayRectangle
    {
        get
        {
            var inset = BorderInset;
            return new Rectangle(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? c.Surface);
        var radius = S(_cornerRadius);
        var back = _surfaceStyle == EnterpriseSurfaceStyle.Filled ? c.Header : c.Surface;
        if (_surfaceStyle == EnterpriseSurfaceStyle.Raised)
        {
            var depth = S(_shadowDepth);
            for (var i = depth; i >= 1; i--)
            {
                var alpha = (int)(46f * (depth - i + 1) / depth / depth * 2);
                var shadow = new Rectangle(i, i, Width - i - 1, Height - i - 1);
                Chrome.FillRounded(g, shadow, Color.FromArgb(Math.Clamp(alpha, 4, 40), 0, 0, 0), radius);
            }

            var body = new Rectangle(0, 0, Width - depth, Height - depth);
            Chrome.FillRounded(g, body, back, radius);
            Chrome.DrawRoundedBorder(g, body, c.Border, radius);
            return;
        }

        Chrome.FillRounded(g, new Rectangle(0, 0, Width, Height), back, radius);
        if (_surfaceStyle != EnterpriseSurfaceStyle.Flat) Chrome.DrawRoundedBorder(g, new Rectangle(0, 0, Width, Height), c.Border, radius);
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        BackColor = _surfaceStyle == EnterpriseSurfaceStyle.Filled ? Colors.Header : Colors.Surface;
        foreach (Control child in Controls)
        {
            if (child is Label or CheckBox or RadioButton) child.ForeColor = Colors.Text;
        }
    }
}

public enum EnterpriseGroupStyle
{
    /// <summary>Caption sits in a gap of the border, like a classic group box.</summary>
    Outlined,
    /// <summary>Caption in a tinted header band above the content.</summary>
    Header,
    /// <summary>Caption followed by a rule; no surrounding border.</summary>
    Underlined
}

/// <summary>Titled container that groups related controls.</summary>
[DefaultProperty(nameof(Text))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseContainerDesigner<EnterpriseUI.WinForms.Design.GroupPanelActions>))]
[ToolboxItem(true)]
public class EnterpriseGroupPanel : EnterpriseContainerBase
{
    private EnterpriseGroupStyle _groupStyle = EnterpriseGroupStyle.Outlined;
    private HorizontalAlignment _captionAlignment = HorizontalAlignment.Left;

    public EnterpriseGroupPanel()
    {
        Size = new Size(260, 140);
        Padding = new Padding(8);
        AccessibleRole = AccessibleRole.Grouping;
    }

    [DefaultValue(EnterpriseGroupStyle.Outlined)]
    public EnterpriseGroupStyle GroupStyle { get => _groupStyle; set { _groupStyle = value; PerformLayout(); Invalidate(); } }

    [DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment CaptionAlignment { get => _captionAlignment; set { _captionAlignment = value; Invalidate(); } }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue("")]
    public override string Text { get => base.Text; set { base.Text = value; AccessibleName = value; PerformLayout(); Invalidate(); } }

    private int CaptionHeight => string.IsNullOrEmpty(Text) ? 0 : Math.Max(Font.Height + S(8), S(24));

    public override Rectangle DisplayRectangle
    {
        get
        {
            var top = _groupStyle == EnterpriseGroupStyle.Outlined ? Math.Max(1, CaptionHeight / 2 + 2) + (CaptionHeight > 0 ? CaptionHeight / 2 : 0) : CaptionHeight + 1;
            var side = _groupStyle == EnterpriseGroupStyle.Underlined ? 0 : 1;
            return new Rectangle(side, top, Math.Max(0, Width - side * 2), Math.Max(0, Height - top - side));
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? c.Surface);
        var radius = S(Chrome.CornerRadius);
        var caption = CaptionHeight;
        var textWidth = string.IsNullOrEmpty(Text) ? 0 : Chrome.MeasureText(Text, Font) + S(10);
        switch (_groupStyle)
        {
            case EnterpriseGroupStyle.Header:
            {
                var frame = new Rectangle(0, 0, Width, Height);
                Chrome.FillRounded(g, frame, c.Surface, radius);
                if (caption > 0)
                {
                    var header = new Rectangle(0, 0, Width, caption);
                    var state = g.Save();
                    g.SetClip(header);
                    Chrome.FillRounded(g, new Rectangle(0, 0, Width, caption + radius), c.Header, radius);
                    g.Restore(state);
                    using var pen = new Pen(c.Border);
                    g.DrawLine(pen, 0, caption, Width, caption);
                    Chrome.DrawText(g, Text, BoldFont(), new Rectangle(S(10), 0, Math.Max(0, Width - S(20)), caption), c.Text, Align());
                }

                Chrome.DrawRoundedBorder(g, frame, c.Border, radius);
                break;
            }
            case EnterpriseGroupStyle.Underlined:
            {
                using var pen = new Pen(c.Border);
                if (caption > 0)
                {
                    Chrome.DrawText(g, Text, BoldFont(), new Rectangle(0, 0, Width, caption), c.Text, Align());
                    g.DrawLine(pen, 0, caption, Width, caption);
                }

                break;
            }
            default:
            {
                var top = caption / 2;
                var frame = new Rectangle(0, top, Width, Height - top);
                Chrome.DrawRoundedBorder(g, frame, c.Border, radius);
                if (textWidth > 0)
                {
                    var x = _captionAlignment switch
                    {
                        HorizontalAlignment.Center => (Width - textWidth) / 2,
                        HorizontalAlignment.Right => Width - textWidth - S(12),
                        _ => S(12)
                    };
                    var gap = new Rectangle(x, top - 1, textWidth, 3);
                    using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, gap);
                    Chrome.DrawText(g, Text, BoldFont(), new Rectangle(x, 0, textWidth, caption), c.Text, ContentAlignment.MiddleCenter);
                }

                break;
            }
        }
    }

    private Font? _bold;

    private Font BoldFont() => _bold is not null && _bold.Size == Font.Size && _bold.FontFamily.Name == Font.FontFamily.Name ? _bold : (_bold = new Font(Font, FontStyle.Bold));

    private ContentAlignment Align() => _captionAlignment switch
    {
        HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
        HorizontalAlignment.Right => ContentAlignment.MiddleRight,
        _ => ContentAlignment.MiddleLeft
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold?.Dispose();
        base.Dispose(disposing);
    }
}

public enum EnterpriseScrollDirection { Vertical, Horizontal, Both }

/// <summary>Scrolling container with themed (light or dark) scroll bars, direction control and scroll-into-view helpers.</summary>
[Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(System.ComponentModel.Design.IDesigner))]
[ToolboxItem(true)]
public class EnterpriseScrollPanel : EnterpriseContainerBase
{
    private EnterpriseScrollDirection _direction = EnterpriseScrollDirection.Vertical;
    private int _wheelStep = 48;

    public EnterpriseScrollPanel()
    {
        AutoScroll = true;
        Size = new Size(240, 160);
    }

    [DefaultValue(EnterpriseScrollDirection.Vertical)]
    public EnterpriseScrollDirection ScrollDirection { get => _direction; set { _direction = value; PerformLayout(); } }

    /// <summary>Pixels moved per mouse wheel notch, in logical pixels.</summary>
    [DefaultValue(48)]
    public int WheelStep { get => _wheelStep; set => _wheelStep = Math.Clamp(value, 8, 400); }

    public event EventHandler? ScrollPositionChanged;

    public int ScrollOffsetY => -AutoScrollPosition.Y;
    public int ScrollOffsetX => -AutoScrollPosition.X;

    public void ScrollToTop() => AutoScrollPosition = new Point(ScrollOffsetX, 0);

    public void ScrollToBottom() => AutoScrollPosition = new Point(ScrollOffsetX, VerticalScroll.Maximum);

    /// <summary>Scrolls the minimum distance that makes the control fully visible.</summary>
    public void EnsureVisible(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        ScrollControlIntoView(control);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (!IsHandleCreated) return;
        if (_direction == EnterpriseScrollDirection.Vertical && HorizontalScroll.Visible) HorizontalScroll.Visible = false;
        else if (_direction == EnterpriseScrollDirection.Horizontal && VerticalScroll.Visible) VerticalScroll.Visible = false;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyScrollBarTheme();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        if (IsHandleCreated) ApplyScrollBarTheme();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!VerticalScroll.Visible && !HorizontalScroll.Visible)
        {
            base.OnMouseWheel(e);
            return;
        }

        var step = S(_wheelStep) * e.Delta / 120;
        if ((ModifierKeys & Keys.Shift) == Keys.Shift || !VerticalScroll.Visible)
            AutoScrollPosition = new Point(ScrollOffsetX - step, ScrollOffsetY);
        else
            AutoScrollPosition = new Point(ScrollOffsetX, ScrollOffsetY - step);
        ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    private void ApplyScrollBarTheme()
    {
        var luminance = 0.299 * Colors.Surface.R + 0.587 * Colors.Surface.G + 0.114 * Colors.Surface.B;
        try { SetWindowTheme(Handle, luminance < 128 ? "DarkMode_Explorer" : "Explorer", null); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
