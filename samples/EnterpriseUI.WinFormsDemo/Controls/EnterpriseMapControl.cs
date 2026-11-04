using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Map;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class EnterpriseMapControl : Control, IThemeableControl
{
    private readonly EnterpriseGeoMap _map = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private GeoMarker? _hoveredMarker;

    public string? ThemeOverride { get; set; }

    public GeoLocation Center
    {
        get => _map.Center;
        set { _map.Center = value; Invalidate(); }
    }

    public int ZoomLevel
    {
        get => _map.ZoomLevel;
        set { _map.ZoomLevel = value; Invalidate(); }
    }

    public List<GeoMarker> Markers => _map.Markers;

    public string? AriaLabel
    {
        get => _map.AriaLabel;
        set => _map.AriaLabel = value;
    }

    public EnterpriseMapControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Size = new Size(600, 380);
        Font = new Font("Segoe UI", 9f);

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
        var marker = FindMarkerAt(e.Location);
        if (marker != _hoveredMarker)
        {
            _hoveredMarker = marker;
            Invalidate();
        }
    }

    private GeoMarker? FindMarkerAt(Point pt)
    {
        foreach (var m in _map.Markers)
        {
            var p = LatLonToScreen(m.Location);
            var r = new RectangleF(p.X - 10, p.Y - 24, 20, 24);
            if (r.Contains(pt)) return m;
        }
        return null;
    }

    private PointF LatLonToScreen(GeoLocation loc)
    {
        // Simple Equirectangular projection centered on map Center
        var cx = Width / 2f;
        var cy = Height / 2f;
        var scale = (float)Math.Pow(1.8, _map.ZoomLevel);

        var x = cx + (float)(loc.Longitude - _map.Center.Longitude) * scale;
        var y = cy - (float)(loc.Latitude - _map.Center.Latitude) * scale;
        return new PointF(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var ocean = Color.FromArgb(235, 242, 250);
        var land = Color.FromArgb(242, 244, 246);

        g.Clear(ocean);

        // Simulated Landmasses
        using (var landBrush = new SolidBrush(land))
        using (var gridPen = new Pen(Color.FromArgb(50, border), 1f) { DashStyle = DashStyle.Dash })
        {
            g.FillEllipse(landBrush, Width * 0.15f, Height * 0.2f, Width * 0.7f, Height * 0.65f);

            // Latitude & Longitude grid
            for (int x = 0; x < Width; x += 60)
            {
                g.DrawLine(gridPen, x, 0, x, Height);
            }
            for (int y = 0; y < Height; y += 60)
            {
                g.DrawLine(gridPen, 0, y, Width, y);
            }
        }

        // Draw Markers
        foreach (var m in _map.Markers)
        {
            var p = LatLonToScreen(m.Location);
            var isHovered = m == _hoveredMarker;

            using var markerBrush = new SolidBrush(ColorTranslator.FromHtml(m.Color));
            using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));

            // Shadow
            g.FillEllipse(shadowBrush, p.X - 5, p.Y + 2, 10, 4);

            // Pin Shape
            var pinPath = new GraphicsPath();
            pinPath.AddEllipse(p.X - 7, p.Y - 22, 14, 14);
            pinPath.AddPolygon(new[] { new PointF(p.X - 6, p.Y - 14), new PointF(p.X + 6, p.Y - 14), new PointF(p.X, p.Y) });
            g.FillPath(markerBrush, pinPath);

            using var centerBrush = new SolidBrush(Color.White);
            g.FillEllipse(centerBrush, p.X - 3, p.Y - 18, 6, 6);

            // Label
            using var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40));
            using var font = new Font(Font, FontStyle.Bold);
            g.DrawString(m.Title, font, textBrush, p.X + 10, p.Y - 20);
        }

        // Active Marker Tooltip
        if (_hoveredMarker != null)
        {
            var p = LatLonToScreen(_hoveredMarker.Location);
            var tip = $"{_hoveredMarker.Title}\n{_hoveredMarker.Description}";
            using var tipFont = new Font("Segoe UI", 9f);
            var sz = g.MeasureString(tip, tipFont);
            var tipRect = new RectangleF(p.X - (sz.Width / 2), p.Y - 50 - sz.Height, sz.Width + 16, sz.Height + 10);

            using var tipBg = new SolidBrush(Color.FromArgb(240, 30, 30, 30));
            using var tipFg = new SolidBrush(Color.White);
            g.FillRoundedRectangle(tipBg, tipRect, 4);
            g.DrawString(tip, tipFont, tipFg, tipRect.X + 8, tipRect.Y + 5);
        }

        // Zoom / Position Overlay
        using (var infoBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
        using (var textBrush = new SolidBrush(Color.Black))
        using (var font = new Font(Font, FontStyle.Regular))
        {
            var status = $"Zoom: {_map.ZoomLevel}x  |  Center: ({_map.Center.Latitude:F2}, {_map.Center.Longitude:F2})  |  Markers: {_map.Markers.Count}";
            g.FillRectangle(infoBrush, 10, Height - 28, 360, 20);
            g.DrawString(status, font, textBrush, 14, Height - 26);
        }
    }
}

