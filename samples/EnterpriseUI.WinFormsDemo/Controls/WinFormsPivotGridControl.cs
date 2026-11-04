using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class WinFormsPivotGridControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;

    public string? ThemeOverride { get; set; }

    // Mock Pivot Matrix: Rows (Region), Cols (Year), Values (Revenue)
    private readonly string[] _rowDimensions = new[] { "North America", "Europe", "Asia-Pacific", "Latin America" };
    private readonly string[] _colDimensions = new[] { "Q1 2026", "Q2 2026", "Q3 2026", "Q4 2026" };
    private readonly decimal[,] _values = new decimal[,]
    {
        { 1240000m, 1450000m, 1680000m, 1920000m },
        { 890000m, 940000m, 1020000m, 1150000m },
        { 1560000m, 1890000m, 2100000m, 2450000m },
        { 420000m, 460000m, 510000m, 580000m }
    };

    public WinFormsPivotGridControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Size = new Size(650, 360);
        Font = new Font("Segoe UI", 9f);

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
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
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var bg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = WinFormsThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var surfaceAlt = WinFormsThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(245, 245, 245));
        var primary = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(bg);

        var rowHeaderWidth = 140;
        var colWidth = (Width - rowHeaderWidth - 120) / (float)_colDimensions.Length;
        var totalColWidth = 110;
        var headerHeight = 32;
        var rowHeight = 28;

        // Top Filter Bar
        using (var filterBrush = new SolidBrush(Color.FromArgb(238, 242, 246)))
        using (var filterPen = new Pen(border))
        using (var font = new Font(Font, FontStyle.Italic))
        using (var textBrush = new SolidBrush(Color.FromArgb(70, 70, 70)))
        {
            g.FillRectangle(filterBrush, 0, 0, Width, 28);
            g.DrawLine(filterPen, 0, 28, Width, 28);
            g.DrawString("Fields: [Row: Region]  [Column: Quarter]  [Data: Sum(Revenue)]  [Filter: All Products]", font, textBrush, 10, 6);
        }

        var startY = 28;

        // Headers
        using (var headerBrush = new SolidBrush(surfaceAlt))
        using (var borderPen = new Pen(border))
        using (var textBrush = new SolidBrush(fg))
        using (var boldFont = new Font(Font, FontStyle.Bold))
        {
            g.FillRectangle(headerBrush, 0, startY, Width, headerHeight);
            g.DrawLine(borderPen, 0, startY + headerHeight, Width, startY + headerHeight);

            // Dimension Title
            g.DrawString("Region \\ Quarter", boldFont, textBrush, 10, startY + 8);
            g.DrawLine(borderPen, rowHeaderWidth, startY, rowHeaderWidth, Height);

            // Columns
            for (int c = 0; c < _colDimensions.Length; c++)
            {
                var x = rowHeaderWidth + (c * colWidth);
                g.DrawLine(borderPen, x, startY, x, Height);
                g.DrawString(_colDimensions[c], boldFont, textBrush, x + 8, startY + 8);
            }

            // Total Col
            var totX = rowHeaderWidth + (_colDimensions.Length * colWidth);
            g.DrawLine(borderPen, totX, startY, totX, Height);
            g.DrawString("Grand Total", boldFont, textBrush, totX + 8, startY + 8);
        }

        // Data Rows
        var curY = startY + headerHeight;
        decimal[] colTotals = new decimal[_colDimensions.Length];
        decimal grandTotal = 0;

        using (var borderPen = new Pen(border))
        using (var textBrush = new SolidBrush(fg))
        using (var boldFont = new Font(Font, FontStyle.Bold))
        {
            for (int r = 0; r < _rowDimensions.Length; r++)
            {
                g.DrawLine(borderPen, 0, curY + rowHeight, Width, curY + rowHeight);

                // Row Header
                g.DrawString(_rowDimensions[r], boldFont, textBrush, 10, curY + 6);

                decimal rowTotal = 0;
                for (int c = 0; c < _colDimensions.Length; c++)
                {
                    var val = _values[r, c];
                    rowTotal += val;
                    colTotals[c] += val;

                    var x = rowHeaderWidth + (c * colWidth);
                    g.DrawString(val.ToString("C0"), Font, textBrush, x + 8, curY + 6);
                }

                grandTotal += rowTotal;
                var totX = rowHeaderWidth + (_colDimensions.Length * colWidth);
                g.DrawString(rowTotal.ToString("C0"), boldFont, textBrush, totX + 8, curY + 6);

                curY += rowHeight;
            }

            // Grand Total Row
            using (var totBrush = new SolidBrush(Color.FromArgb(240, 244, 250)))
            {
                g.FillRectangle(totBrush, 0, curY, Width, rowHeight);
                g.DrawLine(borderPen, 0, curY + rowHeight, Width, curY + rowHeight);
                g.DrawString("Grand Total", boldFont, textBrush, 10, curY + 6);

                for (int c = 0; c < _colDimensions.Length; c++)
                {
                    var x = rowHeaderWidth + (c * colWidth);
                    g.DrawString(colTotals[c].ToString("C0"), boldFont, textBrush, x + 8, curY + 6);
                }

                var totX = rowHeaderWidth + (_colDimensions.Length * colWidth);
                using (var primaryBrush = new SolidBrush(primary))
                {
                    g.DrawString(grandTotal.ToString("C0"), boldFont, primaryBrush, totX + 8, curY + 6);
                }
            }
        }
    }
}

