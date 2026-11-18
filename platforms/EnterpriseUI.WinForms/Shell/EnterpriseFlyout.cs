using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum EnterpriseFlyoutEdge { Left, Right, Top, Bottom }

/// <summary>Panel that slides in from an edge of a host form, optionally dimming and blocking the host and closing on an outside click.</summary>
[ToolboxItem(false)]
[DefaultEvent("FlyoutClosed")]
public class EnterpriseFlyout : Form
{
    private const int HeaderHeight = 40;
    private readonly ValueAnimator _slide = new();
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = HeaderHeight };
    private readonly Label _title = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0), Font = new Font("Segoe UI Semibold", 10.5f) };
    private readonly Label _closeGlyph = new() { Dock = DockStyle.Right, Width = HeaderHeight, Text = "✕", TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Panel _surface = new();
    private readonly Panel _body = new() { Dock = DockStyle.Fill };
    private readonly EnterpriseOverlay _scrim = new() { Indicator = EnterpriseOverlayIndicator.None, Opacity = 0.35, BlockInput = true };
    private readonly EventHandler<Abstractions.Theming.ITheme> _themeHandler;
    private Control? _host;
    private Form? _hostForm;
    private EnterpriseFlyoutEdge _edge = EnterpriseFlyoutEdge.Right;
    private int _extent = 340;
    private int _duration = 200;
    private bool _modal = true;
    private bool _lightDismiss = true;
    private bool _showHeader = true;
    private bool _opening;
    private bool _closing;
    private bool _open;

    public EnterpriseFlyout()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);
        _header.Controls.Add(_title);
        _header.Controls.Add(_closeGlyph);
        _surface.Controls.Add(_body);
        _surface.Controls.Add(_header);
        Controls.Add(_surface);
        _closeGlyph.Click += (_, _) => HideFlyout();
        _slide.Updated += (_, _) => ApplyProgress();
        _scrim.Clicked += (_, _) =>
        {
            if (_lightDismiss) HideFlyout();
        };
        _themeHandler = (_, _) =>
        {
            if (!IsDisposed) ApplyTheme();
        };
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        ApplyTheme();
    }

    /// <summary>Panel that receives the flyout's controls.</summary>
    [Browsable(false)]
    public Panel Content => _body;

    [DefaultValue(EnterpriseFlyoutEdge.Right)]
    public EnterpriseFlyoutEdge Edge
    {
        get => _edge;
        set { _edge = value; if (_open) Reposition(); }
    }

    /// <summary>Width (left and right edges) or height (top and bottom edges) in logical pixels.</summary>
    [DefaultValue(340)]
    public int FlyoutSize
    {
        get => _extent;
        set { _extent = Math.Clamp(value, 120, 2000); if (_open) Reposition(); }
    }

    /// <summary>Milliseconds the slide takes; 0 shows the flyout at once.</summary>
    [DefaultValue(200)]
    public int AnimationDuration
    {
        get => _duration;
        set => _duration = Math.Clamp(value, 0, 2000);
    }

    /// <summary>A modal flyout dims the host and blocks input to it until the flyout closes.</summary>
    [DefaultValue(true)]
    public bool IsModal
    {
        get => _modal;
        set => _modal = value;
    }

    /// <summary>Closes the flyout when the dimmed area is clicked (modal) or when it loses the focus (non-modal).</summary>
    [DefaultValue(true)]
    public bool LightDismiss
    {
        get => _lightDismiss;
        set => _lightDismiss = value;
    }

    [DefaultValue(true)]
    public bool ShowHeader
    {
        get => _showHeader;
        set { _showHeader = value; _header.Visible = value; }
    }

    [AllowNull]
    [DefaultValue("")]
    public override string Text
    {
        get => base.Text;
        set { base.Text = value ?? string.Empty; _title.Text = base.Text; }
    }

    [Browsable(false)]
    public bool IsOpen => _open && !_closing;

    public event EventHandler? FlyoutOpening;
    public event EventHandler? FlyoutOpened;
    public event CancelEventHandler? FlyoutClosing;
    public event EventHandler? FlyoutClosed;

    /// <summary>Slides the flyout in over <paramref name="host"/>; a form hosts it on its full client area.</summary>
    public void ShowFlyout(Control host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_open || _opening) return;
        _opening = true;
        _host = host;
        _hostForm = host.FindForm();
        FlyoutOpening?.Invoke(this, EventArgs.Empty);
        if (_hostForm != null)
        {
            Owner = _hostForm;
            _hostForm.LocationChanged += OnHostChanged;
            _hostForm.SizeChanged += OnHostChanged;
        }

        host.SizeChanged += OnHostChanged;
        if (_modal) _scrim.Show(host);
        _slide.Jump(0);
        Reposition();
        Show();
        Activate();
        _open = true;
        _opening = false;
        _slide.AnimateTo(1, _duration);
    }

    /// <summary>Slides the flyout out; <see cref="FlyoutClosing"/> can cancel.</summary>
    public void HideFlyout()
    {
        if (!_open || _closing) return;
        var args = new CancelEventArgs();
        FlyoutClosing?.Invoke(this, args);
        if (args.Cancel) return;
        _closing = true;
        _slide.AnimateTo(0, _duration);
        if (!_slide.IsRunning) FinishClose();
    }

    public void Toggle(Control host)
    {
        if (IsOpen) HideFlyout();
        else ShowFlyout(host);
    }

    private Rectangle HostBounds() => _host is { IsHandleCreated: true } ? _host.RectangleToScreen(_host.ClientRectangle) : Rectangle.Empty;

    private Size FullSize(Rectangle host)
    {
        var extent = (int)Math.Round(_extent * DeviceDpi / 96.0);
        return _edge is EnterpriseFlyoutEdge.Left or EnterpriseFlyoutEdge.Right ? new Size(Math.Min(extent, host.Width), host.Height) : new Size(host.Width, Math.Min(extent, host.Height));
    }

    private void Reposition()
    {
        var host = HostBounds();
        if (host.IsEmpty) return;
        var size = FullSize(host);
        var x = _edge == EnterpriseFlyoutEdge.Right ? host.Right - size.Width : host.Left;
        var y = _edge == EnterpriseFlyoutEdge.Bottom ? host.Bottom - size.Height : host.Top;
        SetBounds(x, y, size.Width, size.Height);
        _surface.Size = size;
        ApplyProgress();
    }

    private void ApplyProgress()
    {
        var size = ClientSize;
        var t = Math.Clamp(_slide.Value, 0, 1);
        var visible = _edge is EnterpriseFlyoutEdge.Left or EnterpriseFlyoutEdge.Right ? (int)Math.Round(size.Width * t) : (int)Math.Round(size.Height * t);
        Rectangle clip;
        Point offset;
        switch (_edge)
        {
            case EnterpriseFlyoutEdge.Right:
                clip = new Rectangle(size.Width - visible, 0, visible, size.Height);
                offset = new Point(size.Width - visible, 0);
                break;
            case EnterpriseFlyoutEdge.Left:
                clip = new Rectangle(0, 0, visible, size.Height);
                offset = new Point(visible - size.Width, 0);
                break;
            case EnterpriseFlyoutEdge.Top:
                clip = new Rectangle(0, 0, size.Width, visible);
                offset = new Point(0, visible - size.Height);
                break;
            default:
                clip = new Rectangle(0, size.Height - visible, size.Width, visible);
                offset = new Point(0, size.Height - visible);
                break;
        }

        _surface.Location = offset;
        var old = Region;
        Region = new Region(clip);
        old?.Dispose();
        if (_closing && t <= 0.001) FinishClose();
        else if (!_closing && _open && t >= 0.999 && !_opened)
        {
            _opened = true;
            FlyoutOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _opened;

    private void FinishClose()
    {
        if (!_closing) return;
        _closing = false;
        _open = false;
        _opened = false;
        if (_host != null) _host.SizeChanged -= OnHostChanged;
        if (_hostForm != null)
        {
            _hostForm.LocationChanged -= OnHostChanged;
            _hostForm.SizeChanged -= OnHostChanged;
        }

        if (_scrim.IsVisible) _scrim.Hide();
        Owner = null;
        Hide();
        FlyoutClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnHostChanged(object? sender, EventArgs e)
    {
        if (_open && !_closing) Reposition();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (_open && !_modal && _lightDismiss && !_closing && !ContainsFocus && ActiveForm != this) HideFlyout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Escape || !_open) return;
        e.Handled = true;
        HideFlyout();
    }

    private void ApplyTheme()
    {
        var c = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        BackColor = c.Border;
        _surface.BackColor = c.Surface;
        _body.BackColor = c.Surface;
        _header.BackColor = c.Header;
        _title.ForeColor = c.Text;
        _closeGlyph.ForeColor = c.Muted;
        ForeColor = c.Text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            _slide.Dispose();
            _scrim.Dispose();
        }

        base.Dispose(disposing);
    }
}
