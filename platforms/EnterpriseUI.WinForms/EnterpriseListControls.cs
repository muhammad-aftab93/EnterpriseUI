using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

internal static class MemberAccess
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyDescriptor?> Cache = new();

    public static object? Read(object? item, string? member)
    {
        if (item is null) return null;
        if (string.IsNullOrEmpty(member)) return item;
        var descriptor = item is ICustomTypeDescriptor
            ? TypeDescriptor.GetProperties(item).Find(member, true)
            : Cache.GetOrAdd((item.GetType(), member), key => TypeDescriptor.GetProperties(key.Item1).Find(key.Item2, true));
        return descriptor is null ? item : descriptor.GetValue(item);
    }

    public static string Text(object? item, string? member) => Convert.ToString(Read(item, member), CultureInfo.CurrentCulture) ?? string.Empty;
}

public enum ListSelectionMode { None, One, MultiSimple, MultiExtended }

public sealed class ListItemFormattingEventArgs : EventArgs
{
    public ListItemFormattingEventArgs(int index, object item, string text)
    {
        Index = index;
        Item = item;
        Text = text;
    }

    public int Index { get; }
    public object Item { get; }
    public string Text { get; set; }
    public Color? ForeColor { get; set; }
    public Color? BackColor { get; set; }
    public bool Bold { get; set; }
}

/// <summary>Virtualized list with data binding, display/value members, selection modes, filtering, sorting and type-ahead.</summary>
public class EnterpriseListControl : EnterpriseScrollableControl
{
    private readonly List<object> _items = new();
    private readonly HashSet<int> _selected = new();
    private List<int> _view = new();
    private object? _dataSource;
    private int _current = -1;
    private int _anchor = -1;
    private int _hot = -1;
    private int _itemHeight = 28;
    private ListSelectionMode _selectionMode = ListSelectionMode.One;
    private string _displayMember = string.Empty;
    private string _valueMember = string.Empty;
    private Func<object, bool>? _filter;
    private Comparison<object>? _sort;
    private string _typed = string.Empty;
    private long _typedAt;

    public EnterpriseListControl()
    {
        Size = new Size(220, 240);
        AccessibleRole = AccessibleRole.List;
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler<int>? ItemActivate;
    public event EventHandler<int>? ItemClick;
    public event EventHandler<ListItemFormattingEventArgs>? ItemFormatting;

    private int _downPos = -1;

    public string EmptyText { get; set; } = "No items";

    public string DisplayMember
    {
        get => _displayMember;
        set { _displayMember = value ?? string.Empty; Invalidate(); }
    }

    public string ValueMember
    {
        get => _valueMember;
        set => _valueMember = value ?? string.Empty;
    }

    public int ItemHeight
    {
        get => _itemHeight;
        set { _itemHeight = Math.Max(16, value); UpdateScrollBars(); }
    }

    public ListSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode == value) return;
            _selectionMode = value;
            if (value == ListSelectionMode.None || (value == ListSelectionMode.One && _selected.Count > 1))
            {
                var keep = value == ListSelectionMode.None || _current < 0 ? -1 : _view[_current];
                _selected.Clear();
                if (keep >= 0) _selected.Add(keep);
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Binds to a list, binding list or observable collection; the control keeps a snapshot and follows change notifications.</summary>
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value)) return;
            if (_dataSource is INotifyCollectionChanged oldNotify) oldNotify.CollectionChanged -= OnSourceChanged;
            if (_dataSource is IBindingList oldBinding) oldBinding.ListChanged -= OnSourceListChanged;
            _dataSource = value;
            if (value is INotifyCollectionChanged notify) notify.CollectionChanged += OnSourceChanged;
            if (value is IBindingList binding) binding.ListChanged += OnSourceListChanged;
            ReloadFromSource();
        }
    }

    public IReadOnlyList<object> Items => _items;
    public int Count => _items.Count;
    public int VisibleCount => _view.Count;

    public Func<object, bool>? Filter
    {
        get => _filter;
        set { _filter = value; RefreshView(); }
    }

    public Comparison<object>? SortComparison
    {
        get => _sort;
        set { _sort = value; RefreshView(); }
    }

    protected override Size ContentSize => new(0, _view.Count * ItemPixelHeight);
    protected override int LineHeight => ItemPixelHeight;
    protected int ItemPixelHeight => S(_itemHeight);

    public void AddItem(object item)
    {
        _items.Add(item);
        RefreshView();
    }

    public void AddRange(IEnumerable<object> items)
    {
        _items.AddRange(items);
        RefreshView();
    }

    public void ClearItems()
    {
        _items.Clear();
        _selected.Clear();
        _current = _anchor = -1;
        OnItemsReset();
        RefreshView();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Marshal(ReloadFromSource);
    private void OnSourceListChanged(object? sender, ListChangedEventArgs e) => Marshal(ReloadFromSource);

    private void Marshal(Action action)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
        else action();
    }

    private void ReloadFromSource()
    {
        var oldSelection = _selected.Select(i => i < _items.Count ? _items[i] : null).Where(i => i is not null).ToList();
        _items.Clear();
        if (_dataSource is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is not null) _items.Add(item);
            }
        }

        OnItemsReset();
        _selected.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            if (oldSelection.Any(o => ReferenceEquals(o, _items[i]))) _selected.Add(i);
        }

        _current = _anchor = -1;
        RefreshView();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hook for subclasses that keep per-item state to drop it when the items are replaced.</summary>
    protected virtual void OnItemsReset()
    {
    }

    /// <summary>Re-applies filter and sort; call after changing the items in place.</summary>
    public void RefreshView()
    {
        var currentSource = _current >= 0 && _current < _view.Count ? _view[_current] : -1;
        IEnumerable<int> indexes = Enumerable.Range(0, _items.Count);
        if (_filter is not null) indexes = indexes.Where(i => _filter(_items[i]));
        if (_sort is not null)
        {
            var sort = _sort;
            indexes = indexes.OrderBy(i => _items[i], Comparer<object>.Create(sort));
        }

        _view = indexes.ToList();
        _current = currentSource < 0 ? -1 : _view.IndexOf(currentSource);
        _hot = -1;
        UpdateScrollBars();
        Invalidate();
    }

    // ---- selection ----------------------------------------------------------------------

    public int SelectedIndex
    {
        get => _selected.Count == 0 ? -1 : _selected.Min();
        set
        {
            var next = value >= 0 && value < _items.Count ? value : -1;
            if (_selectionMode == ListSelectionMode.None) next = -1;
            if (_selected.Count == (next < 0 ? 0 : 1) && (next < 0 || _selected.Contains(next))) return;
            _selected.Clear();
            if (next >= 0)
            {
                _selected.Add(next);
                _current = _anchor = _view.IndexOf(next);
                if (_current >= 0) EnsureRowVisible(_current);
            }

            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedItem
    {
        get => SelectedIndex is var i && i >= 0 ? _items[i] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    public object? SelectedValue
    {
        get => SelectedItem is { } item ? MemberAccess.Read(item, _valueMember) : null;
        set
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (Equals(MemberAccess.Read(_items[i], _valueMember), value))
                {
                    SelectedIndex = i;
                    return;
                }
            }

            SelectedIndex = -1;
        }
    }

    public IReadOnlyList<int> SelectedIndices => _selected.OrderBy(i => i).ToList();
    public IReadOnlyList<object> SelectedItems => _selected.OrderBy(i => i).Select(i => _items[i]).ToList();

    public void SelectAll()
    {
        if (_selectionMode is not (ListSelectionMode.MultiSimple or ListSelectionMode.MultiExtended)) return;
        foreach (var i in _view) _selected.Add(i);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetItemText(int index) => index >= 0 && index < _items.Count ? MemberAccess.Text(_items[index], _displayMember) : string.Empty;

    public int IndexFromPoint(Point location)
    {
        var view = ViewPort;
        if (!view.Contains(location) || _view.Count == 0) return -1;
        var pos = (location.Y - view.Y + ScrollY) / ItemPixelHeight;
        return pos >= 0 && pos < _view.Count ? _view[pos] : -1;
    }

    protected bool IsSelected(int sourceIndex) => _selected.Contains(sourceIndex);
    protected int CurrentSourceIndex => _current >= 0 && _current < _view.Count ? _view[_current] : -1;

    protected void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    private void EnsureRowVisible(int pos) =>
        EnsureVisible(new Rectangle(0, pos * ItemPixelHeight, 1, ItemPixelHeight));

    private void Select(int pos, bool extend, bool toggle)
    {
        if (pos < 0 || pos >= _view.Count || _selectionMode == ListSelectionMode.None) return;
        var source = _view[pos];
        var before = _selected.OrderBy(i => i).ToArray();

        if (_selectionMode == ListSelectionMode.One)
        {
            _selected.Clear();
            _selected.Add(source);
            _anchor = pos;
        }
        else if (_selectionMode == ListSelectionMode.MultiSimple || toggle)
        {
            if (!_selected.Remove(source)) _selected.Add(source);
            _anchor = pos;
        }
        else if (extend && _anchor >= 0)
        {
            _selected.Clear();
            for (var i = Math.Min(_anchor, pos); i <= Math.Max(_anchor, pos); i++) _selected.Add(_view[i]);
        }
        else
        {
            _selected.Clear();
            _selected.Add(source);
            _anchor = pos;
        }

        _current = pos;
        EnsureRowVisible(pos);
        Invalidate();
        if (!before.SequenceEqual(_selected.OrderBy(i => i))) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- painting -----------------------------------------------------------------------

    /// <summary>Horizontal space reserved in front of the text, for example for a check box.</summary>
    protected virtual int LeadWidth => 0;

    protected virtual void PaintLead(Graphics g, Rectangle row, int sourceIndex, bool hovered)
    {
    }

    /// <summary>Called before selection on a click; return true when the click was consumed.</summary>
    protected virtual bool OnItemMouseDown(int sourceIndex, Rectangle row, Point location) => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;
        g.SetClip(view);

        if (_view.Count == 0)
        {
            Chrome.DrawText(g, EmptyText, Font, view, Colors.Muted, ContentAlignment.MiddleCenter);
            return;
        }

        var h = ItemPixelHeight;
        var first = Math.Max(0, ScrollY / h);
        var last = Math.Min(_view.Count - 1, (ScrollY + view.Height) / h);
        for (var pos = first; pos <= last; pos++)
        {
            var row = new Rectangle(view.X, view.Y + pos * h - ScrollY, view.Width, h);
            PaintItem(g, row, pos);
        }
    }

    private void PaintItem(Graphics g, Rectangle row, int pos)
    {
        var source = _view[pos];
        var selected = _selected.Contains(source);
        var hovered = pos == _hot && Enabled;
        var text = MemberAccess.Text(_items[source], _displayMember);
        var args = new ListItemFormattingEventArgs(source, _items[source], text);
        ItemFormatting?.Invoke(this, args);

        var cell = Rectangle.Inflate(row, -S(3), -S(1));
        var back = args.BackColor ?? (selected || hovered ? Chrome.ItemBack(Colors, selected, hovered) : (Color?)null);
        if (back is { } fill) Chrome.FillRounded(g, cell, fill, S(3));
        if (selected) Chrome.DrawSelectionBar(g, cell, Colors, S(3));

        if (Focused && pos == _current)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(cell, -1, -1), S(3));
            using var pen = new Pen(Colors.FocusRing, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawPath(pen, path);
        }

        PaintLead(g, cell, source, hovered);
        var textBounds = new Rectangle(cell.X + S(10) + LeadWidth, cell.Y, Math.Max(0, cell.Width - S(18) - LeadWidth), cell.Height);
        var color = !Enabled ? Colors.Disabled : args.ForeColor ?? (selected ? Colors.SelectionText : Colors.Text);
        Chrome.DrawText(g, args.Text, args.Bold ? BoldFont : Font, textBounds, color);
    }

    // ---- mouse --------------------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = PositionFromPoint(e.Location);
        if (pos != _hot)
        {
            _hot = pos;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot >= 0)
        {
            _hot = -1;
            Invalidate();
        }
    }

    private int PositionFromPoint(Point location)
    {
        var view = ViewPort;
        if (!view.Contains(location) || _view.Count == 0) return -1;
        var pos = (location.Y - view.Y + ScrollY) / ItemPixelHeight;
        return pos >= 0 && pos < _view.Count ? pos : -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var pos = PositionFromPoint(e.Location);
        _downPos = e.Button == MouseButtons.Left ? pos : -1;
        if (pos < 0 || !Enabled) return;
        var source = _view[pos];
        var view = ViewPort;
        var row = new Rectangle(view.X, view.Y + pos * ItemPixelHeight - ScrollY, view.Width, ItemPixelHeight);
        if (e.Button == MouseButtons.Left && OnItemMouseDown(source, row, e.Location))
        {
            _current = pos;
            Invalidate();
            return;
        }

        if (e.Button == MouseButtons.Right && _selected.Contains(source)) return;
        Select(pos, (ModifierKeys & Keys.Shift) == Keys.Shift, (ModifierKeys & Keys.Control) == Keys.Control);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var pos = PositionFromPoint(e.Location);
        if (e.Button == MouseButtons.Left && pos >= 0 && pos == _downPos) ItemClick?.Invoke(this, _view[pos]);
        _downPos = -1;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var pos = PositionFromPoint(e.Location);
        if (pos >= 0 && e.Button == MouseButtons.Left) ItemActivate?.Invoke(this, _view[pos]);
    }

    // ---- keyboard -----------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_view.Count == 0) return;
        var page = Math.Max(1, ViewPort.Height / ItemPixelHeight - 1);
        var start = _current < 0 ? 0 : _current;
        var target = e.KeyCode switch
        {
            Keys.Down => _current < 0 ? 0 : _current + 1,
            Keys.Up => _current < 0 ? 0 : _current - 1,
            Keys.PageDown => start + page,
            Keys.PageUp => start - page,
            Keys.Home => 0,
            Keys.End => _view.Count - 1,
            _ => -2
        };

        if (target != -2)
        {
            target = Math.Clamp(target, 0, _view.Count - 1);
            if (e.Control && _selectionMode == ListSelectionMode.MultiExtended)
            {
                _current = target;
                EnsureRowVisible(target);
                Invalidate();
            }
            else
            {
                Select(target, e.Shift, false);
            }

            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Space when _current >= 0 && !OnItemKey(_view[_current], e):
                if (_selectionMode is ListSelectionMode.MultiSimple or ListSelectionMode.MultiExtended) Select(_current, false, true);
                else Select(_current, false, false);
                e.Handled = true;
                break;
            case Keys.Enter when _current >= 0:
                ItemActivate?.Invoke(this, _view[_current]);
                e.Handled = true;
                break;
            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Hook for space handling in subclasses; return true when consumed.</summary>
    protected virtual bool OnItemKey(int sourceIndex, KeyEventArgs e) => false;

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (char.IsControl(e.KeyChar) || e.KeyChar == ' ' && _typed.Length == 0 || _view.Count == 0) return;
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_typedAt, now).TotalMilliseconds > 800) _typed = string.Empty;
        _typedAt = now;
        _typed += e.KeyChar;

        var startAt = _typed.Length == 1 && _current >= 0 ? _current + 1 : Math.Max(0, _current);
        for (var n = 0; n < _view.Count; n++)
        {
            var pos = (startAt + n) % _view.Count;
            if (GetItemText(_view[pos]).StartsWith(_typed, StringComparison.CurrentCultureIgnoreCase))
            {
                Select(pos, false, false);
                break;
            }
        }

        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_dataSource is INotifyCollectionChanged notify) notify.CollectionChanged -= OnSourceChanged;
            if (_dataSource is IBindingList binding) binding.ListChanged -= OnSourceListChanged;
        }

        base.Dispose(disposing);
    }
}

/// <summary>List with a check box in front of every item.</summary>
public class EnterpriseCheckedListBoxControl : EnterpriseListControl
{
    private readonly HashSet<int> _checked = new();

    public EnterpriseCheckedListBoxControl()
    {
        AccessibleRole = AccessibleRole.List;
    }

    public event ItemCheckEventHandler? ItemCheck;

    /// <summary>When true a click on the row toggles the check box, not only a click on the box.</summary>
    public bool CheckOnClick { get; set; } = true;

    public IReadOnlyList<int> CheckedIndices => _checked.OrderBy(i => i).ToList();
    public IReadOnlyList<object> CheckedItems => _checked.OrderBy(i => i).Select(i => Items[i]).ToList();

    protected override int LeadWidth => S(24);

    public bool GetItemChecked(int index) => _checked.Contains(index);

    public void SetItemChecked(int index, bool value)
    {
        if (index < 0 || index >= Count || _checked.Contains(index) == value) return;
        var args = new ItemCheckEventArgs(index, value ? CheckState.Checked : CheckState.Unchecked, value ? CheckState.Unchecked : CheckState.Checked);
        ItemCheck?.Invoke(this, args);
        if (args.NewValue == args.CurrentValue) return;
        if (args.NewValue == CheckState.Checked) _checked.Add(index);
        else _checked.Remove(index);
        Invalidate();
    }

    public void CheckAll()
    {
        for (var i = 0; i < Count; i++) SetItemChecked(i, true);
    }

    public void UncheckAll()
    {
        for (var i = 0; i < Count; i++) SetItemChecked(i, false);
    }

    public void InvertChecks()
    {
        for (var i = 0; i < Count; i++) SetItemChecked(i, !GetItemChecked(i));
    }

    protected override void OnItemsReset() => _checked.Clear();

    private Rectangle BoxRect(Rectangle row)
    {
        var size = S(16);
        return new Rectangle(row.X + S(8), row.Y + (row.Height - size) / 2, size, size);
    }

    protected override void PaintLead(Graphics g, Rectangle row, int sourceIndex, bool hovered)
    {
        Chrome.DrawCheckBox(g, Colors, BoxRect(row), _checked.Contains(sourceIndex) ? CheckState.Checked : CheckState.Unchecked, Enabled, hovered);
    }

    protected override bool OnItemMouseDown(int sourceIndex, Rectangle row, Point location)
    {
        var onBox = Rectangle.Inflate(BoxRect(row), S(4), S(4)).Contains(location);
        if (!onBox && !CheckOnClick) return false;
        SetItemChecked(sourceIndex, !GetItemChecked(sourceIndex));
        return onBox;
    }

    protected override bool OnItemKey(int sourceIndex, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Space) return false;
        SetItemChecked(sourceIndex, !GetItemChecked(sourceIndex));
        return true;
    }
}
