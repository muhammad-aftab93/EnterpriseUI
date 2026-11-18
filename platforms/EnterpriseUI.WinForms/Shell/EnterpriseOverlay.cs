using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Theming;
using Timer = System.Windows.Forms.Timer;

namespace EnterpriseUI.WinForms;

public enum EnterpriseOverlayIndicator { None, Ring, Dots }

/// <summary>Click-through-proof dimmer window laid over a host control, with an optional busy indicator card.</summary>
internal sealed class OverlayWindow : LayeredPopupForm
{
    private readonly EnterpriseOverlay _owner;
    private readonly Timer _animation = new() { Interval = 40 };
    private double _phase;

    public OverlayWindow(EnterpriseOverlay owner)
    {
        _owner = owner;
        _animation.Tick += (_, _) =>
        {
            _phase = (_phase + 0.06) % 1.0;
            Redraw();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle &= ~0x8;
            return cp;
        }
    }

    public Rectangle ScreenBounds { get; set; }

    public void StartAnimation(bool run)
    {
        if (run && !_animation.Enabled) _animation.Start();
        else if (!run) _animation.Stop();
    }

    public void Redraw()
    {
        var size = ScreenBounds.Size;
        if (size.Width < 1 || size.Height < 1) return;
        Render(size, ScreenBounds.Location, Draw);
    }

    private void Draw(Graphics g)
    {
        var colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        var dim = _owner.DimColor.IsEmpty ? (colors.Surface.GetBrightness() < 0.5f ? Color.Black : Color.FromArgb(40, 44, 52)) : _owner.DimColor;
        using (var brush = new SolidBrush(Color.FromArgb((int)Math.Round(255 * _owner.Opacity), dim))) g.FillRectangle(brush, 0, 0, ScreenBounds.Width, ScreenBounds.Height);
        if (_owner.Indicator == EnterpriseOverlayIndicator.None && _owner.Caption.Length == 0) return;

        var scale = DeviceDpi / 96f;
        using var font = new Font("Segoe UI", 10f);
        var captionSize = _owner.Caption.Length == 0 ? Size.Empty : TextRenderer.MeasureText(_owner.Caption, font, new Size((int)(ScreenBounds.Width * 0.6), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        var indicator = _owner.Indicator == EnterpriseOverlayIndicator.None ? 0 : (int)(40 * scale);
        var pad = (int)(22 * scale);
        var gap = indicator > 0 && captionSize.Height > 0 ? (int)(12 * scale) : 0;
        var card = new Size(Math.Max((int)(150 * scale), Math.Max(indicator, captionSize.Width) + pad * 2), pad * 2 + indicator + gap + captionSize.Height);
        var cardRect = new Rectangle((ScreenBounds.Width - card.Width) / 2, (ScreenBounds.Height - card.Height) / 2, card.Width, card.Height);
        using var path = Chrome.RoundedRect(cardRect, (int)(8 * scale));
        if (!colors.HighContrast) Shapes.DrawSoftShadow(g, path, (int)(10 * scale), Color.FromArgb(90, 0, 0, 0), (int)(3 * scale));
        using (var fill = new SolidBrush(colors.Surface)) g.FillPath(fill, path);
        using (var face = new Bitmap(card.Width, card.Height, PixelFormat.Format24bppRgb))
        {
            using (var fg = Graphics.FromImage(face))
            {
                fg.Clear(colors.Surface);
                fg.SmoothingMode = SmoothingMode.AntiAlias;
                fg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                DrawIndicator(fg, new Rectangle((card.Width - indicator) / 2, pad, indicator, indicator), colors);
                if (captionSize.Height > 0) TextRenderer.DrawText(fg, _owner.Caption, font, new Rectangle(pad, pad + indicator + gap, card.Width - pad * 2, captionSize.Height), colors.Text, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }

            var state = g.Save();
            g.SetClip(path);
            g.DrawImageUnscaled(face, cardRect.Location);
            g.Restore(state);
        }

        using var border = new Pen(colors.Border);
        g.DrawPath(border, path);
    }

    private void DrawIndicator(Graphics g, Rectangle area, ControlColors colors)
    {
        if (area.Width <= 0) return;
        if (_owner.Indicator == EnterpriseOverlayIndicator.Ring)
        {
            using var track = new Pen(Chrome.Blend(colors.Surface, colors.Border, 0.8), Math.Max(3f, area.Width / 10f));
            using var arc = new Pen(colors.Primary, Math.Max(3f, area.Width / 10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var ring = Rectangle.Inflate(area, -4, -4);
            g.DrawEllipse(track, ring);
            g.DrawArc(arc, ring, (float)(_phase * 360), 100);
            return;
        }

        const int count = 8;
        var radius = area.Width / 2f - area.Width / 10f;
        var dot = Math.Max(4f, area.Width / 7f);
        for (var i = 0; i < count; i++)
        {
            var angle = (i / (double)count + _phase) * Math.PI * 2;
            var strength = 1 - ((i / (double)count + (1 - _phase)) % 1.0) * 0.8;
            using var brush = new SolidBrush(Chrome.Blend(colors.Surface, colors.Primary, strength));
            g.FillEllipse(brush, (float)(area.X + area.Width / 2f + Math.Cos(angle) * radius - dot / 2), (float)(area.Y + area.Height / 2f + Math.Sin(angle) * radius - dot / 2), dot, dot);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _owner.RaiseClicked();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Dims a control or form and blocks input to it while work runs, optionally showing a busy indicator and a caption.</summary>
[DefaultEvent("Clicked")]
public class EnterpriseOverlay : Component
{
    private OverlayWindow? _window;
    private Control? _host;
    private Form? _hostForm;
    private readonly List<(Control Control, bool Enabled)> _disabled = new();
    private double _opacity = 0.45;
    private string _caption = string.Empty;
    private EnterpriseOverlayIndicator _indicator = EnterpriseOverlayIndicator.Ring;

    public EnterpriseOverlay()
    {
    }

    public EnterpriseOverlay(IContainer container) : this() => container.Add(this);

    /// <summary>Control covered while the overlay is visible.</summary>
    [DefaultValue(null)]
    public Control? Host
    {
        get => _host;
        set
        {
            if (_host == value) return;
            var wasVisible = IsVisible;
            if (wasVisible) Hide();
            _host = value;
            if (wasVisible && value != null) Show();
        }
    }

    /// <summary>Strength of the dimming from 0 (clear) to 1 (opaque).</summary>
    [DefaultValue(0.45)]
    public double Opacity
    {
        get => _opacity;
        set
        {
            _opacity = Math.Clamp(value, 0, 1);
            Refresh();
        }
    }

    [DefaultValue(EnterpriseOverlayIndicator.Ring)]
    public EnterpriseOverlayIndicator Indicator
    {
        get => _indicator;
        set
        {
            _indicator = value;
            _window?.StartAnimation(value != EnterpriseOverlayIndicator.None);
            Refresh();
        }
    }

    [DefaultValue("")]
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value ?? string.Empty;
            Refresh();
        }
    }

    /// <summary>Color of the dimming layer; a theme-dependent dark tone when empty.</summary>
    public Color DimColor { get; set; } = Color.Empty;

    private bool ShouldSerializeDimColor() => !DimColor.IsEmpty;
    private void ResetDimColor() => DimColor = Color.Empty;

    /// <summary>Disables the covered controls so that neither mouse nor keyboard reaches them.</summary>
    [DefaultValue(true)]
    public bool BlockInput { get; set; } = true;

    [Browsable(false)]
    public bool IsVisible => _window is { Visible: true };

    public event EventHandler? Shown;
    public event EventHandler? Hidden;
    public event EventHandler? Clicked;

    /// <summary>Covers <paramref name="host"/> (or <see cref="Host"/>); a second call just updates the covered area.</summary>
    public void Show(Control? host = null)
    {
        if (host != null) _host = host;
        var target = _host ?? throw new InvalidOperationException("The overlay has no host control.");
        if (IsVisible)
        {
            Refresh();
            return;
        }

        _hostForm = target.FindForm();
        _window = new OverlayWindow(this);
        Attach(target);
        Track();
        if (_hostForm != null) _window.Owner = _hostForm;
        _window.Redraw();
        _window.Show();
        _window.StartAnimation(_indicator != EnterpriseOverlayIndicator.None);
        if (BlockInput) Block(target);
        Shown?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        var window = _window;
        if (window == null) return;
        _window = null;
        if (_host != null) Detach(_host);
        Unblock();
        window.StartAnimation(false);
        window.Close();
        window.Dispose();
        _hostForm = null;
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateCaption(string caption) => Caption = caption;

    internal void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);

    private void Refresh()
    {
        if (_window == null) return;
        Track();
        _window.Redraw();
    }

    private void Track()
    {
        if (_window == null || _host == null) return;
        _window.ScreenBounds = _host.IsHandleCreated ? _host.RectangleToScreen(_host.ClientRectangle) : Rectangle.Empty;
    }

    private void Attach(Control host)
    {
        host.SizeChanged += OnHostMoved;
        host.LocationChanged += OnHostMoved;
        host.VisibleChanged += OnHostVisibleChanged;
        if (_hostForm == null) return;
        _hostForm.LocationChanged += OnHostMoved;
        _hostForm.SizeChanged += OnHostMoved;
        _hostForm.VisibleChanged += OnHostVisibleChanged;
        _hostForm.FormClosed += OnHostClosed;
    }

    private void Detach(Control host)
    {
        host.SizeChanged -= OnHostMoved;
        host.LocationChanged -= OnHostMoved;
        host.VisibleChanged -= OnHostVisibleChanged;
        if (_hostForm == null) return;
        _hostForm.LocationChanged -= OnHostMoved;
        _hostForm.SizeChanged -= OnHostMoved;
        _hostForm.VisibleChanged -= OnHostVisibleChanged;
        _hostForm.FormClosed -= OnHostClosed;
    }

    private void OnHostMoved(object? sender, EventArgs e) => Refresh();

    private void OnHostVisibleChanged(object? sender, EventArgs e)
    {
        if (_window == null || _host == null) return;
        var visible = _host.Visible && (_hostForm?.WindowState != FormWindowState.Minimized);
        if (visible && !_window.Visible) _window.Show();
        else if (!visible && _window.Visible) _window.Hide();
    }

    private void OnHostClosed(object? sender, FormClosedEventArgs e) => Hide();

    private void Block(Control host)
    {
        _disabled.Clear();
        IEnumerable<Control> targets = host is Form form ? form.Controls.Cast<Control>().Where(c => c is not EnterpriseTitleBarControl) : new[] { host };
        foreach (var control in targets.ToList())
        {
            _disabled.Add((control, control.Enabled));
            control.Enabled = false;
        }
    }

    private void Unblock()
    {
        foreach (var (control, enabled) in _disabled)
        {
            if (!control.IsDisposed) control.Enabled = enabled;
        }

        _disabled.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Hide();
        base.Dispose(disposing);
    }
}
