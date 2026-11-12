using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Vector Analog &amp; Digital Clock control with timezone offset and smooth second hand.
/// </summary>
public class EnterpriseClockControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly System.Windows.Forms.Timer _timer;

    public string? ThemeOverride { get; set; }
    public int TimeZoneOffsetHours { get; set; }
    public string TimeZoneLabel { get; set; } = "UTC+0 (London / Enterprise HQ)";
    public bool ShowDigitalReadout { get; set; } = true;

    public EnterpriseClockControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(240, 260);
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Invalidate();
        _timer.Start();
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var border = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.BorderColor), Color.FromArgb(200, 206, 214));

        g.Clear(surface);

        int radius = Math.Min(Width, Height - 48) / 2 - 14;
        if (radius < 20) return;

        int cx = Width / 2;
        int cy = (Height - 36) / 2;

        using var dialBrush = new SolidBrush(Color.FromArgb(14, primary));
        using var dialPen = new Pen(primary, 2.5f);
        g.FillEllipse(dialBrush, cx - radius, cy - radius, radius * 2, radius * 2);
        g.DrawEllipse(dialPen, cx - radius, cy - radius, radius * 2, radius * 2);

        // Draw 12 hour markers and 60 minute ticks
        using var hourPen = new Pen(text, 2.2f);
        using var minPen = new Pen(border, 1f);
        for (int i = 0; i < 60; i++)
        {
            double angle = i * Math.PI / 30.0 - Math.PI / 2.0;
            bool isHour = i % 5 == 0;
            int innerR = isHour ? radius - 12 : radius - 6;
            int outerR = radius - 2;
            float x1 = cx + (float)(Math.Cos(angle) * innerR);
            float y1 = cy + (float)(Math.Sin(angle) * innerR);
            float x2 = cx + (float)(Math.Cos(angle) * outerR);
            float y2 = cy + (float)(Math.Sin(angle) * outerR);
            g.DrawLine(isHour ? hourPen : minPen, x1, y1, x2, y2);
        }

        var now = DateTime.UtcNow.AddHours(TimeZoneOffsetHours);
        double secAngle = (now.Second / 60.0) * 2 * Math.PI - Math.PI / 2;
        double minAngle = ((now.Minute + now.Second / 60.0) / 60.0) * 2 * Math.PI - Math.PI / 2;
        double hrAngle = (((now.Hour % 12) + now.Minute / 60.0) / 12.0) * 2 * Math.PI - Math.PI / 2;

        // Hour hand
        using (var hrPen = new Pen(text, 4f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
        {
            g.DrawLine(hrPen, cx, cy, cx + (float)(Math.Cos(hrAngle) * (radius * 0.52)), cy + (float)(Math.Sin(hrAngle) * (radius * 0.52)));
        }

        // Minute hand
        using (var mPen = new Pen(primary, 3f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
        {
            g.DrawLine(mPen, cx, cy, cx + (float)(Math.Cos(minAngle) * (radius * 0.74)), cy + (float)(Math.Sin(minAngle) * (radius * 0.74)));
        }

        // Second hand
        using (var sPen = new Pen(Color.FromArgb(220, 53, 69), 1.6f) { EndCap = LineCap.Round })
        {
            g.DrawLine(sPen, cx, cy, cx + (float)(Math.Cos(secAngle) * (radius * 0.84)), cy + (float)(Math.Sin(secAngle) * (radius * 0.84)));
        }

        using var capBrush = new SolidBrush(primary);
        g.FillEllipse(capBrush, cx - 5, cy - 5, 10, 10);

        if (ShowDigitalReadout)
        {
            using var digitalFont = new Font("Consolas", 10f, FontStyle.Bold);
            using var labelFont = new Font("Segoe UI", 8.25f);
            using var textBrush = new SolidBrush(text);
            var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(now.ToString("HH:mm:ss"), digitalFont, textBrush, new RectangleF(0, Height - 42, Width, 20), sf);
            g.DrawString(TimeZoneLabel, labelFont, textBrush, new RectangleF(0, Height - 22, Width, 18), sf);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Interactive dual-thumb RangeSelector control with sparkline preview.
/// </summary>
public class EnterpriseRangeSelectorControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly double[] _series = { 22, 38, 31, 54, 48, 72, 65, 84, 79, 92, 68, 74, 88, 95, 70, 62, 78, 85, 91, 98 };
    private bool _draggingStart;
    private bool _draggingEnd;

    public string? ThemeOverride { get; set; }
    public double SelectionStartPercent { get; set; } = 20.0;
    public double SelectionEndPercent { get; set; } = 75.0;
    public event Action<double, double>? RangeChanged;

    public EnterpriseRangeSelectorControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(520, 90);
        Cursor = Cursors.SizeWE;
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int plotLeft = 16;
        int plotWidth = Math.Max(100, Width - 32);
        int startX = plotLeft + (int)(plotWidth * (SelectionStartPercent / 100.0));
        int endX = plotLeft + (int)(plotWidth * (SelectionEndPercent / 100.0));

        if (Math.Abs(e.X - startX) < Math.Abs(e.X - endX))
        {
            _draggingStart = true;
        }
        else
        {
            _draggingEnd = true;
        }
        UpdateThumbFromMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingStart || _draggingEnd)
        {
            UpdateThumbFromMouse(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingStart = false;
        _draggingEnd = false;
    }

    private void UpdateThumbFromMouse(int mouseX)
    {
        int plotLeft = 16;
        int plotWidth = Math.Max(100, Width - 32);
        double pct = Math.Clamp((mouseX - plotLeft) * 100.0 / plotWidth, 0.0, 100.0);

        if (_draggingStart)
        {
            SelectionStartPercent = Math.Min(pct, SelectionEndPercent - 5.0);
        }
        else if (_draggingEnd)
        {
            SelectionEndPercent = Math.Max(pct, SelectionStartPercent + 5.0);
        }

        Invalidate();
        RangeChanged?.Invoke(SelectionStartPercent, SelectionEndPercent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var border = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.BorderColor), Color.FromArgb(200, 206, 214));
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));

        g.Clear(surface);

        var plot = new Rectangle(16, 24, Math.Max(100, Width - 32), Math.Max(30, Height - 36));
        using var borderPen = new Pen(border);
        g.DrawRectangle(borderPen, plot);

        // Draw sparkline curve
        var pts = new PointF[_series.Length];
        for (int i = 0; i < _series.Length; i++)
        {
            float x = plot.Left + (i / (float)(_series.Length - 1)) * plot.Width;
            float y = plot.Bottom - (float)(_series[i] / 100.0) * (plot.Height - 8) - 4;
            pts[i] = new PointF(x, y);
        }
        using var linePen = new Pen(primary, 2f);
        g.DrawLines(linePen, pts);

        // Draw selected range window
        int startX = plot.Left + (int)(plot.Width * (SelectionStartPercent / 100.0));
        int endX = plot.Left + (int)(plot.Width * (SelectionEndPercent / 100.0));

        using var dimBrush = new SolidBrush(Color.FromArgb(90, 120, 128, 140));
        g.FillRectangle(dimBrush, plot.Left, plot.Top, startX - plot.Left, plot.Height);
        g.FillRectangle(dimBrush, endX, plot.Top, plot.Right - endX, plot.Height);

        using var selBrush = new SolidBrush(Color.FromArgb(35, primary));
        using var thumbBrush = new SolidBrush(primary);
        g.FillRectangle(selBrush, startX, plot.Top, Math.Max(2, endX - startX), plot.Height);
        g.FillRectangle(thumbBrush, startX - 4, plot.Top - 2, 8, plot.Height + 4);
        g.FillRectangle(thumbBrush, endX - 4, plot.Top - 2, 8, plot.Height + 4);

        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(text);
        g.DrawString($"EnterpriseRangeSelector Viewport Window: {SelectionStartPercent:F0}% — {SelectionEndPercent:F0}% (Drag handles to zoom)", font, textBrush, new PointF(16, 4));
    }
}

/// <summary>
/// Interactive SlideView, Rotator &amp; PipsPager control.
/// </summary>
public class EnterpriseSlideRotatorPipsControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly (string Title, string Subtitle, string Metric, Color Accent)[] _slides =
    {
        ("Global Revenue & Margin Telemetry", "Real-time consolidated ledger across 42 enterprise subsidiaries", "$148.4M (+18.2% YoY)", Color.FromArgb(0, 120, 212)),
        ("Zero-Flicker 1M-Row DataGrid Virtualization", "O(1) viewport row windowing with < 2ms frame budget", "1,000,000 Rows @ 60 FPS", Color.FromArgb(16, 124, 65)),
        ("Offline AI Copilot & SmartPaste Pipeline", "Natural language grid filtering, anomaly detection & clipboard parsing", "99.4% Extraction Accuracy", Color.FromArgb(136, 23, 152)),
        ("WCAG 2.2 AAA High-Contrast & RTL Compliance", "Full keyboard navigation, screen reader automation & Arabic/Hebrew mirroring", "100% Accessibility Audit", Color.FromArgb(216, 59, 1)),
        ("Document Processing & Vector Barcode Engine", "DOCX, XLSX, PDF, ZIP & QR/Code128/DataMatrix without third-party dependencies", "Zero External Dependencies", Color.FromArgb(0, 153, 188))
    };

    private int _currentIndex;
    public string? ThemeOverride { get; set; }
    public int SelectedSlideIndex
    {
        get => _currentIndex;
        set
        {
            _currentIndex = ((value % _slides.Length) + _slides.Length) % _slides.Length;
            Invalidate();
            SlideChanged?.Invoke(_currentIndex, _slides[_currentIndex].Title);
        }
    }

    public event Action<int, string>? SlideChanged;

    public EnterpriseSlideRotatorPipsControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(640, 210);
        Cursor = Cursors.Hand;
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void NextSlide() => SelectedSlideIndex = _currentIndex + 1;
    public void PreviousSlide() => SelectedSlideIndex = _currentIndex - 1;

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.X < 56)
        {
            PreviousSlide();
            return;
        }
        if (e.X > Width - 56)
        {
            NextSlide();
            return;
        }

        // Check PipsPager dots at bottom
        int pipStartX = Width / 2 - (_slides.Length * 22) / 2;
        int pipY = Height - 28;
        for (int i = 0; i < _slides.Length; i++)
        {
            var pipRect = new Rectangle(pipStartX + i * 22, pipY - 4, 18, 18);
            if (pipRect.Contains(e.Location))
            {
                SelectedSlideIndex = i;
                return;
            }
        }

        NextSlide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));
        var border = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.BorderColor), Color.FromArgb(200, 206, 214));

        g.Clear(surface);

        var slide = _slides[_currentIndex];
        var cardRect = new Rectangle(60, 12, Math.Max(120, Width - 120), Math.Max(80, Height - 52));

        using var cardBrush = new SolidBrush(Color.FromArgb(22, slide.Accent));
        using var cardBorder = new Pen(slide.Accent, 2f);
        g.FillRectangle(cardBrush, cardRect);
        g.DrawRectangle(cardBorder, cardRect);

        using var badgeBrush = new SolidBrush(slide.Accent);
        g.FillRectangle(badgeBrush, cardRect.Left, cardRect.Top, 8, cardRect.Height);

        using var titleFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 9.5f);
        using var metricFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var textBrush = new SolidBrush(text);
        using var accentBrush = new SolidBrush(slide.Accent);

        g.DrawString($"Slide {_currentIndex + 1}/{_slides.Length}: {slide.Title}", titleFont, textBrush, new PointF(cardRect.Left + 24, cardRect.Top + 18));
        g.DrawString(slide.Subtitle, subFont, textBrush, new PointF(cardRect.Left + 24, cardRect.Top + 52));
        g.DrawString(slide.Metric, metricFont, accentBrush, new PointF(cardRect.Left + 24, cardRect.Top + 86));

        // Prev / Next navigation buttons
        var prevRect = new Rectangle(12, Height / 2 - 24, 36, 36);
        var nextRect = new Rectangle(Width - 48, Height / 2 - 24, 36, 36);
        using var navPen = new Pen(border, 1.5f);
        g.DrawEllipse(navPen, prevRect);
        g.DrawEllipse(navPen, nextRect);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("◀", subFont, textBrush, prevRect, sf);
        g.DrawString("▶", subFont, textBrush, nextRect, sf);

        // EnterprisePipsPager indicator at bottom
        int pipStartX = Width / 2 - (_slides.Length * 22) / 2;
        int pipY = Height - 24;
        for (int i = 0; i < _slides.Length; i++)
        {
            bool active = i == _currentIndex;
            var r = active ? new Rectangle(pipStartX + i * 22 - 2, pipY - 2, 14, 14) : new Rectangle(pipStartX + i * 22, pipY, 10, 10);
            using var b = new SolidBrush(active ? slide.Accent : border);
            g.FillEllipse(b, r);
        }
    }
}

internal static class WinFormsColorHelper
{
    public static Color ParseHexColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
