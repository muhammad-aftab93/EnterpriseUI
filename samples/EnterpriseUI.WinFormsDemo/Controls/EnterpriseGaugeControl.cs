using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Gauges;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class EnterpriseGaugeControl : Control, IThemeableControl
{
    private readonly EnterpriseRadialGauge _gauge = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    public string? ThemeOverride { get; set; }

    public double Value
    {
        get => _gauge.Value;
        set { _gauge.Value = value; Invalidate(); }
    }

    public double Min
    {
        get => _gauge.Min;
        set { _gauge.Min = value; Invalidate(); }
    }

    public double Max
    {
        get => _gauge.Max;
        set { _gauge.Max = value; Invalidate(); }
    }

    public double StartAngle
    {
        get => _gauge.StartAngle;
        set { _gauge.StartAngle = value; Invalidate(); }
    }

    public double EndAngle
    {
        get => _gauge.EndAngle;
        set { _gauge.EndAngle = value; Invalidate(); }
    }

    public string? Unit
    {
        get => _gauge.Unit;
        set { _gauge.Unit = value; Invalidate(); }
    }

    public string? AriaLabel
    {
        get => _gauge.AriaLabel;
        set { _gauge.AriaLabel = value; Invalidate(); }
    }

    public List<GaugeRange> Ranges => _gauge.Ranges;

    public bool IsLinear { get; set; }

    public EnterpriseGaugeControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Size = new Size(260, 260);
        Font = new Font("Segoe UI", 9f);

        _gauge.Min = 0;
        _gauge.Max = 100;
        _gauge.Value = 68;
        _gauge.Ranges.Add(new GaugeRange { From = 0, To = 50, Color = "#107c10" });
        _gauge.Ranges.Add(new GaugeRange { From = 50, To = 80, Color = "#ffb900" });
        _gauge.Ranges.Add(new GaugeRange { From = 80, To = 100, Color = "#d13438" });

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
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(bg);

        if (IsLinear)
        {
            DrawLinear(g, fg, border, primary);
        }
        else
        {
            DrawRadial(g, fg, border, primary);
        }
    }

    private void DrawRadial(Graphics g, Color fg, Color border, Color primary)
    {
        var cx = Width / 2f;
        var cy = Height / 2f;
        var radius = Math.Min(cx, cy) - 24;

        var rect = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);

        // Track Arc
        using (var trackPen = new Pen(Color.FromArgb(50, border), 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawArc(trackPen, rect, 135, 270);
        }

        // Value Arc
        var ratio = Math.Clamp((float)((_gauge.Value - _gauge.Min) / (_gauge.Max - _gauge.Min)), 0f, 1f);
        var sweep = ratio * 270f;
        using (var valuePen = new Pen(primary, 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (sweep > 0)
            {
                g.DrawArc(valuePen, rect, 135, sweep);
            }
        }

        // Center Value Text
        using (var valFont = new Font("Segoe UI", 20f, FontStyle.Bold))
        using (var labelFont = new Font("Segoe UI", 9f))
        using (var textBrush = new SolidBrush(fg))
        {
            var valStr = $"{_gauge.Value:F0}{_gauge.Unit}";
            var sz = g.MeasureString(valStr, valFont);
            g.DrawString(valStr, valFont, textBrush, cx - (sz.Width / 2), cy - (sz.Height / 2) - 4);

            var titleStr = _gauge.AriaLabel ?? "Radial Metric";
            var szT = g.MeasureString(titleStr, labelFont);
            g.DrawString(titleStr, labelFont, textBrush, cx - (szT.Width / 2), cy + 22);
        }
    }

    private void DrawLinear(Graphics g, Color fg, Color border, Color primary)
    {
        var barRect = new RectangleF(24, Height / 2f - 12, Width - 48, 24);

        using (var trackBrush = new SolidBrush(Color.FromArgb(40, border)))
        {
            g.FillRectangle(trackBrush, barRect);
        }

        var ratio = Math.Clamp((float)((_gauge.Value - _gauge.Min) / (_gauge.Max - _gauge.Min)), 0f, 1f);
        var fillW = barRect.Width * ratio;
        using (var valBrush = new SolidBrush(primary))
        {
            g.FillRectangle(valBrush, barRect.X, barRect.Y, fillW, barRect.Height);
        }

        using (var outlinePen = new Pen(border, 1.5f))
        {
            g.DrawRectangle(outlinePen, barRect.X, barRect.Y, barRect.Width, barRect.Height);
        }

        using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
        using (var textBrush = new SolidBrush(fg))
        {
            var txt = $"{_gauge.AriaLabel ?? "Linear Progress"}: {_gauge.Value:F0} / {_gauge.Max:F0}";
            g.DrawString(txt, font, textBrush, 24, barRect.Top - 26);
        }
    }
}

