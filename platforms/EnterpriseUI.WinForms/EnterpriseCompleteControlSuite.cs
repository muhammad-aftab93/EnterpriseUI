using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Charts;
using EnterpriseUI.Editors;
using EnterpriseUI.Gauges;
using EnterpriseUI.Grids;
using EnterpriseUI.Navigation;
using EnterpriseUI.Notifications;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

/// <summary>
/// Clean-room GDI+ Syntax Editor Control.
/// Renders line numbers gutter, code folding markers, and colorized C# lexical tokens.
/// </summary>
public class EnterpriseSyntaxEditorControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly EnterpriseSyntaxEditor _syntaxEditor = new();
    public string? ThemeOverride { get; set; }

    public string Language { get => _syntaxEditor.Language; set { _syntaxEditor.Language = value; Invalidate(); } }
    public bool ShowLineNumbers { get => _syntaxEditor.ShowLineNumbers; set { _syntaxEditor.ShowLineNumbers = value; Invalidate(); } }
    public bool EnableCodeFolding { get => _syntaxEditor.EnableCodeFolding; set { _syntaxEditor.EnableCodeFolding = value; Invalidate(); } }
    public string DocumentText { get => _syntaxEditor.DocumentText; set { _syntaxEditor.DocumentText = value; Invalidate(); } }
    public IReadOnlyList<EnterpriseSyntaxEditor.SyntaxToken> Tokenize() => _syntaxEditor.Tokenize();

    public EnterpriseSyntaxEditorControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Font = new Font("Consolas", 9.5f);
        Size = new Size(740, 260);
        _syntaxEditor.DocumentText =
@"// EnterpriseUI EnterpriseSyntaxEditor — Lexical Tokenizer & Folding Engine
using EnterpriseUI.Grids;
using EnterpriseUI.Theming;

namespace EnterpriseUI.Sample
{
    public sealed class HighThroughputController
    {
        public async Task InitializeGridAsync()
        {
            var grid = new EnterpriseVirtualGrid { RowCount = 1000000, ColumnCount = 24 };
            var theme = ThemeManager.Instance.CurrentTheme;
            // Zero-allocation viewport projection active
            await grid.InitializeAsync();
        }
    }
}";
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var gutterBg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(240, 242, 246));
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);

        g.Clear(bg);
        int gutterW = 48;
        using var gutterBrush = new SolidBrush(gutterBg);
        using var borderPen = new Pen(border);
        using var lineNumBrush = new SolidBrush(Color.FromArgb(130, fg));
        using var kwBrush = new SolidBrush(Color.FromArgb(0, 92, 197));
        using var commentBrush = new SolidBrush(Color.FromArgb(34, 134, 58));
        using var defaultBrush = new SolidBrush(fg);

        g.FillRectangle(gutterBrush, 0, 0, gutterW, Height);
        g.DrawLine(borderPen, gutterW, 0, gutterW, Height);

        var lines = _syntaxEditor.DocumentText.Replace("\r\n", "\n").Split('\n');
        int lineH = 20;
        for (int i = 0; i < lines.Length; i++)
        {
            int y = 8 + i * lineH;
            g.DrawString($"{i + 1,2}", Font, lineNumBrush, 6, y);
            string line = lines[i];
            if (line.Contains('{'))
            {
                g.DrawRectangle(borderPen, gutterW - 14, y + 4, 9, 9);
                g.DrawLine(borderPen, gutterW - 12, y + 8, gutterW - 7, y + 8);
            }

            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                g.DrawString(line, Font, commentBrush, gutterW + 10, y);
            }
            else
            {
                var brush = line.Contains("using ") || line.Contains("namespace ") || line.Contains("public ") || line.Contains("var ") || line.Contains("await ")
                    ? kwBrush : defaultBrush;
                g.DrawString(line, Font, brush, gutterW + 10, y);
            }
        }
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// Clean-room GDI+ Stephen Few EnterpriseBulletGraph Control.
/// </summary>
public class EnterpriseBulletGraphControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly EnterpriseBulletGraph _bulletGraph = new();
    public string Title { get; set; } = "Q4 Enterprise ARR Target ($M)";
    public string? ThemeOverride { get; set; }

    public double Minimum { get => _bulletGraph.Minimum; set { _bulletGraph.Minimum = value; Invalidate(); } }
    public double Maximum { get => _bulletGraph.Maximum; set { _bulletGraph.Maximum = value; Invalidate(); } }
    public double FeaturedMeasure { get => _bulletGraph.FeaturedMeasure; set { _bulletGraph.FeaturedMeasure = value; Invalidate(); } }
    public double ComparativeMeasure { get => _bulletGraph.ComparativeMeasure; set { _bulletGraph.ComparativeMeasure = value; Invalidate(); } }
    public List<GaugeRange> QualitativeRanges => _bulletGraph.QualitativeRanges;
    public bool IsAchievingTarget => _bulletGraph.IsAchievingTarget;

    public EnterpriseBulletGraphControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(680, 74);
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);

        g.Clear(bg);
        using var fgBrush = new SolidBrush(fg);
        using var boldFont = new Font(Font, FontStyle.Bold);
        using var borderPen = new Pen(border);

        int labelW = 210;
        g.DrawString(Title, boldFont, fgBrush, 10, 14);
        g.DrawString($"Actual: {_bulletGraph.FeaturedMeasure:F1}% | Target: {_bulletGraph.ComparativeMeasure:F1}%", Font, fgBrush, 10, 36);

        int barX = labelW;
        int barW = Math.Max(100, Width - labelW - 24);
        int barY = 16;
        int barH = 36;

        Color[] qualColors = { Color.FromArgb(220, 224, 230), Color.FromArgb(190, 198, 210), Color.FromArgb(160, 172, 188) };
        for (int i = 0; i < _bulletGraph.QualitativeRanges.Count; i++)
        {
            var r = _bulletGraph.QualitativeRanges[i];
            float rx = barX + (float)(r.From / _bulletGraph.Maximum) * barW;
            float rw = (float)((r.To - r.From) / _bulletGraph.Maximum) * barW;
            using var qBrush = new SolidBrush(qualColors[i % qualColors.Length]);
            g.FillRectangle(qBrush, rx, barY, rw, barH);
        }

        float featW = (float)(_bulletGraph.FeaturedMeasure / _bulletGraph.Maximum) * barW;
        using var featBrush = new SolidBrush(primary);
        g.FillRectangle(featBrush, barX, barY + 10, featW, barH - 20);

        float targetX = barX + (float)(_bulletGraph.ComparativeMeasure / _bulletGraph.Maximum) * barW;
        using var targetPen = new Pen(Color.FromArgb(200, 30, 30), 3f);
        g.DrawLine(targetPen, targetX, barY + 4, targetX, barY + barH - 4);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// Clean-room GDI+ 3D Elliptical EnterpriseCarousel Control.
/// </summary>
public class EnterpriseCarouselControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly EnterpriseCarousel _carousel = new();
    public string? ThemeOverride { get; set; }
    public event Action<string>? SelectedItemChanged;

    public List<object> Items => _carousel.Items;
    public int SelectedIndex { get => _carousel.SelectedIndex; set { _carousel.SelectedIndex = value; Invalidate(); } }
    public void MoveNext() { _carousel.MoveNext(); Invalidate(); }
    public void MovePrevious() { _carousel.MovePrevious(); Invalidate(); }
    public IReadOnlyList<EnterpriseCarousel.CarouselItemProjection> ComputeProjections() => _carousel.ComputeProjections();

    public EnterpriseCarouselControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(720, 240);

        _carousel.Items.AddRange(new object[]
        {
            "1. Executive BI & Analytics",
            "2. 1M-Row DataGrid Engine",
            "3. Banded Report Designer",
            "4. Real-Time OHLC Charts",
            "5. Offline AI Copilot AST",
            "6. WCAG 2.2 Accessibility"
        });

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.X > Width / 2) _carousel.MoveNext();
        else _carousel.MovePrevious();
        SelectedItemChanged?.Invoke(_carousel.Items[_carousel.SelectedIndex]?.ToString() ?? string.Empty);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Background, Color.FromArgb(245, 247, 250));
        var surface = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));

        g.Clear(bg);
        var projections = _carousel.ComputeProjections();
        float cx = Width / 2f;
        float cy = Height / 2f + 12;

        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        foreach (var p in projections)
        {
            float cardW = (float)(190 * p.Scale);
            float cardH = (float)(110 * p.Scale);
            float x = cx + (float)(p.NormalizedX * Width * 0.85) - cardW / 2f;
            float y = cy + (float)(p.NormalizedY * Height) - cardH / 2f;
            var rect = new RectangleF(x, y, cardW, cardH);

            int alpha = Math.Clamp((int)(p.Opacity * 255), 60, 255);
            using var cardBrush = new SolidBrush(Color.FromArgb(alpha, surface));
            using var borderPen = new Pen(p.Index == _carousel.SelectedIndex ? primary : Color.FromArgb(alpha, Color.Gray), p.Index == _carousel.SelectedIndex ? 2.5f : 1f);
            using var textBrush = new SolidBrush(Color.FromArgb(alpha, fg));
            using var cardFont = new Font(Font.FontFamily, Math.Max(7.5f, (float)(10f * p.Scale)), FontStyle.Bold);

            g.FillRectangle(cardBrush, rect);
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawString(p.Item.ToString(), cardFont, textBrush, rect, sf);
        }
    }
}

/// <summary>
/// GDI+ interactive star rating and waiting indicators.
/// </summary>
public class EnterpriseRatingAndWaitingControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private int _marqueeOffset;
    private readonly EnterpriseRatingControl _rating = new() { MaxStars = 5, Value = 4 };
    private readonly EnterpriseWaitingBar _waiting = new() { IsWaiting = true };
    public string? ThemeOverride { get; set; }
    public event Action<double>? RatingChanged;

    public int MaxStars { get => _rating.MaxStars; set { _rating.MaxStars = value; Invalidate(); } }
    public double Value { get => _rating.Value; set { _rating.Value = value; Invalidate(); } }
    public bool AllowHalfStars { get => _rating.AllowHalfStars; set => _rating.AllowHalfStars = value; }
    public WaitingBarStyle WaitingStyle { get => _waiting.WaitingStyle; set { _waiting.WaitingStyle = value; Invalidate(); } }
    public bool IsWaiting { get => _waiting.IsWaiting; set { _waiting.IsWaiting = value; Invalidate(); } }
    public string WaitingText { get => _waiting.WaitingText; set { _waiting.WaitingText = value; Invalidate(); } }
    public void StartWaiting() { _waiting.StartWaiting(); Invalidate(); }
    public void StopWaiting() { _waiting.StopWaiting(); Invalidate(); }

    public EnterpriseRatingAndWaitingControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(680, 90);
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void StepMarquee()
    {
        _marqueeOffset = (_marqueeOffset + 18) % Math.Max(100, Width - 260);
        Invalidate();
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Y < 45 && e.X >= 130 && e.X <= 300)
        {
            int star = Math.Clamp((e.X - 130) / 32 + 1, 1, _rating.MaxStars);
            _rating.Value = star;
            RatingChanged?.Invoke(star);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);

        g.Clear(bg);
        using var fgBrush = new SolidBrush(fg);
        using var goldBrush = new SolidBrush(Color.FromArgb(245, 166, 35));
        using var mutedBrush = new SolidBrush(Color.FromArgb(200, 205, 212));
        using var starFont = new Font("Segoe UI Symbol", 15f, FontStyle.Bold);
        using var boldFont = new Font(Font, FontStyle.Bold);

        g.DrawString("Star Rating:", boldFont, fgBrush, 10, 12);
        for (int i = 0; i < _rating.MaxStars; i++)
        {
            g.DrawString("★", starFont, i < _rating.Value ? goldBrush : mutedBrush, 130 + i * 32, 5);
        }
        g.DrawString($"({_rating.Value:F1} / {_rating.MaxStars}.0 — Click stars to rate)", Font, fgBrush, 305, 12);

        g.DrawString("EnterpriseWaitingBar:", boldFont, fgBrush, 10, 52);
        var trackRect = new Rectangle(130, 50, Math.Max(120, Width - 160), 22);
        using var borderPen = new Pen(border);
        using var primaryBrush = new SolidBrush(primary);
        g.DrawRectangle(borderPen, trackRect);
        int segW = 90;
        int segX = trackRect.X + (_marqueeOffset % Math.Max(1, trackRect.Width - segW));
        g.FillRectangle(primaryBrush, segX, trackRect.Y + 2, segW, trackRect.Height - 3);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

