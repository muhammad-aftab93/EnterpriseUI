using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

public enum ListViewMode { Details, List, Tiles }

public enum ListViewSortOrder { None, Ascending, Descending }

public sealed class EnterpriseListViewColumn
{
    internal EnterpriseListViewControl? Owner;
    private int _width = 140;
    private string _text;

    public EnterpriseListViewColumn(string text, int width = 140, string field = "")
    {
        _text = text;
        _width = width;
        Field = field;
    }

    public string Text { get => _text; set { _text = value; Owner?.Relayout(); } }
    public int Width { get => _width; set { _width = Math.Max(24, value); Owner?.Relayout(); } }
    public string Field { get; set; }
    public ContentAlignment Alignment { get; set; } = ContentAlignment.MiddleLeft;
    public bool Visible { get; set; } = true;
}

public sealed class EnterpriseListViewItem
{
    internal EnterpriseListViewControl? Owner;
    private string _text;
    private bool _checked;

    public EnterpriseListViewItem(string text, params string[] subItems)
    {
        _text = text;
        SubItems = subItems.ToList();
    }

    public string Text { get => _text; set { _text = value; Owner?.Relayout(); } }
    public List<string> SubItems { get; }
    public object? Tag { get; set; }
    public string? Group { get; set; }
    public Image? Image { get; set; }
    public Color? ForeColor { get; set; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Owner?.RaiseItemChecked(this);
        }
    }
}

public sealed class EnterpriseListViewItemCollection : Collection<EnterpriseListViewItem>
{
    private readonly EnterpriseListViewControl _owner;
    internal EnterpriseListViewItemCollection(EnterpriseListViewControl owner) => _owner = owner;

    public EnterpriseListViewItem Add(string text, params string[] subItems)
    {
        var item = new EnterpriseListViewItem(text, subItems);
        Add(item);
        return item;
    }

    public void AddRange(IEnumerable<EnterpriseListViewItem> items)
    {
        foreach (var item in items) Items.Add(Attach(item));
        _owner.Relayout();
    }

    protected override void InsertItem(int index, EnterpriseListViewItem item)
    {
        base.InsertItem(index, Attach(item));
        _owner.Relayout();
    }

    protected override void SetItem(int index, EnterpriseListViewItem item)
    {
        Items[index].Owner = null;
        base.SetItem(index, Attach(item));
        _owner.Relayout();
    }

    protected override void RemoveItem(int index)
    {
        Items[index].Owner = null;
        base.RemoveItem(index);
        _owner.Relayout();
    }

    protected override void ClearItems()
    {
        foreach (var item in Items) item.Owner = null;
        base.ClearItems();
        _owner.Relayout();
    }

    private EnterpriseListViewItem Attach(EnterpriseListViewItem item)
    {
        item.Owner = _owner;
        return item;
    }
}

/// <summary>Details, list and tile views over the same items, with sortable resizable columns, groups, check boxes and selection.</summary>
public class EnterpriseListViewControl : EnterpriseScrollableControl
{
    private sealed class Entry
    {
        public EnterpriseListViewItem? Item;
        public string? Group;
        public int GroupCount;
        public bool Collapsed;
        public Rectangle Bounds;
        public int Order = -1;
    }

    private readonly List<Entry> _entries = new();
    private readonly List<EnterpriseListViewItem> _order = new();
    private readonly HashSet<EnterpriseListViewItem> _selected = new();
    private readonly HashSet<string> _collapsedGroups = new();
    private readonly List<EnterpriseListViewColumn> _columns = new();
    private object? _dataSource;
    private ListViewMode _mode = ListViewMode.Details;
    private ListSelectionMode _selectionMode = ListSelectionMode.MultiExtended;
    private bool _layoutDirty = true;
    private int _layoutWidth = -1;
    private int _contentHeight;
    private int _flowColumns = 1;
    private int _sortColumn = -1;
    private ListViewSortOrder _sortOrder = ListViewSortOrder.None;
    private EnterpriseListViewItem? _current;
    private EnterpriseListViewItem? _anchor;
    private EnterpriseListViewItem? _hotItem;
    private int _hotColumn = -1;
    private int _resizeColumn = -1;
    private int _resizeStartX;
    private int _resizeStartWidth;
    private bool _checkBoxes;
    private bool _alternateRows = true;

    public EnterpriseListViewControl()
    {
        Items = new EnterpriseListViewItemCollection(this);
        Size = new Size(520, 300);
        AccessibleRole = AccessibleRole.List;
    }

    public EnterpriseListViewItemCollection Items { get; }
    public IReadOnlyList<EnterpriseListViewColumn> Columns => _columns;

    public event EventHandler? SelectionChanged;
    public event EventHandler<EnterpriseListViewItem>? ItemActivate;
    public event EventHandler<EnterpriseListViewItem>? ItemChecked;
    public event EventHandler<int>? ColumnClick;
    public event EventHandler<int>? ColumnWidthChanged;

    public Func<EnterpriseListViewItem, string?>? GroupSelector
    {
        get => _groupSelector;
        set { _groupSelector = value; Relayout(); }
    }

    public Func<EnterpriseListViewItem, bool>? Filter
    {
        get => _filter;
        set { _filter = value; Relayout(); }
    }

    private Func<EnterpriseListViewItem, string?>? _groupSelector;
    private Func<EnterpriseListViewItem, bool>? _filter;
    public string EmptyText { get; set; } = "No items";

    /// <summary>Item width in logical pixels for the list and tile views; 0 chooses a width per view.</summary>
    public int ItemWidth { get; set; }

    public int RowHeight { get; set; } = 30;

    public ListViewMode View
    {
        get => _mode;
        set { _mode = value; Relayout(); }
    }

    public ListSelectionMode SelectionMode
    {
        get => _selectionMode;
        set { _selectionMode = value; ClearSelection(); }
    }

    public bool CheckBoxes
    {
        get => _checkBoxes;
        set { _checkBoxes = value; Relayout(); }
    }

    public bool AlternateRows
    {
        get => _alternateRows;
        set { _alternateRows = value; Invalidate(); }
    }

    public int SortColumn => _sortColumn;
    public ListViewSortOrder SortOrder => _sortOrder;

    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (_dataSource is INotifyCollectionChanged old) old.CollectionChanged -= OnSourceChanged;
            _dataSource = value;
            if (value is INotifyCollectionChanged notify) notify.CollectionChanged += OnSourceChanged;
            ReloadFromSource();
        }
    }

    public EnterpriseListViewColumn AddColumn(string text, int width = 140, string field = "", ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        var column = new EnterpriseListViewColumn(text, width, field) { Alignment = alignment, Owner = this };
        _columns.Add(column);
        Relayout();
        return column;
    }

    public void ClearColumns()
    {
        _columns.Clear();
        _sortColumn = -1;
        Relayout();
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired) BeginInvoke(new Action(ReloadFromSource));
        else ReloadFromSource();
    }

    private void ReloadFromSource()
    {
        Items.Clear();
        if (_dataSource is IEnumerable source)
        {
            var created = new List<EnterpriseListViewItem>();
            foreach (var row in source)
            {
                if (row is null) continue;
                var first = _columns.FirstOrDefault();
                var item = new EnterpriseListViewItem(first is null ? Convert.ToString(row, CultureInfo.CurrentCulture) ?? string.Empty : MemberAccess.Text(row, first.Field)) { Tag = row };
                for (var c = 1; c < _columns.Count; c++) item.SubItems.Add(MemberAccess.Text(row, _columns[c].Field));
                created.Add(item);
            }

            Items.AddRange(created);
        }
    }

    // ---- cells and layout -----------------------------------------------------------------

    public string GetCellText(EnterpriseListViewItem item, int column)
    {
        if (column == 0) return item.Text;
        return column - 1 < item.SubItems.Count ? item.SubItems[column - 1] : string.Empty;
    }

    private IEnumerable<(EnterpriseListViewColumn Column, int Index)> VisibleColumns() =>
        _columns.Select((c, i) => (c, i)).Where(t => t.c.Visible);

    private int HeaderPx => _mode == ListViewMode.Details && _columns.Count > 0 ? S(34) : 0;
    private int RowPx => S(RowHeight);
    private int GroupPx => S(32);
    private int TotalColumnsWidth => VisibleColumns().Sum(t => S(t.Column.Width)) + (_checkBoxes ? S(34) : 0);
    private int CheckGutter => _checkBoxes ? S(34) : 0;

    private int CellWidth => S(ItemWidth > 0 ? ItemWidth : _mode == ListViewMode.Tiles ? 230 : 190);
    private int CellHeight => _mode == ListViewMode.Tiles ? S(60) : RowPx;

    public void Relayout()
    {
        _layoutDirty = true;
        if (IsDisposed) return;
        var selected = _selected.Where(i => Items.Contains(i)).ToList();
        if (selected.Count != _selected.Count)
        {
            _selected.Clear();
            foreach (var item in selected) _selected.Add(item);
        }

        if (_current is not null && !Items.Contains(_current)) _current = _anchor = null;
        UpdateScrollBars();
    }

    protected override int LineHeight => RowPx;

    protected override Size ContentSize
    {
        get
        {
            var inner = InnerBounds.Width;
            EnsureLayout(inner);
            var height = _contentHeight + HeaderPx;
            if (height > InnerBounds.Height - (_mode == ListViewMode.Details && TotalColumnsWidth > inner ? ScrollBarSize : 0))
            {
                inner -= ScrollBarSize;
                EnsureLayout(inner);
                height = _contentHeight + HeaderPx;
            }

            return new Size(_mode == ListViewMode.Details ? TotalColumnsWidth : 0, height);
        }
    }

    private void EnsureLayout(int width)
    {
        if (!_layoutDirty && _layoutWidth == width) return;
        _layoutWidth = width;
        _layoutDirty = false;
        _entries.Clear();
        _order.Clear();

        IEnumerable<EnterpriseListViewItem> source = Items;
        if (Filter is { } filter) source = source.Where(filter);
        var items = source.ToList();
        if (_sortColumn >= 0 && _sortOrder != ListViewSortOrder.None)
        {
            var column = _sortColumn;
            var sign = _sortOrder == ListViewSortOrder.Ascending ? 1 : -1;
            items = items.OrderBy(i => i, Comparer<EnterpriseListViewItem>.Create((a, b) => sign * CompareValues(GetCellText(a, column), GetCellText(b, column)))).ToList();
        }

        string? GroupOf(EnterpriseListViewItem i) => GroupSelector?.Invoke(i) ?? i.Group;
        var grouped = items.Any(i => GroupOf(i) is not null);
        var y = 0;
        var rowWidth = _mode == ListViewMode.Details ? Math.Max(width, TotalColumnsWidth) : width;
        _flowColumns = _mode == ListViewMode.Details ? 1 : Math.Max(1, width / CellWidth);

        void Place(IEnumerable<EnterpriseListViewItem> group)
        {
            var flowIndex = 0;
            foreach (var item in group)
            {
                Rectangle bounds;
                if (_mode == ListViewMode.Details)
                {
                    bounds = new Rectangle(0, y, rowWidth, RowPx);
                    y += RowPx;
                }
                else
                {
                    var col = flowIndex % _flowColumns;
                    var row = flowIndex / _flowColumns;
                    bounds = new Rectangle(col * CellWidth, y + row * CellHeight, CellWidth, CellHeight);
                    flowIndex++;
                }

                _entries.Add(new Entry { Item = item, Bounds = bounds, Order = _order.Count });
                _order.Add(item);
            }

            if (_mode != ListViewMode.Details && flowIndex > 0) y += ((flowIndex + _flowColumns - 1) / _flowColumns) * CellHeight;
        }

        if (!grouped)
        {
            Place(items);
        }
        else
        {
            foreach (var group in items.GroupBy(i => GroupOf(i) ?? string.Empty).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var collapsed = _collapsedGroups.Contains(group.Key);
                _entries.Add(new Entry { Group = group.Key, GroupCount = group.Count(), Collapsed = collapsed, Bounds = new Rectangle(0, y, rowWidth, GroupPx) });
                y += GroupPx;
                if (!collapsed) Place(group);
            }
        }

        _contentHeight = y;
    }

    private static int CompareValues(string a, string b)
    {
        if (decimal.TryParse(a, NumberStyles.Number, CultureInfo.CurrentCulture, out var x) && decimal.TryParse(b, NumberStyles.Number, CultureInfo.CurrentCulture, out var y)) return x.CompareTo(y);
        if (DateTime.TryParse(a, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d1) && DateTime.TryParse(b, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d2)) return d1.CompareTo(d2);
        return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
    }

    public void Sort(int column, ListViewSortOrder order)
    {
        _sortColumn = order == ListViewSortOrder.None ? -1 : column;
        _sortOrder = order;
        Relayout();
    }

    public void ToggleGroup(string group)
    {
        if (!_collapsedGroups.Remove(group)) _collapsedGroups.Add(group);
        Relayout();
    }

    public void AutoSizeColumn(int index)
    {
        if (index < 0 || index >= _columns.Count) return;
        var width = Chrome.MeasureText(_columns[index].Text, Font) + S(40);
        foreach (var item in Items.Take(500)) width = Math.Max(width, Chrome.MeasureText(GetCellText(item, index), Font) + S(24));
        _columns[index].Width = (int)(width / DpiScale);
    }

    internal void RaiseItemChecked(EnterpriseListViewItem item)
    {
        Invalidate();
        ItemChecked?.Invoke(this, item);
    }

    // ---- selection ----------------------------------------------------------------------

    public IReadOnlyList<EnterpriseListViewItem> SelectedItems => Items.Where(_selected.Contains).ToList();
    public EnterpriseListViewItem? SelectedItem => SelectedItems.FirstOrDefault();
    public IEnumerable<EnterpriseListViewItem> CheckedItems => Items.Where(i => i.Checked);

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectItem(EnterpriseListViewItem item)
    {
        if (_selectionMode == ListSelectionMode.None) return;
        _selected.Clear();
        _selected.Add(item);
        _current = _anchor = item;
        EnsureItemVisible(item);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        if (_selectionMode is not (ListSelectionMode.MultiSimple or ListSelectionMode.MultiExtended)) return;
        EnsureLayout(ViewPort.Width);
        foreach (var item in _order) _selected.Add(item);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EnsureItemVisible(EnterpriseListViewItem item)
    {
        EnsureLayout(ViewPort.Width);
        var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Item, item));
        if (entry is null) return;
        var b = entry.Bounds;
        EnsureVisible(new Rectangle(b.X, b.Y, Math.Max(1, b.Width), b.Height + HeaderPx));
    }

    private void Apply(EnterpriseListViewItem item, bool extend, bool toggle)
    {
        if (_selectionMode == ListSelectionMode.None) return;
        var before = _selected.Count;
        var snapshot = _selected.ToArray();
        if (_selectionMode == ListSelectionMode.One || (!extend && !toggle && _selectionMode != ListSelectionMode.MultiSimple))
        {
            _selected.Clear();
            _selected.Add(item);
            _anchor = item;
        }
        else if (_selectionMode == ListSelectionMode.MultiSimple || toggle)
        {
            if (!_selected.Remove(item)) _selected.Add(item);
            _anchor = item;
        }
        else if (extend && _anchor is not null)
        {
            var a = _order.IndexOf(_anchor);
            var b = _order.IndexOf(item);
            if (a < 0) a = b;
            _selected.Clear();
            for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++) _selected.Add(_order[i]);
        }

        _current = item;
        EnsureItemVisible(item);
        Invalidate();
        if (before != _selected.Count || !snapshot.All(_selected.Contains)) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- geometry -----------------------------------------------------------------------

    private Rectangle ToControl(Rectangle contentBounds)
    {
        var view = ViewPort;
        return new Rectangle(view.X + contentBounds.X - (_mode == ListViewMode.Details ? ScrollX : 0), view.Y + HeaderPx + contentBounds.Y - ScrollY, contentBounds.Width, contentBounds.Height);
    }

    private Rectangle RowsArea
    {
        get
        {
            var view = ViewPort;
            return new Rectangle(view.X, view.Y + HeaderPx, view.Width, Math.Max(0, view.Height - HeaderPx));
        }
    }

    private Rectangle CheckBoxRect(Rectangle bounds)
    {
        var size = S(16);
        return new Rectangle(bounds.X + S(10), bounds.Y + (Math.Min(bounds.Height, _mode == ListViewMode.Tiles ? bounds.Height : RowPx) - size) / 2, size, size);
    }

    private Rectangle HeaderCell(int columnIndex)
    {
        var view = ViewPort;
        var x = view.X - ScrollX + CheckGutter;
        foreach (var (column, index) in VisibleColumns())
        {
            var w = S(column.Width);
            if (index == columnIndex) return new Rectangle(x, view.Y, w, HeaderPx);
            x += w;
        }

        return Rectangle.Empty;
    }

    private (Entry? Entry, int Column, bool OnBorder) HitTest(Point location)
    {
        var view = ViewPort;
        if (!view.Contains(location)) return (null, -1, false);
        if (HeaderPx > 0 && location.Y < view.Y + HeaderPx)
        {
            foreach (var (_, index) in VisibleColumns())
            {
                var cell = HeaderCell(index);
                if (Math.Abs(location.X - cell.Right) <= S(4)) return (null, index, true);
                if (cell.Contains(location)) return (null, index, false);
            }

            return (null, -1, false);
        }

        if (!RowsArea.Contains(location)) return (null, -1, false);
        EnsureLayout(view.Width);
        foreach (var entry in _entries)
        {
            if (ToControl(entry.Bounds).Contains(location)) return (entry, -1, false);
        }

        return (null, -1, false);
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;
        EnsureLayout(view.Width);

        var rows = RowsArea;
        g.SetClip(rows);
        if (_entries.Count == 0)
        {
            Chrome.DrawText(g, EmptyText, Font, rows, Colors.Muted, ContentAlignment.MiddleCenter);
        }

        var bandIndex = 0;
        foreach (var entry in _entries)
        {
            var bounds = ToControl(entry.Bounds);
            if (bounds.Bottom < rows.Top || bounds.Top > rows.Bottom) continue;
            if (entry.Item is null) PaintGroup(g, entry, bounds);
            else PaintItem(g, entry.Item, bounds, entry.Order);
            bandIndex++;
        }

        g.ResetClip();
        if (HeaderPx > 0) PaintHeader(g);
    }

    private void PaintGroup(Graphics g, Entry entry, Rectangle bounds)
    {
        var area = new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width, ViewPort.Width), bounds.Height);
        using (var brush = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Header, 0.6)))
        {
            g.FillRectangle(brush, area);
        }

        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, area.X, area.Bottom - 1, area.Right, area.Bottom - 1);
        Chrome.DrawChevron(g, new Rectangle(area.X + S(6), area.Y, S(20), area.Height), entry.Collapsed ? ChevronDirection.Right : ChevronDirection.Down, Colors.Muted);
        var text = $"{entry.Group}";
        Chrome.DrawText(g, text, BoldFont, new Rectangle(area.X + S(30), area.Y, Math.Max(0, area.Width - S(80)), area.Height), Colors.Text);
        var countX = area.X + S(30) + Chrome.MeasureText(text, BoldFont) + S(8);
        Chrome.DrawText(g, $"({entry.GroupCount})", Font, new Rectangle(countX, area.Y, S(60), area.Height), Colors.Muted);
    }

    private void PaintItem(Graphics g, EnterpriseListViewItem item, Rectangle bounds, int order)
    {
        var selected = _selected.Contains(item);
        var hovered = ReferenceEquals(item, _hotItem) && Enabled;
        var fore = !Enabled ? Colors.Disabled : item.ForeColor ?? (selected ? Colors.SelectionText : Colors.Text);

        if (_mode == ListViewMode.Details)
        {
            var area = new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Width, ViewPort.Width), bounds.Height);
            var back = Chrome.ItemBack(Colors, selected, hovered, _alternateRows && order % 2 == 1);
            using (var brush = new SolidBrush(back)) g.FillRectangle(brush, area);
            if (selected) Chrome.DrawSelectionBar(g, area, Colors, S(3));
            if (ReferenceEquals(item, _current) && Focused)
            {
                using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawRectangle(pen, area.X + 1, area.Y + 1, Math.Min(area.Width, ViewPort.Width) - 3, area.Height - 3);
            }

            if (_checkBoxes) Chrome.DrawCheckBox(g, Colors, CheckBoxRect(area), item.Checked ? CheckState.Checked : CheckState.Unchecked, Enabled, hovered);
            var x = bounds.X + CheckGutter;
            foreach (var (column, index) in VisibleColumns())
            {
                var w = S(column.Width);
                var cell = new Rectangle(x + S(10), bounds.Y, Math.Max(0, w - S(18)), bounds.Height);
                Chrome.DrawText(g, GetCellText(item, index), Font, cell, fore, column.Alignment);
                x += w;
            }

            return;
        }

        var tile = Rectangle.Inflate(bounds, -S(3), -S(2));
        if (selected || hovered) Chrome.FillRounded(g, tile, Chrome.ItemBack(Colors, selected, hovered), S(4));
        if (selected) Chrome.DrawSelectionBar(g, tile, Colors, S(3));
        if (ReferenceEquals(item, _current) && Focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(tile, -1, -1), S(4));
            using var pen = new Pen(Colors.FocusRing) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawPath(pen, path);
        }

        var left = tile.X + S(6);
        if (_checkBoxes)
        {
            Chrome.DrawCheckBox(g, Colors, CheckBoxRect(tile), item.Checked ? CheckState.Checked : CheckState.Unchecked, Enabled, hovered);
            left += S(28);
        }

        if (_mode == ListViewMode.Tiles)
        {
            var size = S(36);
            var badge = new Rectangle(left, tile.Y + (tile.Height - size) / 2, size, size);
            if (item.Image is { } image) g.DrawImage(image, badge);
            else
            {
                Chrome.FillRounded(g, badge, Colors.HighContrast ? Colors.Header : Chrome.Blend(Colors.Surface, Colors.Primary, 0.18), size / 2);
                var initial = item.Text.Length > 0 ? item.Text[..1].ToUpper(CultureInfo.CurrentCulture) : "?";
                Chrome.DrawText(g, initial, BoldFont, badge, Colors.HighContrast ? Colors.Text : Colors.Primary, ContentAlignment.MiddleCenter);
            }

            var textX = badge.Right + S(10);
            var titleBounds = new Rectangle(textX, tile.Y + S(8), Math.Max(0, tile.Right - textX - S(6)), (tile.Height - S(16)) / 2);
            Chrome.DrawText(g, item.Text, BoldFont, titleBounds, fore);
            var sub = item.SubItems.FirstOrDefault(s => s.Length > 0) ?? string.Empty;
            Chrome.DrawText(g, sub, Font, new Rectangle(titleBounds.X, titleBounds.Bottom, titleBounds.Width, titleBounds.Height), selected ? fore : Colors.Muted);
        }
        else
        {
            Chrome.DrawText(g, item.Text, Font, new Rectangle(left + S(4), tile.Y, Math.Max(0, tile.Right - left - S(10)), tile.Height), fore);
        }
    }

    private void PaintHeader(Graphics g)
    {
        var view = ViewPort;
        var band = new Rectangle(view.X, view.Y, view.Width, HeaderPx);
        g.SetClip(band);
        using (var brush = new SolidBrush(Colors.Header)) g.FillRectangle(brush, band);
        using (var pen = new Pen(Colors.Border)) g.DrawLine(pen, band.X, band.Bottom - 1, band.Right, band.Bottom - 1);

        foreach (var (column, index) in VisibleColumns())
        {
            var cell = HeaderCell(index);
            if (cell.Right < band.Left || cell.Left > band.Right) continue;
            if (index == _hotColumn && Enabled)
            {
                using var hover = new SolidBrush(Chrome.Blend(Colors.Header, Colors.Primary, 0.1));
                g.FillRectangle(hover, cell.X, cell.Y, cell.Width, cell.Height - 1);
            }

            var arrowSpace = index == _sortColumn ? S(22) : 0;
            Chrome.DrawText(g, column.Text, BoldFont, new Rectangle(cell.X + S(10), cell.Y, Math.Max(0, cell.Width - S(18) - arrowSpace), cell.Height - 1), Colors.Text, column.Alignment);
            if (index == _sortColumn && _sortOrder != ListViewSortOrder.None)
            {
                Chrome.DrawChevron(g, new Rectangle(cell.Right - S(24), cell.Y, S(20), cell.Height - 1), _sortOrder == ListViewSortOrder.Ascending ? ChevronDirection.Up : ChevronDirection.Down, Colors.Primary, 1.8f);
            }

            using var divider = new Pen(Chrome.Blend(Colors.Header, Colors.Border, 0.9));
            g.DrawLine(divider, cell.Right - 1, cell.Y + S(8), cell.Right - 1, cell.Bottom - S(8));
        }

        if (_checkBoxes)
        {
            var any = Items.Count > 0;
            var state = !any ? CheckState.Unchecked : Items.All(i => i.Checked) ? CheckState.Checked : Items.Any(i => i.Checked) ? CheckState.Indeterminate : CheckState.Unchecked;
            Chrome.DrawCheckBox(g, Colors, CheckBoxRect(new Rectangle(view.X, view.Y, S(34), HeaderPx)), state, Enabled);
        }

        g.ResetClip();
    }

    // ---- mouse --------------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_resizeColumn >= 0)
        {
            _columns[_resizeColumn].Width = (int)Math.Max(24, (_resizeStartWidth + e.X - _resizeStartX) / DpiScale);
            return;
        }

        var (entry, column, onBorder) = HitTest(e.Location);
        var item = entry?.Item;
        Cursor = onBorder ? Cursors.VSplit : Cursors.Default;
        if (!ReferenceEquals(item, _hotItem) || column != _hotColumn)
        {
            _hotItem = item;
            _hotColumn = column;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotItem = null;
        _hotColumn = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!Enabled) return;
        var (entry, column, onBorder) = HitTest(e.Location);

        if (HeaderPx > 0 && e.Y < ViewPort.Y + HeaderPx)
        {
            if (_checkBoxes && e.X < ViewPort.X + CheckGutter)
            {
                var all = Items.Count > 0 && Items.All(i => i.Checked);
                foreach (var item in Items) item.Checked = !all;
                return;
            }

            if (onBorder && column >= 0)
            {
                _resizeColumn = column;
                _resizeStartX = e.X;
                _resizeStartWidth = S(_columns[column].Width);
                Capture = true;
            }
            else if (column >= 0 && e.Button == MouseButtons.Left)
            {
                var next = _sortColumn != column ? ListViewSortOrder.Ascending : _sortOrder == ListViewSortOrder.Ascending ? ListViewSortOrder.Descending : ListViewSortOrder.None;
                Sort(column, next);
                ColumnClick?.Invoke(this, column);
            }

            return;
        }

        if (entry is null) return;
        if (entry.Item is null)
        {
            ToggleGroup(entry.Group ?? string.Empty);
            return;
        }

        var bounds = ToControl(entry.Bounds);
        if (_checkBoxes && e.Button == MouseButtons.Left && Rectangle.Inflate(CheckBoxRect(_mode == ListViewMode.Details ? bounds : Rectangle.Inflate(bounds, -S(3), -S(2))), S(4), S(4)).Contains(e.Location))
        {
            entry.Item.Checked = !entry.Item.Checked;
            _current = entry.Item;
            return;
        }

        if (e.Button == MouseButtons.Right && _selected.Contains(entry.Item)) return;
        Apply(entry.Item, (ModifierKeys & Keys.Shift) == Keys.Shift, (ModifierKeys & Keys.Control) == Keys.Control);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_resizeColumn >= 0)
        {
            ColumnWidthChanged?.Invoke(this, _resizeColumn);
            _resizeColumn = -1;
            Capture = false;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var (entry, column, onBorder) = HitTest(e.Location);
        if (onBorder && column >= 0) AutoSizeColumn(column);
        else if (entry?.Item is { } item && e.Button == MouseButtons.Left) ItemActivate?.Invoke(this, item);
    }

    // ---- keyboard -----------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureLayout(ViewPort.Width);
        if (_order.Count == 0) return;
        var index = _current is null ? -1 : _order.IndexOf(_current);
        var flow = _mode != ListViewMode.Details;
        var rowStep = flow ? _flowColumns : 1;
        var page = Math.Max(1, RowsArea.Height / Math.Max(1, CellHeight) - 1) * rowStep;

        var target = e.KeyCode switch
        {
            Keys.Down => index < 0 ? 0 : index + rowStep,
            Keys.Up => index < 0 ? 0 : index - rowStep,
            Keys.Right when flow => index + 1,
            Keys.Left when flow => index - 1,
            Keys.PageDown => Math.Max(0, index) + page,
            Keys.PageUp => Math.Max(0, index) - page,
            Keys.Home => 0,
            Keys.End => _order.Count - 1,
            _ => int.MinValue
        };

        if (target != int.MinValue)
        {
            target = Math.Clamp(target, 0, _order.Count - 1);
            if (e.Control && _selectionMode == ListSelectionMode.MultiExtended)
            {
                _current = _order[target];
                EnsureItemVisible(_current);
                Invalidate();
            }
            else
            {
                Apply(_order[target], e.Shift, false);
            }

            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Space when _current is not null:
                if (_checkBoxes) _current.Checked = !_current.Checked;
                else Apply(_current, false, true);
                e.Handled = true;
                break;
            case Keys.Enter when _current is not null:
                ItemActivate?.Invoke(this, _current);
                e.Handled = true;
                break;
            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _dataSource is INotifyCollectionChanged notify) notify.CollectionChanged -= OnSourceChanged;
        base.Dispose(disposing);
    }
}
