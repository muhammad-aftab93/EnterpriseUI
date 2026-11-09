using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

public enum DataLayoutLabelPosition { Top, Left }

public sealed class DataLayoutItem
{
    public DataLayoutItem(string label, Control control)
    {
        Label = label;
        Control = control;
    }

    public string Label { get; set; }
    public Control Control { get; }
    public string? Group { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; }

    /// <summary>Number of columns the item spans (at least 1).</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Height of the control in logical pixels; 0 keeps the control's own height.</summary>
    public int Height { get; set; }

    internal Rectangle Bounds;
    internal int ControlTop;
    internal int ControlLeft;
    internal int ControlWidth;
    internal int ControlHeight;
}

/// <summary>Arranges any controls with captions in groups and responsive columns; groups collapse and look like cards.</summary>
public class EnterpriseDataLayoutControl : EnterpriseScrollableControl
{
    private sealed class GroupBox
    {
        public string Name = string.Empty;
        public Rectangle Bounds;
        public Rectangle HeaderBounds;
        public bool Collapsed;
    }

    private readonly List<DataLayoutItem> _items = new();
    private readonly List<GroupBox> _groups = new();
    private readonly HashSet<string> _collapsed = new();
    private DataLayoutLabelPosition _labelPosition = DataLayoutLabelPosition.Top;
    private int _columnWidth = 320;
    private int _maxColumns = 4;
    private int _labelWidth = 130;
    private bool _cards = true;
    private int _contentHeight;

    public EnterpriseDataLayoutControl()
    {
        Size = new Size(720, 420);
        AccessibleRole = AccessibleRole.Grouping;
    }

    public IReadOnlyList<DataLayoutItem> Items => _items;

    public DataLayoutLabelPosition LabelPosition
    {
        get => _labelPosition;
        set { _labelPosition = value; LayoutItems(); }
    }

    public int ColumnWidth
    {
        get => _columnWidth;
        set { _columnWidth = Math.Max(160, value); LayoutItems(); }
    }

    public int MaxColumns
    {
        get => _maxColumns;
        set { _maxColumns = Math.Max(1, value); LayoutItems(); }
    }

    public int LabelWidth
    {
        get => _labelWidth;
        set { _labelWidth = Math.Max(60, value); LayoutItems(); }
    }

    public bool CardGroups
    {
        get => _cards;
        set { _cards = value; LayoutItems(); }
    }

    protected override int LineHeight => S(40);
    protected override Size ContentSize => new(0, _contentHeight);

    public DataLayoutItem AddItem(string label, Control control, string? group = null, int columnSpan = 1, int height = 0, string? description = null, bool required = false)
    {
        var item = new DataLayoutItem(label, control) { Group = group, ColumnSpan = Math.Max(1, columnSpan), Height = height, Description = description, Required = required };
        _items.Add(item);
        Controls.Add(control);
        control.Enter += (_, _) => EnsureVisible(item.Bounds);
        LayoutItems();
        return item;
    }

    public bool RemoveItem(DataLayoutItem item)
    {
        if (!_items.Remove(item)) return false;
        Controls.Remove(item.Control);
        LayoutItems();
        return true;
    }

    public void ClearItems()
    {
        foreach (var item in _items) Controls.Remove(item.Control);
        _items.Clear();
        LayoutItems();
    }

    public void ToggleGroup(string name)
    {
        if (!_collapsed.Remove(name)) _collapsed.Add(name);
        LayoutItems();
    }

    public bool IsGroupCollapsed(string name) => _collapsed.Contains(name);

    public int ColumnCount => Columns(InnerBounds.Width);

    private int Columns(int width) => Math.Clamp(Math.Max(1, (width - S(32)) / S(_columnWidth)), 1, _maxColumns);

    private void LayoutItems()
    {
        if (!IsHandleCreated && Width <= 0) return;
        _groups.Clear();
        var width = Math.Max(1, InnerBounds.Width - (_contentHeight > InnerBounds.Height ? ScrollBarSize : 0));
        var columns = Columns(width);
        var pad = S(16);
        var gap = S(16);
        var cardPad = _cards ? S(14) : 0;
        var inner = width - pad * 2 - cardPad * 2;
        var colWidth = Math.Max(S(100), (inner - gap * (columns - 1)) / columns);
        var captionPx = S(22);
        var helpPx = S(18);
        var rowGap = S(6);
        var y = pad;

        var useGroups = _items.Any(i => !string.IsNullOrEmpty(i.Group));
        foreach (var group in _items.GroupBy(i => string.IsNullOrEmpty(i.Group) ? (useGroups ? "General" : string.Empty) : i.Group!))
        {
            var box = new GroupBox { Name = group.Key, Collapsed = _collapsed.Contains(group.Key) };
            var top = y;
            var headerPx = group.Key.Length > 0 ? S(40) : 0;
            box.HeaderBounds = new Rectangle(pad, top, width - pad * 2, headerPx);
            var cursorY = top + headerPx + (headerPx > 0 ? 0 : cardPad);
            var col = 0;
            var rowHeight = 0;
            var rowTop = cursorY;

            foreach (var item in group)
            {
                if (box.Collapsed)
                {
                    item.Bounds = Rectangle.Empty;
                    continue;
                }

                var span = Math.Min(item.ColumnSpan, columns);
                if (col + span > columns)
                {
                    rowTop += rowHeight + rowGap;
                    rowHeight = 0;
                    col = 0;
                }

                var cellWidth = colWidth * span + gap * (span - 1);
                var controlHeight = S(item.Height > 0 ? item.Height : Math.Max(28, (int)(item.Control.Height / DpiScale)));
                var left = _labelPosition == DataLayoutLabelPosition.Left;
                var cellHeight = (left ? Math.Max(controlHeight, captionPx) : captionPx + controlHeight) + (string.IsNullOrEmpty(item.Description) ? 0 : helpPx);
                var x = pad + cardPad + col * (colWidth + gap);

                item.Bounds = new Rectangle(x, rowTop, cellWidth, cellHeight);
                item.ControlTop = left ? 0 : captionPx;
                item.ControlLeft = left ? S(_labelWidth) : 0;
                item.ControlWidth = Math.Max(S(40), cellWidth - item.ControlLeft);
                item.ControlHeight = controlHeight;
                rowHeight = Math.Max(rowHeight, cellHeight);
                col += span;
            }

            var bottom = box.Collapsed ? top + headerPx : rowTop + rowHeight + cardPad + (headerPx > 0 ? S(4) : 0);
            if (!box.Collapsed && !group.Any()) bottom = top + headerPx;
            box.Bounds = new Rectangle(pad, top, width - pad * 2, Math.Max(headerPx, bottom - top));
            _groups.Add(box);
            y = bottom + S(16);
        }

        _contentHeight = y;
        UpdateScrollBars();
        PositionControls();
        Invalidate();
    }

    private void PositionControls()
    {
        var inner = InnerBounds;
        var area = ViewPort;
        foreach (var item in _items)
        {
            if (item.Bounds.IsEmpty)
            {
                item.Control.Visible = false;
                continue;
            }

            var bounds = new Rectangle(item.Bounds.X + item.ControlLeft + inner.X, item.Bounds.Y + item.ControlTop - ScrollY + inner.Y, item.ControlWidth, item.ControlHeight);
            item.Control.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var visible = Rectangle.Intersect(bounds, area);
            item.Control.Visible = visible.Width > 0 && visible.Height > 0;
            if (!item.Control.Visible) continue;
            if (visible == bounds)
            {
                item.Control.Region = null;
            }
            else
            {
                visible.Offset(-bounds.X, -bounds.Y);
                item.Control.Region = new Region(visible);
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_items.Count > 0) LayoutItems();
    }

    protected override void OnScrolled()
    {
        PositionControls();
        base.OnScrolled();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g, drawFocus: false);
        var view = ViewPort;
        g.SetClip(view);
        var offset = new Point(view.X, view.Y - ScrollY);

        if (_items.Count == 0) Chrome.DrawText(g, "Add items to build the layout", Font, view, Colors.Muted, ContentAlignment.MiddleCenter);

        foreach (var group in _groups)
        {
            var box = group.Bounds;
            box.Offset(offset);
            if (box.Bottom < view.Top || box.Top > view.Bottom) continue;
            if (group.Name.Length > 0)
            {
                if (_cards)
                {
                    Chrome.FillRounded(g, box, Chrome.Blend(Colors.Surface, Colors.Header, 0.35), S(6));
                    Chrome.DrawRoundedBorder(g, box, Colors.Border, S(6));
                }

                var header = group.HeaderBounds;
                header.Offset(offset);
                Chrome.DrawChevron(g, new Rectangle(header.X + S(8), header.Y, S(20), header.Height), group.Collapsed ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
                Chrome.DrawText(g, group.Name, BoldFont, new Rectangle(header.X + S(34), header.Y, Math.Max(0, header.Width - S(40)), header.Height), Colors.Text);
                if (!_cards && !group.Collapsed)
                {
                    using var pen = new Pen(Colors.Border);
                    g.DrawLine(pen, header.X, header.Bottom - 2, header.Right, header.Bottom - 2);
                }
            }
            else if (_cards)
            {
                Chrome.FillRounded(g, box, Chrome.Blend(Colors.Surface, Colors.Header, 0.35), S(6));
                Chrome.DrawRoundedBorder(g, box, Colors.Border, S(6));
            }
        }

        foreach (var item in _items)
        {
            if (item.Bounds.IsEmpty) continue;
            var b = item.Bounds;
            b.Offset(offset);
            if (b.Bottom < view.Top || b.Top > view.Bottom) continue;
            var labelBounds = _labelPosition == DataLayoutLabelPosition.Top
                ? new Rectangle(b.X, b.Y, b.Width, S(22))
                : new Rectangle(b.X, b.Y, S(_labelWidth) - S(8), Math.Min(b.Height, S(34)));
            Chrome.DrawText(g, item.Label, Font, labelBounds, Colors.Text, _labelPosition == DataLayoutLabelPosition.Top ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft);
            if (item.Required)
            {
                var star = Chrome.MeasureText(item.Label, Font) + S(3);
                Chrome.DrawText(g, "*", Font, new Rectangle(labelBounds.X + star, labelBounds.Y, S(12), labelBounds.Height), Colors.Danger, _labelPosition == DataLayoutLabelPosition.Top ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft);
            }

            if (!string.IsNullOrEmpty(item.Description))
            {
                var help = new Rectangle(b.X + item.ControlLeft, b.Bottom - S(18), Math.Max(0, b.Width - item.ControlLeft), S(18));
                Chrome.DrawText(g, item.Description, Font, help, Colors.Muted, ContentAlignment.TopLeft);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        var view = ViewPort;
        foreach (var group in _groups.Where(g => g.Name.Length > 0))
        {
            var header = group.HeaderBounds;
            header.Offset(view.X, view.Y - ScrollY);
            if (header.Contains(e.Location))
            {
                ToggleGroup(group.Name);
                return;
            }
        }
    }
}
