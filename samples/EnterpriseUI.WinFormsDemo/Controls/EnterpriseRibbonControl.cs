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

public class RibbonActionItem
{
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public Action? Action { get; set; }
    public RectangleF Bounds { get; set; }
}

public class EnterpriseRibbonControl : Control, IThemeableControl
{
    private readonly List<string> _tabs = new() { "Home", "Data & Grids", "Visualizations", "Navigation & Layout", "Editors & Docs", "Component Catalog", "Intelligence", "Theming", "QA & Lab" };
    private int _selectedTabIndex = 0;
    private readonly List<RibbonActionItem> _actions = new();
    private readonly Dictionary<string, List<RibbonActionItem>> _registeredActions = new(StringComparer.Ordinal);
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private RibbonActionItem? _hoveredAction;

    public event Action? BackstageClicked;
    public event Action<string>? TabChanged;
    public string? ThemeOverride { get; set; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex != value && value >= 0 && value < _tabs.Count)
            {
                _selectedTabIndex = value;
                RebuildActions();
                Invalidate();
                TabChanged?.Invoke(_tabs[_selectedTabIndex]);
            }
        }
    }

    public EnterpriseRibbonControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Height = 115;
        Dock = DockStyle.Top;
        Font = new Font("Segoe UI", 9f);

        RebuildActions();

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    public void RegisterAction(string tab, string text, Action onClick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tab);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(onClick);
        if (!_tabs.Contains(tab)) _tabs.Add(tab);
        if (!_registeredActions.TryGetValue(tab, out var actions))
            _registeredActions[tab] = actions = new List<RibbonActionItem>();
        var existing = actions.FirstOrDefault(action => action.Text == text);
        if (existing != null) existing.Action = onClick;
        else actions.Add(new RibbonActionItem { Text = text, Action = onClick });
        RebuildActions();
        Invalidate();
    }

    private void RebuildActions()
    {
        _actions.Clear();
        var curTab = _tabs[_selectedTabIndex];

        switch (curTab)
        {
            case "Home":
                _actions.Add(new RibbonActionItem { Text = "Overview\nShowcase", Category = "Explore" });
                _actions.Add(new RibbonActionItem { Text = "Learning\nCenter", Category = "Explore" });
                _actions.Add(new RibbonActionItem { Text = "Feature\nMatrix", Category = "Catalog" });
                _actions.Add(new RibbonActionItem { Text = "System\nMetrics", Category = "Diagnostics" });
                break;
            case "Data & Grids":
                _actions.Add(new RibbonActionItem { Text = "Virtual Grid\n1M Rows", Category = "High Perf" });
                _actions.Add(new RibbonActionItem { Text = "Hierarchical\nTreeList", Category = "Structures" });
                _actions.Add(new RibbonActionItem { Text = "OLAP Multi-Dim\nPivotGrid", Category = "Analytics" });
                _actions.Add(new RibbonActionItem { Text = "Streaming\nExport", Category = "I/O" });
                break;
            case "Visualizations":
                _actions.Add(new RibbonActionItem { Text = "Financial &\nCandlestick", Category = "Charts" });
                _actions.Add(new RibbonActionItem { Text = "Radial, Linear\n& EnterpriseBulletGraph", Category = "Gauges" });
                _actions.Add(new RibbonActionItem { Text = "Interactive\nDiagrams", Category = "Visuals" });
                _actions.Add(new RibbonActionItem { Text = "GIS Map\nTracking", Category = "Geo" });
                break;
            case "Navigation & Layout":
                _actions.Add(new RibbonActionItem { Text = "EnterpriseBreadcrumb &\nStepProgress", Category = "Navigation" });
                _actions.Add(new RibbonActionItem { Text = "3D EnterpriseCarousel &\nLive EnterprisePanorama", Category = "Hubs" });
                _actions.Add(new RibbonActionItem { Text = "Wizard &\nDataLayout", Category = "Layout" });
                _actions.Add(new RibbonActionItem { Text = "EnterpriseDesktopAlert &\nTaskDialog", Category = "Alerts" });
                break;
            case "Editors & Docs":
                _actions.Add(new RibbonActionItem { Text = "Masked, Color\n& Calculator", Category = "Editors" });
                _actions.Add(new RibbonActionItem { Text = "EnterpriseSyntaxEditor &\nSpellChecker", Category = "Code" });
                _actions.Add(new RibbonActionItem { Text = "SignaturePad &\nTouch Keyboard", Category = "Touch" });
                _actions.Add(new RibbonActionItem { Text = "PDF, Spread &\nZipLibrary", Category = "Documents" });
                break;
            case "Component Catalog":
                _actions.Add(new RibbonActionItem { Text = "EnterpriseVirtualGrid\n(1M×12 Cells)", Category = "Data" });
                _actions.Add(new RibbonActionItem { Text = "Gantt, Barcode\n& Kanban", Category = "Planning" });
                _actions.Add(new RibbonActionItem { Text = "EnterpriseTreeMap &\nBulletGraph", Category = "Analytics" });
                _actions.Add(new RibbonActionItem { Text = "Rating &\nWaitingBar", Category = "Feedback" });
                break;
            case "Intelligence":
                _actions.Add(new RibbonActionItem { Text = "AI Copilot\nChat", Category = "AI Assistant" });
                _actions.Add(new RibbonActionItem { Text = "NL Query\nTranslation", Category = "AI Assistant" });
                _actions.Add(new RibbonActionItem { Text = "Automated\nSummary", Category = "Intelligence" });
                break;
            case "Theming":
                _actions.Add(new RibbonActionItem { Text = "Fluent\n(Dark/Light)", Category = "Modern" });
                _actions.Add(new RibbonActionItem { Text = "Material\nDesign", Category = "Google" });
                _actions.Add(new RibbonActionItem { Text = "Office\nTheme", Category = "Desktop" });
                _actions.Add(new RibbonActionItem { Text = "High\nContrast", Category = "Accessibility" });
                break;
            case "QA & Lab":
                _actions.Add(new RibbonActionItem { Text = "Stress Test\nMillion Rows", Category = "Performance" });
                _actions.Add(new RibbonActionItem { Text = "WCAG 2.2\nAudit", Category = "Accessibility" });
                _actions.Add(new RibbonActionItem { Text = "API Object\nInspector", Category = "Developer" });
                _actions.Add(new RibbonActionItem { Text = "Regression\nMatrix", Category = "QA" });
                break;
        }

        if (_registeredActions.TryGetValue(curTab, out var registered))
        {
            foreach (var action in registered)
            {
                var builtIn = _actions.FirstOrDefault(existing => existing.Text == action.Text);
                if (builtIn != null) builtIn.Action = action.Action;
                else _actions.Add(action);
            }
        }

        // Compute Layout
        float x = 20;
        foreach (var act in _actions)
        {
            act.Bounds = new RectangleF(x, 38, 90, 70);
            x += 98;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var act = _actions.FirstOrDefault(a => a.Bounds.Contains(e.Location));
        if (act != _hoveredAction)
        {
            _hoveredAction = act;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        // Check Backstage button click
        var backstageRect = new Rectangle(0, 0, 75, 30);
        if (backstageRect.Contains(e.Location))
        {
            BackstageClicked?.Invoke();
            return;
        }

        // Check Tab click
        float tabX = 85;
        using var g = CreateGraphics();
        for (int i = 0; i < _tabs.Count; i++)
        {
            var title = _tabs[i];
            var sz = g.MeasureString(title, Font);
            var tabRect = new RectangleF(tabX, 4, sz.Width + 24, 26);
            if (tabRect.Contains(e.Location))
            {
                SelectedTabIndex = i;
                return;
            }
            tabX += sz.Width + 28;
        }

        // Check Action item click
        var clickedAction = _actions.FirstOrDefault(a => a.Bounds.Contains(e.Location));
        clickedAction?.Action?.Invoke();
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
        var surfaceAlt = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(248, 248, 248));
        var primary = EnterpriseThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(surfaceAlt);

        // 1. Backstage "File / App" Button
        using (var bsBrush = new SolidBrush(primary))
        using (var bsTextBrush = new SolidBrush(Color.White))
        using (var bsFont = new Font(Font, FontStyle.Bold))
        {
            g.FillRectangle(bsBrush, 0, 0, 80, 32);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("FILE", bsFont, bsTextBrush, new RectangleF(0, 0, 80, 32), sf);
        }

        // 2. Tabs
        float tabX = 88;
        using (var tabBorderPen = new Pen(primary, 2.5f))
        using (var textBrush = new SolidBrush(fg))
        using (var activeBrush = new SolidBrush(bg))
        using (var font = new Font(Font, FontStyle.Regular))
        using (var boldFont = new Font(Font, FontStyle.Bold))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var isSelected = (i == _selectedTabIndex);
                var title = _tabs[i];
                var sz = g.MeasureString(title, isSelected ? boldFont : font);
                var tabRect = new RectangleF(tabX, 4, sz.Width + 24, 28);

                if (isSelected)
                {
                    g.FillRectangle(activeBrush, tabRect);
                    g.DrawLine(tabBorderPen, tabRect.Left, tabRect.Bottom, tabRect.Right, tabRect.Bottom);
                }

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(title, isSelected ? boldFont : font, textBrush, tabRect, sf);
                tabX += sz.Width + 28;
            }
        }

        // 3. Ribbon Body Area
        var bodyRect = new RectangleF(0, 32, Width, Height - 32);
        using (var bodyBrush = new SolidBrush(bg))
        using (var borderPen = new Pen(border))
        {
            g.FillRectangle(bodyBrush, bodyRect);
            g.DrawLine(borderPen, 0, 32, Width, 32);
            g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
        }

        // 4. Action Buttons
        using var actionPen = new Pen(Color.FromArgb(50, primary));
        using var hoverBrush = new SolidBrush(Color.FromArgb(20, primary));
        using var actFont = new Font(Font.FontFamily, 8.5f);
        using var textB = new SolidBrush(fg);

        foreach (var act in _actions)
        {
            if (act == _hoveredAction)
            {
                g.FillRoundedRectangle(hoverBrush, act.Bounds, 4);
                g.DrawRoundedRectangle(actionPen, act.Bounds, 4);
            }

            // Small icon simulation glyph
            using (var iconBrush = new SolidBrush(primary))
            {
                g.FillEllipse(iconBrush, act.Bounds.X + (act.Bounds.Width / 2) - 10, act.Bounds.Y + 6, 20, 20);
            }

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(act.Text, actFont, textB, new RectangleF(act.Bounds.X, act.Bounds.Y + 28, act.Bounds.Width, act.Bounds.Height - 30), sf);
        }
    }
}

