using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Theming;
using Timer = System.Windows.Forms.Timer;

namespace EnterpriseUI.WinForms;

/// <summary>Startup splash window with logo, title, status text and progress; updates are thread-safe and drawn without a message loop, so it works while the application is still loading.</summary>
[ToolboxItem(false)]
public class EnterpriseSplashScreen : LayeredPopupForm
{
    private readonly ValueAnimator _fade = new(1);
    private readonly Timer _closeTimer = new();
    private readonly object _gate = new();
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _status = string.Empty;
    private string _footer = string.Empty;
    private Image? _logo;
    private int _progress;
    private bool _showProgress = true;
    private int _minimumDuration;
    private bool _fadeOut = true;
    private Size _size = new(520, 300);
    private DateTime _shownAt;
    private bool _closing;
    private bool _closed;
    private bool _drawn;

    public EnterpriseSplashScreen()
    {
        _fade.Updated += (_, _) =>
        {
            Redraw();
            if (_closing && _fade.Value <= 0.01) FinishClose();
        };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            BeginFade();
        };
    }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? string.Empty);
    }

    [DefaultValue("")]
    public string Subtitle
    {
        get => _subtitle;
        set => Set(ref _subtitle, value ?? string.Empty);
    }

    [DefaultValue("")]
    public string StatusText
    {
        get => _status;
        set => Set(ref _status, value ?? string.Empty);
    }

    /// <summary>Small text at the bottom right, for version or copyright.</summary>
    [DefaultValue("")]
    public string FooterText
    {
        get => _footer;
        set => Set(ref _footer, value ?? string.Empty);
    }

    [DefaultValue(null)]
    public Image? Logo
    {
        get => _logo;
        set
        {
            _logo = value;
            RedrawSafe();
        }
    }

    /// <summary>0 to 100.</summary>
    [DefaultValue(0)]
    public int Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 100);
            RedrawSafe();
        }
    }

    [DefaultValue(true)]
    public bool ShowProgress
    {
        get => _showProgress;
        set
        {
            _showProgress = value;
            RedrawSafe();
        }
    }

    /// <summary>Milliseconds the splash stays up at least, even when closed earlier.</summary>
    [DefaultValue(0)]
    public int MinimumDuration
    {
        get => _minimumDuration;
        set => _minimumDuration = Math.Max(0, value);
    }

    [DefaultValue(true)]
    public bool FadeOut
    {
        get => _fadeOut;
        set => _fadeOut = value;
    }

    /// <summary>Size in logical pixels.</summary>
    public Size SplashSize
    {
        get => _size;
        set
        {
            _size = new Size(Math.Clamp(value.Width, 260, 1200), Math.Clamp(value.Height, 160, 800));
            RedrawSafe();
        }
    }

    private bool ShouldSerializeSplashSize() => _size != new Size(520, 300);
    private void ResetSplashSize() => SplashSize = new Size(520, 300);

    /// <summary>Draws the splash centered on the primary screen and shows it without waiting for a message loop.</summary>
    public void ShowSplash()
    {
        _shownAt = DateTime.UtcNow;
        Redraw();
        Show();
    }

    /// <summary>Updates progress and status text; can be called from any thread.</summary>
    public void ReportProgress(int percent, string? status = null)
    {
        lock (_gate)
        {
            _progress = Math.Clamp(percent, 0, 100);
            if (status != null) _status = status;
        }

        RedrawSafe();
    }

    private void Set(ref string field, string value)
    {
        field = value;
        RedrawSafe();
    }

    private void RedrawSafe()
    {
        if (!_drawn || _closed || IsDisposed) return;
        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            try { BeginInvoke(new Action(Redraw)); } catch (InvalidOperationException) { }
            return;
        }

        Redraw();
    }

    private double DpiFactor => DeviceDpi / 96.0;

    private void Redraw()
    {
        if (_closed || IsDisposed) return;
        _drawn = true;
        var scale = DpiFactor;
        var shadow = (int)(16 * scale);
        var body = new Size((int)(_size.Width * scale), (int)(_size.Height * scale));
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var size = new Size(body.Width + shadow * 2, body.Height + shadow * 2);
        var location = new Point(area.X + (area.Width - body.Width) / 2 - shadow, area.Y + (area.Height - body.Height) / 2 - shadow);
        Render(size, location, g => Draw(g, body, shadow, scale), (byte)Math.Clamp(255 * _fade.Value, 0, 255));
    }

    private void Draw(Graphics g, Size bodySize, int shadow, double scale)
    {
        int S(double v) => (int)Math.Round(v * scale);
        var c = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        string title, subtitle, status;
        int progress;
        lock (_gate)
        {
            title = _title;
            subtitle = _subtitle;
            status = _status;
            progress = _progress;
        }

        var body = new Rectangle(shadow, shadow, bodySize.Width, bodySize.Height);
        using var path = Chrome.RoundedRect(body, S(10));
        if (!c.HighContrast) Shapes.DrawSoftShadow(g, path, S(12), Color.FromArgb(100, 0, 0, 0), S(4));
        using (var fill = new SolidBrush(c.Surface)) g.FillPath(fill, path);
        using (var face = new Bitmap(bodySize.Width, bodySize.Height, PixelFormat.Format24bppRgb))
        {
            using (var fg = Graphics.FromImage(face))
            {
                fg.Clear(c.Surface);
                fg.SmoothingMode = SmoothingMode.AntiAlias;
                fg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                fg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var band = new LinearGradientBrush(new Rectangle(0, 0, face.Width, S(6) + 1), c.Primary, Chrome.Blend(c.Primary, c.Surface, 0.55), LinearGradientMode.Horizontal))
                    fg.FillRectangle(band, 0, 0, face.Width, S(6));
                var x = S(36);
                var y = S(48);
                if (_logo != null)
                {
                    var logo = S(72);
                    fg.DrawImage(_logo, new Rectangle(x, y, logo, logo));
                    x += logo + S(20);
                }

                using var titleFont = new Font("Segoe UI", (float)(22 * scale), FontStyle.Bold);
                using var subFont = new Font("Segoe UI", (float)(11 * scale));
                using var smallFont = new Font("Segoe UI", (float)(9 * scale));
                var textWidth = face.Width - x - S(36);
                Chrome.DrawText(fg, title, titleFont, new Rectangle(x, y, textWidth, titleFont.Height + S(4)), c.Text);
                if (subtitle.Length > 0) Chrome.DrawText(fg, subtitle, subFont, new Rectangle(x, y + titleFont.Height + S(6), textWidth, subFont.Height + S(4)), c.Muted);

                var bottom = face.Height - S(28);
                if (_footer.Length > 0) Chrome.DrawText(fg, _footer, smallFont, new Rectangle(S(36), bottom, face.Width - S(72), smallFont.Height), c.Muted, ContentAlignment.MiddleRight);
                var barY = bottom - S(24);
                if (_showProgress)
                {
                    var track = new Rectangle(S(36), barY, face.Width - S(72), S(6));
                    Chrome.FillRounded(fg, track, Chrome.Blend(c.Surface, c.Border, 0.7), S(3));
                    var filled = (int)(track.Width * progress / 100.0);
                    if (filled > 0) Chrome.FillRounded(fg, new Rectangle(track.X, track.Y, Math.Max(S(6), filled), track.Height), c.Primary, S(3));
                }

                Chrome.DrawText(fg, status, smallFont, new Rectangle(S(36), barY - smallFont.Height - S(8), face.Width - S(72), smallFont.Height + S(2)), c.Muted);
            }

            var state = g.Save();
            g.SetClip(path);
            g.DrawImageUnscaled(face, body.Location);
            g.Restore(state);
        }

        using var border = new Pen(c.Border);
        g.DrawPath(border, path);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && !_drawn) Redraw();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closed && e.CloseReason is CloseReason.UserClosing or CloseReason.None && !_closing)
        {
            e.Cancel = true;
            _closing = true;
            var remaining = _minimumDuration - (int)(DateTime.UtcNow - _shownAt).TotalMilliseconds;
            if (remaining > 0)
            {
                _closeTimer.Interval = remaining;
                _closeTimer.Start();
            }
            else BeginFade();
            return;
        }

        base.OnFormClosing(e);
    }

    private void BeginFade()
    {
        if (_fadeOut) _fade.AnimateTo(0, 220);
        else FinishClose();
        if (_fadeOut && !_fade.IsRunning) FinishClose();
    }

    private void FinishClose()
    {
        if (_closed) return;
        _closed = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeTimer.Dispose();
            _fade.Dispose();
        }

        base.Dispose(disposing);
    }
}
