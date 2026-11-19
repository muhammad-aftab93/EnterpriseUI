using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

public enum EnterpriseToolbarItemKind { Button, Menu, Separator }

/// <summary>A button, drop-down menu or separator of an <see cref="EnterpriseToolbarStrip"/>.</summary>
public sealed class EnterpriseToolbarItem
{
    private string _text;
    private string _glyph;
    private Image? _image;
    private bool _enabled = true;
    private bool _visible = true;
    private bool _checked;

    public EnterpriseToolbarItem(string id, string text = "", string glyph = "", EnterpriseToolbarItemKind kind = EnterpriseToolbarItemKind.Button)
    {
        Id = id;
        _text = text;
        _glyph = glyph;
        Kind = kind;
    }

    internal EnterpriseToolbarStrip? Owner { get; set; }
    internal Rectangle Bounds { get; set; }

    public string Id { get; }
    public EnterpriseToolbarItemKind Kind { get; }
    public string? ToolTip { get; set; }
    public object? Tag { get; set; }

    /// <summary>Menu opened below a <see cref="EnterpriseToolbarItemKind.Menu"/> item.</summary>
    public ContextMenuStrip? DropDown { get; set; }

    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; Owner?.RefreshItems(); }
    }

    /// <summary>Symbol drawn before the text, for example "⚙" or "★".</summary>
    public string Glyph
    {
        get => _glyph;
        set { _glyph = value ?? string.Empty; Owner?.RefreshItems(); }
    }

    public Image? Image
    {
        get => _image;
        set { _image = value; Owner?.RefreshItems(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; Owner?.Invalidate(); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; Owner?.RefreshItems(); }
    }

    /// <summary>Shows the item as pressed, for toggle commands.</summary>
    public bool Checked
    {
        get => _checked;
        set { _checked = value; Owner?.Invalidate(); }
    }
}

public sealed class EnterpriseToolbarItemEventArgs : EventArgs
{
    public EnterpriseToolbarItemEventArgs(EnterpriseToolbarItem item) => Item = item;

    public EnterpriseToolbarItem Item { get; }
}

public sealed class EnterpriseToolbarItemCollection : Collection<EnterpriseToolbarItem>
{
    private readonly EnterpriseToolbarStrip _owner;

    internal EnterpriseToolbarItemCollection(EnterpriseToolbarStrip owner) => _owner = owner;

    protected override void InsertItem(int index, EnterpriseToolbarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.RefreshItems();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.RefreshItems();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) item.Owner = null;
        base.ClearItems();
        _owner.RefreshItems();
    }

    protected override void SetItem(int index, EnterpriseToolbarItem item)
    {
        this[index].Owner = null;
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.RefreshItems();
    }
}

/// <summary>Row of command buttons, drop-down menus and an optional search box that fits in a title bar; items that do not fit move to an overflow menu.</summary>
[DefaultEvent("ItemClick")]
public class EnterpriseToolbarStrip : EnterpriseTitleBarContentControl
{
    private readonly EnterpriseTextBoxControl _search = new() { PlaceholderText = "Search", PrefixGlyph = "⌕", Visible = false };
    private readonly ToolTip _tip = new();
    private readonly List<EnterpriseToolbarItem> _overflow = new();
    private EnterpriseToolbarItem? _hot;
    private EnterpriseToolbarItem? _down;
    private bool _hotOverflow;
    private Rectangle _overflowBounds;
    private bool _showSearch;
    private int _searchWidth = 200;

    public EnterpriseToolbarStrip()
    {
        Items = new EnterpriseToolbarItemCollection(this);
        _search.ValueChanged += (_, _) => SearchTextChanged?.Invoke(this, EventArgs.Empty);
        Controls.Add(_search);
        Size = new Size(400, 36);
    }

    [Browsable(false)]
    public EnterpriseToolbarItemCollection Items { get; }

    [DefaultValue(false)]
    public bool ShowSearch
    {
        get => _showSearch;
        set
        {
            _showSearch = value;
            _search.Visible = value;
            RefreshItems();
        }
    }

    /// <summary>Width of the search box in logical pixels.</summary>
    [DefaultValue(200)]
    public int SearchWidth
    {
        get => _searchWidth;
        set
        {
            _searchWidth = Math.Clamp(value, 80, 600);
            RefreshItems();
        }
    }

    [DefaultValue("Search")]
    public string SearchPlaceholder
    {
        get => _search.PlaceholderText;
        set => _search.PlaceholderText = value;
    }

    [Browsable(false)]
    public string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>Items currently moved to the overflow menu because the strip is too narrow.</summary>
    [Browsable(false)]
    public IReadOnlyList<EnterpriseToolbarItem> OverflowItems => _overflow;

    public event EventHandler<EnterpriseToolbarItemEventArgs>? ItemClick;
    public event EventHandler<string>? SearchSubmitted;
    public event EventHandler? SearchTextChanged;

    public EnterpriseToolbarItem AddItem(string id, string text = "", string glyph = "", string? toolTip = null)
    {
        var item = new EnterpriseToolbarItem(id, text, glyph) { ToolTip = toolTip };
        Items.Add(item);
        return item;
    }

    public EnterpriseToolbarItem AddMenu(string id, string text, ContextMenuStrip dropDown)
    {
        var item = new EnterpriseToolbarItem(id, text, string.Empty, EnterpriseToolbarItemKind.Menu) { DropDown = dropDown };
        Items.Add(item);
        return item;
    }

    public void AddSeparator() => Items.Add(new EnterpriseToolbarItem("separator" + Items.Count, string.Empty, string.Empty, EnterpriseToolbarItemKind.Separator));

    public void FocusSearch()
    {
        if (_showSearch) _search.Focus();
    }

    /// <summary>Layout of an item in strip coordinates; empty while the item sits in the overflow menu.</summary>
    public Rectangle GetItemBounds(EnterpriseToolbarItem item) => item.Owner == this && !_overflow.Contains(item) && item.Visible ? item.Bounds : Rectangle.Empty;

    public Rectangle OverflowBounds => _overflowBounds;

    public Rectangle SearchBounds => _showSearch ? _search.Bounds : Rectangle.Empty;

    public EnterpriseToolbarItem? ItemAt(Point point) => Items.FirstOrDefault(i => i.Visible && i.Kind != EnterpriseToolbarItemKind.Separator && !_overflow.Contains(i) && i.Bounds.Contains(point));

    /// <summary>Clicks an item as the user would; menu items open their drop-down.</summary>
    public void PerformClick(EnterpriseToolbarItem item)
    {
        if (!item.Enabled || item.Owner != this) return;
        ItemClick?.Invoke(this, new EnterpriseToolbarItemEventArgs(item));
        if (item.Kind == EnterpriseToolbarItemKind.Menu && item.DropDown != null && IsHandleCreated) ShowDropDown(item.DropDown, item.Bounds.Left);
    }

    public override bool IsCaptionArea(Point clientPoint) =>
        Parent is EnterpriseTitleBarControl && ItemAt(clientPoint) == null && !_overflowBounds.Contains(clientPoint) && !(_showSearch && _search.Bounds.Contains(clientPoint));

    internal void RefreshItems()
    {
        ComputeLayout();
        Invalidate();
    }

    private void ComputeLayout()
    {
        if (Width <= 0 || Height <= 0) return;
        _overflow.Clear();
        _overflowBounds = Rectangle.Empty;
        var pad = S(4);
        var right = Width - pad;
        if (_showSearch)
        {
            var height = Math.Min(S(28), Math.Max(S(20), Height - S(6)));
            var width = Math.Min(S(_searchWidth), Math.Max(S(60), Width / 2));
            _search.SetBounds(Width - pad - width, (Height - height) / 2, width, height);
            right = _search.Left - S(8);
        }

        var visible = Items.Where(i => i.Visible).ToList();
        var widths = visible.ToDictionary(i => i, MeasureItem);
        var total = widths.Values.Sum();
        var limit = right;
        var needsOverflow = pad + total > right;
        if (needsOverflow) limit = right - S(30);
        var x = pad;
        var itemHeight = Math.Max(S(20), Height - S(8));
        var y = (Height - itemHeight) / 2;
        foreach (var item in visible)
        {
            var w = widths[item];
            if (x + w > limit && item.Kind != EnterpriseToolbarItemKind.Separator)
            {
                _overflow.Add(item);
                continue;
            }

            if (_overflow.Count > 0)
            {
                if (item.Kind != EnterpriseToolbarItemKind.Separator) _overflow.Add(item);
                continue;
            }

            item.Bounds = new Rectangle(x, y, w, itemHeight);
            x += w;
        }

        if (_overflow.Count > 0) _overflowBounds = new Rectangle(Math.Min(x, right - S(28)), y, S(28), itemHeight);
    }

    private int MeasureItem(EnterpriseToolbarItem item)
    {
        if (item.Kind == EnterpriseToolbarItemKind.Separator) return S(9);
        var width = S(20);
        var hasIcon = item.Glyph.Length > 0 || item.Image != null;
        if (hasIcon) width += S(18);
        if (item.Text.Length > 0) width += Chrome.MeasureText(item.Text, Font) + (hasIcon ? S(6) : 0);
        if (item.Kind == EnterpriseToolbarItemKind.Menu) width += S(14);
        return width;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ComputeLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ComputeLayout();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && _showSearch && _search.ContainsFocus)
        {
            SearchSubmitted?.Invoke(this, _search.Text);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = ItemAt(e.Location);
        var overflow = _overflowBounds.Contains(e.Location);
        if (item == _hot && overflow == _hotOverflow) return;
        _hot = item;
        _hotOverflow = overflow;
        _tip.SetToolTip(this, item?.ToolTip ?? (overflow ? "More" : string.Empty));
        Cursor = item != null || overflow ? Cursors.Hand : Cursors.Default;
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
        if (e.Button != MouseButtons.Left) return;
        _down = ItemAt(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var down = _down;
        _down = null;
        Invalidate();
        if (e.Button != MouseButtons.Left) return;
        var item = ItemAt(e.Location);
        if (item != null && item == down) PerformClick(item);
        else if (_overflowBounds.Contains(e.Location)) ShowOverflow();
    }

    private void ShowOverflow()
    {
        var menu = new ContextMenuStrip { Renderer = new EnterpriseMenuRenderer(Colors), BackColor = Colors.Surface, ForeColor = Colors.Text, ShowImageMargin = true };
        foreach (var item in _overflow)
        {
            var entry = new ToolStripMenuItem((item.Glyph.Length > 0 ? item.Glyph + "  " : string.Empty) + item.Text) { Enabled = item.Enabled, Checked = item.Checked };
            var captured = item;
            entry.Click += (_, _) => PerformClick(captured);
            menu.Items.Add(entry);
        }

        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(this, new Point(_overflowBounds.Left, Height));
    }

    private void ShowDropDown(ContextMenuStrip menu, int x)
    {
        menu.Renderer = new EnterpriseMenuRenderer(Colors);
        menu.BackColor = Colors.Surface;
        menu.ForeColor = Colors.Text;
        menu.Show(this, new Point(x, Height));
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        BackColor = Colors.Header;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent is EnterpriseTitleBarControl ? c.Header : c.Surface);
        foreach (var item in Items.Where(i => i.Visible && !_overflow.Contains(i)))
        {
            var b = item.Bounds;
            if (item.Kind == EnterpriseToolbarItemKind.Separator)
            {
                using var line = new Pen(c.Border);
                g.DrawLine(line, b.X + b.Width / 2, b.Y + S(4), b.X + b.Width / 2, b.Bottom - S(4));
                continue;
            }

            var hot = item == _hot && item.Enabled;
            if (item.Checked || hot || (_down == item && item.Enabled))
            {
                var back = _down == item ? c.SelectionBack : item.Checked ? Chrome.Blend(c.Header, c.Primary, 0.25) : c.HoverBack;
                Chrome.FillRounded(g, b, back, Chrome.CornerRadius);
            }

            var fore = item.Enabled ? c.Text : c.Disabled;
            var x = b.X + S(10);
            if (item.Image != null)
            {
                g.DrawImage(item.Image, new Rectangle(x, b.Y + (b.Height - S(16)) / 2, S(16), S(16)));
                x += S(18) + (item.Text.Length > 0 ? S(6) : 0);
            }
            else if (item.Glyph.Length > 0)
            {
                TextRenderer.DrawText(g, item.Glyph, GlyphFonts.Get(Font.SizeInPoints * 1.2f), new Rectangle(x, b.Y, S(18), b.Height), item.Enabled ? c.Primary : c.Disabled, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += S(18) + (item.Text.Length > 0 ? S(6) : 0);
            }

            if (item.Text.Length > 0) Chrome.DrawText(g, item.Text, Font, new Rectangle(x, b.Y, Math.Max(0, b.Right - x - (item.Kind == EnterpriseToolbarItemKind.Menu ? S(16) : S(8))), b.Height), fore);
            if (item.Kind == EnterpriseToolbarItemKind.Menu) Chrome.DrawChevron(g, new Rectangle(b.Right - S(18), b.Y, S(12), b.Height), ChevronDirection.Down, fore, 1.4f);
        }

        if (_overflow.Count > 0)
        {
            if (_hotOverflow) Chrome.FillRounded(g, _overflowBounds, c.HoverBack, Chrome.CornerRadius);
            Chrome.DrawText(g, "»", Font, _overflowBounds, c.Text, ContentAlignment.MiddleCenter);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Compact window that puts menus, command buttons and a search box into its title bar.</summary>
[DefaultEvent("ToolbarItemClick")]
public class EnterpriseToolbarForm : EnterpriseForm
{
    private readonly EnterpriseToolbarStrip _toolbar = new();

    public EnterpriseToolbarForm()
    {
        TitleBar.ContentControl = _toolbar;
        _toolbar.ItemClick += (_, e) => ToolbarItemClick?.Invoke(this, e);
        _toolbar.SearchSubmitted += (_, text) => SearchSubmitted?.Invoke(this, text);
        _toolbar.SearchTextChanged += (_, _) => SearchTextChanged?.Invoke(this, EventArgs.Empty);
    }

    [Browsable(false)]
    public EnterpriseToolbarStrip Toolbar => _toolbar;

    [Browsable(false)]
    public EnterpriseToolbarItemCollection ToolbarItems => _toolbar.Items;

    [DefaultValue(false)]
    public bool ShowSearch
    {
        get => _toolbar.ShowSearch;
        set => _toolbar.ShowSearch = value;
    }

    [Browsable(false)]
    public string SearchText
    {
        get => _toolbar.SearchText;
        set => _toolbar.SearchText = value;
    }

    public event EventHandler<EnterpriseToolbarItemEventArgs>? ToolbarItemClick;
    public event EventHandler<string>? SearchSubmitted;
    public event EventHandler? SearchTextChanged;

    public EnterpriseToolbarItem AddToolbarItem(string id, string text = "", string glyph = "", string? toolTip = null) => _toolbar.AddItem(id, text, glyph, toolTip);

    public EnterpriseToolbarItem AddMenu(string id, string text, ContextMenuStrip dropDown) => _toolbar.AddMenu(id, text, dropDown);

    public void FocusSearch() => _toolbar.FocusSearch();
}
