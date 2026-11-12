using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum CalloutPlacement { Auto, Top, Bottom, Left, Right }

public enum CalloutCloseReason { Programmatic, CloseButton, Escape, ClickOutside, Timeout, BodyClick, Action, TargetLost }

public sealed class CalloutClosedEventArgs : EventArgs
{
    public CalloutClosedEventArgs(CalloutCloseReason reason) => Reason = reason;
    public CalloutCloseReason Reason { get; }
}

public sealed class CalloutActionEventArgs : EventArgs
{
    public CalloutActionEventArgs(string actionId) => ActionId = actionId;
    public string ActionId { get; }

    /// <summary>Keep the callout open after the action ran.</summary>
    public bool KeepOpen { get; set; }
}

/// <summary>Speech-bubble popup anchored to a control or screen rectangle, with an arrow that points at it, a soft shadow and configurable close triggers.</summary>
[DefaultEvent("ActionInvoked")]
public class EnterpriseCalloutControl : Component
{
    private CalloutWindow? _window;
    private Control? _target;
    private Rectangle _targetBounds;
    private Form? _targetForm;

    public EnterpriseCalloutControl()
    {
    }

    public EnterpriseCalloutControl(IContainer container) : this() => container.Add(this);

    [DefaultValue("")]
    public string Title { get; set; } = string.Empty;

    [DefaultValue("")]
    public string Message { get; set; } = string.Empty;

    [DefaultValue(LabelMarkupMode.Plain)]
    public LabelMarkupMode MarkupMode { get; set; } = LabelMarkupMode.Plain;

    /// <summary>Symbol shown in the leading circle; empty uses the severity symbol.</summary>
    [DefaultValue(null)]
    public string? Glyph { get; set; }

    [DefaultValue(null)]
    public Image? Icon { get; set; }

    [DefaultValue(NotificationSeverity.Info)]
    public NotificationSeverity Severity { get; set; }

    /// <summary>Side of the target the callout appears on; Auto picks the side with enough room.</summary>
    [DefaultValue(CalloutPlacement.Auto)]
    public CalloutPlacement Placement { get; set; }

    /// <summary>Side the open callout actually uses.</summary>
    [Browsable(false)]
    public CalloutPlacement ActualPlacement => _window?.ActualPlacement ?? Placement;

    [DefaultValue(12)]
    public int ArrowSize { get; set; } = 12;

    [DefaultValue(8)]
    public int CornerRadius { get; set; } = 8;

    [DefaultValue(340)]
    public int MaxWidth { get; set; } = 340;

    [DefaultValue(true)]
    public bool ShowShadow { get; set; } = true;

    [DefaultValue(12)]
    public int ShadowBlur { get; set; } = 12;

    [DefaultValue(true)]
    public bool ShowCloseButton { get; set; } = true;

    [DefaultValue(true)]
    public bool CloseOnClickOutside { get; set; } = true;

    [DefaultValue(true)]
    public bool CloseOnEscape { get; set; } = true;

    [DefaultValue(false)]
    public bool CloseOnBodyClick { get; set; }

    /// <summary>Closes after this many milliseconds (paused while the mouse is over it); 0 keeps it open.</summary>
    [DefaultValue(0)]
    public int AutoCloseInterval { get; set; }

    public Color BackgroundColor { get; set; } = Color.Empty;
    public Color BorderColor { get; set; } = Color.Empty;

    private bool ShouldSerializeBackgroundColor() => !BackgroundColor.IsEmpty;
    private void ResetBackgroundColor() => BackgroundColor = Color.Empty;
    private bool ShouldSerializeBorderColor() => !BorderColor.IsEmpty;
    private void ResetBorderColor() => BorderColor = Color.Empty;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<NotificationAction> Actions { get; } = new();

    [Browsable(false)]
    public bool IsOpen => _window is { IsDisposed: false };

    public event EventHandler? Opened;
    public event EventHandler<CalloutClosedEventArgs>? Closed;
    public event EventHandler<CalloutActionEventArgs>? ActionInvoked;
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    public void Show(Control target) => Show(target, Rectangle.Empty);

    /// <summary>Points at a part of the target; <paramref name="clientBounds"/> is in the target's client coordinates (empty: the whole control).</summary>
    public void Show(Control target, Rectangle clientBounds)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.IsHandleCreated) throw new InvalidOperationException("The target control has no handle yet.");
        Close();
        _target = target;
        _targetBounds = clientBounds;
        _targetForm = target.FindForm();
        if (_targetForm != null)
        {
            _targetForm.LocationChanged += OnTargetMoved;
            _targetForm.SizeChanged += OnTargetMoved;
        }

        target.Disposed += OnTargetGone;
        target.VisibleChanged += OnTargetGone;
        target.LocationChanged += OnTargetMoved;
        target.SizeChanged += OnTargetMoved;
        Open(CurrentAnchor());
    }

    /// <summary>Points at a rectangle in screen coordinates.</summary>
    public void ShowAt(Rectangle screenAnchor)
    {
        Close();
        Open(screenAnchor);
    }

    private Rectangle CurrentAnchor() => _target == null ? Rectangle.Empty : _target.RectangleToScreen(_targetBounds.IsEmpty ? _target.ClientRectangle : _targetBounds);

    private void Open(Rectangle anchor)
    {
        _window = new CalloutWindow(this, anchor);
        _window.Open();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Close() => Close(CalloutCloseReason.Programmatic);

    internal void Close(CalloutCloseReason reason) => _window?.BeginClose(reason);

    private void OnTargetMoved(object? sender, EventArgs e)
    {
        if (_window != null && _target is { IsDisposed: false }) _window.UpdateAnchor(CurrentAnchor());
    }

    private void OnTargetGone(object? sender, EventArgs e)
    {
        if (_target is { IsDisposed: false, Visible: true }) return;
        _window?.BeginClose(CalloutCloseReason.TargetLost);
    }

    internal void RaiseAction(string id, out bool keepOpen)
    {
        var args = new CalloutActionEventArgs(id);
        ActionInvoked?.Invoke(this, args);
        keepOpen = args.KeepOpen;
    }

    internal void RaiseLink(string href) => LinkClicked?.Invoke(this, new LinkClickedEventArgs(href));

    internal void WindowClosed(CalloutWindow window, CalloutCloseReason reason)
    {
        if (!ReferenceEquals(window, _window)) return;
        _window = null;
        Detach();
        Closed?.Invoke(this, new CalloutClosedEventArgs(reason));
    }

    private void Detach()
    {
        if (_targetForm != null)
        {
            _targetForm.LocationChanged -= OnTargetMoved;
            _targetForm.SizeChanged -= OnTargetMoved;
        }

        if (_target != null)
        {
            _target.Disposed -= OnTargetGone;
            _target.VisibleChanged -= OnTargetGone;
            _target.LocationChanged -= OnTargetMoved;
            _target.SizeChanged -= OnTargetMoved;
        }

        _target = null;
        _targetForm = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _window?.BeginClose(CalloutCloseReason.Programmatic, immediate: true);
            Detach();
        }

        base.Dispose(disposing);
    }
}

internal sealed class CalloutWindow : LayeredPopupForm
{
    private readonly EnterpriseCalloutControl _owner;
    private readonly ControlColors _colors;
    private readonly PopupContent _content;
    private readonly ValueAnimator _fade = new();
    private readonly System.Windows.Forms.Timer _autoClose = new();
    private readonly Font _font;
    private readonly float _scale;
    private Rectangle _anchor;
    private Rectangle _body;
    private Rectangle _surface;
    private Size _bodySize;
    private IDisposable? _dismiss;
    private CalloutCloseReason _reason;
    private bool _closing;
    private bool _finished;

    public CalloutWindow(EnterpriseCalloutControl owner, Rectangle anchor)
    {
        _owner = owner;
        _anchor = anchor;
        _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        _scale = DeviceDpi / 96f;
        var font = new Font("Segoe UI", 9f);
        _font = font;
        Font = font;
        _content = new PopupContent(font, _colors, _scale)
        {
            Title = owner.Title,
            Message = owner.Message,
            Mode = owner.MarkupMode,
            Glyph = owner.Glyph,
            Icon = owner.Icon,
            Severity = owner.Severity,
            Actions = owner.Actions.ToArray(),
            ShowClose = owner.ShowCloseButton
        };
        _fade.Updated += (_, _) => OnFade();
        _autoClose.Tick += (_, _) => BeginClose(CalloutCloseReason.Timeout);
    }

    public CalloutPlacement ActualPlacement { get; private set; }

    private int S(double v) => (int)Math.Round(v * _scale);

    public void Open()
    {
        var width = _content.NaturalWidth(S(200), S(_owner.MaxWidth));
        _bodySize = _content.Measure(width);
        Layout();
        Draw(0);
        Show();
        _dismiss = PopupDismissFilter.Attach(() => Bounds, _owner.CloseOnEscape ? () => BeginClose(CalloutCloseReason.Escape) : null, _owner.CloseOnClickOutside ? () => BeginClose(CalloutCloseReason.ClickOutside) : null);
        _fade.AnimateTo(1, 140);
        if (_owner.AutoCloseInterval > 0)
        {
            _autoClose.Interval = _owner.AutoCloseInterval;
            _autoClose.Start();
        }
    }

    public void UpdateAnchor(Rectangle anchor)
    {
        if (_closing || _finished) return;
        _anchor = anchor;
        Layout();
        Draw((byte)(255 * _fade.Value));
    }

    private int ArrowHeight => S(_owner.ArrowSize);
    private int ShadowMargin => _owner.ShowShadow ? S(_owner.ShadowBlur) + S(4) : S(2);

    private void Layout()
    {
        var wa = Screen.FromRectangle(_anchor.IsEmpty ? new Rectangle(Cursor.Position, new Size(1, 1)) : _anchor).WorkingArea;
        var arrow = ArrowHeight;
        var gap = S(2);
        var size = _bodySize;
        var placement = _owner.Placement;
        if (placement == CalloutPlacement.Auto)
        {
            bool Fits(CalloutPlacement p) => p switch
            {
                CalloutPlacement.Bottom => _anchor.Bottom + gap + arrow + size.Height <= wa.Bottom,
                CalloutPlacement.Top => _anchor.Top - gap - arrow - size.Height >= wa.Top,
                CalloutPlacement.Right => _anchor.Right + gap + arrow + size.Width <= wa.Right,
                _ => _anchor.Left - gap - arrow - size.Width >= wa.Left
            };
            placement = new[] { CalloutPlacement.Bottom, CalloutPlacement.Top, CalloutPlacement.Right, CalloutPlacement.Left }.FirstOrDefault(Fits);
            if (placement == CalloutPlacement.Auto) placement = CalloutPlacement.Bottom;
        }

        ActualPlacement = placement;
        var center = new Point(_anchor.X + _anchor.Width / 2, _anchor.Y + _anchor.Height / 2);
        Point location = placement switch
        {
            CalloutPlacement.Top => new Point(center.X - size.Width / 2, _anchor.Top - gap - arrow - size.Height),
            CalloutPlacement.Left => new Point(_anchor.Left - gap - arrow - size.Width, center.Y - size.Height / 2),
            CalloutPlacement.Right => new Point(_anchor.Right + gap + arrow, center.Y - size.Height / 2),
            _ => new Point(center.X - size.Width / 2, _anchor.Bottom + gap + arrow)
        };
        location.X = Math.Clamp(location.X, wa.Left + S(4), Math.Max(wa.Left + S(4), wa.Right - size.Width - S(4)));
        location.Y = Math.Clamp(location.Y, wa.Top + S(4), Math.Max(wa.Top + S(4), wa.Bottom - size.Height - S(4)));
        _body = new Rectangle(location, size);

        var bubble = placement switch
        {
            CalloutPlacement.Top => Rectangle.FromLTRB(_body.Left, _body.Top, _body.Right, _body.Bottom + arrow),
            CalloutPlacement.Left => Rectangle.FromLTRB(_body.Left, _body.Top, _body.Right + arrow, _body.Bottom),
            CalloutPlacement.Right => Rectangle.FromLTRB(_body.Left - arrow, _body.Top, _body.Right, _body.Bottom),
            _ => Rectangle.FromLTRB(_body.Left, _body.Top - arrow, _body.Right, _body.Bottom)
        };
        var m = ShadowMargin;
        _surface = new Rectangle(bubble.X - m, bubble.Y - m, bubble.Width + m * 2, bubble.Height + m * 2);
    }

    private GraphicsPath BuildPath(Rectangle body)
    {
        var r = Math.Min(S(_owner.CornerRadius), Math.Min(body.Width, body.Height) / 2);
        var d = Math.Max(1, r * 2);
        var h = ArrowHeight;
        var hb = Math.Max(S(7), h);
        var placement = ActualPlacement;
        var anchorCenter = new Point(_anchor.X + _anchor.Width / 2 - _surface.X, _anchor.Y + _anchor.Height / 2 - _surface.Y);
        var ax = Math.Clamp(anchorCenter.X, body.Left + r + hb, body.Right - r - hb);
        var ay = Math.Clamp(anchorCenter.Y, body.Top + r + hb, body.Bottom - r - hb);

        var path = new GraphicsPath();
        path.AddArc(body.Left, body.Top, d, d, 180, 90);
        if (placement == CalloutPlacement.Bottom)
        {
            path.AddLine(ax - hb, body.Top, ax, body.Top - h);
            path.AddLine(ax, body.Top - h, ax + hb, body.Top);
        }

        path.AddArc(body.Right - d, body.Top, d, d, 270, 90);
        if (placement == CalloutPlacement.Left)
        {
            path.AddLine(body.Right, ay - hb, body.Right + h, ay);
            path.AddLine(body.Right + h, ay, body.Right, ay + hb);
        }

        path.AddArc(body.Right - d, body.Bottom - d, d, d, 0, 90);
        if (placement == CalloutPlacement.Top)
        {
            path.AddLine(ax + hb, body.Bottom, ax, body.Bottom + h);
            path.AddLine(ax, body.Bottom + h, ax - hb, body.Bottom);
        }

        path.AddArc(body.Left, body.Bottom - d, d, d, 90, 90);
        if (placement == CalloutPlacement.Right)
        {
            path.AddLine(body.Left, ay + hb, body.Left - h, ay);
            path.AddLine(body.Left - h, ay, body.Left, ay - hb);
        }

        path.CloseFigure();
        return path;
    }

    private void Draw(byte alpha)
    {
        var bodyLocal = new Rectangle(_body.X - _surface.X, _body.Y - _surface.Y, _body.Width, _body.Height);
        Render(_surface.Size, _surface.Location, g =>
        {
            var c = _colors;
            var back = _owner.BackgroundColor.IsEmpty ? c.Surface : _owner.BackgroundColor;
            var border = _owner.BorderColor.IsEmpty ? (_owner.Severity == NotificationSeverity.Info ? c.Border : _content.Accent) : _owner.BorderColor;
            using var path = BuildPath(bodyLocal);
            if (_owner.ShowShadow) Shapes.DrawSoftShadow(g, path, S(_owner.ShadowBlur), Color.FromArgb(c.HighContrast ? 0 : 90, 0, 0, 0), S(3));
            using (var fill = new SolidBrush(back)) g.FillPath(fill, path);

            using (var face = new Bitmap(bodyLocal.Width, bodyLocal.Height, PixelFormat.Format24bppRgb))
            {
                using (var fg = Graphics.FromImage(face))
                {
                    fg.Clear(back);
                    fg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    _content.Paint(fg, face.Size);
                }

                var state = g.Save();
                g.SetClip(path);
                g.IntersectClip(bodyLocal);
                g.DrawImageUnscaled(face, bodyLocal.Location);
                g.Restore(state);
            }

            using var pen = new Pen(border, _owner.Severity == NotificationSeverity.Info ? 1f : 1.5f) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }, alpha);
    }

    private void OnFade()
    {
        if (_finished) return;
        Present(_surface.Location, (byte)Math.Clamp(255 * _fade.Value, 0, 255));
        if (_closing && _fade.Value <= 0.01) Finish();
    }

    public void BeginClose(CalloutCloseReason reason, bool immediate = false)
    {
        if (_closing || _finished) return;
        _closing = true;
        _reason = reason;
        _autoClose.Stop();
        _dismiss?.Dispose();
        _dismiss = null;
        if (immediate) Finish();
        else _fade.AnimateTo(0, 110);
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        _fade.Dispose();
        _autoClose.Dispose();
        _dismiss?.Dispose();
        Hide();
        var owner = _owner;
        var reason = _reason;
        Dispose();
        owner.WindowClosed(this, reason);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_closing) return;
        var local = new Point(e.X - (_body.X - _surface.X), e.Y - (_body.Y - _surface.Y));
        var hit = _content.HitTest(local);
        Cursor = hit.Kind is PopupHitKind.Close or PopupHitKind.Action or PopupHitKind.Link || (hit.Kind == PopupHitKind.Body && _owner.CloseOnBodyClick) ? Cursors.Hand : Cursors.Default;
        if (hit == _content.Hover) return;
        _content.Hover = hit;
        Draw((byte)(255 * _fade.Value));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _autoClose.Stop();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_closing) return;
        _content.Hover = default;
        Draw((byte)(255 * _fade.Value));
        if (_owner.AutoCloseInterval > 0) _autoClose.Start();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _closing) return;
        var local = new Point(e.X - (_body.X - _surface.X), e.Y - (_body.Y - _surface.Y));
        var hit = _content.HitTest(local);
        switch (hit.Kind)
        {
            case PopupHitKind.Close: BeginClose(CalloutCloseReason.CloseButton); break;
            case PopupHitKind.Action:
                _owner.RaiseAction(_owner.Actions[hit.Index].Id, out var keep);
                if (!keep) BeginClose(CalloutCloseReason.Action);
                break;
            case PopupHitKind.Link when hit.Href != null: _owner.RaiseLink(hit.Href); break;
            case PopupHitKind.Body when _owner.CloseOnBodyClick: BeginClose(CalloutCloseReason.BodyClick); break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _content.Dispose();
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
