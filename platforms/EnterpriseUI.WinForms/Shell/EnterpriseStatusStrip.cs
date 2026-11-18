using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

public enum EnterpriseStatusPanelKind { Text, Progress, Link }

public sealed class EnterpriseStatusPanelEventArgs : EventArgs
{
    public EnterpriseStatusPanelEventArgs(EnterpriseStatusPanel panel) => Panel = panel;

    public EnterpriseStatusPanel Panel { get; }
}

/// <summary>One section of an <see cref="EnterpriseStatusStrip"/>: text, a progress bar or a link, with an optional icon.</summary>
public sealed class EnterpriseStatusPanel
{
    private string _text = string.Empty;
    private Image? _image;
    private EnterpriseStatusPanelKind _kind;
    private int _width;
    private bool _spring;
    private int _progress;
    private HorizontalAlignment _alignment;
    private bool _visible = true;

    public EnterpriseStatusPanel(string name, string text = "")
    {
        Name = name;
        _text = text;
    }

    internal EnterpriseStatusStrip? Owner { get; set; }
    internal Rectangle Bounds { get; set; }

    public string Name { get; }
    public string? ToolTip { get; set; }
    public object? Tag { get; set; }

    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; Owner?.RefreshPanels(); }
    }

    public Image? Image
    {
        get => _image;
        set { _image = value; Owner?.RefreshPanels(); }
    }

    public EnterpriseStatusPanelKind Kind
    {
        get => _kind;
        set { _kind = value; Owner?.RefreshPanels(); }
    }

    /// <summary>Fixed width in logical pixels; 0 sizes the panel to its content.</summary>
    public int Width
    {
        get => _width;
        set { _width = Math.Max(0, value); Owner?.RefreshPanels(); }
    }

    /// <summary>A spring panel takes the space the other panels leave free.</summary>
    public bool Spring
    {
        get => _spring;
        set { _spring = value; Owner?.RefreshPanels(); }
    }

    /// <summary>0 to 100; drawn by panels of kind <see cref="EnterpriseStatusPanelKind.Progress"/>.</summary>
    public int Progress
    {
        get => _progress;
        set { _progress = Math.Clamp(value, 0, 100); Owner?.Invalidate(); }
    }

    public HorizontalAlignment TextAlign
    {
        get => _alignment;
        set { _alignment = value; Owner?.Invalidate(); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; Owner?.RefreshPanels(); }
    }
}

public sealed class EnterpriseStatusPanelCollection : Collection<EnterpriseStatusPanel>
{
    private readonly EnterpriseStatusStrip _owner;

    internal EnterpriseStatusPanelCollection(EnterpriseStatusStrip owner) => _owner = owner;

    public EnterpriseStatusPanel? this[string name] => this.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    protected override void InsertItem(int index, EnterpriseStatusPanel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.RefreshPanels();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.RefreshPanels();
    }

    protected override void ClearItems()
    {
        foreach (var panel in this) panel.Owner = null;
        base.ClearItems();
        _owner.RefreshPanels();
    }

    protected override void SetItem(int index, EnterpriseStatusPanel item)
    {
        this[index].Owner = null;
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.RefreshPanels();
    }
}

/// <summary>Application status bar with text, progress and link panels, spring panels and a size grip.</summary>
[DefaultEvent("PanelClick")]
public class EnterpriseStatusStrip : EnterpriseControlBase
{
    private readonly ToolTip _tip = new();
    private EnterpriseStatusPanel? _hot;
    private EnterpriseStatusPanel? _pressed;
    private bool _showGrip = true;

    public EnterpriseStatusStrip()
    {
        Panels = new EnterpriseStatusPanelCollection(this);
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        Dock = DockStyle.Bottom;
        Height = 26;
        AccessibleRole = AccessibleRole.StatusBar;
    }

    [Browsable(false)]
    public EnterpriseStatusPanelCollection Panels { get; }

    [DefaultValue(true)]
    public bool ShowSizeGrip
    {
        get => _showGrip;
        set { _showGrip = value; RefreshPanels(); }
    }

    public event EventHandler<EnterpriseStatusPanelEventArgs>? PanelClick;
    public event EventHandler<EnterpriseStatusPanelEventArgs>? PanelDoubleClick;

    public EnterpriseStatusPanel AddPanel(string name, string text = "", int width = 0, bool spring = false, EnterpriseStatusPanelKind kind = EnterpriseStatusPanelKind.Text)
    {
        var panel = new EnterpriseStatusPanel(name, text) { Width = width, Spring = spring, Kind = kind };
        Panels.Add(panel);
        return panel;
    }

    public void SetText(string name, string text)
    {
        if (Panels[name] is { } panel) panel.Text = text;
    }

    public void SetProgress(string name, int value)
    {
        if (Panels[name] is { } panel)
        {
            panel.Kind = EnterpriseStatusPanelKind.Progress;
            panel.Progress = value;
        }
    }

    public Rectangle GetPanelBounds(EnterpriseStatusPanel panel) => panel.Owner == this ? panel.Bounds : Rectangle.Empty;

    public EnterpriseStatusPanel? PanelAt(Point point) => Panels.FirstOrDefault(p => p.Visible && p.Bounds.Contains(point));

    internal void RefreshPanels()
    {
        ComputeLayout();
        Invalidate();
    }

    private Rectangle GripBounds => _showGrip ? new Rectangle(Width - S(16), Height - S(16), S(16), S(16)) : Rectangle.Empty;

    private void ComputeLayout()
    {
        var visible = Panels.Where(p => p.Visible).ToList();
        var right = Width - (_showGrip ? S(16) : 0);
        var pad = S(8);
        var widths = new Dictionary<EnterpriseStatusPanel, int>();
        foreach (var p in visible)
        {
            if (p.Spring) continue;
            if (p.Width > 0) widths[p] = S(p.Width);
            else if (p.Kind == EnterpriseStatusPanelKind.Progress) widths[p] = S(120);
            else widths[p] = Chrome.MeasureText(p.Text, Font) + pad * 2 + (p.Image != null ? S(20) : 0);
        }

        var springs = visible.Where(p => p.Spring).ToList();
        var free = Math.Max(0, right - widths.Values.Sum() - springs.Count);
        var x = 0;
        foreach (var p in visible)
        {
            var width = p.Spring ? Math.Max(S(20), free / Math.Max(1, springs.Count)) : widths[p];
            p.Bounds = new Rectangle(x, 0, Math.Max(0, Math.Min(width, right - x)), Height);
            x += width;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ComputeLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ComputeLayout();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ShellNative.WmNcHitTest && _showGrip && FindForm() is { WindowState: FormWindowState.Normal, FormBorderStyle: FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow }
            && GripBounds.Contains(PointToClient(ShellNative.PointFromLParam(m.LParam))))
        {
            m.Result = (IntPtr)ShellNative.HtBottomRight;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var panel = PanelAt(e.Location);
        if (panel == _hot) return;
        _hot = panel;
        _tip.SetToolTip(this, panel?.ToolTip ?? string.Empty);
        Cursor = panel?.Kind == EnterpriseStatusPanelKind.Link ? Cursors.Hand : Cursors.Default;
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
        _pressed = e.Button == MouseButtons.Left ? PanelAt(e.Location) : null;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var pressed = _pressed;
        _pressed = null;
        if (pressed != null && PanelAt(e.Location) == pressed) PanelClick?.Invoke(this, new EnterpriseStatusPanelEventArgs(pressed));
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (PanelAt(e.Location) is { } panel) PanelDoubleClick?.Invoke(this, new EnterpriseStatusPanelEventArgs(panel));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Header);
        using (var line = new Pen(c.Border)) g.DrawLine(line, 0, 0, Width, 0);
        var pad = S(8);
        var first = true;
        foreach (var panel in Panels.Where(p => p.Visible))
        {
            var b = panel.Bounds;
            if (!first)
            {
                using var sep = new Pen(Chrome.Blend(c.Header, c.Border, 0.8));
                g.DrawLine(sep, b.X, S(5), b.X, Height - S(5));
            }

            first = false;
            var inner = Rectangle.Inflate(b, -pad, 0);
            if (panel.Image != null)
            {
                var size = S(16);
                g.DrawImage(panel.Image, new Rectangle(inner.X, (Height - size) / 2, size, size));
                inner = new Rectangle(inner.X + size + S(4), inner.Y, Math.Max(0, inner.Width - size - S(4)), inner.Height);
            }

            switch (panel.Kind)
            {
                case EnterpriseStatusPanelKind.Progress:
                    var track = new Rectangle(inner.X, (Height - S(10)) / 2, inner.Width, S(10));
                    Chrome.FillRounded(g, track, Chrome.Blend(c.Header, c.Border, 0.6), track.Height / 2);
                    var fill = (int)(track.Width * panel.Progress / 100.0);
                    if (fill > 0) Chrome.FillRounded(g, new Rectangle(track.X, track.Y, Math.Max(track.Height, fill), track.Height), c.Primary, track.Height / 2);
                    break;
                case EnterpriseStatusPanelKind.Link:
                    using (var linkFont = new Font(Font, FontStyle.Underline))
                        Chrome.DrawText(g, panel.Text, linkFont, inner, _hot == panel ? Chrome.Blend(c.Primary, c.Text, 0.3) : c.Primary, Align(panel.TextAlign));
                    break;
                default:
                    Chrome.DrawText(g, panel.Text, Font, inner, c.Text, Align(panel.TextAlign));
                    break;
            }
        }

        if (_showGrip) PaintGrip(g, c);
    }

    private static ContentAlignment Align(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
        HorizontalAlignment.Right => ContentAlignment.MiddleRight,
        _ => ContentAlignment.MiddleLeft
    };

    private void PaintGrip(Graphics g, ControlColors c)
    {
        var grip = GripBounds;
        var dot = Math.Max(2, S(2));
        using var brush = new SolidBrush(c.Muted);
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col <= row; col++)
            {
                var x = grip.Right - S(4) - col * (dot + S(2)) - dot;
                var y = grip.Bottom - S(4) - (2 - row) * (dot + S(2)) - dot;
                g.FillRectangle(brush, x, y, dot, dot);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
