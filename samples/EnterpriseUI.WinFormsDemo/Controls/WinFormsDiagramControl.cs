using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Diagrams;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class WinFormsDiagramControl : Control, IThemeableControl
{
    private readonly Diagram _diagram = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private DiagramNode? _selectedNode;
    private Point _dragStart;
    private bool _isDragging;

    public Diagram Model => _diagram;
    public string? ThemeOverride { get; set; }

    public WinFormsDiagramControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        DoubleBuffered = true;
        Size = new Size(600, 420);
        Font = new Font("Segoe UI", 9f);

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _selectedNode = _diagram.Nodes.LastOrDefault(n =>
                new RectangleF((float)n.X, (float)n.Y, (float)n.Width, (float)n.Height).Contains(e.Location));

            if (_selectedNode != null)
            {
                _isDragging = true;
                _dragStart = new Point(e.X - (int)_selectedNode.X, e.Y - (int)_selectedNode.Y);
                Invalidate();
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging && _selectedNode != null)
        {
            _selectedNode.X = Math.Max(10, e.X - _dragStart.X);
            _selectedNode.Y = Math.Max(10, e.Y - _dragStart.Y);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = WinFormsThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var primary = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(bg);

        // Draw Subtle Grid Dots
        using (var dotBrush = new SolidBrush(Color.FromArgb(40, border)))
        {
            for (int x = 20; x < Width; x += 25)
            {
                for (int y = 20; y < Height; y += 25)
                {
                    g.FillRectangle(dotBrush, x, y, 2, 2);
                }
            }
        }

        // Draw Connectors
        foreach (var c in _diagram.Connectors)
        {
            var src = _diagram.Nodes.FirstOrDefault(n => n.Id == c.SourceNodeId);
            var tgt = _diagram.Nodes.FirstOrDefault(n => n.Id == c.TargetNodeId);
            if (src != null && tgt != null)
            {
                var p1 = new PointF((float)(src.X + src.Width), (float)(src.Y + src.Height / 2));
                var p2 = new PointF((float)tgt.X, (float)(tgt.Y + tgt.Height / 2));

                using var connPen = new Pen(primary, 2f) { CustomEndCap = new AdjustableArrowCap(4, 5) };
                if (c.IsOrthogonal)
                {
                    var midX = (p1.X + p2.X) / 2;
                    g.DrawLines(connPen, new[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 });
                }
                else
                {
                    g.DrawLine(connPen, p1, p2);
                }

                if (!string.IsNullOrEmpty(c.Label))
                {
                    using var lblBrush = new SolidBrush(fg);
                    using var font = new Font(Font, FontStyle.Italic);
                    var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2 - 14);
                    g.DrawString(c.Label, font, lblBrush, mid);
                }
            }
        }

        // Draw Nodes
        foreach (var node in _diagram.Nodes)
        {
            var r = new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height);
            var isSel = node == _selectedNode;

            using var nodeBrush = new SolidBrush(Color.FromArgb(245, 248, 252));
            using var borderPen = new Pen(isSel ? primary : Color.FromArgb(0, 100, 180), isSel ? 2.5f : 1.5f);
            using var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));

            // Shadow
            g.FillRoundedRectangle(shadowBrush, new RectangleF(r.X + 3, r.Y + 3, r.Width, r.Height), 8);

            // Shape
            g.FillRoundedRectangle(nodeBrush, r, 8);
            g.DrawRoundedRectangle(borderPen, r, 8);

            // Text
            using var textBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
            using var textFont = new Font(Font, FontStyle.Bold);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(node.Text, textFont, textBrush, r, sf);
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = CreateRoundedRectanglePath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF rect, float radius)
    {
        using var path = CreateRoundedRectanglePath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

