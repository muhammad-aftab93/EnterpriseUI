using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>Part of the window frame under a point, as reported by <see cref="EnterpriseForm.FrameHitTest"/>.</summary>
public enum EnterpriseFormHit { Client, Caption, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>Themed top-level window with its own caption bar, sizing frame, maximize handling and DPI scaling; the base of the framed form variants.</summary>
public class EnterpriseForm : Form, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private readonly EnterpriseTitleBarControl _titleBar = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private ControlColors? _colors;
    private Color _borderColor = Color.Empty;
    private bool _allowResize = true;
    private int _captionHeight = 36;
    private int _cornerRadius = 8;
    private FormWindowState _lastState;

    public EnterpriseForm()
    {
        Font = new Font("Segoe UI", 10f);
        Controls.Add(_titleBar);
        _titleBar.Text = base.Text;
        _titleBar.Height = ScaledCaptionHeight;
        _themeHandler = (_, theme) =>
        {
            if (IsDisposed) return;
            _theme = theme;
            _colors = null;
            if (IsHandleCreated && InvokeRequired)
            {
                try { BeginInvoke(new Action(ApplyTheme)); } catch (InvalidOperationException) { }
                return;
            }

            ApplyTheme();
        };
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        ApplyTheme();
    }

    public string? ThemeOverride { get; set; }

    protected ControlColors Colors => _colors ??= ControlColors.FromTheme(_theme);

    /// <summary>The caption bar docked at the top of the window.</summary>
    [Browsable(false)]
    public EnterpriseTitleBarControl TitleBar => _titleBar;

    /// <summary>Height of the caption bar in logical pixels.</summary>
    [DefaultValue(36)]
    public int CaptionHeight
    {
        get => _captionHeight;
        set
        {
            _captionHeight = Math.Clamp(value, 24, 96);
            _titleBar.Height = ScaledCaptionHeight;
        }
    }

    /// <summary>Whether the window can be resized with its frame; fixed windows suit dialogs.</summary>
    [DefaultValue(true)]
    public bool AllowResize
    {
        get => _allowResize;
        set
        {
            if (_allowResize == value) return;
            _allowResize = value;
            FormBorderStyle = value ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle;
        }
    }

    /// <summary>Color of the window outline; the theme border color when empty.</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; RedrawFrame(); }
    }

    private bool ShouldSerializeBorderColor() => !_borderColor.IsEmpty;
    private void ResetBorderColor() => BorderColor = Color.Empty;

    /// <summary>0 for square window corners; any positive value asks the system for rounded corners where supported.</summary>
    [DefaultValue(8)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            if (IsHandleCreated) ShellNative.ApplyWindowEffects(Handle, IsDark, _cornerRadius > 0);
        }
    }

    [AllowNull]
    [DefaultValue("")]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? string.Empty;
            _titleBar.Text = base.Text;
        }
    }

    internal Color EffectiveBorderColor => _borderColor.IsEmpty ? Colors.Border : _borderColor;

    private bool IsDark => Colors.Surface.GetBrightness() < 0.5f;
    internal int ScaledCaptionHeight => (int)Math.Round(_captionHeight * DeviceDpi / 96.0);
    private int FrameWidth => WindowState == FormWindowState.Maximized ? 0 : Math.Max(1, (int)Math.Round((_allowResize ? 4 : 1) * DeviceDpi / 96.0));

    public event EventHandler? ThemeApplied;
    public event EventHandler? CaptionDoubleClick;
    public event EventHandler? WindowStateChanged;

    public void OnThemeChanged(ITheme newTheme) => _themeHandler(this, newTheme);

    /// <summary>Re-reads the active theme into the window, the caption bar and the frame.</summary>
    public void ApplyTheme()
    {
        _colors = null;
        BackColor = Colors.Surface;
        ForeColor = Colors.Text;
        _titleBar.BackColor = Colors.Header;
        _titleBar.Invalidate();
        if (IsHandleCreated) ShellNative.ApplyWindowEffects(Handle, IsDark, _cornerRadius > 0);
        RedrawFrame();
        OnThemeApplied();
        ThemeApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called after the palette changed, for forms that recolor their own controls.</summary>
    protected virtual void OnThemeApplied()
    {
    }

    /// <summary>Re-reads icon and system buttons after the form's ControlBox, MinimizeBox, MaximizeBox or Icon changed.</summary>
    public void RefreshTitleBar() => _titleBar.RefreshLayout();

    /// <summary>Opens the window's system menu (Restore, Move, Size, Close); at the caption's left edge when no point is given.</summary>
    public void ShowSystemMenu(Point? screenLocation = null)
    {
        if (!IsHandleCreated) return;
        ShellNative.ShowSystemMenu(Handle, screenLocation ?? PointToScreen(new Point(0, _titleBar.Height)));
    }

    /// <summary>Classifies a screen point; edges and corners resize, the empty caption moves the window.</summary>
    public EnterpriseFormHit FrameHitTest(Point screenPoint)
    {
        var bounds = Bounds;
        if (!bounds.Contains(screenPoint)) return EnterpriseFormHit.Client;
        if (WindowState == FormWindowState.Normal && _allowResize)
        {
            var grip = FrameWidth;
            var corner = grip * 3;
            var left = screenPoint.X < bounds.Left + grip;
            var right = screenPoint.X >= bounds.Right - grip;
            var top = screenPoint.Y < bounds.Top + grip;
            var bottom = screenPoint.Y >= bounds.Bottom - grip;
            var nearLeft = screenPoint.X < bounds.Left + corner;
            var nearRight = screenPoint.X >= bounds.Right - corner;
            var nearTop = screenPoint.Y < bounds.Top + corner;
            var nearBottom = screenPoint.Y >= bounds.Bottom - corner;
            if ((top && nearLeft) || (left && nearTop)) return EnterpriseFormHit.TopLeft;
            if ((top && nearRight) || (right && nearTop)) return EnterpriseFormHit.TopRight;
            if ((bottom && nearLeft) || (left && nearBottom)) return EnterpriseFormHit.BottomLeft;
            if ((bottom && nearRight) || (right && nearBottom)) return EnterpriseFormHit.BottomRight;
            if (top) return EnterpriseFormHit.Top;
            if (bottom) return EnterpriseFormHit.Bottom;
            if (left) return EnterpriseFormHit.Left;
            if (right) return EnterpriseFormHit.Right;
        }

        if (_titleBar.Visible)
        {
            var local = _titleBar.PointToClient(screenPoint);
            if (_titleBar.ClientRectangle.Contains(local) && _titleBar.IsDragArea(local)) return EnterpriseFormHit.Caption;
        }

        return EnterpriseFormHit.Client;
    }

    private static int ToHitCode(EnterpriseFormHit hit) => hit switch
    {
        EnterpriseFormHit.Caption => ShellNative.HtCaption,
        EnterpriseFormHit.Left => ShellNative.HtLeft,
        EnterpriseFormHit.Right => ShellNative.HtRight,
        EnterpriseFormHit.Top => ShellNative.HtTop,
        EnterpriseFormHit.Bottom => ShellNative.HtBottom,
        EnterpriseFormHit.TopLeft => ShellNative.HtTopLeft,
        EnterpriseFormHit.TopRight => ShellNative.HtTopRight,
        EnterpriseFormHit.BottomLeft => ShellNative.HtBottomLeft,
        EnterpriseFormHit.BottomRight => ShellNative.HtBottomRight,
        _ => ShellNative.HtClient
    };

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case ShellNative.WmNcCalcSize when m.WParam != IntPtr.Zero:
                CalculateClientArea(ref m);
                return;
            case ShellNative.WmNcPaint:
                PaintFrame();
                m.Result = IntPtr.Zero;
                return;
            case ShellNative.WmNcActivate:
                m.LParam = (IntPtr)(-1);
                base.WndProc(ref m);
                PaintFrame();
                return;
            case ShellNative.WmNcHitTest:
                var hit = FrameHitTest(ShellNative.PointFromLParam(m.LParam));
                if (hit != EnterpriseFormHit.Client)
                {
                    m.Result = (IntPtr)ToHitCode(hit);
                    return;
                }

                break;
            case 0xA3 when (int)m.WParam == ShellNative.HtCaption:
                CaptionDoubleClick?.Invoke(this, EventArgs.Empty);
                _titleBar.RaiseCaptionDoubleClick();
                break;
        }

        base.WndProc(ref m);
    }

    private void CalculateClientArea(ref Message m)
    {
        var rect = Marshal.PtrToStructure<ShellNative.Rect>(m.LParam);
        if (WindowState == FormWindowState.Maximized)
        {
            var overhang = _allowResize ? ShellNative.MaximizedOverhang((uint)DeviceDpi) : 0;
            rect.Left += overhang;
            rect.Top += overhang;
            rect.Right -= overhang;
            rect.Bottom -= overhang;
        }
        else
        {
            var frame = FrameWidth;
            rect.Left += frame;
            rect.Right -= frame;
            rect.Bottom -= frame;
        }

        Marshal.StructureToPtr(rect, m.LParam, false);
        m.Result = IntPtr.Zero;
    }

    private void PaintFrame()
    {
        var frame = FrameWidth;
        if (!IsHandleCreated || frame == 0) return;
        var g = ShellNative.WindowGraphics(Handle, out var dc);
        if (g == null) return;
        try
        {
            using var fill = new SolidBrush(Colors.Header);
            using var border = new Pen(EffectiveBorderColor);
            g.FillRectangle(fill, 0, 0, frame, Height);
            g.FillRectangle(fill, Width - frame, 0, frame, Height);
            g.FillRectangle(fill, 0, Height - frame, Width, frame);
            g.DrawLine(border, 0, 0, 0, Height - 1);
            g.DrawLine(border, Width - 1, 0, Width - 1, Height - 1);
            g.DrawLine(border, 0, Height - 1, Width - 1, Height - 1);
        }
        finally
        {
            g.Dispose();
            ShellNative.ReleaseWindowGraphics(Handle, dc);
        }
    }

    private void RedrawFrame()
    {
        PaintFrame();
        _titleBar.Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ShellNative.ApplyWindowEffects(Handle, IsDark, _cornerRadius > 0);
        _lastState = WindowState;
        _titleBar.RefreshLayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) _titleBar.RefreshLayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == _lastState) return;
        _lastState = WindowState;
        _titleBar.Invalidate();
        WindowStateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _titleBar.Height = ScaledCaptionHeight;
    }

    /// <summary>Keeps the caption bar docked first so that fill controls added later do not slide under it.</summary>
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != _titleBar) ArrangeChrome();
    }

    /// <summary>Puts the framed form's own docked controls in dock order: the caption bar is laid out first.</summary>
    protected virtual void ArrangeChrome()
    {
        if (Controls.Contains(_titleBar)) Controls.SetChildIndex(_titleBar, Controls.Count - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}
