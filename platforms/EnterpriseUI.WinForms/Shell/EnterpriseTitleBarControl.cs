using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

/// <summary>Implemented by controls placed in a title bar so that their empty areas can still drag the window.</summary>
public interface ITitleBarContent
{
    /// <summary>Whether the point (in the content's client coordinates) belongs to the draggable caption rather than to an interactive element.</summary>
    bool IsCaptionArea(Point clientPoint);
}

/// <summary>Base of title bar content controls: empty areas pass mouse input through to the window caption.</summary>
public abstract class EnterpriseTitleBarContentControl : EnterpriseControlBase, ITitleBarContent
{
    protected EnterpriseTitleBarContentControl()
    {
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    public abstract bool IsCaptionArea(Point clientPoint);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ShellNative.WmNcHitTest && FindForm() is EnterpriseForm && IsCaptionArea(PointToClient(ShellNative.PointFromLParam(m.LParam))))
        {
            m.Result = (IntPtr)ShellNative.HtTransparent;
            return;
        }

        base.WndProc(ref m);
    }
}

public enum TitleBarPart { None, Icon, Caption, Minimize, Maximize, Close, Button, Content }

/// <summary>A command button shown in a title bar next to the system buttons.</summary>
public sealed class EnterpriseTitleBarButton
{
    private string _glyph;
    private Image? _image;
    private bool _enabled = true;
    private bool _visible = true;

    public EnterpriseTitleBarButton(string id, string glyph, string? toolTip = null)
    {
        Id = id;
        _glyph = glyph;
        ToolTip = toolTip;
    }

    internal EnterpriseTitleBarControl? Owner { get; set; }

    public string Id { get; }
    public string? ToolTip { get; set; }
    public object? Tag { get; set; }

    /// <summary>Symbol drawn on the button, for example "☰" or "⚙".</summary>
    public string Glyph
    {
        get => _glyph;
        set { _glyph = value ?? string.Empty; Owner?.Invalidate(); }
    }

    public Image? Image
    {
        get => _image;
        set { _image = value; Owner?.Invalidate(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; Owner?.Invalidate(); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; Owner?.Invalidate(); }
    }
}

public sealed class EnterpriseTitleBarButtonEventArgs : EventArgs
{
    public EnterpriseTitleBarButtonEventArgs(EnterpriseTitleBarButton button) => Button = button;

    public EnterpriseTitleBarButton Button { get; }
}

public sealed class EnterpriseTitleBarButtonCollection : Collection<EnterpriseTitleBarButton>
{
    private readonly EnterpriseTitleBarControl _owner;

    internal EnterpriseTitleBarButtonCollection(EnterpriseTitleBarControl owner) => _owner = owner;

    protected override void InsertItem(int index, EnterpriseTitleBarButton item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.RefreshLayout();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.RefreshLayout();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) item.Owner = null;
        base.ClearItems();
        _owner.RefreshLayout();
    }

    protected override void SetItem(int index, EnterpriseTitleBarButton item)
    {
        this[index].Owner = null;
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.RefreshLayout();
    }
}

/// <summary>Caption bar with icon, title, an optional content area, command buttons and minimize, maximize and close buttons; drags the window it sits on.</summary>
[DefaultEvent("ButtonClick")]
public class EnterpriseTitleBarControl : EnterpriseControlBase
{
    private readonly ToolTip _tip = new();
    private readonly List<(TitleBarPart Part, EnterpriseTitleBarButton? Button, Rectangle Bounds)> _parts = new();
    private Rectangle _iconRect, _textRect, _contentRect;
    private Form? _form;
    private Control? _content;
    private Icon? _icon;
    private bool _showIcon = true;
    private bool _showCaption = true;
    private bool _showMinimize = true;
    private bool _showMaximize = true;
    private bool _showClose = true;
    private bool _active = true;
    private int _buttonWidth = 46;
    private TitleBarPart _hotPart;
    private EnterpriseTitleBarButton? _hotButton;
    private TitleBarPart _downPart;
    private EnterpriseTitleBarButton? _downButton;
    private bool _layingOut;

    public EnterpriseTitleBarControl()
    {
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        Buttons = new EnterpriseTitleBarButtonCollection(this);
        Dock = DockStyle.Top;
        Height = 36;
        AccessibleRole = AccessibleRole.TitleBar;
    }

    [Browsable(false)]
    public EnterpriseTitleBarButtonCollection Buttons { get; }

    /// <summary>Icon shown before the title; the owning form's icon is used when empty.</summary>
    [DefaultValue(null)]
    public Icon? Icon
    {
        get => _icon;
        set { _icon = value; RefreshLayout(); }
    }

    [DefaultValue(true)]
    public bool ShowIcon
    {
        get => _showIcon;
        set { _showIcon = value; RefreshLayout(); }
    }

    [DefaultValue(true)]
    public bool ShowCaptionText
    {
        get => _showCaption;
        set { _showCaption = value; RefreshLayout(); }
    }

    [DefaultValue(true)]
    public bool ShowMinimize
    {
        get => _showMinimize;
        set { _showMinimize = value; RefreshLayout(); }
    }

    [DefaultValue(true)]
    public bool ShowMaximize
    {
        get => _showMaximize;
        set { _showMaximize = value; RefreshLayout(); }
    }

    [DefaultValue(true)]
    public bool ShowClose
    {
        get => _showClose;
        set { _showClose = value; RefreshLayout(); }
    }

    /// <summary>Width of a system button in logical pixels.</summary>
    [DefaultValue(46)]
    public int ButtonWidth
    {
        get => _buttonWidth;
        set { _buttonWidth = Math.Clamp(value, 24, 120); RefreshLayout(); }
    }

    /// <summary>Whether the owning window is the active one; inactive captions are dimmed.</summary>
    [DefaultValue(true)]
    public bool IsActive
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    /// <summary>Control hosted between the title and the buttons, for tabs, toolbars or ribbon headers.</summary>
    [Browsable(false)]
    public Control? ContentControl
    {
        get => _content;
        set
        {
            if (_content == value) return;
            if (_content != null) Controls.Remove(_content);
            _content = value;
            if (value != null) Controls.Add(value);
            RefreshLayout();
        }
    }

    [AllowNull]
    [DefaultValue("")]
    public override string Text
    {
        get => base.Text;
        set { base.Text = value ?? string.Empty; AccessibleName = base.Text; RefreshLayout(); }
    }

    public event EventHandler<EnterpriseTitleBarButtonEventArgs>? ButtonClick;
    public event EventHandler? CaptionDoubleClick;
    public event EventHandler? DragStarted;

    internal void RefreshLayout()
    {
        ComputeLayout();
        Invalidate();
    }

    private Icon? EffectiveIcon => _icon ?? (_form is { ShowIcon: true } ? _form.Icon : null);

    private void ComputeLayout()
    {
        if (_layingOut) return;
        _layingOut = true;
        try
        {
            _parts.Clear();
            var bw = S(_buttonWidth);
            var x = Width;
            var form = _form;
            if (_showClose && (form?.ControlBox ?? true)) _parts.Add((TitleBarPart.Close, null, new Rectangle(x -= bw, 0, bw, Height)));
            if (_showMaximize && (form?.ControlBox ?? true) && (form?.MaximizeBox ?? true)) _parts.Add((TitleBarPart.Maximize, null, new Rectangle(x -= bw, 0, bw, Height)));
            if (_showMinimize && (form?.ControlBox ?? true) && (form?.MinimizeBox ?? true)) _parts.Add((TitleBarPart.Minimize, null, new Rectangle(x -= bw, 0, bw, Height)));
            for (var i = Buttons.Count - 1; i >= 0; i--)
            {
                var b = Buttons[i];
                if (b.Visible) _parts.Add((TitleBarPart.Button, b, new Rectangle(x -= S(40), 0, S(40), Height)));
            }

            var left = S(10);
            _iconRect = Rectangle.Empty;
            if (_showIcon && EffectiveIcon != null)
            {
                var size = S(16);
                _iconRect = new Rectangle(left, (Height - size) / 2, size, size);
                left += size + S(8);
            }

            _textRect = Rectangle.Empty;
            var hasContent = _content is { Visible: true };
            if (_showCaption && Text.Length > 0)
            {
                var available = Math.Max(0, x - left - S(8));
                var measured = Chrome.MeasureText(Text, Font);
                var width = Math.Min(measured, hasContent ? Math.Min(available, Math.Max(S(80), Width / 5)) : available);
                _textRect = new Rectangle(left, 0, Math.Max(0, width), Height);
                left += width + S(10);
            }

            _contentRect = Rectangle.Empty;
            if (_content != null)
            {
                _contentRect = new Rectangle(left, 0, Math.Max(0, x - left - S(4)), Height);
                _content.Bounds = _contentRect;
            }
        }
        finally
        {
            _layingOut = false;
        }
    }

    /// <summary>Which element is at the point (client coordinates) and, for command buttons, the button.</summary>
    public TitleBarPart HitTest(Point point, out EnterpriseTitleBarButton? button)
    {
        button = null;
        if (!ClientRectangle.Contains(point)) return TitleBarPart.None;
        foreach (var (part, b, bounds) in _parts)
        {
            if (!bounds.Contains(point)) continue;
            button = b;
            return part;
        }

        if (_iconRect.Contains(point)) return TitleBarPart.Icon;
        if (_content != null && _contentRect.Contains(point)) return TitleBarPart.Content;
        return TitleBarPart.Caption;
    }

    /// <summary>Whether a press at the point should move the window.</summary>
    public bool IsDragArea(Point point)
    {
        var part = HitTest(point, out _);
        return part switch
        {
            TitleBarPart.Caption => true,
            TitleBarPart.Content => _content is ITitleBarContent c && c.IsCaptionArea(new Point(point.X - _contentRect.X, point.Y - _contentRect.Y)),
            _ => false
        };
    }

    /// <summary>Layout of a system or command button, for tests and automation.</summary>
    public Rectangle GetPartBounds(TitleBarPart part, EnterpriseTitleBarButton? button = null)
    {
        foreach (var (p, b, bounds) in _parts)
        {
            if (p == part && (part != TitleBarPart.Button || b == button)) return bounds;
        }

        return part switch
        {
            TitleBarPart.Icon => _iconRect,
            TitleBarPart.Content => _contentRect,
            TitleBarPart.Caption => _textRect,
            _ => Rectangle.Empty
        };
    }

    public EnterpriseTitleBarButton AddButton(string id, string glyph, string? toolTip = null)
    {
        var button = new EnterpriseTitleBarButton(id, glyph, toolTip);
        Buttons.Add(button);
        return button;
    }

    /// <summary>Runs the action of a system button (minimize, maximize or restore, close).</summary>
    public void PerformSystemButton(TitleBarPart part)
    {
        var form = FindForm();
        if (form == null) return;
        switch (part)
        {
            case TitleBarPart.Minimize: form.WindowState = FormWindowState.Minimized; break;
            case TitleBarPart.Maximize: Restore(); break;
            case TitleBarPart.Close: form.Close(); break;
        }
    }

    /// <summary>Toggles between maximized and normal.</summary>
    public void Restore()
    {
        var form = FindForm();
        if (form == null) return;
        form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }

    /// <summary>Starts moving the window with the mouse, as if the caption had been pressed.</summary>
    public void BeginDrag()
    {
        var form = FindForm();
        if (form is not { IsHandleCreated: true } || form.WindowState == FormWindowState.Maximized) return;
        DragStarted?.Invoke(this, EventArgs.Empty);
        ShellNative.BeginCaptionDrag(form.Handle);
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        AttachForm();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AttachForm();
    }

    private void AttachForm()
    {
        var form = FindForm();
        if (form == _form) return;
        if (_form != null)
        {
            _form.Activated -= OnFormActivation;
            _form.Deactivate -= OnFormActivation;
            _form.Resize -= OnFormResize;
        }

        _form = form;
        if (form != null)
        {
            form.Activated += OnFormActivation;
            form.Deactivate += OnFormActivation;
            form.Resize += OnFormResize;
            _active = form == Form.ActiveForm || !form.Visible;
        }

        RefreshLayout();
    }

    private void OnFormActivation(object? sender, EventArgs e) => IsActive = _form == Form.ActiveForm;

    private void OnFormResize(object? sender, EventArgs e) => Invalidate();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshLayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RefreshLayout();
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        if (FindForm() is EnterpriseForm form) Height = form.ScaledCaptionHeight;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control == _content) RefreshLayout();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        BackColor = Colors.Header;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ShellNative.WmNcHitTest && FindForm() is EnterpriseForm form && form.FrameHitTest(ShellNative.PointFromLParam(m.LParam)) != EnterpriseFormHit.Client)
        {
            m.Result = (IntPtr)ShellNative.HtTransparent;
            return;
        }

        base.WndProc(ref m);
    }

    private void SetHot(TitleBarPart part, EnterpriseTitleBarButton? button)
    {
        if (part == _hotPart && button == _hotButton) return;
        _hotPart = part;
        _hotButton = button;
        var text = part switch
        {
            TitleBarPart.Minimize => "Minimize",
            TitleBarPart.Maximize => _form?.WindowState == FormWindowState.Maximized ? "Restore" : "Maximize",
            TitleBarPart.Close => "Close",
            TitleBarPart.Button => button?.ToolTip,
            _ => null
        };
        _tip.SetToolTip(this, text ?? string.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var part = HitTest(e.Location, out var button);
        SetHot(part, button);
        Cursor = part is TitleBarPart.Button or TitleBarPart.Minimize or TitleBarPart.Maximize or TitleBarPart.Close ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHot(TitleBarPart.None, null);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _downPart = HitTest(e.Location, out _downButton);
        Invalidate();
        if (_downPart == TitleBarPart.Caption && FindForm() is not EnterpriseForm) BeginDrag();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var downPart = _downPart;
        var downButton = _downButton;
        _downPart = TitleBarPart.None;
        _downButton = null;
        Invalidate();
        if (e.Button != MouseButtons.Left) return;
        var part = HitTest(e.Location, out var button);
        if (part != downPart || button != downButton) return;
        switch (part)
        {
            case TitleBarPart.Minimize:
            case TitleBarPart.Maximize:
            case TitleBarPart.Close:
                PerformSystemButton(part);
                break;
            case TitleBarPart.Button when button is { Enabled: true }:
                ButtonClick?.Invoke(this, new EnterpriseTitleBarButtonEventArgs(button));
                break;
            case TitleBarPart.Icon when _form is { IsHandleCreated: true }:
                ShellNative.ShowSystemMenu(_form.Handle, PointToScreen(new Point(_iconRect.Left, Height)));
                break;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || HitTest(e.Location, out _) != TitleBarPart.Caption) return;
        RaiseCaptionDoubleClick();
        if (FindForm() is not EnterpriseForm) Restore();
    }

    internal void RaiseCaptionDoubleClick() => CaptionDoubleClick?.Invoke(this, EventArgs.Empty);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        if (_parts.Count == 0 && Width > 0) ComputeLayout();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Header);
        using (var line = new Pen(c.Border)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
        if (_form is EnterpriseForm { WindowState: FormWindowState.Normal } f)
        {
            using var edge = new Pen(f.EffectiveBorderColor);
            g.DrawLine(edge, 0, 0, Width, 0);
        }

        var icon = EffectiveIcon;
        if (_showIcon && icon != null && !_iconRect.IsEmpty) g.DrawIcon(icon, _iconRect);
        if (!_textRect.IsEmpty) Chrome.DrawText(g, Text, Font, _textRect, _active ? c.Text : c.Muted);

        foreach (var (part, button, bounds) in _parts)
        {
            var hot = _hotPart == part && _hotButton == button;
            var down = hot && _downPart == part && _downButton == button;
            var enabled = button?.Enabled ?? true;
            var isClose = part == TitleBarPart.Close;
            if (hot && enabled)
            {
                var back = isClose ? c.Danger : c.HighContrast ? c.Primary : Chrome.Blend(c.Header, c.Text, down ? 0.2 : 0.1);
                using var brush = new SolidBrush(back);
                g.FillRectangle(brush, bounds.X, bounds.Y, bounds.Width, bounds.Height - 1);
            }

            var fore = !enabled ? c.Disabled : hot && isClose ? Color.White : _active ? c.Text : c.Muted;
            if (button != null) PaintCustomButton(g, button, bounds, fore);
            else PaintSystemGlyph(g, part, bounds, fore);
        }
    }

    private void PaintCustomButton(Graphics g, EnterpriseTitleBarButton button, Rectangle bounds, Color fore)
    {
        if (button.Image != null)
        {
            var size = S(16);
            g.DrawImage(button.Image, new Rectangle(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size));
            return;
        }

        TextRenderer.DrawText(g, button.Glyph, GlyphFonts.Get(Font.SizeInPoints * 1.15f), bounds, fore, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void PaintSystemGlyph(Graphics g, TitleBarPart part, Rectangle bounds, Color color)
    {
        var s = S(10);
        var box = new Rectangle(bounds.X + (bounds.Width - s) / 2, bounds.Y + (bounds.Height - s) / 2, s, s);
        using var pen = new Pen(color, Math.Max(1f, DpiScale));
        switch (part)
        {
            case TitleBarPart.Minimize:
                g.SmoothingMode = SmoothingMode.None;
                g.DrawLine(pen, box.Left, box.Top + s / 2, box.Right, box.Top + s / 2);
                break;
            case TitleBarPart.Maximize:
                g.SmoothingMode = SmoothingMode.None;
                if (_form?.WindowState == FormWindowState.Maximized)
                {
                    var d = Math.Max(2, S(2));
                    g.DrawRectangle(pen, box.X, box.Y + d, s - d - 1, s - d - 1);
                    g.DrawLines(pen, new[] { new Point(box.X + d, box.Y + d), new Point(box.X + d, box.Y), new Point(box.Right - 1, box.Y), new Point(box.Right - 1, box.Bottom - d - 1), new Point(box.Right - d - 1, box.Bottom - d - 1) });
                }
                else g.DrawRectangle(pen, box.X, box.Y, s - 1, s - 1);
                break;
            case TitleBarPart.Close:
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(pen, box.Left, box.Top, box.Right, box.Bottom);
                g.DrawLine(pen, box.Right, box.Top, box.Left, box.Bottom);
                break;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_form != null)
            {
                _form.Activated -= OnFormActivation;
                _form.Deactivate -= OnFormActivation;
                _form.Resize -= OnFormResize;
            }

            _tip.Dispose();
        }

        base.Dispose(disposing);
    }
}
