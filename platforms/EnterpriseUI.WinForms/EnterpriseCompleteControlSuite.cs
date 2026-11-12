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


