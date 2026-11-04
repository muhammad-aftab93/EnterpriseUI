using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Charts;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class WinFormsChartControl : Control, IThemeableControl
{
    private readonly Chart _chart = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private int _hoveredIndex = -1;

    public Chart Model => _chart;
    public string? ThemeOverride { get; set; }

    public WinFormsChartControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9f);
        Size = new Size(600, 380);

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var series = _chart.Series.FirstOrDefault(s => s.Visible);
        if (series == null || series.DataPoints.Count == 0) return;

        var plotRect = GetPlotRectangle();
        if (plotRect.Contains(e.Location))
        {
            var count = series.DataPoints.Count;
            var barWidth = plotRect.Width / (float)count;
            var idx = (int)((e.X - plotRect.X) / barWidth);
            idx = Math.Clamp(idx, 0, count - 1);
            if (idx != _hoveredIndex)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
        }
        else if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            Invalidate();
        }
    }

    private RectangleF GetPlotRectangle()
    {
        return new RectangleF(60, 50, Width - 90, Height - 90);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.FromArgb(32, 32, 32));
        var border = WinFormsThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var primary = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));
        var surfaceAlt = WinFormsThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(248, 248, 248));

        g.Clear(bg);

        // Title
        using (var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
        using (var textBrush = new SolidBrush(fg))
        {
            var title = string.IsNullOrEmpty(_chart.Title) ? "Chart Visualization" : _chart.Title;
            g.DrawString(title, titleFont, textBrush, new PointF(18, 14));
        }

        var plotRect = GetPlotRectangle();
        using (var plotBgBrush = new SolidBrush(surfaceAlt))
        using (var gridPen = new Pen(Color.FromArgb(50, border), 1f) { DashStyle = DashStyle.Dash })
        using (var axisPen = new Pen(border, 1.5f))
        {
            g.FillRectangle(plotBgBrush, plotRect);

            // Horizontal Grid Lines
            int gridSteps = 5;
            for (int i = 0; i <= gridSteps; i++)
            {
                var y = plotRect.Bottom - (plotRect.Height / gridSteps * i);
                g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            }

            g.DrawRectangle(axisPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
        }

        var seriesList = _chart.Series.Where(s => s.Visible).ToList();
        if (seriesList.Count == 0) return;

        var firstSeries = seriesList[0];
        if (firstSeries.DataPoints.Count == 0) return;

        double maxVal = seriesList.SelectMany(s => s.DataPoints).Max(p => Math.Max(p.Value, p.High ?? p.Value));
        double minVal = seriesList.SelectMany(s => s.DataPoints).Min(p => Math.Min(p.Value, p.Low ?? p.Value));
        if (minVal > 0) minVal = 0;
        if (maxVal <= minVal) maxVal = minVal + 100;

        Color[] defaultPalette = new[]
        {
            primary,
            Color.FromArgb(16, 124, 65),
            Color.FromArgb(232, 17, 35),
            Color.FromArgb(255, 140, 0),
            Color.FromArgb(136, 23, 152),
            Color.FromArgb(0, 153, 188)
        };

        for (int sIdx = 0; sIdx < seriesList.Count; sIdx++)
        {
            var s = seriesList[sIdx];
            var seriesColor = defaultPalette[sIdx % defaultPalette.Length];
            var pts = s.DataPoints;
            var count = pts.Count;
            var slotWidth = plotRect.Width / count;

            switch (s.Type)
            {
                case SeriesType.Column:
                case SeriesType.Bar:
                    {
                        var barMargin = slotWidth * 0.15f;
                        var barWidth = slotWidth - (barMargin * 2);
                        for (int i = 0; i < count; i++)
                        {
                            var pt = pts[i];
                            var h = (float)((pt.Value - minVal) / (maxVal - minVal) * plotRect.Height);
                            var x = plotRect.Left + (i * slotWidth) + barMargin;
                            var y = plotRect.Bottom - h;
                            var barRect = new RectangleF(x, y, barWidth, h);

                            var isHovered = (i == _hoveredIndex && sIdx == 0);
                            using var brush = new SolidBrush(isHovered ? ControlPaint.Light(seriesColor) : seriesColor);
                            g.FillRectangle(brush, barRect);

                            // Category Label
                            using var labelBrush = new SolidBrush(fg);
                            using var font = new Font(Font, FontStyle.Regular);
                            var cat = pt.Category?.ToString() ?? $"#{i + 1}";
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                            g.DrawString(cat, font, labelBrush, new RectangleF(x - 5, plotRect.Bottom + 6, barWidth + 10, 24), sf);
                        }
                    }
                    break;

                case SeriesType.Line:
                case SeriesType.Spline:
                case SeriesType.Area:
                    {
                        var linePoints = new PointF[count];
                        for (int i = 0; i < count; i++)
                        {
                            var pt = pts[i];
                            var h = (float)((pt.Value - minVal) / (maxVal - minVal) * plotRect.Height);
                            var x = plotRect.Left + (i * slotWidth) + (slotWidth / 2);
                            var y = plotRect.Bottom - h;
                            linePoints[i] = new PointF(x, y);
                        }

                        if (s.Type == SeriesType.Area && count > 1)
                        {
                            var areaPath = new GraphicsPath();
                            areaPath.AddLine(linePoints[0].X, plotRect.Bottom, linePoints[0].X, linePoints[0].Y);
                            areaPath.AddLines(linePoints);
                            areaPath.AddLine(linePoints[^1].X, plotRect.Bottom, linePoints[0].X, plotRect.Bottom);
                            using var areaBrush = new SolidBrush(Color.FromArgb(45, seriesColor));
                            g.FillPath(areaBrush, areaPath);
                        }

                        using var linePen = new Pen(seriesColor, 2.5f) { LineJoin = LineJoin.Round };
                        if (count > 1)
                        {
                            g.DrawLines(linePen, linePoints);
                        }

                        // Markers
                        using var markerBrush = new SolidBrush(seriesColor);
                        using var markerPen = new Pen(bg, 2f);
                        for (int i = 0; i < count; i++)
                        {
                            var p = linePoints[i];
                            g.FillEllipse(markerBrush, p.X - 4, p.Y - 4, 8, 8);
                            g.DrawEllipse(markerPen, p.X - 4, p.Y - 4, 8, 8);
                        }
                    }
                    break;

                case SeriesType.Pie:
                case SeriesType.Donut:
                    {
                        var pieRect = new RectangleF(plotRect.Left + (plotRect.Width - plotRect.Height) / 2, plotRect.Top + 10, plotRect.Height - 20, plotRect.Height - 20);
                        double total = pts.Sum(p => p.Value);
                        if (total <= 0) total = 1;
                        float startAngle = -90f;

                        for (int i = 0; i < count; i++)
                        {
                            var sweep = (float)(pts[i].Value / total * 360f);
                            var sliceColor = defaultPalette[i % defaultPalette.Length];
                            using var sliceBrush = new SolidBrush(sliceColor);
                            g.FillPie(sliceBrush, pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height, startAngle, sweep);
                            startAngle += sweep;
                        }

                        if (s.Type == SeriesType.Donut)
                        {
                            var innerMargin = pieRect.Width * 0.25f;
                            var innerRect = new RectangleF(pieRect.X + innerMargin, pieRect.Y + innerMargin, pieRect.Width - (innerMargin * 2), pieRect.Height - (innerMargin * 2));
                            using var innerBrush = new SolidBrush(bg);
                            g.FillEllipse(innerBrush, innerRect);
                        }
                    }
                    break;

                case SeriesType.Candlestick:
                    {
                        var slot = slotWidth * 0.8f;
                        for (int i = 0; i < count; i++)
                        {
                            var pt = pts[i];
                            var open = pt.Open ?? pt.Value;
                            var close = pt.Close ?? pt.Value;
                            var high = pt.High ?? Math.Max(open, close);
                            var low = pt.Low ?? Math.Min(open, close);

                            var isBullish = close >= open;
                            var candleColor = isBullish ? Color.FromArgb(16, 124, 65) : Color.FromArgb(232, 17, 35);
                            using var candlePen = new Pen(candleColor, 1.5f);
                            using var candleBrush = new SolidBrush(candleColor);

                            var xCenter = plotRect.Left + (i * slotWidth) + (slotWidth / 2);
                            var yHigh = (float)(plotRect.Bottom - ((high - minVal) / (maxVal - minVal) * plotRect.Height));
                            var yLow = (float)(plotRect.Bottom - ((low - minVal) / (maxVal - minVal) * plotRect.Height));
                            var yOpen = (float)(plotRect.Bottom - ((open - minVal) / (maxVal - minVal) * plotRect.Height));
                            var yClose = (float)(plotRect.Bottom - ((close - minVal) / (maxVal - minVal) * plotRect.Height));

                            // Wick
                            g.DrawLine(candlePen, xCenter, yHigh, xCenter, yLow);

                            // Body
                            var top = Math.Min(yOpen, yClose);
                            var bot = Math.Max(yOpen, yClose);
                            var candleW = Math.Max(4f, slot * 0.6f);
                            g.FillRectangle(candleBrush, xCenter - (candleW / 2), top, candleW, Math.Max(2f, bot - top));
                        }
                    }
                    break;
            }
        }

        // Tooltip for hovered item
        if (_hoveredIndex >= 0 && _hoveredIndex < firstSeries.DataPoints.Count)
        {
            var pt = firstSeries.DataPoints[_hoveredIndex];
            var info = $"{pt.Category}: {pt.Value:N1}";
            using var tipFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            var sz = g.MeasureString(info, tipFont);
            var tipRect = new RectangleF(plotRect.Right - sz.Width - 16, plotRect.Top + 10, sz.Width + 12, sz.Height + 8);

            using var tipBg = new SolidBrush(Color.FromArgb(230, fg));
            using var tipFg = new SolidBrush(bg);
            g.FillRectangle(tipBg, tipRect);
            g.DrawString(info, tipFont, tipFg, tipRect.X + 6, tipRect.Y + 4);
        }
    }
}

