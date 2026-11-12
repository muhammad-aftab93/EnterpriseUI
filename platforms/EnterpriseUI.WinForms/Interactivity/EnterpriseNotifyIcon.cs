using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum NotifyToastMode { Custom, Native, None }

public enum ToastPlacement { BottomRight, BottomLeft, TopRight, TopLeft }

public enum ToastCloseReason { Timeout, CloseButton, Action, Clicked, Overflow, Programmatic }

public sealed class NotifyToastEventArgs : EventArgs
{
    public NotifyToastEventArgs(AppNotification notification, string? actionId = null)
    {
        Notification = notification;
        ActionId = actionId;
    }

    public AppNotification Notification { get; }
    public string? ActionId { get; }
}

/// <summary>Tray icon with an unread-count badge drawn on the icon, themed toast alerts that stack above the taskbar, action buttons and a notification history.</summary>
[DefaultEvent("NotificationClicked")]
public class EnterpriseNotifyIcon : Component
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _icon = new();
    private readonly NotificationCenter _center = new();
    private readonly SynchronizationContext _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    private readonly List<ToastWindow> _toasts = new();
    private Icon? _baseIcon;
    private Icon? _renderedIcon;
    private string _text = string.Empty;
    private bool _showBadge = true;
    private Color _badgeColor = Color.Empty;
    private ContextMenuStrip? _menu;
    private int _maxToasts = 3;
    private int _toastWidth = 340;

    public EnterpriseNotifyIcon()
    {
        _icon.Click += (_, e) => Click?.Invoke(this, e);
        _icon.DoubleClick += (_, e) => DoubleClick?.Invoke(this, e);
        _center.Added += (_, e) => OnAdded(e.Notification);
        _center.Removed += (_, e) => CloseToastFor(e.Notification, ToastCloseReason.Programmatic);
        _center.Changed += (_, _) => RefreshIcon();
        _center.ActionInvoked += (_, e) => ActionInvoked?.Invoke(this, new NotifyToastEventArgs(e.Notification, e.ActionId));
        RefreshIcon();
    }

    public EnterpriseNotifyIcon(IContainer container) : this() => container.Add(this);

    /// <summary>History and unread counter; add notifications through <see cref="Notify"/> or directly.</summary>
    [Browsable(false)]
    public NotificationCenter Center => _center;

    [DefaultValue(null)]
    public Icon? Icon
    {
        get => _baseIcon;
        set { _baseIcon = value; RefreshIcon(); }
    }

    [DefaultValue("")]
    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; RefreshIcon(); }
    }

    [DefaultValue(false)]
    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    [DefaultValue(null)]
    public ContextMenuStrip? ContextMenuStrip
    {
        get => _menu;
        set
        {
            _menu = value;
            _icon.ContextMenuStrip = value;
            if (value == null) return;
            var colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
            value.Renderer = new ThemedMenuRenderer(colors);
            value.BackColor = colors.Surface;
            value.ForeColor = colors.Text;
        }
    }

    /// <summary>Draws the unread count on the tray icon.</summary>
    [DefaultValue(true)]
    public bool ShowBadge
    {
        get => _showBadge;
        set { _showBadge = value; RefreshIcon(); }
    }

    public Color BadgeColor
    {
        get => _badgeColor;
        set { _badgeColor = value; RefreshIcon(); }
    }

    private bool ShouldSerializeBadgeColor() => !_badgeColor.IsEmpty;
    private void ResetBadgeColor() => BadgeColor = Color.Empty;

    [DefaultValue(NotifyToastMode.Custom)]
    public NotifyToastMode ToastMode { get; set; } = NotifyToastMode.Custom;

    [DefaultValue(ToastPlacement.BottomRight)]
    public ToastPlacement ToastPlacement { get; set; } = ToastPlacement.BottomRight;

    [DefaultValue(340)]
    public int ToastWidth
    {
        get => _toastWidth;
        set => _toastWidth = Math.Clamp(value, 220, 640);
    }

    /// <summary>Older toasts are dismissed (they stay in the history) when more are showing.</summary>
    [DefaultValue(3)]
    public int MaxVisibleToasts
    {
        get => _maxToasts;
        set => _maxToasts = Math.Clamp(value, 1, 10);
    }

    [Browsable(false)]
    public int UnreadCount => _center.UnreadCount;

    [Browsable(false)]
    public int VisibleToastCount => _toasts.Count;

    public event EventHandler? Click;
    public event EventHandler? DoubleClick;
    public event EventHandler<NotifyToastEventArgs>? NotificationClicked;
    public event EventHandler<NotifyToastEventArgs>? ActionInvoked;
    public event EventHandler<NotifyToastEventArgs>? ToastClosed;

    /// <summary>Adds a notification and shows a toast; can be called from any thread.</summary>
    public AppNotification Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, params NotificationAction[] actions)
    {
        AppNotification? result = null;
        Run(() => result = _center.Notify(title, message, severity, null, actions));
        return result!;
    }

    public void MarkAllRead() => Run(_center.MarkAllRead);

    public void CloseToasts() => Run(() =>
    {
        foreach (var t in _toasts.ToArray()) t.BeginClose(ToastCloseReason.Programmatic);
    });

    private void Run(Action action)
    {
        if (SynchronizationContext.Current == _ui) action();
        else _ui.Send(_ => action(), null);
    }

    private void OnAdded(AppNotification notification)
    {
        if (ToastMode == NotifyToastMode.None) return;
        if (ToastMode == NotifyToastMode.Native)
        {
            _icon.ShowBalloonTip(Math.Max(1000, (int)notification.Timeout.TotalMilliseconds), notification.Title, notification.Message, notification.Severity switch
            {
                NotificationSeverity.Warning => ToolTipIcon.Warning,
                NotificationSeverity.Error => ToolTipIcon.Error,
                _ => ToolTipIcon.Info
            });
            return;
        }

        var toast = new ToastWindow(this, notification, ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme));
        _toasts.Add(toast);
        while (_toasts.Count(t => !t.IsClosing) > _maxToasts) _toasts.First(t => !t.IsClosing).BeginClose(ToastCloseReason.Overflow);
        toast.Open();
        LayoutToasts();
    }

    private void CloseToastFor(AppNotification notification, ToastCloseReason reason) => _toasts.FirstOrDefault(t => t.Notification == notification)?.BeginClose(reason);

    internal void ToastClicked(ToastWindow toast)
    {
        _center.MarkRead(toast.Notification.Id);
        NotificationClicked?.Invoke(this, new NotifyToastEventArgs(toast.Notification));
        toast.BeginClose(ToastCloseReason.Clicked);
    }

    internal void ToastAction(ToastWindow toast, string actionId)
    {
        _center.InvokeAction(toast.Notification.Id, actionId);
        toast.BeginClose(ToastCloseReason.Action);
    }

    internal void ToastDismissed(ToastWindow toast, ToastCloseReason reason)
    {
        if (reason == ToastCloseReason.CloseButton) _center.MarkRead(toast.Notification.Id);
    }

    internal void ToastFinished(ToastWindow toast, ToastCloseReason reason)
    {
        _toasts.Remove(toast);
        ToastClosed?.Invoke(this, new NotifyToastEventArgs(toast.Notification));
        LayoutToasts();
    }

    private void LayoutToasts()
    {
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var margin = 12;
        var spacing = 10;
        var bottom = ToastPlacement is ToastPlacement.BottomRight or ToastPlacement.BottomLeft;
        var right = ToastPlacement is ToastPlacement.BottomRight or ToastPlacement.TopRight;
        var cursor = bottom ? wa.Bottom - margin : wa.Top + margin;
        foreach (var toast in _toasts.Where(t => !t.IsClosing).Reverse())
        {
            var size = toast.BodySize;
            var x = right ? wa.Right - margin - size.Width : wa.Left + margin;
            var y = bottom ? cursor - size.Height : cursor;
            toast.MoveTo(new Point(x, y), right ? 1 : -1);
            cursor += bottom ? -(size.Height + spacing) : size.Height + spacing;
        }
    }

    private void RefreshIcon()
    {
        var count = _center.UnreadCount;
        var rendered = RenderIcon(count);
        _icon.Icon = rendered;
        _renderedIcon?.Dispose();
        _renderedIcon = rendered;
        var text = count > 0 ? (_text.Length > 0 ? $"{_text} — {count} unread" : $"{count} unread") : _text;
        _icon.Text = text.Length > 63 ? text[..63] : text;
    }

    private Icon RenderIcon(int unread)
    {
        var size = SystemInformation.SmallIconSize;
        var source = _baseIcon ?? SystemIcons.Application;
        using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawIcon(source, new Rectangle(0, 0, size.Width, size.Height));
            if (_showBadge && unread > 0)
            {
                var colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
                var back = _badgeColor.IsEmpty ? colors.Danger : _badgeColor;
                var d = Math.Max(8, (int)(size.Width * 0.62));
                var rect = new Rectangle(size.Width - d, size.Height - d, d, d);
                using (var brush = new SolidBrush(back)) g.FillEllipse(brush, rect);
                using (var pen = new Pen(Color.White, Math.Max(1f, size.Width / 16f))) g.DrawEllipse(pen, rect);
                var label = unread > 9 ? "9+" : unread.ToString();
                using var font = new Font("Segoe UI", Math.Max(5f, d * (unread > 9 ? 0.34f : 0.5f)), FontStyle.Bold, GraphicsUnit.Pixel);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(label, font, Brushes.White, new RectangleF(rect.X, rect.Y + 0.5f, rect.Width, rect.Height), format);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var toast in _toasts.ToArray()) toast.BeginClose(ToastCloseReason.Programmatic, immediate: true);
            _icon.Visible = false;
            _icon.Dispose();
            _renderedIcon?.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class ToastWindow : LayeredPopupForm
{
    private readonly EnterpriseNotifyIcon _owner;
    private readonly ControlColors _colors;
    private readonly PopupContent _content;
    private readonly Font _font;
    private readonly ValueAnimator _fade = new();
    private readonly ValueAnimator _slide = new();
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 50 };
    private readonly float _scale;
    private Point _from, _to;
    private int _direction = 1;
    private double _remaining;
    private bool _hover;
    private bool _finished;
    private ToastCloseReason _reason;
    private bool _placed;

    public ToastWindow(EnterpriseNotifyIcon owner, AppNotification notification, ControlColors colors)
    {
        _owner = owner;
        Notification = notification;
        _colors = colors;
        _scale = DeviceDpi / 96f;
        _font = new Font("Segoe UI", 9f);
        Font = _font;
        _content = new PopupContent(_font, colors, _scale)
        {
            Title = notification.Title,
            Message = notification.Message,
            Severity = notification.Severity,
            Actions = notification.Actions,
            ShowClose = true,
            MaxMessageLines = 4
        };
        BodySize = _content.Measure(S(owner.ToastWidth));
        _remaining = notification.Timeout.TotalMilliseconds;
        _fade.Updated += (_, _) => Update();
        _slide.Updated += (_, _) => Update();
        _tick.Tick += (_, _) => OnTick();
    }

    public AppNotification Notification { get; }
    public Size BodySize { get; }
    public bool IsClosing { get; private set; }

    private int S(double v) => (int)Math.Round(v * _scale);
    private int Shadow => S(14);

    public void Open()
    {
        Draw();
        Show();
        _fade.AnimateTo(1, 200);
        if (Notification.Timeout > TimeSpan.Zero) _tick.Start();
    }

    /// <summary>Moves to the body location on screen; the first call slides in from the screen edge.</summary>
    public void MoveTo(Point bodyLocation, int direction)
    {
        _direction = direction;
        var target = new Point(bodyLocation.X - Shadow, bodyLocation.Y - Shadow);
        if (!_placed)
        {
            _placed = true;
            _from = _to = target;
            Present(CurrentLocation(), (byte)Math.Clamp(255 * _fade.Value, 0, 255));
            return;
        }

        _from = CurrentLocation();
        _to = target;
        _slide.Jump(0);
        _slide.AnimateTo(1, 180, EasingKind.EaseOut);
    }

    private Point CurrentLocation()
    {
        var t = _slide.Value;
        var slideIn = (int)((1 - _fade.Value) * S(36) * _direction);
        return new Point((int)(_from.X + (_to.X - _from.X) * t) + slideIn, (int)(_from.Y + (_to.Y - _from.Y) * t));
    }

    private void Update()
    {
        if (_finished || !_placed) return;
        Present(CurrentLocation(), (byte)Math.Clamp(255 * _fade.Value, 0, 255));
        if (IsClosing && _fade.Value <= 0.01) Finish();
    }

    private void OnTick()
    {
        if (IsClosing || _hover) return;
        _remaining -= _tick.Interval;
        if (_remaining <= 0)
        {
            BeginClose(ToastCloseReason.Timeout);
            return;
        }

        Draw();
    }

    private void Draw()
    {
        var size = new Size(BodySize.Width + Shadow * 2, BodySize.Height + Shadow * 2);
        var body = new Rectangle(Shadow, Shadow, BodySize.Width, BodySize.Height);
        Render(size, _placed ? CurrentLocation() : Point.Empty, g =>
        {
            var c = _colors;
            using var path = Chrome.RoundedRect(body, S(8));
            if (!c.HighContrast) Shapes.DrawSoftShadow(g, path, S(10), Color.FromArgb(80, 0, 0, 0), S(3));
            using (var fill = new SolidBrush(c.Surface)) g.FillPath(fill, path);
            using (var face = new Bitmap(body.Width, body.Height, PixelFormat.Format24bppRgb))
            {
                using (var fg = Graphics.FromImage(face))
                {
                    fg.Clear(c.Surface);
                    fg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    _content.Paint(fg, face.Size);
                    using var stripe = new SolidBrush(_content.Accent);
                    fg.FillRectangle(stripe, 0, 0, S(4), face.Height);
                    if (Notification.Timeout > TimeSpan.Zero)
                    {
                        var fraction = Math.Clamp(_remaining / Notification.Timeout.TotalMilliseconds, 0, 1);
                        using var bar = new SolidBrush(Color.FromArgb(150, _content.Accent));
                        fg.FillRectangle(bar, 0, face.Height - S(2), (int)(face.Width * fraction), S(2));
                    }
                }

                var state = g.Save();
                g.SetClip(path);
                g.DrawImageUnscaled(face, body.Location);
                g.Restore(state);
            }

            using var border = new Pen(c.Border);
            g.DrawPath(border, path);
        }, (byte)Math.Clamp(255 * _fade.Value, 0, 255));
    }

    public void BeginClose(ToastCloseReason reason, bool immediate = false)
    {
        if (IsClosing || _finished) return;
        IsClosing = true;
        _reason = reason;
        _tick.Stop();
        _owner.ToastDismissed(this, reason);
        if (immediate) Finish();
        else _fade.AnimateTo(0, 160);
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        Hide();
        var owner = _owner;
        var reason = _reason;
        Dispose();
        owner.ToastFinished(this, reason);
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
        _content.Hover = default;
        Draw();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsClosing) return;
        var hit = _content.HitTest(Local(e.Location));
        Cursor = hit.Kind == PopupHitKind.None ? Cursors.Default : Cursors.Hand;
        if (hit == _content.Hover) return;
        _content.Hover = hit;
        Draw();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || IsClosing) return;
        var hit = _content.HitTest(Local(e.Location));
        switch (hit.Kind)
        {
            case PopupHitKind.Close: BeginClose(ToastCloseReason.CloseButton); break;
            case PopupHitKind.Action: _owner.ToastAction(this, Notification.Actions[hit.Index].Id); break;
            default: _owner.ToastClicked(this); break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tick.Dispose();
            _fade.Dispose();
            _slide.Dispose();
            _content.Dispose();
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
