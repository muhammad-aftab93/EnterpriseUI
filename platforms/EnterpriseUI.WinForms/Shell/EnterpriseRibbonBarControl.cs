using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Navigation;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseRibbonTabEventArgs : EventArgs
{
    public EnterpriseRibbonTabEventArgs(RibbonTab tab) => Tab = tab;

    public RibbonTab Tab { get; }
}

public sealed class EnterpriseRibbonItemEventArgs : EventArgs
{
    public EnterpriseRibbonItemEventArgs(RibbonItem item) => Item = item;

    public RibbonItem Item { get; }
}

/// <summary>Tab headers and quick access buttons of a ribbon; sits on top of the ribbon or inside a window's title bar.</summary>
internal sealed class RibbonHeaderStrip : EnterpriseTitleBarContentControl
{
    private readonly EnterpriseRibbonBarControl _ribbon;
    private readonly List<(RibbonTab Tab, Rectangle Bounds)> _tabs = new();
    private readonly List<(RibbonItem Item, Rectangle Bounds)> _quick = new();
    private RibbonTab? _hotTab;
    private RibbonItem? _hotQuick;

    public RibbonHeaderStrip(EnterpriseRibbonBarControl ribbon)
    {
        _ribbon = ribbon;
        Size = new Size(400, 30);
    }

    public Rectangle TabBounds(RibbonTab tab) => _tabs.FirstOrDefault(t => t.Tab == tab).Bounds;

    public Rectangle QuickBounds(RibbonItem item) => _quick.FirstOrDefault(q => q.Item == item).Bounds;

    public void Relayout()
    {
        _tabs.Clear();
        _quick.Clear();
        var x = S(6);
        var buttonHeight = Math.Min(S(26), Height);
        foreach (var item in _ribbon.QuickAccessItems)
        {
            _quick.Add((item, new Rectangle(x, (Height - buttonHeight) / 2, S(30), buttonHeight)));
            x += S(30);
        }

        if (_quick.Count > 0) x += S(10);
        foreach (var tab in _ribbon.VisibleTabs)
        {
            var width = Chrome.MeasureText(tab.Title, Font) + S(26);
            _tabs.Add((tab, new Rectangle(x, S(2), width, Math.Max(0, Height - S(2)))));
            x += width + S(2);
        }

        Invalidate();
    }

    public override bool IsCaptionArea(Point clientPoint) =>
        Parent is EnterpriseTitleBarControl && _tabs.All(t => !t.Bounds.Contains(clientPoint)) && _quick.All(q => !q.Bounds.Contains(clientPoint));

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_ribbon is not null) Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Relayout();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var tab = _tabs.Where(t => t.Bounds.Contains(e.Location)).Select(t => t.Tab).FirstOrDefault();
        var quick = _quick.Where(q => q.Bounds.Contains(e.Location)).Select(q => q.Item).FirstOrDefault();
        if (tab == _hotTab && quick == _hotQuick) return;
        _hotTab = tab;
        _hotQuick = quick;
        Cursor = tab != null || quick != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotTab = null;
        _hotQuick = null;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        foreach (var (tab, bounds) in _tabs)
        {
            if (!bounds.Contains(e.Location)) continue;
            _ribbon.SelectTab(tab, true);
            return;
        }

        foreach (var (item, bounds) in _quick)
        {
            if (!bounds.Contains(e.Location)) continue;
            _ribbon.PerformItem(item, true);
            return;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_tabs.Any(t => t.Bounds.Contains(e.Location))) _ribbon.IsCollapsed = !_ribbon.IsCollapsed;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent is EnterpriseTitleBarControl ? c.Header : Chrome.Blend(c.Surface, c.Header, 0.7));
        foreach (var (item, bounds) in _quick)
        {
            if (_hotQuick == item && item.IsEnabled) Chrome.FillRounded(g, bounds, c.HoverBack, Chrome.CornerRadius);
            var glyph = string.IsNullOrEmpty(item.Icon) ? (item.Text.Length > 0 ? item.Text[..1] : "•") : item.Icon;
            TextRenderer.DrawText(g, glyph, GlyphFonts.Get(Font.SizeInPoints * 1.15f), bounds, item.IsEnabled ? c.Primary : c.Disabled, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        foreach (var (tab, bounds) in _tabs)
        {
            var selected = tab == _ribbon.SelectedTab;
            var accent = ColorUtil.Parse(tab.ContextualColor) ?? c.Primary;
            var contextual = !string.IsNullOrEmpty(tab.ContextualGroup);
            if (contextual) using (var tint = new SolidBrush(Color.FromArgb(selected ? 70 : 38, accent))) g.FillRectangle(tint, bounds);
            else if (_hotTab == tab && !selected) Chrome.FillRounded(g, bounds, c.HoverBack, Chrome.CornerRadius);
            if (contextual) using (var bar = new SolidBrush(accent)) g.FillRectangle(bar, bounds.X, bounds.Y, bounds.Width, S(3));
            Chrome.DrawText(g, tab.Title, selected ? BoldFont : Font, bounds, selected ? c.Text : c.Muted, ContentAlignment.MiddleCenter);
            if (selected && !contextual)
            {
                using var underline = new SolidBrush(c.Primary);
                g.FillRectangle(underline, bounds.X + S(8), bounds.Bottom - S(3), bounds.Width - S(16), S(3));
            }
        }
    }
}

/// <summary>Tabbed ribbon: tab headers with contextual tabs, a quick access toolbar and a body of grouped large and small command buttons; bound to <see cref="EnterpriseRibbonBar"/>.</summary>
[DefaultEvent("ItemClick")]
public class EnterpriseRibbonBarControl : EnterpriseControlBase
{
    private const int HeaderHeight = 30;
    private const int BodyHeight = 96;
    private readonly HashSet<string> _activeGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(RibbonItem Item, Rectangle Bounds)> _itemBounds = new();
    private readonly List<Rectangle> _groupBounds = new();
    private EnterpriseRibbonBar _model = new();
    private RibbonHeaderStrip? _header;
    private RibbonHeaderStrip? _embedded;
    private RibbonTab? _selected;
    private RibbonItem? _hot;
    private RibbonItem? _down;
    private bool _showHeaders = true;

    public EnterpriseRibbonBarControl()
    {
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        Dock = DockStyle.Top;
        AccessibleRole = AccessibleRole.ToolBar;
        EmbedHeader();
        UpdateHeight();
    }

    /// <summary>Tabs, groups, items and the quick access toolbar.</summary>
    [Browsable(false)]
    public EnterpriseRibbonBar Model
    {
        get => _model;
        set
        {
            _model = value ?? throw new ArgumentNullException(nameof(value));
            _selected = null;
            RefreshRibbon();
        }
    }

    [Browsable(false)]
    public IList<RibbonTab> Tabs => _model.Tabs;

    [Browsable(false)]
    public IList<RibbonItem> QuickAccessItems => _model.QuickAccessToolbar;

    /// <summary>Tabs that are shown now: ordinary tabs and the tabs of active contextual groups.</summary>
    [Browsable(false)]
    public IReadOnlyList<RibbonTab> VisibleTabs => _model.Tabs.Where(IsVisibleTab).ToList();

    [Browsable(false)]
    public RibbonTab? SelectedTab
    {
        get
        {
            if (_selected != null && IsVisibleTab(_selected)) return _selected;
            var visible = VisibleTabs;
            return visible.Count > 0 ? visible[0] : null;
        }
        set
        {
            if (value != null) SelectTab(value, false);
        }
    }

    /// <summary>Draws the tab headers on top of the ribbon; turn off when a window shows them in its title bar.</summary>
    [DefaultValue(true)]
    public bool ShowTabHeaders
    {
        get => _showHeaders;
        set
        {
            if (_showHeaders == value) return;
            _showHeaders = value;
            if (value) EmbedHeader();
            else RemoveEmbeddedHeader();
            UpdateHeight();
            Invalidate();
        }
    }

    /// <summary>When collapsed only the headers show; choosing a tab expands the ribbon again.</summary>
    [DefaultValue(false)]
    public bool IsCollapsed
    {
        get => _model.IsMinimized;
        set
        {
            if (_model.IsMinimized == value) return;
            _model.IsMinimized = value;
            UpdateHeight();
            Invalidate();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public Control? TabHeaders => _header;

    public event EventHandler<EnterpriseRibbonTabEventArgs>? TabSelected;
    public event EventHandler<EnterpriseRibbonItemEventArgs>? ItemClick;
    public event EventHandler<EnterpriseRibbonItemEventArgs>? QuickAccessClick;
    public event EventHandler? CollapsedChanged;

    /// <summary>Creates the header strip for a title bar and detaches the one drawn on the ribbon itself.</summary>
    public EnterpriseTitleBarContentControl CreateHeaderStrip()
    {
        ShowTabHeaders = false;
        _header = new RibbonHeaderStrip(this);
        _header.Relayout();
        return _header;
    }

    public void SelectTab(RibbonTab tab, bool expand = false)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!IsVisibleTab(tab)) return;
        var changed = SelectedTab != tab;
        _selected = tab;
        if (expand && IsCollapsed) IsCollapsed = false;
        RefreshRibbon();
        if (changed) TabSelected?.Invoke(this, new EnterpriseRibbonTabEventArgs(tab));
    }

    public void Collapse() => IsCollapsed = true;

    public void Expand() => IsCollapsed = false;

    /// <summary>Shows the tabs of a contextual group and selects the first of them.</summary>
    public void ShowContextualGroup(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        _activeGroups.Add(group);
        var first = _model.Tabs.FirstOrDefault(t => string.Equals(t.ContextualGroup, group, StringComparison.OrdinalIgnoreCase));
        if (first != null) SelectTab(first, true);
        else RefreshRibbon();
    }

    public void HideContextualGroup(string group)
    {
        if (!_activeGroups.Remove(group)) return;
        RefreshRibbon();
    }

    public bool IsContextualGroupActive(string group) => _activeGroups.Contains(group);

    /// <summary>Recalculates headers and layout after tabs, groups or items were changed through <see cref="Model"/>.</summary>
    public void RefreshRibbon()
    {
        ComputeBody();
        _header?.Relayout();
        _embedded?.Relayout();
        Invalidate();
    }

    /// <summary>Runs the command of an item (or quick access button) and raises the click event.</summary>
    public void PerformItem(RibbonItem item, bool quickAccess = false)
    {
        if (!item.IsEnabled) return;
        if (item.Command is { } command)
        {
            if (!command.CanExecute(null)) return;
            command.Execute(null);
        }

        var args = new EnterpriseRibbonItemEventArgs(item);
        if (quickAccess) QuickAccessClick?.Invoke(this, args);
        ItemClick?.Invoke(this, args);
    }

    public Rectangle GetTabBounds(RibbonTab tab) => (_header ?? _embedded)?.TabBounds(tab) ?? Rectangle.Empty;

    public Rectangle GetQuickAccessBounds(RibbonItem item) => (_header ?? _embedded)?.QuickBounds(item) ?? Rectangle.Empty;

    /// <summary>Layout of an item of the selected tab in ribbon coordinates.</summary>
    public Rectangle GetItemBounds(RibbonItem item) => _itemBounds.FirstOrDefault(i => i.Item == item).Bounds;

    private bool IsVisibleTab(RibbonTab tab) => string.IsNullOrEmpty(tab.ContextualGroup) || _activeGroups.Contains(tab.ContextualGroup);

    private void EmbedHeader()
    {
        if (_embedded != null) return;
        _embedded = new RibbonHeaderStrip(this) { Dock = DockStyle.Top };
        Controls.Add(_embedded);
        _header ??= _embedded;
    }

    private void RemoveEmbeddedHeader()
    {
        if (_embedded == null) return;
        Controls.Remove(_embedded);
        if (_header == _embedded) _header = null;
        _embedded.Dispose();
        _embedded = null;
    }

    private void UpdateHeight()
    {
        var header = _embedded != null ? S(HeaderHeight) : 0;
        Height = header + (IsCollapsed ? 0 : S(BodyHeight));
        if (_embedded != null) _embedded.Height = S(HeaderHeight);
        ComputeBody();
    }

    private Rectangle BodyArea => new(0, _embedded != null ? S(HeaderHeight) : 0, Width, Math.Max(0, Height - (_embedded != null ? S(HeaderHeight) : 0)));

    private void ComputeBody()
    {
        _itemBounds.Clear();
        _groupBounds.Clear();
        var tab = SelectedTab;
        var area = BodyArea;
        if (tab == null || area.Height < S(40) || IsCollapsed) return;
        var titleHeight = S(18);
        var itemArea = new Rectangle(0, area.Y + S(6), 0, area.Height - titleHeight - S(10));
        var x = S(6);
        foreach (var group in tab.Groups)
        {
            var start = x;
            var smallColumn = new List<RibbonItem>();
            void FlushSmall()
            {
                if (smallColumn.Count == 0) return;
                var width = smallColumn.Max(i => S(26) + Chrome.MeasureText(i.Text, Font) + S(10));
                var rowHeight = Math.Min(S(26), itemArea.Height / 3);
                for (var r = 0; r < smallColumn.Count; r++) _itemBounds.Add((smallColumn[r], new Rectangle(x, itemArea.Y + r * rowHeight, width, rowHeight)));
                x += width + S(4);
                smallColumn.Clear();
            }

            foreach (var item in group.Items)
            {
                if (item.IsLarge)
                {
                    FlushSmall();
                    var text = string.Join(" ", item.Text.Split('\n'));
                    var width = Math.Max(S(54), Math.Min(S(96), Chrome.MeasureText(text, Font) / 2 + S(24)));
                    _itemBounds.Add((item, new Rectangle(x, itemArea.Y, width, itemArea.Height)));
                    x += width + S(4);
                }
                else
                {
                    smallColumn.Add(item);
                    if (smallColumn.Count == 3) FlushSmall();
                }
            }

            FlushSmall();
            var groupWidth = Math.Max(x - start, Chrome.MeasureText(group.Title, Font) + S(16));
            x = start + groupWidth;
            _groupBounds.Add(new Rectangle(start, area.Y, groupWidth, area.Height));
            x += S(10);
        }
    }

    private RibbonItem? ItemAt(Point p) => _itemBounds.Where(i => i.Bounds.Contains(p)).Select(i => i.Item).FirstOrDefault();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeBody();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshRibbon();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateHeight();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = ItemAt(e.Location);
        if (item == _hot) return;
        _hot = item;
        Cursor = item != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) _down = ItemAt(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var down = _down;
        _down = null;
        Invalidate();
        if (e.Button != MouseButtons.Left || down == null) return;
        if (ItemAt(e.Location) == down) PerformItem(down);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Surface);
        var area = BodyArea;
        if (IsCollapsed || area.Height <= 0) return;
        using (var line = new Pen(c.Border))
        {
            g.DrawLine(line, 0, area.Y, Width, area.Y);
            g.DrawLine(line, 0, Height - 1, Width, Height - 1);
        }

        var tab = SelectedTab;
        if (tab == null) return;
        var small = new Font(Font.FontFamily, Font.Size - 0.5f);
        try
        {
            for (var i = 0; i < tab.Groups.Count && i < _groupBounds.Count; i++)
            {
                var gb = _groupBounds[i];
                Chrome.DrawText(g, tab.Groups[i].Title, small, new Rectangle(gb.X, gb.Bottom - S(20), gb.Width, S(18)), c.Muted, ContentAlignment.MiddleCenter);
                using var sep = new Pen(c.Border);
                g.DrawLine(sep, gb.Right + S(5), gb.Y + S(8), gb.Right + S(5), gb.Bottom - S(8));
            }

            foreach (var (item, bounds) in _itemBounds)
            {
                var hot = _hot == item && item.IsEnabled;
                if (hot) Chrome.FillRounded(g, bounds, _down == item ? c.SelectionBack : c.HoverBack, Chrome.CornerRadius);
                var fore = item.IsEnabled ? c.Text : c.Disabled;
                var glyph = string.IsNullOrEmpty(item.Icon) ? string.Empty : item.Icon;
                if (item.IsLarge)
                {
                    TextRenderer.DrawText(g, glyph, GlyphFonts.Get(Font.SizeInPoints * 2.1f), new Rectangle(bounds.X, bounds.Y + S(4), bounds.Width, S(34)), item.IsEnabled ? c.Primary : c.Disabled, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(g, item.Text, Font, new Rectangle(bounds.X + S(2), bounds.Y + S(40), bounds.Width - S(4), bounds.Height - S(40)), fore, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(g, glyph, GlyphFonts.Get(Font.SizeInPoints * 1.1f), new Rectangle(bounds.X + S(2), bounds.Y, S(22), bounds.Height), item.IsEnabled ? c.Primary : c.Disabled, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    Chrome.DrawText(g, item.Text, Font, new Rectangle(bounds.X + S(26), bounds.Y, bounds.Width - S(28), bounds.Height), fore);
                }
            }
        }
        finally
        {
            small.Dispose();
        }
    }
}

/// <summary>Window with a ribbon in its frame: quick access buttons and tab headers (with contextual tabs) share the title bar, the ribbon body sits below it.</summary>
[DefaultEvent("QuickAccessClick")]
public class EnterpriseRibbonForm : EnterpriseForm
{
    private readonly EnterpriseRibbonBarControl _ribbon = new();

    public EnterpriseRibbonForm()
    {
        TitleBar.ShowCaptionText = false;
        TitleBar.ContentControl = _ribbon.CreateHeaderStrip();
        Controls.Add(_ribbon);
        _ribbon.CollapsedChanged += (_, e) => RibbonCollapsedChanged?.Invoke(this, e);
        _ribbon.QuickAccessClick += (_, e) => QuickAccessClick?.Invoke(this, e);
        _ribbon.TabSelected += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Tab.ContextualGroup)) ContextualTabSelected?.Invoke(this, e);
        };
    }

    [Browsable(false)]
    public EnterpriseRibbonBarControl Ribbon => _ribbon;

    [Browsable(false)]
    public IList<RibbonItem> QuickAccessItems => _ribbon.QuickAccessItems;

    /// <summary>Tabs that belong to a contextual group, whether or not the group is active.</summary>
    [Browsable(false)]
    public IEnumerable<RibbonTab> ContextualTabs => _ribbon.Tabs.Where(t => !string.IsNullOrEmpty(t.ContextualGroup));

    [DefaultValue(false)]
    public bool IsRibbonCollapsed
    {
        get => _ribbon.IsCollapsed;
        set => _ribbon.IsCollapsed = value;
    }

    public event EventHandler? RibbonCollapsedChanged;
    public event EventHandler<EnterpriseRibbonTabEventArgs>? ContextualTabSelected;
    public event EventHandler<EnterpriseRibbonItemEventArgs>? QuickAccessClick;

    public void ExpandRibbon() => _ribbon.Expand();

    public void CollapseRibbon() => _ribbon.Collapse();

    public void ShowContextualTab(string group) => _ribbon.ShowContextualGroup(group);

    public void HideContextualTab(string group) => _ribbon.HideContextualGroup(group);

    protected override void ArrangeChrome()
    {
        base.ArrangeChrome();
        if (Controls.Contains(_ribbon) && Controls.Count >= 2) Controls.SetChildIndex(_ribbon, Controls.Count - 2);
    }
}
