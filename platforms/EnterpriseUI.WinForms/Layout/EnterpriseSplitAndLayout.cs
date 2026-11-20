using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum EnterpriseSplitCollapse { None, Panel1, Panel2 }

/// <summary>Resizable two-pane container with a themed splitter, grip, collapse buttons, double-click toggle and keyboard resizing.</summary>
[DefaultEvent(nameof(SplitterMoved))]
[ToolboxItem(true)]
public class EnterpriseSplitPanel : SplitContainer, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
    private bool _hot;
    private bool _showButtons = true;
    private bool _doubleClickToggle = true;
    private int _restoreDistance = -1;
    private Rectangle _button1, _button2;
    private int _hotButton;

    public EnterpriseSplitPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SplitterWidth = 9;
        Font = new Font("Segoe UI", 10f);
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        ApplyColors();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeOverride { get; set; }

    /// <summary>Shows chevron buttons on the splitter that collapse either pane.</summary>
    [DefaultValue(true)]
    public bool ShowCollapseButtons { get => _showButtons; set { _showButtons = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool DoubleClickToggle { get => _doubleClickToggle; set => _doubleClickToggle = value; }

    [Browsable(false)]
    public EnterpriseSplitCollapse Collapsed => Panel1Collapsed ? EnterpriseSplitCollapse.Panel1 : Panel2Collapsed ? EnterpriseSplitCollapse.Panel2 : EnterpriseSplitCollapse.None;

    public event EventHandler? CollapsedChanged;

    public void Collapse(EnterpriseSplitCollapse pane)
    {
        if (pane == Collapsed) return;
        if (pane == EnterpriseSplitCollapse.None) { Restore(); return; }
        if (Collapsed == EnterpriseSplitCollapse.None) _restoreDistance = SplitterDistance;
        Panel1Collapsed = pane == EnterpriseSplitCollapse.Panel1;
        Panel2Collapsed = pane == EnterpriseSplitCollapse.Panel2;
        Invalidate();
        CollapsedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Restore()
    {
        if (Collapsed == EnterpriseSplitCollapse.None) return;
        Panel1Collapsed = false;
        Panel2Collapsed = false;
        if (_restoreDistance >= 0) SplitterDistance = Math.Clamp(_restoreDistance, Panel1MinSize, Math.Max(Panel1MinSize, (Orientation == Orientation.Vertical ? Width : Height) - Panel2MinSize - SplitterWidth));
        Invalidate();
        CollapsedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _colors = ControlColors.FromTheme(newTheme);
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(ApplyColors)); } catch (InvalidOperationException) { }
            return;
        }

        ApplyColors();
    }

    private void ApplyColors()
    {
        BackColor = _colors.Header;
        Panel1.BackColor = Panel2.BackColor = _colors.Surface;
        Panel1.ForeColor = Panel2.ForeColor = _colors.Text;
        Invalidate();
    }

    private int S(double value) => (int)Math.Round(value * DeviceDpi / 96f);

    private bool Horizontal => Orientation == Orientation.Vertical; // splitter bar is vertical, panes side by side

    private void UpdateButtons()
    {
        _button1 = _button2 = Rectangle.Empty;
        if (!_showButtons) return;
        var bar = SplitterRectangle;
        var size = Math.Min(S(18), Horizontal ? bar.Width : bar.Height) + S(4);
        if (Horizontal)
        {
            var x = bar.X + (bar.Width - size) / 2;
            var y = bar.Y + bar.Height / 2 - size - S(24);
            _button1 = new Rectangle(x, y, size, size);
            _button2 = new Rectangle(x, y + size + S(4), size, size);
        }
        else
        {
            var y = bar.Y + (bar.Height - size) / 2;
            var x = bar.X + bar.Width / 2 - size - S(24);
            _button1 = new Rectangle(x, y, size, size);
            _button2 = new Rectangle(x + size + S(4), y, size, size);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var c = _colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bar = SplitterRectangle;
        if (Panel1Collapsed || Panel2Collapsed) bar = Panel1Collapsed ? new Rectangle(0, 0, Horizontal ? SplitterWidth : Width, Horizontal ? Height : SplitterWidth) : bar;
        using (var brush = new SolidBrush(_hot || Capture ? Chrome.Blend(c.Header, c.Primary, 0.25) : c.Header)) g.FillRectangle(brush, bar);
        using (var pen = new Pen(c.Border))
        {
            if (Horizontal)
            {
                g.DrawLine(pen, bar.X, bar.Y, bar.X, bar.Bottom);
                g.DrawLine(pen, bar.Right - 1, bar.Y, bar.Right - 1, bar.Bottom);
            }
            else
            {
                g.DrawLine(pen, bar.X, bar.Y, bar.Right, bar.Y);
                g.DrawLine(pen, bar.X, bar.Bottom - 1, bar.Right, bar.Bottom - 1);
            }
        }

        if (Panel1Collapsed || Panel2Collapsed)
        {
            UpdateButtonsCollapsed(bar);
        }
        else
        {
            using var dot = new SolidBrush(c.Muted);
            var cx = bar.X + bar.Width / 2f;
            var cy = bar.Y + bar.Height / 2f;
            for (var i = -2; i <= 2; i++)
            {
                var dx = Horizontal ? 0 : i * S(6);
                var dy = Horizontal ? i * S(6) : 0;
                g.FillEllipse(dot, cx + dx - 1.5f, cy + dy - 1.5f, 3, 3);
            }

            UpdateButtons();
        }

        DrawButton(g, _button1, 1);
        DrawButton(g, _button2, 2);
    }

    private void UpdateButtonsCollapsed(Rectangle bar)
    {
        if (!_showButtons) { _button1 = _button2 = Rectangle.Empty; return; }
        var size = Math.Min(S(18), Horizontal ? bar.Width : bar.Height) + S(4);
        _button2 = Rectangle.Empty;
        _button1 = new Rectangle(bar.X + (bar.Width - size) / 2, bar.Y + (bar.Height - size) / 2, size, size);
        if (Horizontal) _button1.Y = Math.Max(bar.Y + S(8), bar.Y + S(40));
        else _button1.X = Math.Max(bar.X + S(8), bar.X + S(40));
    }

    private void DrawButton(Graphics g, Rectangle bounds, int id)
    {
        if (bounds == Rectangle.Empty) return;
        var c = _colors;
        Chrome.FillRounded(g, bounds, _hotButton == id ? c.SelectionBack : c.Surface, 3);
        Chrome.DrawRoundedBorder(g, bounds, c.Border, 3);
        var collapsed = Collapsed;
        ChevronDirection direction;
        if (collapsed != EnterpriseSplitCollapse.None)
            direction = Horizontal ? (collapsed == EnterpriseSplitCollapse.Panel1 ? ChevronDirection.Right : ChevronDirection.Left) : (collapsed == EnterpriseSplitCollapse.Panel1 ? ChevronDirection.Down : ChevronDirection.Up);
        else if (id == 1) direction = Horizontal ? ChevronDirection.Left : ChevronDirection.Up;
        else direction = Horizontal ? ChevronDirection.Right : ChevronDirection.Down;
        Chrome.DrawChevron(g, bounds, direction, c.Muted);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = SplitterRectangle.Contains(e.Location);
        var button = _button1.Contains(e.Location) ? 1 : _button2.Contains(e.Location) ? 2 : 0;
        if (hot != _hot || button != _hotButton)
        {
            _hot = hot;
            _hotButton = button;
            if (button != 0) Cursor = Cursors.Hand;
            else Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        _hotButton = 0;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (Collapsed != EnterpriseSplitCollapse.None && _button1.Contains(e.Location)) { Restore(); return; }
        if (_button1.Contains(e.Location)) Collapse(EnterpriseSplitCollapse.Panel1);
        else if (_button2.Contains(e.Location)) Collapse(EnterpriseSplitCollapse.Panel2);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (!_doubleClickToggle || !SplitterRectangle.Contains(e.Location) || _button1.Contains(e.Location) || _button2.Contains(e.Location)) return;
        if (Collapsed != EnterpriseSplitCollapse.None) Restore();
        else Collapse(EnterpriseSplitCollapse.Panel2);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}

/// <summary>One cell group of an <see cref="EnterpriseCustomizableLayout"/>.</summary>
public sealed class EnterpriseLayoutItem
{
    internal EnterpriseLayoutItem(string name, string caption, Control control, int columnSpan, int rowSpan)
    {
        Name = name;
        Caption = caption;
        Control = control;
        ColumnSpan = columnSpan;
        RowSpan = rowSpan;
        DefaultColumnSpan = columnSpan;
        DefaultRowSpan = rowSpan;
    }

    public string Name { get; }
    public string Caption { get; set; }
    public Control Control { get; }
    public int ColumnSpan { get; internal set; }
    public int RowSpan { get; internal set; }
    public bool Visible { get; internal set; } = true;

    /// <summary>Locked items cannot be moved, resized or hidden while customizing.</summary>
    public bool Locked { get; set; }

    internal int DefaultColumnSpan { get; }
    internal int DefaultRowSpan { get; }
    internal int DefaultIndex { get; set; }
    internal Rectangle Frame { get; set; }
}

/// <summary>Grid of captioned controls that end users can rearrange, resize and hide in customization mode, with saveable layouts.</summary>
[DefaultEvent(nameof(LayoutChanged))]
[Designer(typeof(EnterpriseUI.WinForms.Design.EnterpriseContainerDesigner<EnterpriseUI.WinForms.Design.CustomizableLayoutActions>))]
[ToolboxItem(true)]
public class EnterpriseCustomizableLayout : EnterpriseContainerBase
{
    private enum DragKind { None, Move, Resize }

    private readonly List<EnterpriseLayoutItem> _items = new();
    private readonly List<(EnterpriseLayoutItem Item, Rectangle Bounds)> _hiddenChips = new();
    private int _columns = 3;
    private int _rowHeight = 140;
    private int _spacing = 8;
    private bool _customizing;
    private bool _showCaptions = true;
    private DragKind _drag;
    private EnterpriseLayoutItem? _dragItem;
    private EnterpriseLayoutItem? _hotItem;
    private int _contentHeight;

    public EnterpriseCustomizableLayout()
    {
        AutoScroll = true;
        Size = new Size(640, 420);
        Padding = new Padding(8);
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = "Customizable layout";
    }

    [DefaultValue(3)]
    public int ColumnCount { get => _columns; set { _columns = Math.Clamp(value, 1, 12); Relayout(); } }

    /// <summary>Height of one grid row in logical pixels, captions included.</summary>
    [DefaultValue(140)]
    public int RowHeight { get => _rowHeight; set { _rowHeight = Math.Clamp(value, 48, 800); Relayout(); } }

    [DefaultValue(8)]
    public int Spacing { get => _spacing; set { _spacing = Math.Clamp(value, 0, 48); Relayout(); } }

    [DefaultValue(true)]
    public bool ShowCaptions { get => _showCaptions; set { _showCaptions = value; Relayout(); } }

    /// <summary>When on, items show frames, can be dragged by their caption, resized from the corner grip and hidden.</summary>
    [DefaultValue(false)]
    public bool IsCustomizing
    {
        get => _customizing;
        set
        {
            if (_customizing == value) return;
            _customizing = value;
            Relayout();
            CustomizingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public IReadOnlyList<EnterpriseLayoutItem> Items => _items;

    public event EventHandler? LayoutChanged;
    public event EventHandler? CustomizingChanged;

    public EnterpriseLayoutItem AddItem(string caption, Control control, int columnSpan = 1, int rowSpan = 1, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        var item = new EnterpriseLayoutItem(name ?? (string.IsNullOrEmpty(control.Name) ? caption : control.Name), caption, control, Math.Clamp(columnSpan, 1, 12), Math.Clamp(rowSpan, 1, 12))
        {
            DefaultIndex = _items.Count
        };
        _items.Add(item);
        control.Dock = DockStyle.None;
        control.Parent = this;
        Relayout();
        return item;
    }

    public EnterpriseLayoutItem? FindItem(string name) => _items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

    public void MoveItem(EnterpriseLayoutItem item, int index)
    {
        var old = _items.IndexOf(item);
        if (old < 0 || item.Locked) return;
        index = Math.Clamp(index, 0, _items.Count - 1);
        if (old == index) return;
        _items.RemoveAt(old);
        _items.Insert(index, item);
        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResizeItem(EnterpriseLayoutItem item, int columnSpan, int rowSpan)
    {
        if (item.Locked) return;
        item.ColumnSpan = Math.Clamp(columnSpan, 1, _columns);
        item.RowSpan = Math.Clamp(rowSpan, 1, 12);
        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetVisible(EnterpriseLayoutItem item, bool visible)
    {
        if (item.Locked && !visible) return;
        item.Visible = visible;
        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLayout()
    {
        foreach (var item in _items)
        {
            item.ColumnSpan = item.DefaultColumnSpan;
            item.RowSpan = item.DefaultRowSpan;
            item.Visible = true;
        }

        _items.Sort((a, b) => a.DefaultIndex.CompareTo(b.DefaultIndex));
        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record ItemState(string Name, int ColumnSpan, int RowSpan, bool Visible);

    public string SaveLayout() => JsonSerializer.Serialize(_items.Select(i => new ItemState(i.Name, i.ColumnSpan, i.RowSpan, i.Visible)).ToList(), new JsonSerializerOptions { WriteIndented = true });

    public void LoadLayout(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var states = JsonSerializer.Deserialize<List<ItemState>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        var ordered = new List<EnterpriseLayoutItem>();
        foreach (var state in states)
        {
            var item = FindItem(state.Name);
            if (item is null || ordered.Contains(item)) continue;
            if (!item.Locked)
            {
                item.ColumnSpan = Math.Clamp(state.ColumnSpan, 1, _columns);
                item.RowSpan = Math.Clamp(state.RowSpan, 1, 12);
                item.Visible = state.Visible;
            }

            ordered.Add(item);
        }

        ordered.AddRange(_items.Where(i => !ordered.Contains(i)));
        _items.Clear();
        _items.AddRange(ordered);
        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- layout ----

    private int CaptionHeight => _showCaptions || _customizing ? S(24) : 0;

    private int CellWidth => Math.Max(S(40), (ClientSize.Width - Padding.Horizontal - S(_spacing) * (_columns - 1)) / _columns);

    private void Relayout()
    {
        if (Width <= 0 || IsDisposed) return;
        var cellW = CellWidth;
        var cellH = S(_rowHeight);
        var gap = S(_spacing);
        var occupied = new List<bool[]>();
        bool[] Row(int index)
        {
            while (occupied.Count <= index) occupied.Add(new bool[_columns]);
            return occupied[index];
        }

        var maxRow = 0;
        foreach (var item in _items)
        {
            if (!item.Visible)
            {
                item.Control.Visible = false;
                continue;
            }

            var cols = Math.Min(item.ColumnSpan, _columns);
            var rows = item.RowSpan;
            int r = 0, col = 0;
            var placed = false;
            for (r = 0; !placed; r++)
            {
                for (col = 0; col + cols <= _columns && !placed; col++)
                {
                    var free = true;
                    for (var rr = 0; rr < rows && free; rr++)
                    {
                        var line = Row(r + rr);
                        for (var cc = 0; cc < cols; cc++) if (line[col + cc]) { free = false; break; }
                    }

                    if (!free) continue;
                    for (var rr = 0; rr < rows; rr++)
                    {
                        var line = Row(r + rr);
                        for (var cc = 0; cc < cols; cc++) line[col + cc] = true;
                    }

                    placed = true;
                    r--;
                    col--;
                }
            }

            maxRow = Math.Max(maxRow, r + rows);
            item.Frame = new Rectangle(Padding.Left + col * (cellW + gap), Padding.Top + r * (cellH + gap), cols * cellW + (cols - 1) * gap, rows * cellH + (rows - 1) * gap);
        }

        _contentHeight = Padding.Top + maxRow * cellH + Math.Max(0, maxRow - 1) * gap + Padding.Bottom + (_customizing ? S(44) : 0);
        AutoScrollMinSize = new Size(0, _contentHeight);
        var scroll = AutoScrollPosition;
        var caption = CaptionHeight;
        var inset = _customizing ? S(3) : 0;
        foreach (var item in _items.Where(i => i.Visible))
        {
            var frame = item.Frame;
            item.Control.Visible = true;
            item.Control.Bounds = new Rectangle(frame.X + inset + scroll.X, frame.Y + caption + inset + scroll.Y, Math.Max(1, frame.Width - inset * 2), Math.Max(1, frame.Height - caption - inset * 2 - (_customizing ? S(10) : 0)));
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

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    // ---- paint ----

    private Point Virtual(Point screen) => new(screen.X - AutoScrollPosition.X, screen.Y - AutoScrollPosition.Y);

    private Rectangle CaptionBounds(EnterpriseLayoutItem item) => new(item.Frame.X, item.Frame.Y, item.Frame.Width, CaptionHeight);

    private Rectangle HideButton(EnterpriseLayoutItem item) => new(item.Frame.Right - S(24), item.Frame.Y + S(2), S(20), S(20));

    private Rectangle GripBounds(EnterpriseLayoutItem item) => new(item.Frame.Right - S(16), item.Frame.Bottom - S(16), S(14), S(14));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(c.Surface);
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        foreach (var item in _items.Where(i => i.Visible))
        {
            var frame = item.Frame;
            var dragging = item == _dragItem;
            if (_customizing)
            {
                Chrome.FillRounded(g, frame, Chrome.Blend(c.Surface, c.Header, dragging ? 1 : 0.6), Chrome.CornerRadius);
                Chrome.DrawRoundedBorder(g, frame, dragging || item == _hotItem ? c.Primary : c.Border, Chrome.CornerRadius, dragging ? 2f : 1f);
            }
            else if (_showCaptions)
            {
                using var pen = new Pen(c.Border);
                g.DrawLine(pen, frame.X, frame.Y + CaptionHeight - 2, frame.Right, frame.Y + CaptionHeight - 2);
            }

            if (CaptionHeight > 0)
            {
                var bounds = CaptionBounds(item);
                var textBounds = new Rectangle(bounds.X + S(8), bounds.Y, Math.Max(0, bounds.Width - S(_customizing ? 40 : 12)), bounds.Height);
                Chrome.DrawText(g, item.Caption, BoldFont(), textBounds, c.Text);
                if (_customizing && !item.Locked)
                {
                    DrawCross(g, HideButton(item), c.Muted);
                    DrawGrip(g, GripBounds(item), c.Muted);
                }
            }
        }

        if (_customizing) DrawChips(g);
    }

    private Font? _bold;

    private Font BoldFont() => _bold is not null && Math.Abs(_bold.Size - Font.Size) < 0.01f ? _bold : (_bold = new Font(Font, FontStyle.Bold));

    private static void DrawCross(Graphics g, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var cx = bounds.X + bounds.Width / 2f;
        var cy = bounds.Y + bounds.Height / 2f;
        var r = bounds.Width / 5f;
        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
        g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
    }

    private static void DrawGrip(Graphics g, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        for (var i = 1; i <= 3; i++) g.DrawLine(pen, bounds.Right - 2, bounds.Y + bounds.Height - i * 4 - 2, bounds.Right - i * 4 - 2, bounds.Bottom - 2);
    }

    private void DrawChips(Graphics g)
    {
        _hiddenChips.Clear();
        var hidden = _items.Where(i => !i.Visible).ToList();
        var y = _contentHeight - S(38);
        var x = Padding.Left;
        var c = Colors;
        Chrome.DrawText(g, hidden.Count == 0 ? "All items are visible" : "Hidden:", Font, new Rectangle(x, y, S(100), S(30)), c.Muted);
        x += S(hidden.Count == 0 ? 0 : 64);
        if (hidden.Count == 0) return;
        foreach (var item in hidden)
        {
            var width = Chrome.MeasureText(item.Caption, Font) + S(30);
            var chip = new Rectangle(x, y + S(2), width, S(26));
            _hiddenChips.Add((item, chip));
            Chrome.FillRounded(g, chip, c.Header, S(13));
            Chrome.DrawRoundedBorder(g, chip, c.Border, S(13));
            Chrome.DrawText(g, "+ " + item.Caption, Font, chip, c.Text, ContentAlignment.MiddleCenter);
            x += width + S(6);
        }
    }

    // ---- mouse ----

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_customizing || e.Button != MouseButtons.Left) return;
        var p = Virtual(e.Location);
        foreach (var (item, bounds) in _hiddenChips)
        {
            if (!bounds.Contains(p)) continue;
            SetVisible(item, true);
            return;
        }

        foreach (var item in _items.Where(i => i.Visible && !i.Locked))
        {
            if (HideButton(item).Contains(p)) { SetVisible(item, false); return; }
            if (GripBounds(item).Contains(p)) { StartDrag(item, DragKind.Resize); return; }
            if (CaptionBounds(item).Contains(p)) { StartDrag(item, DragKind.Move); return; }
        }
    }

    private void StartDrag(EnterpriseLayoutItem item, DragKind kind)
    {
        _drag = kind;
        _dragItem = item;
        Capture = true;
        Cursor = kind == DragKind.Resize ? Cursors.SizeNWSE : Cursors.SizeAll;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = Virtual(e.Location);
        if (_drag == DragKind.Move && _dragItem is not null)
        {
            var target = _items.FirstOrDefault(i => i.Visible && i != _dragItem && i.Frame.Contains(p));
            if (target is not null && !target.Locked) MoveItem(_dragItem, _items.IndexOf(target));
            return;
        }

        if (_drag == DragKind.Resize && _dragItem is not null)
        {
            var unitW = CellWidth + S(_spacing);
            var unitH = S(_rowHeight) + S(_spacing);
            var cols = Math.Clamp((int)Math.Round((p.X - _dragItem.Frame.X + S(_spacing)) / (double)unitW), 1, _columns);
            var rows = Math.Clamp((int)Math.Round((p.Y - _dragItem.Frame.Y + S(_spacing)) / (double)unitH), 1, 12);
            if (cols != _dragItem.ColumnSpan || rows != _dragItem.RowSpan) ResizeItem(_dragItem, cols, rows);
            return;
        }

        if (!_customizing) return;
        var hot = _items.FirstOrDefault(i => i.Visible && i.Frame.Contains(p));
        if (hot != _hotItem) { _hotItem = hot; Invalidate(); }
        var overCaption = hot is not null && !hot.Locked && CaptionBounds(hot).Contains(p);
        var overGrip = hot is not null && !hot.Locked && GripBounds(hot).Contains(p);
        Cursor = overGrip ? Cursors.SizeNWSE : overCaption ? Cursors.SizeAll : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_drag == DragKind.None) return;
        _drag = DragKind.None;
        _dragItem = null;
        Capture = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold?.Dispose();
        base.Dispose(disposing);
    }
}
