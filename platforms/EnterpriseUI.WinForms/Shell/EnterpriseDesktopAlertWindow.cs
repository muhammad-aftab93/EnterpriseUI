using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Notifications;
using EnterpriseUI.Theming;
using Timer = System.Windows.Forms.Timer;

namespace EnterpriseUI.WinForms;

public enum EnterpriseAlertPart { None, Close, Options, Pin, Action, Body }

public sealed class EnterpriseAlertActionEventArgs : EventArgs
{
    public EnterpriseAlertActionEventArgs(NotificationAction action) => Action = action;

    public NotificationAction Action { get; }
}

public sealed class EnterpriseAlertClosedEventArgs : EventArgs
{
    public EnterpriseAlertClosedEventArgs(ToastCloseReason reason) => Reason = reason;

    public ToastCloseReason Reason { get; }
}

/// <summary>Screen-corner popup alert that never takes focus: caption, text, action links, pin and options buttons and an auto-close countdown; bound to <see cref="EnterpriseDesktopAlert"/>.</summary>
[ToolboxItem(false)]
[DefaultEvent("ActionClicked")]
public class EnterpriseDesktopAlertWindow : LayeredPopupForm
{
    private static readonly List<EnterpriseDesktopAlertWindow> OpenAlerts = new();
    private readonly ValueAnimator _fade = new();
    private readonly Timer _tick = new() { Interval = 50 };
    private readonly Dictionary<EnterpriseAlertPart, Rectangle> _parts = new();
    private readonly List<(Rectangle Bounds, NotificationAction Action)> _actionBounds = new();
    private ControlColors _colors;
    private Font _font;
    private Font _bold;
    private EnterpriseDesktopAlert _model;
    private Size _body;
    private double _remaining;
    private bool _hover;
    private bool _finished;
    private bool _placed;
    private ToastCloseReason _reason;
    private EnterpriseAlertPart _hot;
    private int _hotAction = -1;
    private Point _location;
    private ToastPlacement _placement = ToastPlacement.BottomRight;
    private NotificationSeverity _severity;
    private int _width = 340;

    public EnterpriseDesktopAlertWindow() : this(new EnterpriseDesktopAlert())
    {
    }

    public EnterpriseDesktopAlertWindow(EnterpriseDesktopAlert model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        _font = new Font("Segoe UI", 10f);
        _bold = new Font(_font, FontStyle.Bold);
        Font = _font;
        _fade.Updated += (_, _) => PresentCurrent();
        _tick.Tick += (_, _) => OnTick();
        _remaining = _model.AutoCloseDelaySeconds * 1000.0;
        Opacity = 1;
    }

    private int S(double v) => (int)Math.Round(v * DeviceDpi / 96.0);
    private int Shadow => S(14);

    /// <summary>The bound model: caption, text, countdown, button visibility and pin state.</summary>
    [Browsable(false)]
    public EnterpriseDesktopAlert Model
    {
        get => _model;
        set
        {
            _model = value ?? throw new ArgumentNullException(nameof(value));
            _remaining = _model.AutoCloseDelaySeconds * 1000.0;
            if (Visible) Redraw();
        }
    }

    [DefaultValue(ToastPlacement.BottomRight)]
    public ToastPlacement Placement
    {
        get => _placement;
        set { _placement = value; if (!_finished) LayoutAll(); }
    }

    [DefaultValue(NotificationSeverity.Info)]
    public NotificationSeverity Severity
    {
        get => _severity;
        set { _severity = value; if (Visible) Redraw(); }
    }

    /// <summary>Width in logical pixels.</summary>
    [DefaultValue(340)]
    public int AlertWidth
    {
        get => _width;
        set { _width = Math.Clamp(value, 220, 640); if (Visible) Redraw(); }
    }

    /// <summary>Action links shown under the text.</summary>
    [Browsable(false)]
    public IList<NotificationAction> Actions { get; } = new List<NotificationAction>();

    /// <summary>Menu opened by the options button.</summary>
    [Browsable(false)]
    public ContextMenuStrip? OptionsMenu { get; set; }

    [Browsable(false)]
    public bool IsPinned => _model.IsPinned;

    [Browsable(false)]
    public bool IsClosing { get; private set; }

    [Browsable(false)]
    public static int OpenCount => OpenAlerts.Count;

    public event EventHandler<EnterpriseAlertActionEventArgs>? ActionClicked;
    public event EventHandler? PinnedChanged;
    public event EventHandler? OptionsClicked;
    public event EventHandler<EnterpriseAlertClosedEventArgs>? AlertClosed;

    /// <summary>Shows the alert without taking focus and stacks it with the other open alerts.</summary>
    public void ShowAlert()
    {
        if (_finished) return;
        _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        UpdateFonts();
        _remaining = _model.AutoCloseDelaySeconds * 1000.0;
        if (!OpenAlerts.Contains(this)) OpenAlerts.Add(this);
        Redraw();
        Show();
        _fade.AnimateTo(1, 180);
        if (_model.AutoCloseDelaySeconds > 0) _tick.Start();
        LayoutAll();
    }

    public void Pin(bool pinned)
    {
        if (_model.IsPinned == pinned) return;
        _model.IsPinned = pinned;
        Redraw();
        PinnedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the alert with a fade; the alert stays in memory until the fade ends.</summary>
    public void CloseAlert(ToastCloseReason reason = ToastCloseReason.Programmatic, bool immediate = false)
    {
        if (IsClosing || _finished) return;
        IsClosing = true;
        _reason = reason;
        _tick.Stop();
        if (immediate) Finish();
        else _fade.AnimateTo(0, 160);
    }

    /// <summary>Closes every alert that is currently open.</summary>
    public static void CloseAll()
    {
        foreach (var alert in OpenAlerts.ToArray()) alert.CloseAlert(ToastCloseReason.Programmatic, true);
    }

    /// <summary>Part of the alert at a point given relative to the alert body.</summary>
    public EnterpriseAlertPart HitTest(Point bodyPoint, out NotificationAction? action)
    {
        action = null;
        foreach (var (bounds, a) in _actionBounds)
        {
            if (!bounds.Contains(bodyPoint)) continue;
            action = a;
            return EnterpriseAlertPart.Action;
        }

        foreach (var part in new[] { EnterpriseAlertPart.Close, EnterpriseAlertPart.Options, EnterpriseAlertPart.Pin })
        {
            if (_parts.TryGetValue(part, out var r) && r.Contains(bodyPoint)) return part;
        }

        return new Rectangle(Point.Empty, _body).Contains(bodyPoint) ? EnterpriseAlertPart.Body : EnterpriseAlertPart.None;
    }

    public Rectangle GetPartBounds(EnterpriseAlertPart part) => _parts.TryGetValue(part, out var r) ? r : Rectangle.Empty;

    public IReadOnlyList<Rectangle> GetActionBounds() => _actionBounds.Select(a => a.Bounds).ToList();

    public Size BodySize => _body;

    /// <summary>Performs what a click at a point (relative to the alert body) does.</summary>
    public void ClickAt(Point bodyPoint)
    {
        if (IsClosing) return;
        switch (HitTest(bodyPoint, out var action))
        {
            case EnterpriseAlertPart.Close:
                CloseAlert(ToastCloseReason.CloseButton);
                break;
            case EnterpriseAlertPart.Pin:
                Pin(!_model.IsPinned);
                break;
            case EnterpriseAlertPart.Options:
                OptionsClicked?.Invoke(this, EventArgs.Empty);
                OptionsMenu?.Show(PointToScreen(new Point(Shadow + _parts[EnterpriseAlertPart.Options].Left, Shadow + _parts[EnterpriseAlertPart.Options].Bottom)));
                break;
            case EnterpriseAlertPart.Action when action != null:
                ActionClicked?.Invoke(this, new EnterpriseAlertActionEventArgs(action));
                CloseAlert(ToastCloseReason.Action);
                break;
            case EnterpriseAlertPart.Body:
                CloseAlert(ToastCloseReason.Clicked);
                break;
        }
    }

    private Color Accent => _severity switch
    {
        NotificationSeverity.Success => _colors.Success,
        NotificationSeverity.Warning => _colors.Warning,
        NotificationSeverity.Error => _colors.Danger,
        _ => _colors.Primary
    };

    private void Measure()
    {
        _parts.Clear();
        _actionBounds.Clear();
        var pad = S(14);
        var width = S(_width);
        var inner = width - pad * 2;
        var y = pad;
        var header = Math.Max(_bold.Height, S(22));
        var button = S(22);
        var x = width - pad - button;
        _parts[EnterpriseAlertPart.Close] = new Rectangle(x, y + (header - button) / 2, button, button);
        if (_model.ShowOptionsButton) _parts[EnterpriseAlertPart.Options] = new Rectangle(x -= button, y + (header - button) / 2, button, button);
        if (_model.ShowPinButton) _parts[EnterpriseAlertPart.Pin] = new Rectangle(x - button, y + (header - button) / 2, button, button);
        y += header + S(6);
        var content = _model.ContentText;
        var measured = content.Length == 0 ? 0 : TextRenderer.MeasureText(content, _font, new Size(inner, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
        var contentHeight = Math.Min(measured, _font.Height * 5);
        _contentRect = new Rectangle(pad, y, inner, contentHeight);
        y += contentHeight;
        if (Actions.Count > 0)
        {
            y += S(10);
            var ax = pad;
            foreach (var action in Actions)
            {
                var w = Chrome.MeasureText(action.Text, _bold) + S(4);
                _actionBounds.Add((new Rectangle(ax, y, w, _font.Height + S(4)), action));
                ax += w + S(16);
            }

            y += _font.Height + S(4);
        }

        y += pad;
        _body = new Size(width, y);
    }

    private Rectangle _contentRect;

    private void Redraw()
    {
        Measure();
        var size = new Size(_body.Width + Shadow * 2, _body.Height + Shadow * 2);
        Render(size, _placed ? _location : Point.Empty, Draw, Alpha);
    }

    private byte Alpha => (byte)Math.Clamp(255 * _fade.Value, 0, 255);

    private void PresentCurrent()
    {
        if (_finished || !_placed) return;
        Present(_location, Alpha);
        if (IsClosing && _fade.Value <= 0.01) Finish();
    }

    private void Draw(Graphics g)
    {
        var c = _colors;
        var body = new Rectangle(Shadow, Shadow, _body.Width, _body.Height);
        using var path = Chrome.RoundedRect(body, S(8));
        if (!c.HighContrast) Shapes.DrawSoftShadow(g, path, S(10), Color.FromArgb(80, 0, 0, 0), S(3));
        using (var fill = new SolidBrush(c.Surface)) g.FillPath(fill, path);
        using (var face = new Bitmap(body.Width, body.Height, PixelFormat.Format24bppRgb))
        {
            using (var fg = Graphics.FromImage(face))
            {
                fg.Clear(c.Surface);
                fg.SmoothingMode = SmoothingMode.AntiAlias;
                fg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                PaintFace(fg, face.Size);
            }

            var state = g.Save();
            g.SetClip(path);
            g.DrawImageUnscaled(face, body.Location);
            g.Restore(state);
        }

        using var border = new Pen(c.Border);
        g.DrawPath(border, path);
    }

    private void PaintFace(Graphics g, Size size)
    {
        var c = _colors;
        using (var stripe = new SolidBrush(Accent)) g.FillRectangle(stripe, 0, 0, S(4), size.Height);
        var pad = S(14);
        var captionRight = _parts.Values.Min(r => r.Left) - S(6);
        Chrome.DrawText(g, _model.CaptionText, _bold, new Rectangle(pad, pad, Math.Max(0, captionRight - pad), Math.Max(_bold.Height, S(22))), c.Text);
        PaintButtons(g, c);
        if (_contentRect.Height > 0) TextRenderer.DrawText(g, _model.ContentText, _font, _contentRect, c.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        for (var i = 0; i < _actionBounds.Count; i++)
        {
            using var link = new Font(_bold, FontStyle.Bold | (i == _hotAction ? FontStyle.Underline : 0));
            Chrome.DrawText(g, _actionBounds[i].Action.Text, link, _actionBounds[i].Bounds, c.Primary);
        }

        if (_model.AutoCloseDelaySeconds > 0 && !_model.IsPinned)
        {
            var fraction = Math.Clamp(_remaining / (_model.AutoCloseDelaySeconds * 1000.0), 0, 1);
            using var bar = new SolidBrush(Color.FromArgb(150, Accent));
            g.FillRectangle(bar, 0, size.Height - S(2), (int)(size.Width * fraction), S(2));
        }
    }

    private void PaintButtons(Graphics g, ControlColors c)
    {
        foreach (var (part, rect) in _parts)
        {
            var hot = _hot == part;
            if (hot) Chrome.FillRounded(g, rect, c.HoverBack, S(4));
            var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            var r = S(4);
            using var pen = new Pen(hot ? c.Text : c.Muted, Math.Max(1.4f, DeviceDpi / 96f * 1.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            switch (part)
            {
                case EnterpriseAlertPart.Close:
                    g.DrawLine(pen, center.X - r, center.Y - r, center.X + r, center.Y + r);
                    g.DrawLine(pen, center.X + r, center.Y - r, center.X - r, center.Y + r);
                    break;
                case EnterpriseAlertPart.Options:
                    using (var dot = new SolidBrush(pen.Color))
                    {
                        for (var i = -1; i <= 1; i++) g.FillEllipse(dot, center.X + i * S(5) - S(1.5), center.Y - S(1.5), S(3), S(3));
                    }

                    break;
                case EnterpriseAlertPart.Pin:
                    var pinned = _model.IsPinned;
                    var head = new RectangleF(center.X - r, center.Y - r - 1, r * 2, r * 1.6f);
                    if (pinned)
                    {
                        using var fill = new SolidBrush(c.Primary);
                        g.FillEllipse(fill, head);
                    }
                    else g.DrawEllipse(pen, head);
                    g.DrawLine(pen, center.X, head.Bottom, center.X, center.Y + r + 2);
                    break;
            }
        }
    }

    private void LayoutAll()
    {
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var margin = S(12);
        var spacing = S(10);
        var bottom = _placement is ToastPlacement.BottomRight or ToastPlacement.BottomLeft;
        var right = _placement is ToastPlacement.BottomRight or ToastPlacement.TopRight;
        var cursor = bottom ? wa.Bottom - margin : wa.Top + margin;
        foreach (var alert in OpenAlerts.Where(a => a._placement == _placement && !a.IsClosing).Reverse())
        {
            var size = alert._body;
            var x = right ? wa.Right - margin - size.Width : wa.Left + margin;
            var y = bottom ? cursor - size.Height : cursor;
            alert.MoveTo(new Point(x - alert.Shadow, y - alert.Shadow));
            cursor += bottom ? -(size.Height + spacing) : size.Height + spacing;
        }
    }

    private void MoveTo(Point location)
    {
        _placed = true;
        _location = location;
        Present(location, Alpha);
    }

    private void OnTick()
    {
        if (IsClosing || _hover || _model.IsPinned) return;
        _remaining -= _tick.Interval;
        if (_remaining <= 0)
        {
            CloseAlert(ToastCloseReason.Timeout);
            return;
        }

        Redraw();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateFonts();
        if (Visible)
        {
            Redraw();
            LayoutAll();
        }
    }

    private void UpdateFonts()
    {
        var font = new Font("Segoe UI", 9f * DeviceDpi / 96f);
        var bold = new Font(font, FontStyle.Bold);
        var previousFont = _font;
        var previousBold = _bold;
        _font = font;
        _bold = bold;
        Font = font;
        previousBold.Dispose();
        previousFont.Dispose();
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        OpenAlerts.Remove(this);
        Hide();
        var reason = _reason;
        AlertClosed?.Invoke(this, new EnterpriseAlertClosedEventArgs(reason));
        Dispose();
        foreach (var other in OpenAlerts.Where(a => a._placement == _placement).Take(1)) other.LayoutAll();
    }

    private Point Local(Point p) => new(p.X - Shadow, p.Y - Shadow);

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _hot = EnterpriseAlertPart.None;
        _hotAction = -1;
        if (!IsClosing) Redraw();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsClosing) return;
        var part = HitTest(Local(e.Location), out var action);
        var index = action == null ? -1 : _actionBounds.FindIndex(a => a.Action == action);
        Cursor = part is EnterpriseAlertPart.None or EnterpriseAlertPart.Body ? Cursors.Default : Cursors.Hand;
        if (part == _hot && index == _hotAction) return;
        _hot = part;
        _hotAction = index;
        Redraw();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) ClickAt(Local(e.Location));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            OpenAlerts.Remove(this);
            _tick.Dispose();
            _fade.Dispose();
            _font.Dispose();
            _bold.Dispose();
        }

        base.Dispose(disposing);
    }
}
