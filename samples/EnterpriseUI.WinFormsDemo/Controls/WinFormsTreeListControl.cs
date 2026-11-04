using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class TreeListDemoItem
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public bool IsExpanded { get; set; } = true;
}

public class WinFormsTreeListControl : Control, IThemeableControl
{
    private readonly List<TreeListDemoItem> _items = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private int _hoveredRow = -1;

    public List<TreeListDemoItem> Items => _items;
    public string? ThemeOverride { get; set; }

    public WinFormsTreeListControl()
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

        LoadDefaultData();

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    private void LoadDefaultData()
    {
        _items.Add(new TreeListDemoItem { Id = 1, ParentId = null, Name = "Corporate Headquarters", Department = "Executive", Title = "Chief Executive Suite", Budget = 4500000m });
        _items.Add(new TreeListDemoItem { Id = 2, ParentId = 1, Name = "Engineering & Architecture", Department = "R&D", Title = "VP of Engineering", Budget = 2800000m });
        _items.Add(new TreeListDemoItem { Id = 3, ParentId = 2, Name = "Core Framework Team", Department = "R&D", Title = "Principal Engineer", Budget = 1200000m });
        _items.Add(new TreeListDemoItem { Id = 4, ParentId = 2, Name = "UI Automation & QA", Department = "Quality", Title = "QA Director", Budget = 650000m });
        _items.Add(new TreeListDemoItem { Id = 5, ParentId = 1, Name = "Global Sales & Marketing", Department = "Commercial", Title = "Chief Revenue Officer", Budget = 1700000m });
        _items.Add(new TreeListDemoItem { Id = 6, ParentId = 5, Name = "Enterprise Accounts", Department = "Sales", Title = "Sales VP", Budget = 950000m });
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var headerHeight = 32;
        var rowHeight = 28;
        if (e.Y > headerHeight)
        {
            var idx = (e.Y - headerHeight) / rowHeight;
            if (idx != _hoveredRow)
            {
                _hoveredRow = idx;
                Invalidate();
            }
        }
        else if (_hoveredRow != -1)
        {
            _hoveredRow = -1;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var headerHeight = 32;
        var rowHeight = 28;
        if (e.Y > headerHeight)
        {
            var idx = (e.Y - headerHeight) / rowHeight;
            var flat = GetVisibleRows();
            if (idx >= 0 && idx < flat.Count)
            {
                var item = flat[idx].Item;
                if (_items.Any(i => i.ParentId == item.Id))
                {
                    item.IsExpanded = !item.IsExpanded;
                    Invalidate();
                }
            }
        }
    }

    private List<(TreeListDemoItem Item, int Level)> GetVisibleRows()
    {
        var result = new List<(TreeListDemoItem, int)>();
        void AddChildren(int? parentId, int level)
        {
            foreach (var child in _items.Where(i => i.ParentId == parentId))
            {
                result.Add((child, level));
                if (child.IsExpanded)
                {
                    AddChildren(child.Id, level + 1);
                }
            }
        }
        AddChildren(null, 0);
        return result;
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
        var surfaceAlt = WinFormsThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(245, 245, 245));
        var primary = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(bg);

        var headerHeight = 32;
        var rowHeight = 28;

        // Columns
        var cols = new (string Title, int Width)[]
        {
            ("Organization Hierarchy", 240),
            ("Department", 120),
            ("Leadership Title", 160),
            ("Annual Budget", 120)
        };

        // Header
        using (var headerBrush = new SolidBrush(surfaceAlt))
        using (var borderPen = new Pen(border))
        using (var textBrush = new SolidBrush(fg))
        using (var boldFont = new Font(Font, FontStyle.Bold))
        {
            g.FillRectangle(headerBrush, 0, 0, Width, headerHeight);
            g.DrawLine(borderPen, 0, headerHeight, Width, headerHeight);

            int curX = 0;
            foreach (var col in cols)
            {
                g.DrawString(col.Title, boldFont, textBrush, curX + 8, 8);
                curX += col.Width;
                g.DrawLine(borderPen, curX, 0, curX, Height);
            }
        }

        // Rows
        var visible = GetVisibleRows();
        var y = headerHeight;
        using (var rowBorder = new Pen(border))
        using (var textBrush = new SolidBrush(fg))
        using (var hoverBrush = new SolidBrush(Color.FromArgb(20, primary)))
        {
            for (int r = 0; r < visible.Count && y < Height; r++)
            {
                var row = visible[r];
                var rowRect = new Rectangle(0, y, Width, rowHeight);
                if (r == _hoveredRow)
                {
                    g.FillRectangle(hoverBrush, rowRect);
                }

                g.DrawLine(rowBorder, 0, y + rowHeight, Width, y + rowHeight);

                var indent = row.Level * 20;
                var hasChildren = _items.Any(i => i.ParentId == row.Item.Id);
                var glyph = hasChildren ? (row.Item.IsExpanded ? "▼ " : "► ") : "• ";

                // Hierarchy Column
                var nameDisplay = $"{new string(' ', row.Level * 2)}{glyph}{row.Item.Name}";
                g.DrawString(nameDisplay, Font, textBrush, 12 + indent, y + 6);

                // Department
                g.DrawString(row.Item.Department, Font, textBrush, cols[0].Width + 8, y + 6);

                // Title
                g.DrawString(row.Item.Title, Font, textBrush, cols[0].Width + cols[1].Width + 8, y + 6);

                // Budget
                g.DrawString(row.Item.Budget.ToString("C0"), Font, textBrush, cols[0].Width + cols[1].Width + cols[2].Width + 8, y + 6);

                y += rowHeight;
            }
        }
    }
}

