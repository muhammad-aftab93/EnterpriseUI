using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public sealed class GanttTaskItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int DurationDays { get; set; }
    public int ProgressPercent { get; set; }
    public string? DependsOnId { get; set; }
    public bool IsCriticalPath { get; set; }
}

/// <summary>
/// Clean-room GDI+ double-buffered Gantt View control.
/// Renders task hierarchy split, timeline header, progress bars, dependency arrows, and critical path.
/// </summary>
public class EnterpriseGanttControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    public string? ThemeOverride { get; set; }
    public bool HighlightCriticalPath { get; set; } = true;
    public int TotalTimelineDays { get; set; } = 30;
    public List<GanttTaskItem> Tasks { get; } = new();

    public EnterpriseGanttControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(780, 320);

        Tasks.AddRange(new[]
        {
            new GanttTaskItem { Id = "T1", Title = "1. Architecture & Token Spec", StartDay = 1, DurationDays = 5, ProgressPercent = 100, IsCriticalPath = true },
            new GanttTaskItem { Id = "T2", Title = "2. Virtualized Grid Engine", StartDay = 6, DurationDays = 8, ProgressPercent = 90, DependsOnId = "T1", IsCriticalPath = true },
            new GanttTaskItem { Id = "T3", Title = "3. Theme & Palette Compiler", StartDay = 5, DurationDays = 6, ProgressPercent = 85, DependsOnId = "T1", IsCriticalPath = false },
            new GanttTaskItem { Id = "T4", Title = "4. Charting & GIS Layers", StartDay = 11, DurationDays = 7, ProgressPercent = 70, DependsOnId = "T3", IsCriticalPath = false },
            new GanttTaskItem { Id = "T5", Title = "5. Live Code Generator Studio", StartDay = 14, DurationDays = 8, ProgressPercent = 80, DependsOnId = "T2", IsCriticalPath = true },
            new GanttTaskItem { Id = "T6", Title = "6. WCAG 2.2 AA & Perf QA Audit", StartDay = 22, DurationDays = 6, ProgressPercent = 65, DependsOnId = "T5", IsCriticalPath = true }
        });

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
        var headerBg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(245, 247, 250));
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));
        var danger = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Danger, Color.FromArgb(210, 50, 50));

        g.Clear(bg);

        int splitX = 240;
        int headerH = 34;
        int rowH = 38;
        int timelineW = Math.Max(100, Width - splitX - 16);
        float dayWidth = (float)timelineW / Math.Max(1, TotalTimelineDays);

        using var headerBrush = new SolidBrush(headerBg);
        using var borderPen = new Pen(border);
        using var textBrush = new SolidBrush(fg);
        using var boldFont = new Font(Font, FontStyle.Bold);

        g.FillRectangle(headerBrush, 0, 0, Width, headerH);
        g.DrawLine(borderPen, 0, headerH, Width, headerH);
        g.DrawLine(borderPen, splitX, 0, splitX, Height);
        g.DrawString("WBS Task Name", boldFont, textBrush, 10, 9);

        for (int d = 0; d <= TotalTimelineDays; d += 5)
        {
            float dx = splitX + d * dayWidth;
            g.DrawLine(borderPen, dx, 0, dx, Height);
            if (d < TotalTimelineDays)
            {
                g.DrawString($"Day {d + 1}", Font, textBrush, dx + 4, 9);
            }
        }

        var barRects = new Dictionary<string, RectangleF>();
        for (int i = 0; i < Tasks.Count; i++)
        {
            var t = Tasks[i];
            int y = headerH + i * rowH;
            g.DrawLine(borderPen, 0, y + rowH, Width, y + rowH);
            g.DrawString($"{t.Title} ({t.ProgressPercent}%)", Font, textBrush, new RectangleF(10, y + 10, splitX - 16, rowH - 12));

            float bx = splitX + (t.StartDay - 1) * dayWidth;
            float bw = Math.Max(14, t.DurationDays * dayWidth);
            var barRect = new RectangleF(bx, y + 8, bw, rowH - 16);
            barRects[t.Id] = barRect;

            var barColor = HighlightCriticalPath && t.IsCriticalPath ? danger : primary;
            using var trackBrush = new SolidBrush(Color.FromArgb(55, barColor));
            using var fillBrush = new SolidBrush(barColor);

            g.FillRectangle(trackBrush, barRect);
            g.FillRectangle(fillBrush, barRect.X, barRect.Y, barRect.Width * (t.ProgressPercent / 100f), barRect.Height);
            using var barPen = new Pen(barColor, 1.2f);
            g.DrawRectangle(barPen, barRect.X, barRect.Y, barRect.Width, barRect.Height);
        }

        // Draw dependency connectors
        using var depPen = new Pen(fg, 1.5f) { CustomEndCap = new AdjustableArrowCap(4, 4) };
        foreach (var t in Tasks)
        {
            if (!string.IsNullOrEmpty(t.DependsOnId) && barRects.TryGetValue(t.DependsOnId, out var src) && barRects.TryGetValue(t.Id, out var dst))
            {
                float midY = src.Y + src.Height / 2f;
                float dstY = dst.Y + dst.Height / 2f;
                g.DrawLines(depPen, new[]
                {
                    new PointF(src.Right, midY),
                    new PointF(src.Right + 8, midY),
                    new PointF(src.Right + 8, dstY),
                    new PointF(dst.Left, dstY)
                });
            }
        }

        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

public enum BarcodeSymbology
{
    QrCode2D,
    Code128Linear,
    DataMatrix2D
}

/// <summary>
/// Clean-room GDI+ Barcode and QR Matrix Renderer.
/// </summary>
public class EnterpriseBarcodeControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private string _payload = "ENTERPRISE-UI-2026-PRO";
    private BarcodeSymbology _symbology = BarcodeSymbology.QrCode2D;

    public string? ThemeOverride { get; set; }

    public string Payload
    {
        get => _payload;
        set { _payload = value ?? string.Empty; Invalidate(); }
    }

    public BarcodeSymbology Symbology
    {
        get => _symbology;
        set { _symbology = value; Invalidate(); }
    }

    public EnterpriseBarcodeControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(320, 220);
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
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);

        g.Clear(bg);
        using var fgBrush = new SolidBrush(fg);
        using var borderPen = new Pen(border);

        int hash = StringComparer.Ordinal.GetHashCode(_payload);
        if (_symbology == BarcodeSymbology.Code128Linear)
        {
            int barX = 24;
            int barTop = 24;
            int barH = Math.Max(40, Height - 70);
            for (int i = 0; i < Math.Max(16, _payload.Length * 3); i++)
            {
                int ch = _payload.Length > 0 ? _payload[i % _payload.Length] : 65;
                int w = ((ch + i * 7) % 3) + 1;
                if ((ch + i) % 2 == 0 && barX + w < Width - 24)
                {
                    g.FillRectangle(fgBrush, barX, barTop, w * 2, barH);
                }
                barX += w * 2 + 2;
                if (barX >= Width - 24) break;
            }
        }
        else
        {
            int gridN = _symbology == BarcodeSymbology.QrCode2D ? 21 : 18;
            int boxSize = Math.Min(Width - 40, Height - 60);
            int cell = Math.Max(4, boxSize / gridN);
            int startX = (Width - cell * gridN) / 2;
            int startY = 16;

            for (int r = 0; r < gridN; r++)
            {
                for (int c = 0; c < gridN; c++)
                {
                    bool finder = _symbology == BarcodeSymbology.QrCode2D &&
                        ((r < 7 && c < 7) || (r < 7 && c >= gridN - 7) || (r >= gridN - 7 && c < 7));
                    bool fill;
                    if (finder)
                    {
                        int lr = r >= gridN - 7 ? r - (gridN - 7) : r;
                        int lc = c >= gridN - 7 ? c - (gridN - 7) : c;
                        fill = lr == 0 || lr == 6 || lc == 0 || lc == 6 || (lr >= 2 && lr <= 4 && lc >= 2 && lc <= 4);
                    }
                    else
                    {
                        int ch = _payload.Length > 0 ? _payload[(r * gridN + c) % _payload.Length] : 42;
                        fill = ((ch ^ (r * 31 + c * 17 + hash)) & 1) == 1;
                    }

                    if (fill)
                    {
                        g.FillRectangle(fgBrush, startX + c * cell, startY + r * cell, cell, cell);
                    }
                }
            }
        }

        using var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString($"{_symbology}: {_payload}", Font, fgBrush, new RectangleF(0, Height - 28, Width, 22), sf);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// Clean-room GDI+ Interactive Kanban TaskBoard and EnterpriseCardView Control.
/// </summary>
public class EnterpriseKanbanBoardControl : Control, IThemeableControl
{
    public sealed class KanbanCard
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Lane { get; set; } = "Backlog";
        public string Owner { get; set; } = string.Empty;
        public string Priority { get; set; } = "High";
        public Rectangle Bounds { get; set; }
    }

    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly string[] _lanes = { "Backlog", "In Progress", "Code Review", "Production" };
    public List<KanbanCard> Cards { get; } = new();
    public string? ThemeOverride { get; set; }
    public event Action<KanbanCard>? CardAdvanced;

    public EnterpriseKanbanBoardControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(780, 280);

        Cards.AddRange(new[]
        {
            new KanbanCard { Id = "K-101", Title = "1M-Row Viewport Indexing", Lane = "Production", Owner = "Core Team", Priority = "Critical" },
            new KanbanCard { Id = "K-102", Title = "Multi-Format Live CodeGen", Lane = "Production", Owner = "DX Team", Priority = "High" },
            new KanbanCard { Id = "K-103", Title = "WCAG 2.2 AAA Contrast Audit", Lane = "Code Review", Owner = "A11y Lead", Priority = "High" },
            new KanbanCard { Id = "K-104", Title = "Banded Grid Header Export", Lane = "In Progress", Owner = "Grid Team", Priority = "Normal" },
            new KanbanCard { Id = "K-105", Title = "Offline ONNX Prompt Router", Lane = "Backlog", Owner = "AI Team", Priority = "High" }
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
        foreach (var card in Cards)
        {
            if (card.Bounds.Contains(e.Location))
            {
                int idx = Array.IndexOf(_lanes, card.Lane);
                card.Lane = _lanes[(idx + 1) % _lanes.Length];
                CardAdvanced?.Invoke(card);
                Invalidate();
                break;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Background, Color.FromArgb(245, 247, 250));
        var surface = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));

        g.Clear(bg);
        int laneW = Math.Max(140, (Width - 20) / _lanes.Length);
        using var headerFont = new Font(Font, FontStyle.Bold);
        using var fgBrush = new SolidBrush(fg);
        using var surfaceBrush = new SolidBrush(surface);
        using var borderPen = new Pen(border);
        using var accentPen = new Pen(primary, 2f);

        for (int i = 0; i < _lanes.Length; i++)
        {
            string lane = _lanes[i];
            int lx = 8 + i * laneW;
            var laneRect = new Rectangle(lx, 8, laneW - 8, Height - 16);
            g.DrawRectangle(borderPen, laneRect);
            g.DrawString($"{lane} ({Cards.Count(c => c.Lane == lane)})", headerFont, fgBrush, lx + 10, 16);

            int cy = 44;
            foreach (var card in Cards.Where(c => c.Lane == lane))
            {
                var cardRect = new Rectangle(lx + 8, cy, laneRect.Width - 16, 62);
                card.Bounds = cardRect;
                g.FillRectangle(surfaceBrush, cardRect);
                g.DrawRectangle(borderPen, cardRect);
                g.DrawLine(accentPen, cardRect.X, cardRect.Y, cardRect.X, cardRect.Bottom);
                g.DrawString($"[{card.Id}] {card.Title}", headerFont, fgBrush, new RectangleF(cardRect.X + 8, cardRect.Y + 6, cardRect.Width - 12, 22));
                g.DrawString($"Owner: {card.Owner} • {card.Priority} (Click to advance)", Font, fgBrush, new RectangleF(cardRect.X + 8, cardRect.Y + 30, cardRect.Width - 12, 24));
                cy += 70;
            }
        }
    }
}

/// <summary>
/// Clean-room GDI+ Hierarchical EnterpriseTreeMap Control.
/// Renders nested proportional rectangles with heat-map color encoding and interactive node selection.
/// </summary>
public class EnterpriseTreeMapControl : Control, IThemeableControl
{
    public sealed class TreeMapLeaf
    {
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Weight { get; set; }
        public double GrowthPct { get; set; }
        public RectangleF Bounds { get; set; }
    }

    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    public string? ThemeOverride { get; set; }
    public List<TreeMapLeaf> Leaves { get; } = new();
    public string SelectedNodeLabel { get; private set; } = "Cloud Infrastructure";
    public event Action<TreeMapLeaf>? NodeSelected;

    public EnterpriseTreeMapControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(760, 280);

        Leaves.AddRange(new[]
        {
            new TreeMapLeaf { Category = "Platform", Label = "Cloud Infrastructure", Weight = 38, GrowthPct = 18.4 },
            new TreeMapLeaf { Category = "Platform", Label = "AI Copilot Compute", Weight = 26, GrowthPct = 34.2 },
            new TreeMapLeaf { Category = "Analytics", Label = "Real-Time BI Grid", Weight = 22, GrowthPct = 12.1 },
            new TreeMapLeaf { Category = "Analytics", Label = "OLAP Pivot Engine", Weight = 16, GrowthPct = 9.5 },
            new TreeMapLeaf { Category = "Security", Label = "Zero-Trust IAM", Weight = 14, GrowthPct = 21.0 },
            new TreeMapLeaf { Category = "Operations", Label = "GIS Fleet Telemetry", Weight = 12, GrowthPct = 6.8 }
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
        foreach (var leaf in Leaves)
        {
            if (leaf.Bounds.Contains(e.Location))
            {
                SelectedNodeLabel = leaf.Label;
                NodeSelected?.Invoke(leaf);
                Invalidate();
                break;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.White);
        g.Clear(bg);

        double total = Math.Max(1, Leaves.Sum(l => l.Weight));
        float x = 8, y = 8, availW = Math.Max(100, Width - 16), availH = Math.Max(80, Height - 16);

        Color[] palette =
        {
            Color.FromArgb(0, 120, 212),
            Color.FromArgb(16, 124, 65),
            Color.FromArgb(135, 100, 184),
            Color.FromArgb(216, 59, 1),
            Color.FromArgb(0, 153, 188),
            Color.FromArgb(73, 130, 5)
        };

        using var titleFont = new Font(Font.FontFamily, 10f, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(Color.White);
        using var borderPen = new Pen(bg, 3f);
        using var selPen = new Pen(Color.Gold, 3f);

        bool splitVertical = true;
        for (int i = 0; i < Leaves.Count; i++)
        {
            var leaf = Leaves[i];
            double remWeight = Leaves.Skip(i).Sum(l => l.Weight);
            float ratio = (float)(leaf.Weight / Math.Max(1, remWeight));

            RectangleF rect;
            if (i == Leaves.Count - 1)
            {
                rect = new RectangleF(x, y, availW, availH);
            }
            else if (splitVertical)
            {
                float w = availW * ratio;
                rect = new RectangleF(x, y, w, availH);
                x += w;
                availW -= w;
            }
            else
            {
                float h = availH * ratio;
                rect = new RectangleF(x, y, availW, h);
                y += h;
                availH -= h;
            }
            splitVertical = !splitVertical;
            leaf.Bounds = rect;

            using var fill = new SolidBrush(palette[i % palette.Length]);
            g.FillRectangle(fill, rect);
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);

            if (leaf.Label == SelectedNodeLabel)
            {
                g.DrawRectangle(selPen, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            }

            g.DrawString($"[{leaf.Category}] {leaf.Label}", titleFont, whiteBrush, new RectangleF(rect.X + 8, rect.Y + 8, rect.Width - 12, 24));
            g.DrawString($"Allocation: {leaf.Weight:F0}M  (+{leaf.GrowthPct:F1}% YoY)", Font, whiteBrush, new RectangleF(rect.X + 8, rect.Y + 32, rect.Width - 12, 22));
        }
    }
}

/// <summary>
/// Clean-room Interactive Vector Signature Pad Control.
/// Supports smooth GDI+ ink stroke capture, Clear, and stroke verification telemetry.
/// </summary>
public class EnterpriseSignaturePadControl : Control, IThemeableControl
{
    private readonly List<List<Point>> _strokes = new();
    private List<Point>? _activeStroke;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;

    public string? ThemeOverride { get; set; }
    public int StrokeCount => _strokes.Count;
    public event Action<int>? SignatureChanged;

    public EnterpriseSignaturePadControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(640, 220);
        Cursor = Cursors.Cross;
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void ClearSignature()
    {
        _strokes.Clear();
        _activeStroke = null;
        SignatureChanged?.Invoke(0);
        Invalidate();
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
            _activeStroke = new List<Point> { e.Location };
            _strokes.Add(_activeStroke);
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_activeStroke != null && e.Button == MouseButtons.Left)
        {
            _activeStroke.Add(e.Location);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_activeStroke != null)
        {
            _activeStroke = null;
            SignatureChanged?.Invoke(_strokes.Count);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 90, 180));
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);

        g.Clear(bg);
        using var borderPen = new Pen(border);
        using var linePen = new Pen(border) { DashStyle = DashStyle.Dash };
        using var fgBrush = new SolidBrush(Color.FromArgb(140, fg));

        int baselineY = Height - 48;
        g.DrawLine(linePen, 32, baselineY, Width - 32, baselineY);
        g.DrawString("X  Authorized Enterprise Signatory (Draw signature above with mouse/touch)", Font, fgBrush, 32, baselineY + 8);

        using var inkPen = new Pen(primary, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        foreach (var stroke in _strokes)
        {
            if (stroke.Count > 1)
            {
                g.DrawLines(inkPen, stroke.ToArray());
            }
        }

        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// Clean-room On-Screen Virtual Keyboard Control.
/// Touch-friendly QWERTY layout dispatching virtual key presses.
/// </summary>
public class EnterpriseVirtualKeyboardControl : Control, IThemeableControl
{
    private static readonly string[][] Rows =
    {
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "BKSP" },
        new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
        new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", "ENTER" },
        new[] { "Z", "X", "C", "V", "B", "N", "M", ".", "-", "SPACE" }
    };

    private readonly List<(string Key, Rectangle Bounds)> _keys = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    public string? ThemeOverride { get; set; }
    public string TypedBuffer { get; set; } = "ENTERPRISE-READY";
    public event Action<string>? KeyPressed;

    public EnterpriseVirtualKeyboardControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Size = new Size(720, 230);
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
        foreach (var (key, rect) in _keys)
        {
            if (rect.Contains(e.Location))
            {
                if (key == "BKSP")
                {
                    if (TypedBuffer.Length > 0) TypedBuffer = TypedBuffer[..^1];
                }
                else if (key == "SPACE")
                {
                    TypedBuffer += " ";
                }
                else if (key != "ENTER")
                {
                    TypedBuffer += key;
                }
                KeyPressed?.Invoke(key);
                Invalidate();
                break;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Background, Color.FromArgb(240, 242, 245));
        var keyBg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.LightGray);
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 212));

        g.Clear(bg);
        _keys.Clear();

        using var keyBrush = new SolidBrush(keyBg);
        using var fgBrush = new SolidBrush(fg);
        using var primaryBrush = new SolidBrush(primary);
        using var borderPen = new Pen(border);
        using var boldFont = new Font(Font, FontStyle.Bold);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Display buffer bar
        var displayRect = new Rectangle(12, 8, Width - 24, 32);
        g.FillRectangle(keyBrush, displayRect);
        g.DrawRectangle(borderPen, displayRect);
        g.DrawString($"Input Buffer: {TypedBuffer}▏", boldFont, primaryBrush, displayRect.X + 10, displayRect.Y + 7);

        int startY = 48;
        int rowH = Math.Max(34, (Height - 56) / Rows.Length);
        for (int r = 0; r < Rows.Length; r++)
        {
            var row = Rows[r];
            int keyW = Math.Max(42, (Width - 24) / (row.Length + 1));
            int curX = 12 + r * 12;
            for (int c = 0; c < row.Length; c++)
            {
                string k = row[c];
                int w = k.Length > 1 ? keyW + 28 : keyW;
                var rect = new Rectangle(curX, startY + r * rowH, w - 4, rowH - 6);
                _keys.Add((k, rect));

                g.FillRectangle(keyBrush, rect);
                g.DrawRectangle(borderPen, rect);
                g.DrawString(k, boldFont, fgBrush, rect, sf);
                curX += w;
            }
        }
    }
}

