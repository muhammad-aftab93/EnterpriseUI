using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

/// <summary>Entry of a navigation pane, compact navigation bar or backstage menu: a destination, a section header, a separator or a group with children.</summary>
public sealed class EnterpriseNavigationItem
{
    private string _text;
    private string? _glyph;
    private string? _badge;
    private bool _enabled = true;
    private bool _expanded;

    public EnterpriseNavigationItem(string text, string? glyph = null, object? tag = null, string? badge = null)
    {
        _text = text ?? string.Empty;
        _glyph = glyph;
        Tag = tag;
        _badge = badge;
        Children = new EnterpriseNavigationItemCollection(this, () => Changed?.Invoke());
    }

    internal Action? Changed { get; set; }

    public string Text { get => _text; set { _text = value ?? string.Empty; Changed?.Invoke(); } }

    /// <summary>Single character or symbol drawn as the icon.</summary>
    public string? Glyph { get => _glyph; set { _glyph = value; Changed?.Invoke(); } }

    /// <summary>Short count or status shown as a pill (for example "3" or "New").</summary>
    public string? Badge { get => _badge; set { _badge = value; Changed?.Invoke(); } }

    public string? ToolTip { get; set; }
    public string? Key { get; set; }
    public object? Tag { get; set; }
    public bool IsEnabled { get => _enabled; set { _enabled = value; Changed?.Invoke(); } }
    public bool IsExpanded { get => _expanded; set { _expanded = value; Changed?.Invoke(); } }

    /// <summary>A non selectable caption that starts a section.</summary>
    public bool IsHeader { get; set; }

    public bool IsSeparator { get; set; }

    /// <summary>Command items raise <c>ItemInvoked</c> but never become the selected item (settings, tools, toggles).</summary>
    public bool IsCommand { get; set; }

    public EnterpriseNavigationItem? Parent { get; internal set; }

    public EnterpriseNavigationItemCollection Children { get; }

    public static EnterpriseNavigationItem Header(string text) => new(text) { IsHeader = true };

    public static EnterpriseNavigationItem Separator() => new(string.Empty) { IsSeparator = true };

    public bool IsSelectable => !IsHeader && !IsSeparator && IsEnabled;

    public IEnumerable<EnterpriseNavigationItem> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var nested in child.Descendants()) yield return nested;
        }
    }

    public override string ToString() => Text;
}

public sealed class EnterpriseNavigationItemCollection : Collection<EnterpriseNavigationItem>
{
    private readonly EnterpriseNavigationItem? _parent;
    private readonly Action _changed;

    internal EnterpriseNavigationItemCollection(EnterpriseNavigationItem? parent, Action changed)
    {
        _parent = parent;
        _changed = changed;
    }

    public EnterpriseNavigationItem Add(string text, string? glyph = null, object? tag = null, string? badge = null)
    {
        var item = new EnterpriseNavigationItem(text, glyph, tag, badge);
        Add(item);
        return item;
    }

    public EnterpriseNavigationItem? Find(string key) =>
        this.Concat(this.SelectMany(i => i.Descendants())).FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));

    protected override void InsertItem(int index, EnterpriseNavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Attach(item);
        base.InsertItem(index, item);
        _changed();
    }

    protected override void SetItem(int index, EnterpriseNavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Detach(this[index]);
        Attach(item);
        base.SetItem(index, item);
        _changed();
    }

    protected override void RemoveItem(int index)
    {
        Detach(this[index]);
        base.RemoveItem(index);
        _changed();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) Detach(item);
        base.ClearItems();
        _changed();
    }

    private void Attach(EnterpriseNavigationItem item)
    {
        item.Parent = _parent;
        item.Changed = _changed;
    }

    private static void Detach(EnterpriseNavigationItem item)
    {
        item.Parent = null;
        item.Changed = null;
    }
}

public sealed class EnterpriseNavigationEventArgs : EventArgs
{
    public EnterpriseNavigationEventArgs(EnterpriseNavigationItem item) => Item = item;

    public EnterpriseNavigationItem Item { get; }
}

public enum EnterpriseNavigationDisplayMode
{
    /// <summary>Icons with labels.</summary>
    Expanded,
    /// <summary>Icon rail; labels appear as tooltips and groups open as fly-outs.</summary>
    Compact,
    /// <summary>Compact while the parent is narrower than <see cref="EnterpriseNavigationPane.AutoCompactBreakpoint"/>.</summary>
    Auto
}

/// <summary>Side navigation: collapsible icon rail, sections, nested groups with badges, pinned footer items, fly-outs and full keyboard and screen reader support.</summary>
[DefaultEvent(nameof(SelectedItemChanged))]
[DefaultProperty(nameof(Items))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseControlDesigner<EnterpriseUI.WinForms.Design.NavigationPaneActions>))]
[ToolboxItem(true)]
public class EnterpriseNavigationPane : EnterpriseControlBase
{
    private sealed record Row(EnterpriseNavigationItem Item, Rectangle Bounds, int Level, bool Footer);

    private readonly EnterpriseNavigationItemCollection _items;
    private readonly EnterpriseNavigationItemCollection _footer;
    private readonly List<Row> _rows = new();
    private readonly EnterpriseScrollBar _scroll = new(Orientation.Vertical) { Visible = false, TabStop = false, Width = 8 };
    private readonly ToolTip _tip = new() { ShowAlways = true };
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private EnterpriseNavigationItem? _selected;
    private Row? _hot;
    private int _focusIndex = -1;
    private bool _toggleHot;
    private EnterpriseNavigationDisplayMode _mode = EnterpriseNavigationDisplayMode.Expanded;
    private int _expandedWidth = 240;
    private int _compactWidth = 48;
    private int _itemHeight = 40;
    private int _autoBreakpoint = 800;
    private string _title = string.Empty;
    private bool _showToggle = true;
    private bool _effectiveCompact;
    private int _targetWidth;
    private Control? _watchedParent;
    private string? _tipText;

    public EnterpriseNavigationPane()
    {
        _items = new EnterpriseNavigationItemCollection(null, OnItemsChanged);
        _footer = new EnterpriseNavigationItemCollection(null, OnItemsChanged);
        Size = new Size(_expandedWidth, 360);
        Dock = DockStyle.Left;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Navigation";
        _scroll.ValueChanged += (_, _) => Invalidate();
        _scroll.ApplyColors(Colors);
        Controls.Add(_scroll);
        _animation.Tick += OnAnimationTick;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public EnterpriseNavigationItemCollection Items => _items;

    /// <summary>Items pinned to the bottom of the pane (settings, account).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public EnterpriseNavigationItemCollection FooterItems => _footer;

    [DefaultValue(EnterpriseNavigationDisplayMode.Expanded)]
    public EnterpriseNavigationDisplayMode DisplayMode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            EvaluateCompact(false);
            DisplayModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>True while the pane shows only the icon rail.</summary>
    [Browsable(false)]
    public bool IsCompact => _effectiveCompact;

    [DefaultValue(240)]
    public int ExpandedWidth { get => _expandedWidth; set { _expandedWidth = Math.Max(120, value); EvaluateCompact(true); } }

    [DefaultValue(48)]
    public int CompactWidth { get => _compactWidth; set { _compactWidth = Math.Max(32, value); EvaluateCompact(true); } }

    [DefaultValue(40)]
    public int ItemHeight { get => _itemHeight; set { _itemHeight = Math.Clamp(value, 24, 72); Relayout(); } }

    [DefaultValue(800)]
    public int AutoCompactBreakpoint { get => _autoBreakpoint; set { _autoBreakpoint = value; EvaluateCompact(false); } }

    [DefaultValue("")]
    public string Title { get => _title; set { _title = value ?? string.Empty; Relayout(); } }

    [DefaultValue(true)]
    public bool ShowToggleButton { get => _showToggle; set { _showToggle = value; Relayout(); } }

    [DefaultValue(true)]
    public bool AnimatePaneChange { get; set; } = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseNavigationItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            for (var p = value?.Parent; p is not null; p = p.Parent) p.IsExpanded = true;
            Invalidate();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.Selection, -1);
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedItemChanged;
    public event EventHandler<EnterpriseNavigationEventArgs>? ItemInvoked;
    public event EventHandler? DisplayModeChanged;

    public void ToggleCompact() => DisplayMode = _effectiveCompact ? EnterpriseNavigationDisplayMode.Expanded : EnterpriseNavigationDisplayMode.Compact;

    public bool SelectKey(string key)
    {
        var item = _items.Find(key) ?? _footer.Find(key);
        if (item is null || !item.IsSelectable) return false;
        Invoke(item);
        return true;
    }

    private void OnItemsChanged()
    {
        if (IsDisposed) return;
        Relayout();
    }

    private int HeaderHeight => _showToggle || _title.Length > 0 ? S(44) : 0;

    private Rectangle BodyBounds
    {
        get
        {
            var footer = _rows.Where(r => r.Footer).Sum(r => r.Bounds.Height);
            return new Rectangle(0, HeaderHeight, Width - 1, Math.Max(0, Height - HeaderHeight - footer));
        }
    }

    private Rectangle ToggleBounds => new(S(4), S(4), S(40), S(36));

    private int ScrollOffset => _scroll.Visible ? _scroll.Value : 0;

    private void Relayout()
    {
        _rows.Clear();
        if (Width <= 0 || Height <= 0 || IsDisposed) return;
        var rowH = S(_itemHeight);
        var y = 0;
        void AddRows(IEnumerable<EnterpriseNavigationItem> source, int level)
        {
            foreach (var item in source)
            {
                if (_effectiveCompact && item.IsHeader)
                {
                    _rows.Add(new Row(item, new Rectangle(0, y, Width, S(9)), level, false));
                    y += S(9);
                    continue;
                }

                var h = item.IsSeparator ? S(9) : item.IsHeader ? S(32) : rowH;
                _rows.Add(new Row(item, new Rectangle(0, y, Width, h), level, false));
                y += h;
                if (!_effectiveCompact && item.IsExpanded && item.Children.Count > 0) AddRows(item.Children, level + 1);
            }
        }

        AddRows(_items, 0);
        var contentHeight = y;

        var footerHeight = _footer.Sum(f => f.IsSeparator ? S(9) : rowH);
        var fy = Height - footerHeight;
        var footerRows = new List<Row>();
        foreach (var item in _footer)
        {
            var h = item.IsSeparator ? S(9) : rowH;
            footerRows.Add(new Row(item, new Rectangle(0, fy, Width, h), 0, true));
            fy += h;
        }

        _rows.AddRange(footerRows);
        var body = BodyBounds;
        var needScroll = contentHeight > body.Height && body.Height > 0;
        _scroll.Visible = needScroll;
        if (needScroll)
        {
            _scroll.SetBounds(Width - 1 - S(8), body.Y, S(8), body.Height);
            _scroll.Minimum = 0;
            _scroll.Maximum = Math.Max(0, contentHeight - 1);
            _scroll.LargeChange = Math.Max(1, body.Height);
            _scroll.SmallChange = rowH;
            _scroll.BringToFront();
        }
        else
        {
            _scroll.Value = 0;
        }

        if (_focusIndex >= _rows.Count) _focusIndex = _rows.Count - 1;
        Invalidate();
    }

    private Rectangle ScreenRowBounds(Row row) => row.Footer ? row.Bounds : new Rectangle(row.Bounds.X, row.Bounds.Y + BodyBounds.Y - ScrollOffset, row.Bounds.Width, row.Bounds.Height);

    private Row? HitTest(Point point)
    {
        var body = BodyBounds;
        foreach (var row in _rows)
        {
            if (row.Item.IsSeparator || row.Item.IsHeader) continue;
            var bounds = ScreenRowBounds(row);
            if (!row.Footer && (bounds.Bottom <= body.Y || bounds.Y >= body.Bottom)) continue;
            if (bounds.Contains(point)) return row;
        }

        return null;
    }

    // ---- compact / expanded handling ----

    private bool ComputeCompact() => _mode switch
    {
        EnterpriseNavigationDisplayMode.Compact => true,
        EnterpriseNavigationDisplayMode.Expanded => false,
        _ => (Parent?.ClientSize.Width ?? int.MaxValue) < S(_autoBreakpoint)
    };

    private bool DockedHorizontally => Dock is DockStyle.Left or DockStyle.Right;

    private void EvaluateCompact(bool force)
    {
        var compact = ComputeCompact();
        if (compact == _effectiveCompact && !force)
        {
            return;
        }

        _effectiveCompact = compact;
        _targetWidth = S(compact ? _compactWidth : _expandedWidth);
        if (DockedHorizontally || Dock == DockStyle.None && !DesignMode && Parent is not null)
        {
            if (AnimatePaneChange && IsHandleCreated && !DesignMode) _animation.Start();
            else Width = _targetWidth;
        }

        Relayout();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var diff = _targetWidth - Width;
        if (Math.Abs(diff) <= 2)
        {
            Width = _targetWidth;
            _animation.Stop();
            return;
        }

        Width += (int)Math.Ceiling(Math.Abs(diff) * 0.3) * Math.Sign(diff);
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (_watchedParent is not null) _watchedParent.SizeChanged -= OnParentSizeChanged;
        _watchedParent = Parent;
        if (_watchedParent is not null) _watchedParent.SizeChanged += OnParentSizeChanged;
        EvaluateCompact(true);
    }

    private void OnParentSizeChanged(object? sender, EventArgs e)
    {
        if (_mode == EnterpriseNavigationDisplayMode.Auto) EvaluateCompact(false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _effectiveCompact = ComputeCompact();
        _targetWidth = S(_effectiveCompact ? _compactWidth : _expandedWidth);
        if (DockedHorizontally) Width = _targetWidth;
        Relayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _targetWidth = S(_effectiveCompact ? _compactWidth : _expandedWidth);
        if (DockedHorizontally) Width = _targetWidth;
        Relayout();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _scroll.ApplyColors(Colors);
    }

    // ---- input ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        var toggle = _showToggle && ToggleBounds.Contains(e.Location);
        if (hit?.Item != _hot?.Item || toggle != _toggleHot)
        {
            _hot = hit;
            _toggleHot = toggle;
            Cursor = hit is not null || toggle ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        var text = hit is null ? null : _effectiveCompact ? (hit.Item.ToolTip ?? hit.Item.Text) : hit.Item.ToolTip;
        if (text != _tipText)
        {
            _tipText = text;
            _tip.SetToolTip(this, text);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        _toggleHot = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (_showToggle && ToggleBounds.Contains(e.Location))
        {
            ToggleCompact();
            return;
        }

        var hit = HitTest(e.Location);
        if (hit is null) return;
        _focusIndex = _rows.IndexOf(hit);
        Activate(hit);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scroll.Visible) _scroll.Value -= e.Delta / 120 * S(_itemHeight);
    }

    private void Activate(Row row)
    {
        var item = row.Item;
        if (!item.IsSelectable) return;
        if (item.Children.Count > 0)
        {
            if (_effectiveCompact) ShowFlyout(row);
            else item.IsExpanded = !item.IsExpanded;
            return;
        }

        Invoke(item);
    }

    private void Invoke(EnterpriseNavigationItem item)
    {
        if (!item.IsCommand) SelectedItem = item;
        ItemInvoked?.Invoke(this, new EnterpriseNavigationEventArgs(item));
    }

    private void ShowFlyout(Row row)
    {
        var menu = new ContextMenuStrip();
        EnterpriseMenuRenderer.Apply(menu);
        menu.Items.Add(new ToolStripLabel(row.Item.Text) { Font = BoldFont, ForeColor = Colors.Muted, Enabled = false });
        void Fill(ToolStripItemCollection target, IEnumerable<EnterpriseNavigationItem> source)
        {
            foreach (var child in source)
            {
                if (child.IsSeparator) { target.Add(new ToolStripSeparator()); continue; }
                var entry = new ToolStripMenuItem(child.Text) { Enabled = child.IsEnabled, Checked = child == _selected };
                if (child.Children.Count > 0) Fill(entry.DropDownItems, child.Children);
                else entry.Click += (_, _) => Invoke(child);
                target.Add(entry);
            }
        }

        Fill(menu.Items, row.Item.Children);
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        var bounds = ScreenRowBounds(row);
        menu.Show(this, new Point(bounds.Right, bounds.Y));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var selectable = _rows.Where(r => r.Item.IsSelectable).ToList();
        if (selectable.Count == 0) return;
        var current = _focusIndex >= 0 && _focusIndex < _rows.Count ? _rows[_focusIndex] : null;
        var position = current is null ? -1 : selectable.IndexOf(current);
        switch (e.KeyCode)
        {
            case Keys.Down: position = Math.Min(selectable.Count - 1, position + 1); break;
            case Keys.Up: position = Math.Max(0, position < 0 ? 0 : position - 1); break;
            case Keys.Home: position = 0; break;
            case Keys.End: position = selectable.Count - 1; break;
            case Keys.Right when current?.Item.Children.Count > 0 && !_effectiveCompact: current.Item.IsExpanded = true; e.Handled = true; return;
            case Keys.Left when current?.Item.Children.Count > 0 && current.Item.IsExpanded: current.Item.IsExpanded = false; e.Handled = true; return;
            case Keys.Left when current?.Item.Parent is { } parent: _focusIndex = _rows.FindIndex(r => r.Item == parent); EnsureVisibleRow(_focusIndex); Invalidate(); e.Handled = true; return;
            case Keys.Enter or Keys.Space when current is not null: Activate(current); e.Handled = true; return;
            default: return;
        }

        _focusIndex = _rows.IndexOf(selectable[position]);
        EnsureVisibleRow(_focusIndex);
        e.Handled = true;
        Invalidate();
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.Focus, _focusIndex);
    }

    private void EnsureVisibleRow(int index)
    {
        if (index < 0 || index >= _rows.Count || !_scroll.Visible) return;
        var row = _rows[index];
        if (row.Footer) return;
        var body = BodyBounds;
        if (row.Bounds.Y < _scroll.Value) _scroll.Value = row.Bounds.Y;
        else if (row.Bounds.Bottom > _scroll.Value + body.Height) _scroll.Value = row.Bounds.Bottom - body.Height;
    }

    // ---- painting ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Header);
        using (var border = new Pen(c.Border)) g.DrawLine(border, Width - 1, 0, Width - 1, Height);

        var body = BodyBounds;
        var state = g.Save();
        g.SetClip(body);
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Footer) PaintRow(g, _rows[i], i);
        }

        g.Restore(state);
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Footer) PaintRow(g, _rows[i], i);
        }

        if (_rows.Any(r => r.Footer))
        {
            using var border = new Pen(c.Border);
            g.DrawLine(border, S(8), body.Bottom, Width - S(8), body.Bottom);
        }

        PaintHeader(g);
    }

    private void PaintHeader(Graphics g)
    {
        var height = HeaderHeight;
        if (height == 0) return;
        var c = Colors;
        using (var brush = new SolidBrush(c.Header)) g.FillRectangle(brush, 0, 0, Width - 1, height);
        if (_showToggle)
        {
            var toggle = ToggleBounds;
            if (_toggleHot) Chrome.FillRounded(g, toggle, c.HoverBack, Chrome.CornerRadius);
            using var pen = new Pen(c.Text, Math.Max(1.6f, S(1.6))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = toggle.X + toggle.Width / 2;
            var cy = toggle.Y + toggle.Height / 2;
            var half = S(8);
            for (var k = -1; k <= 1; k++) g.DrawLine(pen, cx - half, cy + k * S(5), cx + half, cy + k * S(5));
            if (Focused && _focusIndex < 0) Chrome.DrawFocusRing(g, toggle, c);
        }

        if (!_effectiveCompact && _title.Length > 0)
        {
            var x = _showToggle ? ToggleBounds.Right + S(6) : S(14);
            Chrome.DrawText(g, _title, BoldFont, new Rectangle(x, 0, Math.Max(0, Width - x - S(8)), height), c.Text);
        }
    }

    private void PaintRow(Graphics g, Row row, int index)
    {
        var c = Colors;
        var item = row.Item;
        var bounds = ScreenRowBounds(row);
        if (item.IsSeparator || (_effectiveCompact && item.IsHeader))
        {
            using var pen = new Pen(c.Border);
            g.DrawLine(pen, S(8), bounds.Y + bounds.Height / 2, Width - S(9), bounds.Y + bounds.Height / 2);
            return;
        }

        if (item.IsHeader)
        {
            Chrome.DrawText(g, item.Text.ToUpperInvariant(), Font, new Rectangle(S(16), bounds.Y + S(8), Width - S(24), bounds.Height - S(8)), c.Muted, ContentAlignment.MiddleLeft);
            return;
        }

        var selected = item == _selected || (_effectiveCompact && item.Children.Count > 0 && item.Descendants().Contains(_selected!));
        var hot = _hot?.Item == item;
        var rect = new Rectangle(S(4), bounds.Y + S(2), Width - S(9), bounds.Height - S(4));
        if (selected) Chrome.FillRounded(g, rect, c.SelectionBack, Chrome.CornerRadius);
        else if (hot && item.IsEnabled) Chrome.FillRounded(g, rect, c.HoverBack, Chrome.CornerRadius);
        if (selected) Chrome.DrawSelectionBar(g, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), c, S(3));
        if (Focused && index == _focusIndex) Chrome.DrawFocusRing(g, rect, c);

        var textColor = !item.IsEnabled ? c.Disabled : selected ? c.SelectionText : c.Text;
        var iconWidth = S(_compactWidth) - S(8);
        var indent = row.Level * S(18);
        var iconRect = new Rectangle(rect.X + indent, rect.Y, iconWidth - S(4), rect.Height);
        if (!string.IsNullOrEmpty(item.Glyph))
            TextRenderer.DrawText(g, item.Glyph, GlyphFonts.Get(Font.SizeInPoints * 1.2f), iconRect, !item.IsEnabled ? c.Disabled : selected ? c.Primary : c.Text,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (_effectiveCompact)
        {
            if (!string.IsNullOrEmpty(item.Badge)) DrawBadge(g, item.Badge!, new Rectangle(rect.Right - S(18), rect.Y + S(3), S(16), S(14)), true);
            return;
        }

        var textLeft = iconRect.Right + (string.IsNullOrEmpty(item.Glyph) ? -iconWidth + S(10) : S(2));
        var right = rect.Right - S(8);
        if (item.Children.Count > 0)
        {
            Chrome.DrawChevron(g, new Rectangle(right - S(16), rect.Y, S(16), rect.Height), item.IsExpanded ? ChevronDirection.Down : ChevronDirection.Right, c.Muted);
            right -= S(20);
        }

        if (!string.IsNullOrEmpty(item.Badge))
        {
            var width = Math.Max(S(20), Chrome.MeasureText(item.Badge!, Font) + S(12));
            DrawBadge(g, item.Badge!, new Rectangle(right - width, rect.Y + (rect.Height - S(20)) / 2, width, S(20)), false);
            right -= width + S(6);
        }

        Chrome.DrawText(g, item.Text, selected ? BoldFont : Font, new Rectangle(textLeft, rect.Y, Math.Max(0, right - textLeft), rect.Height), textColor);
    }

    private void DrawBadge(Graphics g, string text, Rectangle bounds, bool dot)
    {
        var c = Colors;
        Chrome.FillRounded(g, bounds, c.Primary, bounds.Height / 2);
        if (dot) return;
        Chrome.DrawText(g, text, Font, bounds, c.PrimaryText, ContentAlignment.MiddleCenter);
    }

    // ---- accessibility ----

    protected override AccessibleObject CreateAccessibilityInstance() => new PaneAccessible(this);

    private sealed class PaneAccessible : ControlAccessibleObject
    {
        private readonly EnterpriseNavigationPane _pane;

        public PaneAccessible(EnterpriseNavigationPane owner) : base(owner) => _pane = owner;

        private List<Row> Selectable => _pane._rows.Where(r => r.Item.IsSelectable || r.Item.IsHeader).ToList();

        public override int GetChildCount() => Selectable.Count;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < Selectable.Count ? new ItemAccessible(_pane, Selectable[index], this) : null;
    }

    private sealed class ItemAccessible : AccessibleObject
    {
        private readonly EnterpriseNavigationPane _pane;
        private readonly Row _row;
        private readonly AccessibleObject _parent;

        public ItemAccessible(EnterpriseNavigationPane pane, Row row, AccessibleObject parent)
        {
            _pane = pane;
            _row = row;
            _parent = parent;
        }

        public override string? Name { get => _row.Item.Text; set { } }
        public override AccessibleRole Role => _row.Item.IsHeader ? AccessibleRole.StaticText : _row.Item.Children.Count > 0 ? AccessibleRole.OutlineItem : AccessibleRole.ListItem;
        public override AccessibleObject? Parent => _parent;
        public override string? Description => _row.Item.Badge is { Length: > 0 } badge ? badge : null;
        public override string? DefaultAction => _row.Item.IsSelectable ? "Invoke" : null;
        public override Rectangle Bounds => _pane.RectangleToScreen(_pane.ScreenRowBounds(_row));
        public override AccessibleStates State
        {
            get
            {
                var state = AccessibleStates.Selectable | AccessibleStates.Focusable;
                if (_row.Item == _pane._selected) state |= AccessibleStates.Selected;
                if (!_row.Item.IsEnabled) state |= AccessibleStates.Unavailable;
                if (_row.Item.Children.Count > 0) state |= _row.Item.IsExpanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                return state;
            }
        }

        public override void DoDefaultAction()
        {
            if (_row.Item.IsSelectable) _pane.Activate(_row);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
            _tip.Dispose();
            if (_watchedParent is not null) _watchedParent.SizeChanged -= OnParentSizeChanged;
        }

        base.Dispose(disposing);
    }
}

public enum EnterpriseCompactNavOrientation { Horizontal, Vertical }

/// <summary>Compact switcher between a handful of top-level destinations (mail, calendar, people): labelled buttons that collapse to icons and an overflow menu.</summary>
[DefaultEvent(nameof(SelectedItemChanged))]
[ToolboxItem(true)]
public class EnterpriseCompactNavBar : EnterpriseControlBase
{
    private const string Ellipsis = "\u00B7\u00B7\u00B7";
    private readonly EnterpriseNavigationItemCollection _items;
    private readonly List<(EnterpriseNavigationItem Item, Rectangle Bounds)> _slots = new();
    private readonly ToolTip _tip = new() { ShowAlways = true };
    private Rectangle _overflow;
    private EnterpriseNavigationItem? _selected;
    private EnterpriseNavigationItem? _hot;
    private bool _hotOverflow;
    private bool _iconsOnly;
    private bool _forceIconsOnly;
    private EnterpriseCompactNavOrientation _orientation = EnterpriseCompactNavOrientation.Horizontal;

    public EnterpriseCompactNavBar()
    {
        _items = new EnterpriseNavigationItemCollection(null, Relayout);
        Size = new Size(360, 40);
        Dock = DockStyle.Bottom;
        AccessibleRole = AccessibleRole.PageTabList;
        AccessibleName = "Navigation bar";
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public EnterpriseNavigationItemCollection Items => _items;

    [DefaultValue(EnterpriseCompactNavOrientation.Horizontal)]
    public EnterpriseCompactNavOrientation Orientation { get => _orientation; set { _orientation = value; Relayout(); } }

    /// <summary>Always show icons only.</summary>
    [DefaultValue(false)]
    public bool IconsOnly { get => _forceIconsOnly; set { _forceIconsOnly = value; Relayout(); } }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterpriseNavigationItem? SelectedItem
    {
        get => _selected ?? _items.FirstOrDefault(i => i.IsSelectable);
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedItemChanged;

    private void Relayout()
    {
        _slots.Clear();
        _overflow = Rectangle.Empty;
        if (Width <= 0 || Height <= 0 || IsDisposed) return;
        var visible = _items.Where(i => !i.IsSeparator && !i.IsHeader).ToList();
        var pad = S(14);
        var vertical = _orientation == EnterpriseCompactNavOrientation.Vertical;
        var available = vertical ? Height : Width;
        var iconSlot = vertical ? S(44) : S(48);
        var labelWidths = visible.Select(i => Math.Max(S(40), Chrome.MeasureText(i.Text, Font) + pad * 2 + (string.IsNullOrEmpty(i.Glyph) ? 0 : S(24)))).ToList();
        _iconsOnly = _forceIconsOnly || vertical || labelWidths.Sum() > available;
        var sizes = _iconsOnly ? visible.Select(_ => iconSlot).ToList() : labelWidths;
        var reserve = S(40);
        var fit = visible.Count;
        var total = sizes.Sum();
        while (fit > 0 && total > available - (fit < visible.Count ? reserve : 0))
        {
            fit--;
            total -= sizes[fit];
        }

        var pos = 0;
        for (var i = 0; i < fit; i++)
        {
            _slots.Add((visible[i], vertical ? new Rectangle(0, pos, Width, sizes[i]) : new Rectangle(pos, 0, sizes[i], Height)));
            pos += sizes[i];
        }

        if (fit < visible.Count) _overflow = vertical ? new Rectangle(0, pos, Width, reserve) : new Rectangle(pos, 0, reserve, Height);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = _slots.Where(s => s.Bounds.Contains(e.Location)).Select(s => s.Item).FirstOrDefault();
        var overflow = _overflow.Contains(e.Location);
        if (hit == _hot && overflow == _hotOverflow) return;
        _hot = hit;
        _hotOverflow = overflow;
        Cursor = hit is not null || overflow ? Cursors.Hand : Cursors.Default;
        _tip.SetToolTip(this, hit is not null && _iconsOnly ? hit.Text : null);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        _hotOverflow = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (_overflow.Contains(e.Location))
        {
            ShowOverflow();
            return;
        }

        var hit = _slots.Where(s => s.Bounds.Contains(e.Location) && s.Item.IsEnabled).Select(s => s.Item).FirstOrDefault();
        if (hit is not null) SelectedItem = hit;
    }

    private void ShowOverflow()
    {
        var menu = new ContextMenuStrip();
        EnterpriseMenuRenderer.Apply(menu);
        var shown = _slots.Select(s => s.Item).ToHashSet();
        foreach (var item in _items.Where(i => i.IsSelectable && !shown.Contains(i)))
        {
            var target = item;
            var entry = new ToolStripMenuItem(item.Text) { Checked = item == SelectedItem };
            entry.Click += (_, _) => SelectedItem = target;
            menu.Items.Add(entry);
        }

        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(this, new Point(_overflow.X, _orientation == EnterpriseCompactNavOrientation.Horizontal ? _overflow.Y - menu.PreferredSize.Height : _overflow.Y));
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.KeyCode switch { Keys.Left or Keys.Up => -1, Keys.Right or Keys.Down => 1, _ => 0 };
        if (step == 0) return;
        var list = _slots.Select(s => s.Item).Where(i => i.IsEnabled).ToList();
        var index = list.IndexOf(SelectedItem!);
        if (list.Count > 0) SelectedItem = list[Math.Clamp(index + step, 0, list.Count - 1)];
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Header);
        using (var pen = new Pen(c.Border))
        {
            if (_orientation == EnterpriseCompactNavOrientation.Horizontal) g.DrawLine(pen, 0, 0, Width, 0);
            else g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }

        foreach (var (item, bounds) in _slots)
        {
            var selected = item == SelectedItem;
            var hot = item == _hot;
            var rect = Rectangle.Inflate(bounds, -S(2), -S(4));
            if (selected) Chrome.FillRounded(g, rect, c.SelectionBack, Chrome.CornerRadius);
            else if (hot && item.IsEnabled) Chrome.FillRounded(g, rect, c.HoverBack, Chrome.CornerRadius);
            if (selected)
            {
                using var brush = new SolidBrush(c.Primary);
                if (_orientation == EnterpriseCompactNavOrientation.Horizontal) g.FillRectangle(brush, rect.X + S(8), rect.Bottom - S(3), rect.Width - S(16), S(3));
                else g.FillRectangle(brush, rect.X, rect.Y + S(8), S(3), rect.Height - S(16));
            }

            var color = !item.IsEnabled ? c.Disabled : selected ? c.Primary : c.Text;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            if (_iconsOnly)
            {
                var glyph = string.IsNullOrEmpty(item.Glyph) ? (item.Text.Length > 0 ? item.Text[..1] : "?") : item.Glyph;
                TextRenderer.DrawText(g, glyph, GlyphFonts.Get(Font.SizeInPoints * 1.3f), rect, color, flags);
            }
            else
            {
                var textRect = rect;
                if (!string.IsNullOrEmpty(item.Glyph))
                {
                    TextRenderer.DrawText(g, item.Glyph, GlyphFonts.Get(Font.SizeInPoints * 1.15f), new Rectangle(rect.X + S(6), rect.Y, S(24), rect.Height), color, flags);
                    textRect = new Rectangle(rect.X + S(26), rect.Y, rect.Width - S(26), rect.Height);
                }

                Chrome.DrawText(g, item.Text, selected ? BoldFont : Font, textRect, color, ContentAlignment.MiddleCenter);
            }

            if (!string.IsNullOrEmpty(item.Badge))
            {
                var badge = new Rectangle(rect.Right - S(14), rect.Y + S(2), S(12), S(12));
                Chrome.FillRounded(g, badge, c.Danger, S(6));
            }

            if (Focused && selected) Chrome.DrawFocusRing(g, rect, c);
        }

        if (_overflow != Rectangle.Empty)
        {
            if (_hotOverflow) Chrome.FillRounded(g, Rectangle.Inflate(_overflow, -S(2), -S(4)), c.HoverBack, Chrome.CornerRadius);
            Chrome.DrawText(g, Ellipsis, BoldFont, _overflow, c.Text, ContentAlignment.MiddleCenter);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Full-window application menu: a command and page list on the left, the selected page on the right, and a back button that closes it.</summary>
[ToolboxItem(false)]
public class EnterpriseBackstageMenu : EnterpriseContainerBase
{
    private readonly EnterpriseNavigationPane _pane;
    private readonly Panel _content;
    private readonly Dictionary<EnterpriseNavigationItem, Action> _commands = new();
    private readonly Dictionary<EnterpriseNavigationItem, Control> _pages = new();
    private Control? _host;
    private Control? _activePage;

    public EnterpriseBackstageMenu()
    {
        Dock = DockStyle.Fill;
        Visible = false;
        _pane = new EnterpriseNavigationPane
        {
            Dock = DockStyle.Left,
            DisplayMode = EnterpriseNavigationDisplayMode.Expanded,
            ShowToggleButton = false,
            AnimatePaneChange = false,
            ExpandedWidth = 220,
            Width = 220,
            AccessibleName = "Application menu"
        };
        _pane.Items.Add(new EnterpriseNavigationItem("Back", "\u2190") { Key = "__back" });
        _pane.Items.Add(EnterpriseNavigationItem.Separator());
        _pane.ItemInvoked += OnItemInvoked;
        _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20) };
        Controls.Add(_content);
        Controls.Add(_pane);
        TabStop = false;
    }

    /// <summary>The list on the left; add entries with <see cref="AddPage"/> and <see cref="AddCommand"/>.</summary>
    [Browsable(false)]
    public EnterpriseNavigationPane Navigation => _pane;

    [Browsable(false)]
    public bool IsOpen => Visible && Parent is not null;

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    public EnterpriseNavigationItem AddPage(string text, Control page, string? glyph = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        var item = new EnterpriseNavigationItem(text, glyph);
        _pages[item] = page;
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _content.Controls.Add(page);
        _pane.Items.Add(item);
        return item;
    }

    /// <summary>Adds an entry that runs <paramref name="action"/> and closes the menu.</summary>
    public EnterpriseNavigationItem AddCommand(string text, Action action, string? glyph = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var item = new EnterpriseNavigationItem(text, glyph);
        _commands[item] = action;
        _pane.Items.Add(item);
        return item;
    }

    public void AddSeparator() => _pane.Items.Add(EnterpriseNavigationItem.Separator());

    /// <summary>Covers <paramref name="host"/> with the menu and selects the first page.</summary>
    public void Open(Control host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        if (Parent != host) host.Controls.Add(this);
        BackColor = Colors.Surface;
        Visible = true;
        BringToFront();
        var first = _pane.Items.FirstOrDefault(i => _pages.ContainsKey(i));
        if (first is not null && _pane.SelectedItem is null) ShowPage(first);
        _pane.Focus();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        _host?.Controls.Remove(this);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemInvoked(object? sender, EnterpriseNavigationEventArgs e)
    {
        if (e.Item.Key == "__back")
        {
            Close();
            return;
        }

        if (_commands.TryGetValue(e.Item, out var action))
        {
            Close();
            action();
            return;
        }

        ShowPage(e.Item);
    }

    private void ShowPage(EnterpriseNavigationItem item)
    {
        if (!_pages.TryGetValue(item, out var page)) return;
        if (_activePage is not null) _activePage.Visible = false;
        _activePage = page;
        page.Visible = true;
        _pane.SelectedItem = item;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && IsOpen)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _content.BackColor = Colors.Surface;
        _content.ForeColor = Colors.Text;
    }
}
