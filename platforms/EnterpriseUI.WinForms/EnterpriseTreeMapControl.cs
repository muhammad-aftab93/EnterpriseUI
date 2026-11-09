using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Grids;

namespace EnterpriseUI.WinForms;

public enum TreeMapColorMode { Category, Sequential, Diverging }

/// <summary>Area-proportional hierarchy: squarified tiles with group headers, color by category or value, drill-down and a breadcrumb.</summary>
public class EnterpriseTreeMapControl : EnterpriseControlBase
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(0x3B, 0x82, 0xC4), Color.FromArgb(0x2E, 0xA0, 0x8C), Color.FromArgb(0xE0, 0x8E, 0x2B), Color.FromArgb(0x8E, 0x6B, 0xBF),
        Color.FromArgb(0xD1, 0x4F, 0x69), Color.FromArgb(0x5B, 0x9E, 0x3C), Color.FromArgb(0x2B, 0x9C, 0xC9), Color.FromArgb(0xC0, 0x7A, 0x3E),
        Color.FromArgb(0x6F, 0x7F, 0x8F), Color.FromArgb(0xB3, 0x5F, 0xA5)
    };

    private sealed class Tile
    {
        public TreeMapItem Item = null!;
        public Rectangle Bounds;
        public bool IsHeader;
        public int Depth;
        public int Group;
    }

    private readonly List<TreeMapItem> _roots = new();
    private readonly List<Tile> _tiles = new();
    private readonly List<(Rectangle Bounds, TreeMapItem? Item, string Text)> _crumbs = new();
    private readonly ToolTip _tip = new() { InitialDelay = 400, ReshowDelay = 100 };
    private TreeMapItem? _current;
    private TreeMapItem? _selected;
    private TreeMapItem? _hot;
    private TreeMapColorMode _colorMode = TreeMapColorMode.Category;
    private int _levels = 2;
    private bool _dirty = true;
    private string _valueFormat = "N0";
    private string _tipText = string.Empty;
    private double _minColor;
    private double _maxColor;

    public EnterpriseTreeMapControl()
    {
        Size = new Size(640, 400);
        AccessibleRole = AccessibleRole.Graphic;
    }

    public event EventHandler<TreeMapItem>? ItemClick;
    public event EventHandler<TreeMapItem>? ItemActivate;
    public event EventHandler? CurrentChanged;
    public event EventHandler? SelectionChanged;

    public IReadOnlyList<TreeMapItem> Items => _roots;
    public TreeMapItem? CurrentItem => _current;
    public TreeMapItem? SelectedItem => _selected;
    public string RootCaption { get; set; } = "All";
    public bool ShowBreadcrumb { get; set; } = true;

    public TreeMapColorMode ColorMode
    {
        get => _colorMode;
        set { _colorMode = value; Relayout(); }
    }

    /// <summary>Hierarchy levels drawn at once; deeper levels appear after drilling in.</summary>
    public int Levels
    {
        get => _levels;
        set { _levels = Math.Clamp(value, 1, 5); Relayout(); }
    }

    public string ValueFormat
    {
        get => _valueFormat;
        set { _valueFormat = value; Invalidate(); }
    }

    public void SetItems(IEnumerable<TreeMapItem> items)
    {
        _roots.Clear();
        _roots.AddRange(items);
        _current = _selected = null;
        Relayout();
    }

    /// <summary>Builds the hierarchy from objects: grouped by the given fields, one tile per object sized by <paramref name="valueField"/>.</summary>
    public void Bind(IEnumerable data, string nameField, string valueField, string? colorField = null, params string[] groupFields)
    {
        var rows = data.Cast<object>().Where(o => o is not null).ToList();

        TreeMapItem Leaf(object row) => new(
            MemberAccess.Text(row, nameField),
            ToDouble(MemberAccess.Read(row, valueField)),
            colorField is null ? null : ToNullableDouble(MemberAccess.Read(row, colorField)),
            row);

        IEnumerable<TreeMapItem> Build(IEnumerable<object> source, int level)
        {
            if (level >= groupFields.Length)
            {
                foreach (var row in source) yield return Leaf(row);
                yield break;
            }

            foreach (var group in source.GroupBy(r => MemberAccess.Text(r, groupFields[level])).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var node = new TreeMapItem(group.Key);
                foreach (var child in Build(group, level + 1)) node.Add(child);
                yield return node;
            }
        }

        SetItems(Build(rows, 0).ToList());
    }

    private static double ToDouble(object? value) => value is null ? 0 : Convert.ToDouble(value, CultureInfo.CurrentCulture);
    private static double? ToNullableDouble(object? value) => value is null ? null : Convert.ToDouble(value, CultureInfo.CurrentCulture);

    public void DrillInto(TreeMapItem item)
    {
        if (item.Children.Count == 0) return;
        _current = item;
        _selected = null;
        Relayout();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DrillUp()
    {
        if (_current is null) return false;
        _current = _current.Parent;
        _selected = null;
        Relayout();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void NavigateTo(TreeMapItem? item)
    {
        if (ReferenceEquals(item, _current)) return;
        _current = item;
        _selected = null;
        Relayout();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Relayout()
    {
        _dirty = true;
        Invalidate();
    }

    // ---- layout -------------------------------------------------------------------------

    private int CrumbPx => ShowBreadcrumb ? S(34) : 0;

    private Rectangle MapArea => new(1, 1 + CrumbPx, Math.Max(0, Width - 2), Math.Max(0, Height - 2 - CrumbPx));

    private void EnsureLayout()
    {
        if (!_dirty) return;
        _dirty = false;
        _tiles.Clear();
        _crumbs.Clear();

        var source = _current is null ? _roots : _current.Children;
        var area = MapArea;
        var pad = S(3);
        var header = S(22);
        var rects = TreeMapLayout.Compute(source, area.X, area.Y, area.Width, area.Height, _levels, header, pad);
        var topLevel = source.Where(i => i.TotalValue > 0).OrderByDescending(i => i.TotalValue).ToList();
        foreach (var r in rects)
        {
            var top = r.Item;
            while (top.Parent is not null && !topLevel.Contains(top)) top = top.Parent;
            var bounds = Rectangle.FromLTRB((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.X + r.Width), (int)Math.Round(r.Y + r.Height));
            _tiles.Add(new Tile { Item = r.Item, Bounds = bounds, IsHeader = r.IsHeader, Depth = r.Depth, Group = Math.Max(0, topLevel.IndexOf(top)) });
        }

        var colorValues = _tiles.Where(t => !t.IsHeader && t.Item.ColorValue.HasValue).Select(t => t.Item.ColorValue!.Value).ToList();
        _minColor = colorValues.Count == 0 ? 0 : colorValues.Min();
        _maxColor = colorValues.Count == 0 ? 0 : colorValues.Max();

        var x = S(10);
        var chain = new List<TreeMapItem?> { null };
        var path = new Stack<TreeMapItem>();
        for (var p = _current; p is not null; p = p.Parent) path.Push(p);
        chain.AddRange(path);
        for (var i = 0; i < chain.Count; i++)
        {
            var text = chain[i]?.Name ?? RootCaption;
            var w = Chrome.MeasureText(text, Font) + S(14);
            _crumbs.Add((new Rectangle(x, 1 + S(4), w, CrumbPx - S(8)), chain[i], text));
            x += w + S(18);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    // ---- painting -----------------------------------------------------------------------

    private Color TileColor(Tile tile)
    {
        var item = tile.Item;
        if (tile.IsHeader)
        {
            var group = Palette[tile.Group % Palette.Length];
            return Colors.HighContrast ? Colors.Header : _colorMode == TreeMapColorMode.Category ? Chrome.Blend(Colors.Surface, group, 0.28) : Chrome.Blend(Colors.Surface, Colors.Header, 0.9);
        }

        if (Colors.HighContrast) return Colors.Surface;
        switch (_colorMode)
        {
            case TreeMapColorMode.Sequential when item.ColorValue.HasValue && _maxColor > _minColor:
            {
                var t = (item.ColorValue.Value - _minColor) / (_maxColor - _minColor);
                return Chrome.Blend(Chrome.Blend(Colors.Surface, Colors.Primary, 0.2), Colors.Primary, t);
            }
            case TreeMapColorMode.Diverging when item.ColorValue.HasValue:
            {
                var span = Math.Max(Math.Abs(_minColor), Math.Abs(_maxColor));
                var t = span <= 0 ? 0 : Math.Min(1, Math.Abs(item.ColorValue.Value) / span);
                var neutral = Chrome.Blend(Colors.Surface, Colors.Muted, 0.3);
                return Chrome.Blend(neutral, item.ColorValue.Value >= 0 ? Colors.Success : Colors.Danger, 0.25 + 0.75 * t);
            }
            default:
            {
                var group = Palette[tile.Group % Palette.Length];
                return Chrome.Blend(group, Color.White, Math.Min(0.35, tile.Depth * 0.12));
            }
        }
    }

    private static Color TextOn(Color back) => (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) > 150 ? Color.FromArgb(32, 32, 32) : Color.White;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        EnsureLayout();
        if (ShowBreadcrumb) PaintBreadcrumb(g);

        var area = MapArea;
        g.SetClip(area);
        if (_tiles.Count == 0)
        {
            Chrome.DrawText(g, "No data", Font, area, Colors.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        foreach (var tile in _tiles)
        {
            var b = Rectangle.Inflate(tile.Bounds, -1, -1);
            if (b.Width < 2 || b.Height < 2) continue;
            var fill = TileColor(tile);
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, b);

            var hovered = ReferenceEquals(tile.Item, _hot) && Enabled;
            var selected = ReferenceEquals(tile.Item, _selected);
            var textColor = TextOn(fill);

            if (tile.IsHeader)
            {
                Chrome.DrawText(g, tile.Item.Name, BoldFont, new Rectangle(b.X + S(6), b.Y, Math.Max(0, b.Width - S(12)), S(22)), Colors.HighContrast ? Colors.Text : TextOn(fill));
            }
            else if (b.Width > S(44) && b.Height > S(26))
            {
                var title = new Rectangle(b.X + S(6), b.Y + S(4), b.Width - S(12), S(18));
                Chrome.DrawText(g, tile.Item.Name, BoldFont, title, textColor, ContentAlignment.TopLeft);
                if (b.Height > S(44)) Chrome.DrawText(g, tile.Item.TotalValue.ToString(_valueFormat, CultureInfo.CurrentCulture), Font, new Rectangle(b.X + S(6), title.Bottom, b.Width - S(12), S(18)), Color.FromArgb(220, textColor), ContentAlignment.TopLeft);
            }

            if (hovered || selected)
            {
                using var pen = new Pen(selected ? Colors.FocusRing : Colors.Text, selected ? 3f : 2f);
                g.DrawRectangle(pen, b.X + 1, b.Y + 1, b.Width - 3, b.Height - 3);
            }
            else
            {
                using var edge = new Pen(Color.FromArgb(70, Colors.Surface));
                g.DrawRectangle(edge, b.X, b.Y, b.Width - 1, b.Height - 1);
            }
        }
    }

    private void PaintBreadcrumb(Graphics g)
    {
        var band = new Rectangle(1, 1, Width - 2, CrumbPx);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, band);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, band.X, band.Bottom - 1, band.Right, band.Bottom - 1);
        for (var i = 0; i < _crumbs.Count; i++)
        {
            var (bounds, _, text) = _crumbs[i];
            var last = i == _crumbs.Count - 1;
            Chrome.DrawText(g, text, last ? BoldFont : Font, bounds, last ? Colors.Text : Colors.Primary, ContentAlignment.MiddleCenter);
            if (!last) Chrome.DrawChevron(g, new Rectangle(bounds.Right, bounds.Y, S(18), bounds.Height), ChevronDirection.Right, Colors.Muted, 1.4f);
        }
    }

    // ---- input --------------------------------------------------------------------------

    public TreeMapItem? ItemAt(Point location)
    {
        EnsureLayout();
        if (!MapArea.Contains(location)) return null;
        for (var i = _tiles.Count - 1; i >= 0; i--)
        {
            if (_tiles[i].Bounds.Contains(location)) return _tiles[i].Item;
        }

        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = ItemAt(e.Location);
        if (!ReferenceEquals(item, _hot))
        {
            _hot = item;
            Invalidate();
            if (item is null)
            {
                _tip.SetToolTip(this, null);
            }
            else
            {
                var parentTotal = item.Parent?.TotalValue ?? _roots.Sum(r => r.TotalValue);
                var share = parentTotal > 0 ? item.TotalValue / parentTotal : 0;
                _tipText = $"{item.Path}\n{item.TotalValue.ToString(_valueFormat, CultureInfo.CurrentCulture)} ({share:P1})" + (item.ColorValue.HasValue ? $"\n{item.ColorValue.Value.ToString("0.##", CultureInfo.CurrentCulture)}" : string.Empty);
                _tip.SetToolTip(this, _tipText);
            }
        }

        Cursor = _crumbs.Any(c => c.Bounds.Contains(e.Location) && !ReferenceEquals(c.Item, _current)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        _tip.SetToolTip(this, null);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!Enabled || e.Button != MouseButtons.Left) return;
        EnsureLayout();
        if (ShowBreadcrumb && e.Y < 1 + CrumbPx)
        {
            foreach (var (bounds, item, _) in _crumbs)
            {
                if (bounds.Contains(e.Location)) NavigateTo(item);
            }

            return;
        }

        var hit = ItemAt(e.Location);
        if (hit is null) return;
        Select(hit);
        ItemClick?.Invoke(this, hit);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || ItemAt(e.Location) is not { } item) return;
        ItemActivate?.Invoke(this, item);
        DrillInto(item);
    }

    private void Select(TreeMapItem? item)
    {
        if (ReferenceEquals(item, _selected)) return;
        _selected = item;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectItem(TreeMapItem? item) => Select(item);

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.Enter or Keys.Back
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureLayout();
        var leaves = _tiles.Where(t => !t.IsHeader).ToList();
        if (e.KeyCode == Keys.Back)
        {
            DrillUp();
            e.Handled = true;
            return;
        }

        if (leaves.Count == 0) return;
        if (e.KeyCode == Keys.Enter && _selected is not null)
        {
            ItemActivate?.Invoke(this, _selected);
            DrillInto(_selected);
            e.Handled = true;
            return;
        }

        var currentTile = leaves.FirstOrDefault(t => ReferenceEquals(t.Item, _selected));
        if (e.KeyCode == Keys.Home || currentTile is null)
        {
            if (e.KeyCode is Keys.Home or Keys.Up or Keys.Down or Keys.Left or Keys.Right)
            {
                Select(leaves[0].Item);
                e.Handled = true;
            }

            return;
        }

        var (dx, dy) = e.KeyCode switch { Keys.Left => (-1, 0), Keys.Right => (1, 0), Keys.Up => (0, -1), Keys.Down => (0, 1), _ => (0, 0) };
        if (dx == 0 && dy == 0) return;
        var from = new Point(currentTile.Bounds.X + currentTile.Bounds.Width / 2, currentTile.Bounds.Y + currentTile.Bounds.Height / 2);
        Tile? best = null;
        var bestScore = double.MaxValue;
        foreach (var tile in leaves)
        {
            if (ReferenceEquals(tile, currentTile)) continue;
            var center = new Point(tile.Bounds.X + tile.Bounds.Width / 2, tile.Bounds.Y + tile.Bounds.Height / 2);
            var ox = center.X - from.X;
            var oy = center.Y - from.Y;
            var along = ox * dx + oy * dy;
            if (along <= 0) continue;
            var across = Math.Abs(ox * dy) + Math.Abs(oy * dx);
            var score = along + across * 2;
            if (score < bestScore)
            {
                bestScore = score;
                best = tile;
            }
        }

        if (best is not null) Select(best.Item);
        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
