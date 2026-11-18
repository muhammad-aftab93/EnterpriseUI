using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace EnterpriseUI.WinForms;

/// <summary>A document tab of an <see cref="EnterpriseTabbedForm"/>; its content fills the window while the tab is selected.</summary>
public sealed class EnterpriseTabbedFormTab
{
    private string _title;
    private Image? _icon;
    private bool _pinned;
    private bool _closable = true;

    public EnterpriseTabbedFormTab(string title, Control? content = null)
    {
        _title = title;
        Content = content;
    }

    internal EnterpriseTabbedForm? Owner { get; set; }
    internal Rectangle Bounds { get; set; }
    internal Rectangle CloseBounds { get; set; }

    public Control? Content { get; }
    public object? Tag { get; set; }

    /// <summary>Window the tab currently belongs to; null while it is in no window.</summary>
    public EnterpriseTabbedForm? Form => Owner;
    public string? ToolTip { get; set; }

    public string Title
    {
        get => _title;
        set { _title = value ?? string.Empty; Owner?.RefreshTabs(); }
    }

    public Image? Icon
    {
        get => _icon;
        set { _icon = value; Owner?.RefreshTabs(); }
    }

    /// <summary>Pinned tabs stay at the left, show only their icon and cannot be closed with the mouse.</summary>
    public bool Pinned
    {
        get => _pinned;
        set
        {
            if (_pinned == value) return;
            if (Owner != null) Owner.PinTab(this, value);
            else _pinned = value;
        }
    }

    internal void SetPinned(bool value) => _pinned = value;

    public bool Closable
    {
        get => _closable;
        set { _closable = value; Owner?.RefreshTabs(); }
    }
}

public sealed class EnterpriseTabbedFormTabEventArgs : EventArgs
{
    public EnterpriseTabbedFormTabEventArgs(EnterpriseTabbedFormTab tab) => Tab = tab;

    public EnterpriseTabbedFormTab Tab { get; }
}

/// <summary>Raised when the new-tab button is pressed; a handler can replace the tab that will be added or cancel.</summary>
public sealed class EnterpriseTabbedFormTabAddingEventArgs : CancelEventArgs
{
    public EnterpriseTabbedFormTabAddingEventArgs(EnterpriseTabbedFormTab tab) => Tab = tab;

    public EnterpriseTabbedFormTab Tab { get; set; }
}

public sealed class EnterpriseTabbedFormTabClosingEventArgs : CancelEventArgs
{
    public EnterpriseTabbedFormTabClosingEventArgs(EnterpriseTabbedFormTab tab) => Tab = tab;

    public EnterpriseTabbedFormTab Tab { get; }
}

public sealed class EnterpriseTabbedFormDetachedEventArgs : EventArgs
{
    public EnterpriseTabbedFormDetachedEventArgs(EnterpriseTabbedFormTab tab, EnterpriseTabbedForm newForm)
    {
        Tab = tab;
        NewForm = newForm;
    }

    public EnterpriseTabbedFormTab Tab { get; }

    public EnterpriseTabbedForm NewForm { get; }
}

public sealed class EnterpriseTabbedFormTabCollection : Collection<EnterpriseTabbedFormTab>
{
    private readonly EnterpriseTabbedForm _owner;

    internal EnterpriseTabbedFormTabCollection(EnterpriseTabbedForm owner) => _owner = owner;

    protected override void InsertItem(int index, EnterpriseTabbedFormTab item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Owner != null) throw new InvalidOperationException("The tab already belongs to a form.");
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.OnTabInserted(item);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        item.Owner = null;
        _owner.OnTabRemoved(item, index);
    }

    protected override void ClearItems()
    {
        var all = this.ToList();
        base.ClearItems();
        foreach (var item in all)
        {
            item.Owner = null;
            _owner.OnTabRemoved(item, 0);
        }
    }

    protected override void SetItem(int index, EnterpriseTabbedFormTab item)
    {
        RemoveItem(index);
        InsertItem(index, item);
    }

    internal void Reorder(EnterpriseTabbedFormTab tab, int newIndex)
    {
        var old = Items.IndexOf(tab);
        if (old < 0 || old == newIndex) return;
        Items.RemoveAt(old);
        Items.Insert(Math.Clamp(newIndex, 0, Items.Count), tab);
    }
}

/// <summary>Draws and operates the tabs inside a title bar: select, close, pin look, new-tab button, drag to reorder and drag out to tear a tab off.</summary>
internal sealed class TabStripControl : EnterpriseTitleBarContentControl
{
    private readonly EnterpriseTabbedForm _form;
    private Rectangle _newBounds;
    private EnterpriseTabbedFormTab? _hot;
    private bool _hotClose;
    private bool _hotNew;
    private EnterpriseTabbedFormTab? _pressed;
    private int _grabOffset;
    private Point _pressPoint;
    private bool _dragging;

    public TabStripControl(EnterpriseTabbedForm form)
    {
        _form = form;
        Size = new Size(400, 36);
    }

    public Rectangle NewTabBounds => _newBounds;

    public void Relayout()
    {
        var tabs = _form.Tabs;
        var margin = S(6);
        var newWidth = _form.ShowNewTabButton ? S(32) : 0;
        var right = Width - margin - newWidth;
        var pinned = tabs.Count(t => t.Pinned);
        var normal = tabs.Count - pinned;
        var pinnedWidth = S(44);
        var width = normal == 0 ? 0 : Math.Clamp((right - margin - pinned * pinnedWidth) / normal, S(64), S(220));
        var x = margin;
        var top = S(6);
        foreach (var tab in tabs)
        {
            var w = tab.Pinned ? pinnedWidth : width;
            tab.Bounds = new Rectangle(x, top, Math.Max(0, w), Math.Max(0, Height - top));
            var close = S(18);
            tab.CloseBounds = tab.Closable && !tab.Pinned ? new Rectangle(tab.Bounds.Right - close - S(6), tab.Bounds.Y + (tab.Bounds.Height - close) / 2, close, close) : Rectangle.Empty;
            x += w;
        }

        _newBounds = _form.ShowNewTabButton ? new Rectangle(Math.Min(x + S(4), Width - margin - newWidth), (Height - S(26)) / 2 + S(2), S(26), S(26)) : Rectangle.Empty;
        Invalidate();
    }

    public EnterpriseTabbedFormTab? TabAt(Point p) => _form.Tabs.FirstOrDefault(t => t.Bounds.Contains(p));

    public override bool IsCaptionArea(Point clientPoint) => Parent is EnterpriseTitleBarControl && TabAt(clientPoint) == null && !_newBounds.Contains(clientPoint);

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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var tab = TabAt(e.Location);
        if (e.Button == MouseButtons.Middle && tab is { Closable: true })
        {
            _form.CloseTab(tab);
            return;
        }

        if (e.Button != MouseButtons.Left || tab == null || tab.CloseBounds.Contains(e.Location)) return;
        _form.SelectedTab = tab;
        _pressed = tab;
        _pressPoint = e.Location;
        _grabOffset = e.X - tab.Bounds.X;
        _dragging = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressed != null && (e.Button & MouseButtons.Left) != 0)
        {
            DragTab(e.Location);
            return;
        }

        var tab = TabAt(e.Location);
        var close = tab != null && tab.CloseBounds.Contains(e.Location);
        var isNew = _newBounds.Contains(e.Location);
        if (tab == _hot && close == _hotClose && isNew == _hotNew) return;
        _hot = tab;
        _hotClose = close;
        _hotNew = isNew;
        Cursor = isNew ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    private void DragTab(Point p)
    {
        var tab = _pressed!;
        if (!_dragging && Math.Abs(p.X - _pressPoint.X) < S(5) && Math.Abs(p.Y - _pressPoint.Y) < S(5)) return;
        _dragging = true;
        if (_form.AllowTearOff && _form.Tabs.Count > 1 && (p.Y < -S(24) || p.Y > Height + S(24)))
        {
            _pressed = null;
            _dragging = false;
            _form.TearOffTab(tab);
            return;
        }

        if (!_form.AllowReorder) return;
        var center = p.X - _grabOffset + tab.Bounds.Width / 2;
        var target = _form.Tabs.Count(t => t != tab && t.Bounds.X + t.Bounds.Width / 2 < center);
        _form.MoveTab(tab, target);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var pressed = _pressed;
        var wasDragging = _dragging;
        _pressed = null;
        _dragging = false;
        if (e.Button != MouseButtons.Left || wasDragging) return;
        if (pressed == null)
        {
            var tab = TabAt(e.Location);
            if (tab != null && tab.CloseBounds.Contains(e.Location)) _form.CloseTab(tab);
            else if (_newBounds.Contains(e.Location)) _form.RequestNewTab();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        _hotClose = false;
        _hotNew = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent is EnterpriseTitleBarControl ? c.Header : c.Surface);
        foreach (var tab in _form.Tabs)
        {
            var b = tab.Bounds;
            if (b.Width <= 0) continue;
            var selected = tab == _form.SelectedTab;
            var hot = tab == _hot;
            using (var path = Chrome.RoundedRect(new Rectangle(b.X, b.Y, b.Width, b.Height + S(8)), S(7)))
            {
                var state = g.Save();
                g.SetClip(new Rectangle(b.X, b.Y, b.Width, b.Height), CombineMode.Replace);
                if (selected || hot)
                {
                    using var fill = new SolidBrush(selected ? c.Surface : c.HoverBack);
                    g.FillPath(fill, path);
                }

                g.Restore(state);
            }

            if (selected)
            {
                using var accent = new SolidBrush(c.Primary);
                g.FillRectangle(accent, b.X + S(6), b.Y, b.Width - S(12), S(2));
            }

            var x = b.X + S(10);
            var fore = selected ? c.Text : c.Muted;
            if (tab.Icon != null)
            {
                g.DrawImage(tab.Icon, new Rectangle(tab.Pinned ? b.X + (b.Width - S(16)) / 2 : x, b.Y + (b.Height - S(16)) / 2, S(16), S(16)));
                x += S(22);
            }
            else if (tab.Pinned)
            {
                Chrome.DrawText(g, tab.Title.Length > 0 ? tab.Title[..1].ToUpperInvariant() : "•", BoldFont, b, fore, ContentAlignment.MiddleCenter);
            }

            if (!tab.Pinned)
            {
                var textRight = (tab.CloseBounds.IsEmpty ? b.Right - S(8) : tab.CloseBounds.Left - S(2));
                Chrome.DrawText(g, tab.Title, selected ? BoldFont : Font, new Rectangle(x, b.Y, Math.Max(0, textRight - x), b.Height), fore);
            }

            if (!tab.CloseBounds.IsEmpty && (selected || hot))
            {
                var cb = tab.CloseBounds;
                if (hot && _hotClose) Chrome.FillRounded(g, cb, c.SelectionBack, S(4));
                var s = S(4);
                using var pen = new Pen(fore, Math.Max(1.2f, DpiScale * 1.2f));
                g.DrawLine(pen, cb.X + cb.Width / 2 - s, cb.Y + cb.Height / 2 - s, cb.X + cb.Width / 2 + s, cb.Y + cb.Height / 2 + s);
                g.DrawLine(pen, cb.X + cb.Width / 2 + s, cb.Y + cb.Height / 2 - s, cb.X + cb.Width / 2 - s, cb.Y + cb.Height / 2 + s);
            }
        }

        if (!_newBounds.IsEmpty)
        {
            if (_hotNew) Chrome.FillRounded(g, _newBounds, c.HoverBack, S(5));
            var cx = _newBounds.X + _newBounds.Width / 2f;
            var cy = _newBounds.Y + _newBounds.Height / 2f;
            var arm = S(5);
            using var pen = new Pen(c.Text, Math.Max(1.4f, DpiScale * 1.4f));
            g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
            g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
        }
    }
}

/// <summary>Window with browser-style document tabs in its title bar: new-tab button, close, pinned tabs, drag to reorder and drag out to open a tab in its own window.</summary>
[DefaultEvent("SelectedTabChanged")]
public class EnterpriseTabbedForm : EnterpriseForm
{
    private readonly TabStripControl _strip;
    private readonly Panel _host = new() { Dock = DockStyle.Fill };
    private EnterpriseTabbedFormTab? _selected;
    private bool _showNew = true;
    private bool _reorder = true;
    private bool _tearOff = true;
    private bool _closeWithLast;

    public EnterpriseTabbedForm()
    {
        Tabs = new EnterpriseTabbedFormTabCollection(this);
        _strip = new TabStripControl(this);
        TitleBar.ShowCaptionText = false;
        TitleBar.ContentControl = _strip;
        Controls.Add(_host);
    }

    [Browsable(false)]
    public EnterpriseTabbedFormTabCollection Tabs { get; }

    /// <summary>The strip that draws the tabs, for automation and tests.</summary>
    [Browsable(false)]
    public Control TabStrip => _strip;

    [Browsable(false)]
    public EnterpriseTabbedFormTab? SelectedTab
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            if (value != null && value.Owner != this) throw new InvalidOperationException("The tab does not belong to this form.");
            _selected = value;
            foreach (var tab in Tabs)
            {
                if (tab.Content != null) tab.Content.Visible = tab == value;
            }

            if (value?.Content != null) value.Content.BringToFront();
            _strip.Invalidate();
            SelectedTabChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public int SelectedIndex
    {
        get => _selected == null ? -1 : Tabs.IndexOf(_selected);
        set => SelectedTab = value >= 0 && value < Tabs.Count ? Tabs[value] : null;
    }

    [DefaultValue(true)]
    public bool ShowNewTabButton
    {
        get => _showNew;
        set { _showNew = value; _strip.Relayout(); }
    }

    [DefaultValue(true)]
    public bool AllowReorder
    {
        get => _reorder;
        set => _reorder = value;
    }

    /// <summary>Dragging a tab far above or below the title bar opens it in a new window.</summary>
    [DefaultValue(true)]
    public bool AllowTearOff
    {
        get => _tearOff;
        set => _tearOff = value;
    }

    /// <summary>Closes the window when its last tab is closed.</summary>
    [DefaultValue(false)]
    public bool CloseFormWithLastTab
    {
        get => _closeWithLast;
        set => _closeWithLast = value;
    }

    public event EventHandler? SelectedTabChanged;
    public event EventHandler<EnterpriseTabbedFormTabAddingEventArgs>? TabAdding;
    public event EventHandler<EnterpriseTabbedFormTabClosingEventArgs>? TabClosing;
    public event EventHandler<EnterpriseTabbedFormTabEventArgs>? TabClosed;
    public event EventHandler<EnterpriseTabbedFormTabEventArgs>? TabPinnedChanged;
    public event EventHandler<EnterpriseTabbedFormDetachedEventArgs>? TabDetached;

    public EnterpriseTabbedFormTab AddTab(string title, Control? content = null, bool pinned = false, bool select = true)
    {
        var tab = new EnterpriseTabbedFormTab(title, content);
        tab.SetPinned(pinned);
        var index = pinned ? Tabs.Count(t => t.Pinned) : Tabs.Count;
        Tabs.Insert(index, tab);
        if (select) SelectedTab = tab;
        return tab;
    }

    /// <summary>Closes a tab after <see cref="TabClosing"/> had the chance to cancel; its content is disposed.</summary>
    public bool CloseTab(EnterpriseTabbedFormTab tab)
    {
        if (tab.Owner != this) return false;
        var args = new EnterpriseTabbedFormTabClosingEventArgs(tab);
        TabClosing?.Invoke(this, args);
        if (args.Cancel) return false;
        Tabs.Remove(tab);
        tab.Content?.Dispose();
        TabClosed?.Invoke(this, new EnterpriseTabbedFormTabEventArgs(tab));
        if (Tabs.Count == 0 && _closeWithLast) Close();
        return true;
    }

    /// <summary>Pins or unpins a tab; pinned tabs are kept together at the left.</summary>
    public void PinTab(EnterpriseTabbedFormTab tab, bool pinned)
    {
        if (tab.Owner != this || tab.Pinned == pinned) return;
        tab.SetPinned(pinned);
        var pinnedCount = Tabs.Count(t => t.Pinned && t != tab);
        Tabs.Reorder(tab, pinnedCount);
        _strip.Relayout();
        TabPinnedChanged?.Invoke(this, new EnterpriseTabbedFormTabEventArgs(tab));
    }

    /// <summary>Moves a tab to a position; pinned tabs stay before unpinned ones.</summary>
    public void MoveTab(EnterpriseTabbedFormTab tab, int index)
    {
        if (tab.Owner != this) return;
        var pinned = Tabs.Count(t => t.Pinned);
        index = tab.Pinned ? Math.Clamp(index, 0, pinned - 1) : Math.Clamp(index, pinned, Tabs.Count - 1);
        Tabs.Reorder(tab, index);
        _strip.Relayout();
    }

    /// <summary>Moves a tab with its content into a new window next to this one; null when it is the only tab.</summary>
    public EnterpriseTabbedForm? DetachTab(EnterpriseTabbedFormTab tab, Point? screenLocation = null)
    {
        if (tab.Owner != this || Tabs.Count < 2) return null;
        Tabs.Remove(tab);
        var form = CreateDetachedForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Size = Size;
        form.Location = screenLocation ?? new Point(Left + 32, Top + 32);
        form.Tabs.Add(tab);
        form.SelectedTab = tab;
        form.Show();
        TabDetached?.Invoke(this, new EnterpriseTabbedFormDetachedEventArgs(tab, form));
        return form;
    }

    /// <summary>Creates the window that receives a detached tab; override to return a form of your own type.</summary>
    protected virtual EnterpriseTabbedForm CreateDetachedForm() => new() { ShowNewTabButton = _showNew, AllowReorder = _reorder, AllowTearOff = _tearOff, CloseFormWithLastTab = true };

    public Rectangle GetTabBounds(EnterpriseTabbedFormTab tab) => tab.Owner == this ? tab.Bounds : Rectangle.Empty;

    public Rectangle GetCloseBounds(EnterpriseTabbedFormTab tab) => tab.Owner == this ? tab.CloseBounds : Rectangle.Empty;

    public Rectangle GetNewTabBounds() => _strip.NewTabBounds;

    internal void RefreshTabs() => _strip.Relayout();

    internal void RequestNewTab()
    {
        var args = new EnterpriseTabbedFormTabAddingEventArgs(new EnterpriseTabbedFormTab("New tab", new Panel()));
        TabAdding?.Invoke(this, args);
        if (args.Cancel || args.Tab.Owner != null) return;
        Tabs.Insert(Tabs.Count, args.Tab);
        SelectedTab = args.Tab;
    }

    internal void TearOffTab(EnterpriseTabbedFormTab tab)
    {
        var form = DetachTab(tab, new Point(Cursor.Position.X - 60, Cursor.Position.Y - 16));
        if (form == null || !form.IsHandleCreated || (MouseButtons & MouseButtons.Left) == 0) return;
        form.BeginInvoke(new Action(() => form.TitleBar.BeginDrag()));
    }

    internal void OnTabInserted(EnterpriseTabbedFormTab tab)
    {
        if (tab.Content != null)
        {
            tab.Content.Visible = false;
            tab.Content.Dock = DockStyle.Fill;
            _host.Controls.Add(tab.Content);
        }

        if (_selected == null) SelectedTab = tab;
        _strip.Relayout();
    }

    internal void OnTabRemoved(EnterpriseTabbedFormTab tab, int index)
    {
        if (tab.Content != null && tab.Content.Parent == _host) _host.Controls.Remove(tab.Content);
        if (_selected == tab)
        {
            _selected = null;
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }

        _strip.Relayout();
    }
}
