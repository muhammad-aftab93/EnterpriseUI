using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Navigation;

namespace EnterpriseUI.WinForms;

/// <summary>One page of an <see cref="EnterprisePageViewControl"/>; put the page content in it.</summary>
[DefaultProperty(nameof(Title))]
[Designer("System.Windows.Forms.Design.ScrollableControlDesigner, System.Design", typeof(System.ComponentModel.Design.IDesigner))]
[ToolboxItem(false)]
public class EnterprisePageViewPage : EnterpriseContainerBase
{
    private string _title = string.Empty;
    private string? _glyph;
    private string? _description;

    public EnterprisePageViewPage()
    {
        Padding = new Padding(10);
    }

    internal EnterprisePageViewControl? Owner { get; set; }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set { _title = value ?? string.Empty; Owner?.RefreshHeaders(); }
    }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public override string Text { get => _title; set => Title = value; }

    [DefaultValue(null)]
    public string? Glyph { get => _glyph; set { _glyph = value; Owner?.RefreshHeaders(); } }

    [DefaultValue(null)]
    public string? Description { get => _description; set { _description = value; Owner?.RefreshHeaders(); } }
}

/// <summary>Multi-view page container: tab strip, vertical stack, Outlook bar, accordion, backstage list or navigation pane, all showing the same pages.</summary>
[DefaultEvent(nameof(SelectedPageChanged))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseControlDesigner<EnterpriseUI.WinForms.Design.PageViewActions>))]
[ToolboxItem(true)]
public class EnterprisePageViewControl : EnterpriseContainerBase
{
    private readonly List<EnterprisePageViewPage> _pages = new();
    private readonly List<(EnterprisePageViewPage Page, Rectangle Bounds)> _headers = new();
    private PageViewMode _mode = PageViewMode.Strip;
    private EnterprisePageViewPage? _selected;
    private EnterprisePageViewPage? _hot;
    private EnterpriseNavigationPane? _nav;
    private bool _syncingNav;

    public EnterprisePageViewControl() : this(new EnterprisePageView())
    {
    }

    public EnterprisePageViewControl(EnterprisePageView model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Size = new Size(480, 320);
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
        AccessibleRole = AccessibleRole.PageTabList;
        AccessibleName = "Page view";
    }

    [Browsable(false)]
    public EnterprisePageView Model { get; }

    [DefaultValue(PageViewMode.Strip)]
    public PageViewMode ViewMode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Model.ViewMode = value;
            EnsureNavigation();
            Relayout();
            ViewModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public IReadOnlyList<EnterprisePageViewPage> Pages => _pages;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EnterprisePageViewPage? SelectedPage
    {
        get => _selected ?? _pages.FirstOrDefault();
        set
        {
            if (value is null || !_pages.Contains(value) || value == _selected) return;
            _selected = value;
            Model.SelectedPage = Model.Pages.FirstOrDefault(p => p.Content == value);
            SyncNavigation();
            Relayout();
            SelectedPageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public int SelectedIndex
    {
        get => SelectedPage is null ? -1 : _pages.IndexOf(SelectedPage);
        set { if (value >= 0 && value < _pages.Count) SelectedPage = _pages[value]; }
    }

    public event EventHandler? SelectedPageChanged;
    public event EventHandler? ViewModeChanged;

    public EnterprisePageViewPage AddPage(string title, string? glyph = null, string? description = null)
    {
        var page = new EnterprisePageViewPage { Title = title, Glyph = glyph, Description = description };
        AddPage(page);
        return page;
    }

    public void AddPage(EnterprisePageViewPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_pages.Contains(page)) return;
        page.Owner = this;
        page.Dock = DockStyle.None;
        _pages.Add(page);
        Model.Pages.Add(new PageViewItem { Title = page.Title, Description = page.Description, Icon = page.Glyph, Content = page });
        Controls.Add(page);
        RebuildNavigation();
        if (_selected is null) _selected = page;
        Relayout();
    }

    public void RemovePage(EnterprisePageViewPage page)
    {
        if (!_pages.Remove(page)) return;
        Model.Pages.RemoveAll(p => p.Content == page);
        Controls.Remove(page);
        page.Owner = null;
        if (_selected == page) _selected = _pages.FirstOrDefault();
        RebuildNavigation();
        Relayout();
        SelectedPageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void RefreshHeaders()
    {
        for (var i = 0; i < _pages.Count && i < Model.Pages.Count; i++)
        {
            Model.Pages[i].Title = _pages[i].Title;
            Model.Pages[i].Icon = _pages[i].Glyph;
            Model.Pages[i].Description = _pages[i].Description;
        }

        RebuildNavigation();
        Relayout();
    }

    // ---- navigation pane mode ----

    private void EnsureNavigation()
    {
        if (_mode == PageViewMode.NavigationView)
        {
            if (_nav is null)
            {
                _nav = new EnterpriseNavigationPane { Dock = DockStyle.Left, DisplayMode = EnterpriseNavigationDisplayMode.Auto, AutoCompactBreakpoint = 520, Title = "Menu" };
                _nav.SelectedItemChanged += (_, _) =>
                {
                    if (_syncingNav) return;
                    if (_nav.SelectedItem?.Tag is EnterprisePageViewPage page) SelectedPage = page;
                };
                _nav.SizeChanged += (_, _) => Relayout();
                Controls.Add(_nav);
            }

            RebuildNavigation();
        }
        else if (_nav is not null)
        {
            Controls.Remove(_nav);
            _nav.Dispose();
            _nav = null;
        }
    }

    private void RebuildNavigation()
    {
        if (_nav is null) return;
        _nav.Items.Clear();
        foreach (var page in _pages) _nav.Items.Add(new EnterpriseNavigationItem(page.Title, page.Glyph, page));
        SyncNavigation();
    }

    private void SyncNavigation()
    {
        if (_nav is null) return;
        _syncingNav = true;
        try { _nav.SelectedItem = _nav.Items.FirstOrDefault(i => i.Tag == SelectedPage); }
        finally { _syncingNav = false; }
    }

    // ---- layout ----

    private int HeaderThickness => _mode switch
    {
        PageViewMode.Strip => S(38),
        PageViewMode.Stack => S(176),
        PageViewMode.Backstage => S(210),
        PageViewMode.Outlook => S(38),
        _ => S(34)
    };

    private void Relayout()
    {
        if (Width <= 0 || Height <= 0 || IsDisposed) return;
        _headers.Clear();
        var selected = SelectedPage;
        var content = new Rectangle(0, 0, Width, Height);
        switch (_mode)
        {
            case PageViewMode.Strip:
            {
                var x = S(6);
                foreach (var page in _pages)
                {
                    var w = Chrome.MeasureText(page.Title, Font) + S(32) + (string.IsNullOrEmpty(page.Glyph) ? 0 : S(22));
                    _headers.Add((page, new Rectangle(x, S(4), w, HeaderThickness - S(4))));
                    x += w + S(2);
                }

                content = new Rectangle(0, HeaderThickness, Width, Math.Max(0, Height - HeaderThickness));
                break;
            }
            case PageViewMode.Stack:
            case PageViewMode.Backstage:
            {
                var y = S(8);
                var row = S(_mode == PageViewMode.Backstage ? 44 : 40);
                foreach (var page in _pages)
                {
                    _headers.Add((page, new Rectangle(S(4), y, HeaderThickness - S(8), row)));
                    y += row + S(2);
                }

                content = new Rectangle(HeaderThickness, 0, Math.Max(0, Width - HeaderThickness), Height);
                break;
            }
            case PageViewMode.Outlook:
            {
                var total = _pages.Count * HeaderThickness;
                var y = Height - total;
                foreach (var page in _pages)
                {
                    _headers.Add((page, new Rectangle(0, y, Width, HeaderThickness)));
                    y += HeaderThickness;
                }

                content = new Rectangle(0, 0, Width, Math.Max(0, Height - total));
                break;
            }
            case PageViewMode.ExplorerBar:
            {
                var headers = _pages.Count * HeaderThickness;
                var body = Math.Max(0, Height - headers);
                var y = 0;
                foreach (var page in _pages)
                {
                    _headers.Add((page, new Rectangle(0, y, Width, HeaderThickness)));
                    y += HeaderThickness;
                    var open = page == selected;
                    page.Visible = open;
                    if (!open) continue;
                    page.Bounds = new Rectangle(0, y, Width, body);
                    page.BringToFront();
                    y += body;
                }

                Invalidate();
                return;
            }
            default:
            {
                if (_nav is not null) content = new Rectangle(_nav.Width, 0, Math.Max(0, Width - _nav.Width), Height);
                break;
            }
        }

        foreach (var page in _pages)
        {
            page.Visible = page == selected;
            if (page == selected) page.Bounds = content;
        }

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

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        foreach (var page in _pages) page.Invalidate();
    }

    // ---- input ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = _headers.Where(h => h.Bounds.Contains(e.Location)).Select(h => h.Page).FirstOrDefault();
        if (hot == _hot) return;
        _hot = hot;
        Cursor = hot is null ? Cursors.Default : Cursors.Hand;
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
        if (e.Button != MouseButtons.Left) return;
        Focus();
        var hit = _headers.Where(h => h.Bounds.Contains(e.Location)).Select(h => h.Page).FirstOrDefault();
        if (hit is not null && hit.Enabled) SelectedPage = hit;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_pages.Count == 0) return;
        var horizontal = _mode == PageViewMode.Strip;
        var index = SelectedIndex;
        var step = e.KeyCode switch
        {
            Keys.Left when horizontal => -1,
            Keys.Right when horizontal => 1,
            Keys.Up when !horizontal => -1,
            Keys.Down when !horizontal => 1,
            _ => 0
        };
        if (e.KeyCode == Keys.Home) index = 0;
        else if (e.KeyCode == Keys.End) index = _pages.Count - 1;
        else if (step != 0) index = Math.Clamp(index + step, 0, _pages.Count - 1);
        else return;
        SelectedIndex = index;
        e.Handled = true;
    }

    // ---- painting ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Surface);
        var selected = SelectedPage;
        switch (_mode)
        {
            case PageViewMode.Strip:
            {
                using var back = new SolidBrush(c.Header);
                g.FillRectangle(back, 0, 0, Width, HeaderThickness);
                using var line = new Pen(c.Border);
                g.DrawLine(line, 0, HeaderThickness - 1, Width, HeaderThickness - 1);
                break;
            }
            case PageViewMode.Stack:
            {
                using var back = new SolidBrush(c.Header);
                g.FillRectangle(back, 0, 0, HeaderThickness, Height);
                using var line = new Pen(c.Border);
                g.DrawLine(line, HeaderThickness - 1, 0, HeaderThickness - 1, Height);
                break;
            }
            case PageViewMode.Backstage:
            {
                using var back = new SolidBrush(Chrome.Blend(c.Primary, Color.Black, 0.25));
                g.FillRectangle(back, 0, 0, HeaderThickness, Height);
                break;
            }
        }

        foreach (var (page, bounds) in _headers)
        {
            var isSelected = page == selected;
            var hot = page == _hot && page.Enabled;
            var textColor = !page.Enabled ? c.Disabled : c.Text;
            switch (_mode)
            {
                case PageViewMode.Strip:
                    if (isSelected)
                    {
                        using var surface = new SolidBrush(c.Surface);
                        g.FillRectangle(surface, bounds.X, bounds.Y, bounds.Width, bounds.Height + 1);
                        using var accent = new SolidBrush(c.Primary);
                        g.FillRectangle(accent, bounds.X, bounds.Y, bounds.Width, S(3));
                    }
                    else if (hot) Chrome.FillRounded(g, Rectangle.Inflate(bounds, -S(1), -S(2)), c.HoverBack, Chrome.CornerRadius);
                    break;
                case PageViewMode.Backstage:
                    if (isSelected) { using var brush = new SolidBrush(Color.FromArgb(70, Color.White)); g.FillRectangle(brush, bounds); }
                    else if (hot) { using var brush = new SolidBrush(Color.FromArgb(36, Color.White)); g.FillRectangle(brush, bounds); }
                    textColor = !page.Enabled ? Color.FromArgb(150, Color.White) : Color.White;
                    break;
                case PageViewMode.Outlook:
                case PageViewMode.ExplorerBar:
                    using (var brush = new SolidBrush(isSelected ? c.SelectionBack : hot ? c.HoverBack : c.Header)) g.FillRectangle(brush, bounds);
                    using (var line = new Pen(c.Border)) g.DrawLine(line, bounds.X, bounds.Y, bounds.Right, bounds.Y);
                    break;
                default:
                    if (isSelected) Chrome.FillRounded(g, bounds, c.SelectionBack, Chrome.CornerRadius);
                    else if (hot) Chrome.FillRounded(g, bounds, c.HoverBack, Chrome.CornerRadius);
                    if (isSelected) Chrome.DrawSelectionBar(g, bounds, c, S(3));
                    break;
            }

            var x = bounds.X + S(_mode is PageViewMode.Strip ? 12 : 12);
            if (!string.IsNullOrEmpty(page.Glyph))
            {
                TextRenderer.DrawText(g, page.Glyph, GlyphFonts.Get(Font.SizeInPoints * 1.15f), new Rectangle(x - S(2), bounds.Y, S(24), bounds.Height),
                    _mode == PageViewMode.Backstage ? textColor : isSelected ? c.Primary : c.Muted, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += S(26);
            }

            var right = bounds.Right - S(8);
            if (_mode == PageViewMode.ExplorerBar)
            {
                Chrome.DrawChevron(g, new Rectangle(right - S(20), bounds.Y, S(20), bounds.Height), isSelected ? ChevronDirection.Up : ChevronDirection.Down, c.Muted);
                right -= S(24);
            }

            Chrome.DrawText(g, page.Title, isSelected ? EmphasisFont : Font, new Rectangle(x, bounds.Y, Math.Max(0, right - x), bounds.Height), textColor);
            if (Focused && isSelected) Chrome.DrawFocusRing(g, Rectangle.Inflate(bounds, -S(1), -S(1)), c);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _nav?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class EnterprisePanoramaTileEventArgs : EventArgs
{
    public EnterprisePanoramaTileEventArgs(EnterprisePanorama.LiveTileItem tile) => Tile = tile;

    public EnterprisePanorama.LiveTileItem Tile { get; }
}

/// <summary>Horizontally scrolling hub of live tiles organized in named groups; tiles with several frames cycle through them.</summary>
[DefaultEvent(nameof(TileClicked))]
[ToolboxItem(true)]
public class EnterprisePanoramaControl : EnterpriseControlBase
{
    private readonly List<(EnterprisePanorama.LiveTileItem Tile, Rectangle Bounds, int GroupIndex)> _layout = new();
    private readonly List<(string Group, Rectangle Bounds)> _groupTitles = new();
    private readonly Dictionary<string, string> _previousFrames = new();
    private readonly EnterpriseScrollBar _scroll = new(Orientation.Horizontal) { Visible = false, TabStop = false };
    private readonly System.Windows.Forms.Timer _cycle = new() { Interval = 4000 };
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 25 };
    private EnterprisePanorama.LiveTileItem? _hot;
    private int _focusIndex;
    private int _contentWidth;
    private double _animProgress = 1;
    private int _tileSize = 112;

    public EnterprisePanoramaControl() : this(new EnterprisePanorama())
    {
    }

    public EnterprisePanoramaControl(EnterprisePanorama model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Size = new Size(640, 400);
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Live tiles";
        _scroll.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scroll);
        _cycle.Tick += (_, _) => CycleFrames();
        _anim.Tick += (_, _) =>
        {
            _animProgress = Math.Min(1, _animProgress + 0.1);
            if (_animProgress >= 1) _anim.Stop();
            Invalidate();
        };
    }

    [Browsable(false)]
    public EnterprisePanorama Model { get; }

    [DefaultValue("Enterprise Operations Hub")]
    public string HeaderTitle { get => Model.HeaderTitle; set { Model.HeaderTitle = value; Relayout(); } }

    [DefaultValue(112)]
    public int TileSize { get => _tileSize; set { _tileSize = Math.Clamp(value, 64, 320); Relayout(); } }

    /// <summary>Cycles the frames of multi-frame tiles automatically.</summary>
    [DefaultValue(false)]
    public bool AutoCycle
    {
        get => _cycle.Enabled;
        set { if (!DesignMode) _cycle.Enabled = value; }
    }

    [DefaultValue(4000)]
    public int FrameInterval { get => _cycle.Interval; set => _cycle.Interval = Math.Clamp(value, 500, 600000); }

    public event EventHandler<EnterprisePanoramaTileEventArgs>? TileClicked;

    public EnterprisePanorama.LiveTileItem AddTile(string group, string title, int columnSpan = 1, string? badge = null, params string[] frames)
    {
        var tile = new EnterprisePanorama.LiveTileItem { Group = group, Title = title, ColumnSpan = Math.Clamp(columnSpan, 1, 4), BadgeText = badge ?? string.Empty };
        tile.Frames.AddRange(frames);
        Model.Tiles.Add(tile);
        Relayout();
        return tile;
    }

    public void CycleFrames()
    {
        foreach (var tile in Model.Tiles) _previousFrames[tile.Id] = tile.CurrentFrameText;
        Model.CycleAllTileFrames();
        _animProgress = 0;
        if (IsHandleCreated && !DesignMode) _anim.Start();
        else _animProgress = 1;
        Invalidate();
    }

    public void Refresh(string tileId, string frameText)
    {
        var tile = Model.Tiles.FirstOrDefault(t => t.Id == tileId);
        if (tile is null) return;
        _previousFrames[tile.Id] = tile.CurrentFrameText;
        tile.Frames.Add(frameText);
        tile.ActiveFrameIndex = tile.Frames.Count - 1;
        _animProgress = 0;
        if (IsHandleCreated && !DesignMode) _anim.Start();
        Invalidate();
    }

    private int HeaderHeight => string.IsNullOrEmpty(Model.HeaderTitle) ? S(12) : S(64);
    private int GroupTitleHeight => S(30);

    private void Relayout()
    {
        _layout.Clear();
        _groupTitles.Clear();
        if (Width <= 0 || Height <= 0 || IsDisposed) return;
        var unit = S(_tileSize);
        var gap = S(8);
        var barHeight = S(10);
        var available = Height - HeaderHeight - GroupTitleHeight - barHeight - S(16);
        var rows = Math.Max(1, (available + gap) / (unit + gap));
        var x = S(24);
        var groupIndex = 0;
        foreach (var group in Model.Tiles.GroupBy(t => t.Group))
        {
            var occupied = new List<bool[]>();
            var maxCol = 0;
            var startX = x;
            foreach (var tile in group)
            {
                var span = Math.Min(tile.ColumnSpan, 4);
                var placed = false;
                for (var col = 0; !placed; col++)
                {
                    while (occupied.Count < col + span) occupied.Add(new bool[rows]);
                    for (var row = 0; row < rows && !placed; row++)
                    {
                        var free = true;
                        for (var k = 0; k < span; k++) if (occupied[col + k][row]) { free = false; break; }
                        if (!free) continue;
                        for (var k = 0; k < span; k++) occupied[col + k][row] = true;
                        var bounds = new Rectangle(startX + col * (unit + gap), HeaderHeight + GroupTitleHeight + row * (unit + gap), span * unit + (span - 1) * gap, unit);
                        _layout.Add((tile, bounds, groupIndex));
                        maxCol = Math.Max(maxCol, col + span);
                        placed = true;
                    }
                }
            }

            var width = Math.Max(1, maxCol) * (unit + gap) - gap;
            _groupTitles.Add((group.Key, new Rectangle(startX, HeaderHeight, width, GroupTitleHeight)));
            x = startX + width + S(40);
            groupIndex++;
        }

        _contentWidth = x;
        var needScroll = _contentWidth > Width;
        _scroll.Visible = needScroll;
        if (needScroll)
        {
            _scroll.SetBounds(0, Height - barHeight - S(2), Width, barHeight + S(2));
            _scroll.Minimum = 0;
            _scroll.Maximum = _contentWidth - 1;
            _scroll.LargeChange = Width;
            _scroll.SmallChange = unit;
        }
        else
        {
            _scroll.Value = 0;
        }

        Invalidate();
    }

    private int OffsetX => _scroll.Visible ? _scroll.Value : 0;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _scroll.ApplyColors(Colors);
        Relayout();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _scroll.ApplyColors(Colors);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scroll.Visible) _scroll.Value -= e.Delta / 120 * S(_tileSize);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = _layout.Where(t => Shifted(t.Bounds).Contains(e.Location)).Select(t => t.Tile).FirstOrDefault();
        if (hot == _hot) return;
        _hot = hot;
        Cursor = hot is null ? Cursors.Default : Cursors.Hand;
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
        Focus();
        if (e.Button != MouseButtons.Left) return;
        var index = _layout.FindIndex(t => Shifted(t.Bounds).Contains(e.Location));
        if (index < 0) return;
        _focusIndex = index;
        TileClicked?.Invoke(this, new EnterprisePanoramaTileEventArgs(_layout[index].Tile));
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Enter or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_layout.Count == 0) return;
        switch (e.KeyCode)
        {
            case Keys.Right or Keys.Down: _focusIndex = Math.Min(_layout.Count - 1, _focusIndex + 1); break;
            case Keys.Left or Keys.Up: _focusIndex = Math.Max(0, _focusIndex - 1); break;
            case Keys.Home: _focusIndex = 0; break;
            case Keys.End: _focusIndex = _layout.Count - 1; break;
            case Keys.Enter: TileClicked?.Invoke(this, new EnterprisePanoramaTileEventArgs(_layout[_focusIndex].Tile)); e.Handled = true; return;
            default: return;
        }

        var bounds = _layout[_focusIndex].Bounds;
        if (_scroll.Visible)
        {
            if (bounds.X < _scroll.Value + S(12)) _scroll.Value = bounds.X - S(24);
            else if (bounds.Right > _scroll.Value + Width - S(12)) _scroll.Value = bounds.Right - Width + S(24);
        }

        e.Handled = true;
        Invalidate();
    }

    private Rectangle Shifted(Rectangle r) => new(r.X - OffsetX, r.Y, r.Width, r.Height);

    private Color TileColor(int groupIndex)
    {
        var c = Colors;
        var palette = new[] { c.Primary, Chrome.Blend(c.Success, Color.Black, 0.2), Chrome.Blend(c.Warning, Color.Black, 0.3), Chrome.Blend(c.Danger, Color.Black, 0.15), Chrome.Blend(c.Primary, Color.Black, 0.45) };
        return palette[groupIndex % palette.Length];
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Surface);
        if (!string.IsNullOrEmpty(Model.HeaderTitle))
        {
            using var large = new Font(Font.FontFamily, Font.SizeInPoints * 2f, FontStyle.Regular, GraphicsUnit.Point);
            Chrome.DrawText(g, Model.HeaderTitle, large, new Rectangle(S(24), S(6), Width - S(48), S(52)), c.Text);
        }

        var state = g.Save();
        g.SetClip(new Rectangle(0, HeaderHeight, Width, Height - HeaderHeight));
        foreach (var (group, bounds) in _groupTitles)
            Chrome.DrawText(g, group, Font, Shifted(bounds), c.Muted, ContentAlignment.MiddleLeft);
        for (var i = 0; i < _layout.Count; i++)
        {
            var (tile, bounds, groupIndex) = _layout[i];
            var rect = Shifted(bounds);
            if (rect.Right < 0 || rect.X > Width) continue;
            var color = TileColor(groupIndex);
            var hot = tile == _hot;
            Chrome.FillRounded(g, rect, hot ? Chrome.Blend(color, Color.White, 0.12) : color, Chrome.CornerRadius);
            var fore = Color.White;
            var inner = Rectangle.Inflate(rect, -S(10), -S(8));
            var clipState = g.Save();
            g.SetClip(Rectangle.Inflate(rect, -1, -1));
            Chrome.DrawText(g, tile.Title, Font, new Rectangle(inner.X, inner.Y, inner.Width - S(28), S(20)), Color.FromArgb(220, fore));
            var frame = tile.CurrentFrameText;
            using var big = new Font(Font.FontFamily, Font.SizeInPoints * 1.5f, FontStyle.Bold, GraphicsUnit.Point);
            var body = new Rectangle(inner.X, inner.Y + S(24), inner.Width, inner.Height - S(24));
            if (_animProgress < 1 && _previousFrames.TryGetValue(tile.Id, out var previous) && previous != frame)
            {
                var shift = (int)(body.Height * _animProgress);
                Chrome.DrawText(g, previous, big, new Rectangle(body.X, body.Y - shift, body.Width, body.Height), Color.FromArgb((int)(255 * (1 - _animProgress)), fore), ContentAlignment.MiddleLeft, true);
                Chrome.DrawText(g, frame, big, new Rectangle(body.X, body.Y + body.Height - shift, body.Width, body.Height), Color.FromArgb((int)(255 * _animProgress), fore), ContentAlignment.MiddleLeft, true);
            }
            else
            {
                Chrome.DrawText(g, frame, big, body, fore, ContentAlignment.MiddleLeft, true);
            }

            g.Restore(clipState);
            if (!string.IsNullOrEmpty(tile.BadgeText))
            {
                var badge = new Rectangle(rect.Right - S(30), rect.Y + S(8), S(22), S(20));
                Chrome.FillRounded(g, badge, Color.FromArgb(60, Color.White), S(10));
                Chrome.DrawText(g, tile.BadgeText, Font, badge, fore, ContentAlignment.MiddleCenter);
            }

            if (tile.Frames.Count > 1)
            {
                for (var f = 0; f < tile.Frames.Count && f < 6; f++)
                {
                    var active = f == tile.ActiveFrameIndex % tile.Frames.Count;
                    using var brush = new SolidBrush(Color.FromArgb(active ? 255 : 110, fore));
                    g.FillEllipse(brush, rect.X + S(10) + f * S(8), rect.Bottom - S(12), S(5), S(5));
                }
            }

            if (Focused && i == _focusIndex) Chrome.DrawFocusRing(g, rect, c);
        }

        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cycle.Dispose();
            _anim.Dispose();
        }

        base.Dispose(disposing);
    }
}
