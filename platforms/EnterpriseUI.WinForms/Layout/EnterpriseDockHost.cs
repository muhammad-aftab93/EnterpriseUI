using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;
using EnterpriseUI.Layout;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseDockPanelEventArgs : CancelEventArgs
{
    public EnterpriseDockPanelEventArgs(EnterpriseDockPanelControl panel) => Panel = panel;

    public EnterpriseDockPanelControl Panel { get; }
}

/// <summary>A tool window or document of an <see cref="EnterpriseDockHost"/>; add the content controls to it like to any panel.</summary>
[DefaultProperty(nameof(Title))]
[Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(System.ComponentModel.Design.IDesigner))]
[ToolboxItem(true)]
public class EnterpriseDockPanelControl : EnterpriseContainerBase
{
    private DockPosition _position = DockPosition.Left;

    public EnterpriseDockPanelControl() : this("Panel")
    {
    }

    public EnterpriseDockPanelControl(string title)
    {
        Model = new DockPanel { Title = title };
        base.Text = title;
        Size = new Size(240, 200);
        Padding = new Padding(4);
    }

    [Browsable(false)]
    public DockPanel Model { get; }

    [Browsable(false)]
    public EnterpriseDockHost? Host { get; internal set; }

    [Category("Docking")]
    [DefaultValue("Panel")]
    public string Title
    {
        get => Model.Title;
        set
        {
            Model.Title = value ?? string.Empty;
            base.Text = Model.Title;
            Host?.RefreshChrome();
        }
    }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public override string Text { get => Title; set => Title = value; }

    [Category("Docking")]
    [DefaultValue(DockPosition.Left)]
    public DockPosition Position
    {
        get => _position;
        set
        {
            if (Host is not null) Host.DockTo(this, value);
            else _position = Model.Position = value;
        }
    }

    internal void SetPositionCore(DockPosition value)
    {
        _position = value;
        Model.Position = value;
    }

    /// <summary>Side an auto-hidden panel slides in from.</summary>
    [Category("Docking")]
    [DefaultValue(DockPosition.Left)]
    public DockPosition AutoHideSide { get; set; } = DockPosition.Left;

    [Category("Docking")]
    [DefaultValue(true)]
    public bool CanClose { get => Model.CanClose; set => Model.CanClose = value; }

    [Category("Docking")]
    [DefaultValue(true)]
    public bool CanFloat { get => Model.CanFloat; set => Model.CanFloat = value; }

    /// <summary>True while the panel is docked, auto-hidden or floating and not closed.</summary>
    [Browsable(false)]
    public bool IsOpen => Model.IsVisible;
}

internal sealed class EnterpriseFloatingDockWindow : Form
{
    public EnterpriseFloatingDockWindow(EnterpriseDockPanelControl panel, EnterpriseDockHost host)
    {
        Panel = panel;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = panel.Title;
        ClientSize = new Size(Math.Max(220, panel.Width), Math.Max(160, panel.Height));
        KeyPreview = true;
        Host = host;
    }

    public EnterpriseDockPanelControl Panel { get; }
    public EnterpriseDockHost Host { get; }
    public bool Redocking { get; set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (Redocking || e.CloseReason == CloseReason.FormOwnerClosing || e.CloseReason == CloseReason.ApplicationExitCall) return;
        e.Cancel = !Host.ClosePanelFromFloat(Panel);
    }
}

/// <summary>Docking surface: tool windows on the four sides and a tabbed document area, with splitters, tab strips, auto-hide, floating windows, a panel menu and layout save and restore.</summary>
[DefaultEvent(nameof(LayoutChanged))]
[ToolboxItem(true)]
public class EnterpriseDockHost : EnterpriseContainerBase
{
    private sealed class Zone : EnterpriseControlBase
    {
        private readonly EnterpriseDockHost _host;
        private readonly List<(EnterpriseDockPanelControl Panel, Rectangle Bounds)> _tabs = new();
        private Rectangle _closeBtn, _pinBtn, _menuBtn;
        private int _hotTab = -1;
        private int _hotButton;

        public Zone(EnterpriseDockHost host, DockPosition position)
        {
            _host = host;
            Position = position;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
        }

        public DockPosition Position { get; }
        public List<EnterpriseDockPanelControl> Panels { get; } = new();
        public EnterpriseDockPanelControl? Active { get; set; }
        public bool IsDocument => Position == DockPosition.Document;
        public bool IsOverlay { get; set; }

        private int CaptionHeight => IsDocument ? 0 : S(26);
        private int TabStripHeight => IsDocument ? S(30) : Panels.Count > 1 ? S(26) : 0;
        private bool TabsOnTop => IsDocument;

        public Rectangle ContentBounds
        {
            get
            {
                var top = CaptionHeight + (TabsOnTop ? TabStripHeight : 0);
                var bottom = TabsOnTop ? 0 : TabStripHeight;
                return new Rectangle(1, top + 1, Math.Max(0, Width - 2), Math.Max(0, Height - top - bottom - 2));
            }
        }

        public void Sync()
        {
            if (Active is null || !Panels.Contains(Active)) Active = Panels.FirstOrDefault();
            foreach (var panel in Panels)
            {
                if (panel.Parent != this) panel.Parent = this;
                panel.Dock = DockStyle.None;
                panel.Visible = panel == Active;
            }

            if (Active is not null)
            {
                Active.Bounds = ContentBounds;
                Active.BringToFront();
            }

            LayoutChrome();
            Invalidate();
        }

        private void LayoutChrome()
        {
            _tabs.Clear();
            var x = S(2);
            var y = TabsOnTop ? CaptionHeight : Height - TabStripHeight;
            var chromeButtons = S(22);
            foreach (var panel in Panels)
            {
                if (TabStripHeight == 0) break;
                var width = Math.Min(Chrome.MeasureText(panel.Title, BoldFont) + S(IsDocument ? 44 : 28), Math.Max(S(60), (Width - S(8)) / Math.Max(1, Panels.Count)));
                _tabs.Add((panel, new Rectangle(x, y, width, TabStripHeight)));
                x += width;
            }

            var right = Width - S(4);
            _closeBtn = _pinBtn = _menuBtn = Rectangle.Empty;
            if (CaptionHeight > 0)
            {
                if (Active?.CanClose == true) { right -= chromeButtons; _closeBtn = new Rectangle(right, (CaptionHeight - chromeButtons) / 2 + 1, chromeButtons, chromeButtons); }
                right -= chromeButtons;
                _pinBtn = new Rectangle(right, (CaptionHeight - chromeButtons) / 2 + 1, chromeButtons, chromeButtons);
                right -= chromeButtons;
                _menuBtn = new Rectangle(right, (CaptionHeight - chromeButtons) / 2 + 1, chromeButtons, chromeButtons);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Active is not null) Active.Bounds = ContentBounds;
            LayoutChrome();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var tab = _tabs.FindIndex(t => t.Bounds.Contains(e.Location));
            var button = _closeBtn.Contains(e.Location) ? 1 : _pinBtn.Contains(e.Location) ? 2 : _menuBtn.Contains(e.Location) ? 3 : 0;
            if (tab == _hotTab && button == _hotButton) return;
            _hotTab = tab;
            _hotButton = button;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hotTab = -1;
            _hotButton = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var tabIndex = _tabs.FindIndex(t => t.Bounds.Contains(e.Location));
            if (tabIndex >= 0)
            {
                var (panel, bounds) = _tabs[tabIndex];
                if (e.Button == MouseButtons.Middle && panel.CanClose) { _host.ClosePanel(panel); return; }
                if (e.Button == MouseButtons.Right) { Active = panel; Sync(); _host.ShowPanelMenu(panel, this, e.Location); return; }
                if (IsDocument && panel.CanClose && new Rectangle(bounds.Right - S(24), bounds.Y, S(22), bounds.Height).Contains(e.Location)) { _host.ClosePanel(panel); return; }
                Active = panel;
                Sync();
                _host.OnActivePanelChanged(panel);
                return;
            }

            if (Active is null || e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
            if (_closeBtn.Contains(e.Location)) _host.ClosePanel(Active);
            else if (_pinBtn.Contains(e.Location)) _host.ToggleAutoHide(Active);
            else if (_menuBtn.Contains(e.Location) || e.Button == MouseButtons.Right && e.Y < CaptionHeight) _host.ShowPanelMenu(Active, this, e.Location);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (Active is not null && !IsDocument && e.Y < CaptionHeight && !_closeBtn.Contains(e.Location) && !_pinBtn.Contains(e.Location) && !_menuBtn.Contains(e.Location))
                _host.ToggleFloat(Active);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var c = Colors;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(c.Surface);
            using (var border = new Pen(c.Border)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            if (CaptionHeight > 0)
            {
                var focused = ContainsFocus || _host.ActivePanel == Active;
                var captionRect = new Rectangle(1, 1, Width - 2, CaptionHeight);
                using (var brush = new SolidBrush(focused ? Chrome.Blend(c.Header, c.Primary, 0.18) : c.Header)) g.FillRectangle(brush, captionRect);
                Chrome.DrawText(g, Active?.Title ?? string.Empty, BoldFont, new Rectangle(S(8), 1, Math.Max(0, _menuBtn.X - S(12)), CaptionHeight), c.Text);
                DrawButton(g, _menuBtn, 3, ChevronDirection.Down);
                DrawButton(g, _pinBtn, 2, null);
                DrawButton(g, _closeBtn, 1, null);
            }

            foreach (var (panel, bounds) in _tabs)
            {
                var selected = panel == Active;
                var hot = _tabs.IndexOf((panel, bounds)) == _hotTab;
                var rect = TabsOnTop ? new Rectangle(bounds.X, bounds.Y + S(3), bounds.Width, bounds.Height - S(3)) : new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height - S(3));
                if (selected)
                {
                    using var brush = new SolidBrush(c.Surface);
                    g.FillRectangle(brush, rect);
                    using var accent = new SolidBrush(c.Primary);
                    g.FillRectangle(accent, rect.X, TabsOnTop ? rect.Y : rect.Bottom - S(2), rect.Width, S(2));
                }
                else if (hot)
                {
                    Chrome.FillRounded(g, Rectangle.Inflate(rect, -S(2), -S(2)), c.HoverBack, Chrome.CornerRadius);
                }

                var textWidth = IsDocument && panel.CanClose ? rect.Width - S(26) : rect.Width;
                Chrome.DrawText(g, panel.Title, selected ? BoldFont : Font, new Rectangle(rect.X + S(8), rect.Y, Math.Max(0, textWidth - S(10)), rect.Height), selected ? c.Text : c.Muted);
                if (IsDocument && panel.CanClose && (selected || hot))
                {
                    var x = new Rectangle(rect.Right - S(24), rect.Y + (rect.Height - S(18)) / 2, S(18), S(18));
                    DrawCross(g, x, c.Muted);
                }
            }

            if (TabStripHeight > 0)
            {
                using var pen = new Pen(c.Border);
                var line = TabsOnTop ? CaptionHeight + TabStripHeight - 1 : Height - TabStripHeight;
                g.DrawLine(pen, 1, line, Width - 2, line);
            }
        }

        private void DrawButton(Graphics g, Rectangle bounds, int id, ChevronDirection? chevron)
        {
            if (bounds == Rectangle.Empty) return;
            var c = Colors;
            if (_hotButton == id) Chrome.FillRounded(g, bounds, c.HoverBack, Chrome.CornerRadius);
            if (chevron is { } direction) { Chrome.DrawChevron(g, bounds, direction, c.Muted); return; }
            if (id == 1) { DrawCross(g, bounds, c.Muted); return; }
            using var pen = new Pen(c.Muted, Math.Max(1.4f, S(1.4))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = bounds.X + bounds.Width / 2f;
            var cy = bounds.Y + bounds.Height / 2f;
            var r = bounds.Width / 5f;
            if (_host.IsAutoHidden(Active)) { g.DrawLine(pen, cx - r, cy, cx + r, cy); g.DrawLine(pen, cx, cy - r, cx, cy + r); g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2); }
            else { g.DrawLine(pen, cx - r, cy - r, cx + r, cy - r); g.DrawLine(pen, cx, cy - r, cx, cy + r * 1.2f); g.DrawLine(pen, cx - r, cy + r * 0.4f, cx + r, cy + r * 0.4f); }
        }

        private void DrawCross(Graphics g, Rectangle bounds, Color color)
        {
            using var pen = new Pen(color, Math.Max(1.4f, S(1.4))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = bounds.X + bounds.Width / 2f;
            var cy = bounds.Y + bounds.Height / 2f;
            var r = bounds.Width / 5f;
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
        }
    }

    private const int SplitterSize = 5;
    private readonly Dictionary<DockPosition, Zone> _zones = new();
    private readonly List<EnterpriseDockPanelControl> _panels = new();
    private readonly Dictionary<DockPosition, int> _sizes = new()
    {
        [DockPosition.Left] = 260, [DockPosition.Right] = 260, [DockPosition.Top] = 180, [DockPosition.Bottom] = 180
    };
    private readonly Dictionary<EnterpriseDockPanelControl, EnterpriseFloatingDockWindow> _floating = new();
    private readonly Dictionary<EnterpriseDockPanelControl, DockPosition> _lastDocked = new();
    private readonly List<(DockPosition Side, EnterpriseDockPanelControl Panel, Rectangle Bounds)> _stripTabs = new();
    private readonly List<(DockPosition Position, Rectangle Bounds)> _splitters = new();
    private Zone? _overlay;
    private IDisposable? _overlayFilter;
    private DockPosition? _dragging;
    private DockPosition? _hotSplitter;
    private EnterpriseDockPanelControl? _active;
    private bool _suspend;

    public EnterpriseDockHost()
    {
        Model = new EnterpriseDockManager();
        foreach (var position in new[] { DockPosition.Left, DockPosition.Right, DockPosition.Top, DockPosition.Bottom, DockPosition.Document })
        {
            var zone = new Zone(this, position) { Visible = false };
            _zones[position] = zone;
            Controls.Add(zone);
        }

        Size = new Size(640, 420);
        AccessibleRole = AccessibleRole.Pane;
        AccessibleName = "Dock host";
    }

    [Browsable(false)]
    public EnterpriseDockManager Model { get; }

    [Browsable(false)]
    public IReadOnlyList<EnterpriseDockPanelControl> Panels => _panels;

    [Browsable(false)]
    public EnterpriseDockPanelControl? ActivePanel => _active;

    public event EventHandler? LayoutChanged;
    public event EventHandler<EnterpriseDockPanelEventArgs>? PanelClosing;
    public event EventHandler<EnterpriseDockPanelEventArgs>? PanelClosed;
    public event EventHandler<EnterpriseDockPanelEventArgs>? ActivePanelChanged;

    // ---- panel management ----

    public EnterpriseDockPanelControl AddPanel(string title, DockPosition position, int size = 0)
    {
        var panel = new EnterpriseDockPanelControl(title);
        AddPanel(panel, position, size);
        return panel;
    }

    public void AddPanel(EnterpriseDockPanelControl panel, DockPosition position, int size = 0)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!_panels.Contains(panel))
        {
            _panels.Add(panel);
            panel.Host = this;
            Model.Panels.Add(panel.Model);
        }

        if (size > 0 && _sizes.ContainsKey(position)) _sizes[position] = size;
        DockTo(panel, position);
    }

    /// <summary>Docks (or re-docks) a panel to a side, the document area, a floating window or the auto-hide strip.</summary>
    public void DockTo(EnterpriseDockPanelControl panel, DockPosition position)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!_panels.Contains(panel)) { AddPanel(panel, position); return; }
        DetachFromZone(panel);
        CloseOverlay();
        if (_floating.TryGetValue(panel, out var window)) { window.Redocking = true; panel.Parent = null; window.Close(); window.Dispose(); _floating.Remove(panel); }

        panel.SetPositionCore(position);
        panel.Model.IsVisible = true;
        Model.Dock(panel.Model, position);
        switch (position)
        {
            case DockPosition.Floating:
                Float(panel);
                return;
            case DockPosition.AutoHide:
                if (panel.AutoHideSide is DockPosition.Document or DockPosition.Floating or DockPosition.AutoHide) panel.AutoHideSide = DockPosition.Left;
                break;
            default:
                _lastDocked[panel] = position;
                var zone = _zones[position];
                if (!zone.Panels.Contains(panel)) zone.Panels.Add(panel);
                zone.Active = panel;
                break;
        }

        _active = panel;
        Relayout();
        RaiseLayoutChanged();
    }

    public void ToggleAutoHide(EnterpriseDockPanelControl panel)
    {
        if (IsAutoHidden(panel)) DockTo(panel, _lastDocked.TryGetValue(panel, out var last) ? last : panel.AutoHideSide);
        else
        {
            if (_lastDocked.TryGetValue(panel, out var side) && side is DockPosition.Left or DockPosition.Right or DockPosition.Top or DockPosition.Bottom) panel.AutoHideSide = side;
            DockTo(panel, DockPosition.AutoHide);
        }
    }

    public bool IsAutoHidden(EnterpriseDockPanelControl? panel) => panel is not null && panel.Position == DockPosition.AutoHide && panel.Model.IsVisible;

    public void ToggleFloat(EnterpriseDockPanelControl panel)
    {
        if (_floating.ContainsKey(panel)) DockTo(panel, _lastDocked.TryGetValue(panel, out var last) ? last : DockPosition.Left);
        else if (panel.CanFloat) Float(panel);
    }

    public void Float(EnterpriseDockPanelControl panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!panel.CanFloat || _floating.ContainsKey(panel)) return;
        DetachFromZone(panel);
        CloseOverlay();
        var bounds = panel.Visible ? panel.RectangleToScreen(panel.ClientRectangle) : RectangleToScreen(new Rectangle(40, 40, 260, 220));
        panel.SetPositionCore(DockPosition.Floating);
        panel.Model.IsVisible = true;
        var window = new EnterpriseFloatingDockWindow(panel, this) { Location = new Point(bounds.X + 24, bounds.Y + 24) };
        panel.Dock = DockStyle.Fill;
        panel.Visible = true;
        window.Controls.Add(panel);
        _floating[panel] = window;
        var owner = FindForm();
        if (owner is not null) window.Show(owner);
        else window.Show();
        Relayout();
        RaiseLayoutChanged();
    }

    public bool ClosePanel(EnterpriseDockPanelControl panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!panel.CanClose) return false;
        var args = new EnterpriseDockPanelEventArgs(panel);
        PanelClosing?.Invoke(this, args);
        if (args.Cancel) return false;
        HidePanel(panel);
        PanelClosed?.Invoke(this, new EnterpriseDockPanelEventArgs(panel));
        return true;
    }

    internal bool ClosePanelFromFloat(EnterpriseDockPanelControl panel)
    {
        if (!panel.CanClose) return false;
        var args = new EnterpriseDockPanelEventArgs(panel);
        PanelClosing?.Invoke(this, args);
        if (args.Cancel) return false;
        HidePanel(panel);
        PanelClosed?.Invoke(this, new EnterpriseDockPanelEventArgs(panel));
        return true;
    }

    private void HidePanel(EnterpriseDockPanelControl panel)
    {
        DetachFromZone(panel);
        CloseOverlay();
        if (_floating.TryGetValue(panel, out var window))
        {
            window.Redocking = true;
            panel.Parent = null;
            window.Close();
            window.Dispose();
            _floating.Remove(panel);
        }

        panel.Parent = null;
        panel.Visible = false;
        panel.Model.IsVisible = false;
        if (_active == panel) _active = null;
        Relayout();
        RaiseLayoutChanged();
    }

    /// <summary>Opens a closed panel again at its last docked position.</summary>
    public void ShowPanel(EnterpriseDockPanelControl panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (panel.Model.IsVisible) { Activate(panel); return; }
        DockTo(panel, _lastDocked.TryGetValue(panel, out var last) ? last : panel.Position == DockPosition.Floating || panel.Position == DockPosition.AutoHide ? DockPosition.Left : panel.Position);
    }

    public void Activate(EnterpriseDockPanelControl panel)
    {
        if (panel.Position == DockPosition.AutoHide) { ShowOverlay(panel); return; }
        if (_zones.TryGetValue(panel.Position, out var zone) && zone.Panels.Contains(panel))
        {
            zone.Active = panel;
            zone.Sync();
        }

        OnActivePanelChanged(panel);
    }

    internal void OnActivePanelChanged(EnterpriseDockPanelControl panel)
    {
        _active = panel;
        ActivePanelChanged?.Invoke(this, new EnterpriseDockPanelEventArgs(panel));
        foreach (var zone in _zones.Values) zone.Invalidate();
    }

    public EnterpriseDockPanelControl? FindPanel(string name) => _panels.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Title, name, StringComparison.OrdinalIgnoreCase));

    private void DetachFromZone(EnterpriseDockPanelControl panel)
    {
        foreach (var zone in _zones.Values)
        {
            if (!zone.Panels.Remove(panel)) continue;
            if (zone.Active == panel) zone.Active = zone.Panels.LastOrDefault();
            zone.Sync();
        }
    }

    internal void RefreshChrome()
    {
        foreach (var zone in _zones.Values) zone.Sync();
        Invalidate();
    }

    private void RaiseLayoutChanged()
    {
        if (!_suspend) LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ShowPanelMenu(EnterpriseDockPanelControl panel, Control anchor, Point location)
    {
        var menu = new ContextMenuStrip();
        EnterpriseMenuRenderer.Apply(menu);
        ToolStripMenuItem Item(string text, Action action, bool enabled = true, bool isChecked = false)
        {
            var item = new ToolStripMenuItem(text) { Enabled = enabled, Checked = isChecked };
            item.Click += (_, _) => action();
            return item;
        }

        var docked = panel.Position is DockPosition.Left or DockPosition.Right or DockPosition.Top or DockPosition.Bottom or DockPosition.Document;
        menu.Items.Add(Item("Float", () => ToggleFloat(panel), panel.CanFloat, panel.Position == DockPosition.Floating));
        menu.Items.Add(Item("Auto hide", () => ToggleAutoHide(panel), docked && panel.Position != DockPosition.Document || IsAutoHidden(panel), IsAutoHidden(panel)));
        menu.Items.Add(new ToolStripSeparator());
        foreach (var target in new[] { DockPosition.Left, DockPosition.Right, DockPosition.Top, DockPosition.Bottom, DockPosition.Document })
        {
            var position = target;
            menu.Items.Add(Item("Dock " + (target == DockPosition.Document ? "as document" : target.ToString().ToLowerInvariant()), () => DockTo(panel, position), true, panel.Position == target));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Close", () => ClosePanel(panel), panel.CanClose));
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(anchor, location);
    }

    // ---- layout ----

    private int Size_(DockPosition position) => S(_sizes[position]);

    private Rectangle StripBounds(DockPosition side)
    {
        var strip = S(26);
        return side switch
        {
            DockPosition.Left => new Rectangle(0, 0, strip, Height),
            DockPosition.Right => new Rectangle(Width - strip, 0, strip, Height),
            DockPosition.Top => new Rectangle(0, 0, Width, strip),
            _ => new Rectangle(0, Height - strip, Width, strip)
        };
    }

    private bool HasAutoHide(DockPosition side) => _panels.Any(p => IsAutoHidden(p) && p.AutoHideSide == side);

    private void Relayout()
    {
        if (Width <= 0 || Height <= 0 || IsDisposed || _zones.Count == 0) return;
        _splitters.Clear();
        _stripTabs.Clear();
        var rect = new Rectangle(0, 0, Width, Height);
        var strip = S(26);
        var autoSides = new[] { DockPosition.Left, DockPosition.Right, DockPosition.Top, DockPosition.Bottom }.Where(HasAutoHide).ToList();
        foreach (var side in autoSides)
        {
            switch (side)
            {
                case DockPosition.Left: rect = new Rectangle(rect.X + strip, rect.Y, rect.Width - strip, rect.Height); break;
                case DockPosition.Right: rect = new Rectangle(rect.X, rect.Y, rect.Width - strip, rect.Height); break;
                case DockPosition.Top: rect = new Rectangle(rect.X, rect.Y + strip, rect.Width, rect.Height - strip); break;
                default: rect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - strip); break;
            }
        }

        var gap = S(SplitterSize);
        var minCenter = S(120);
        foreach (var side in new[] { DockPosition.Left, DockPosition.Right })
        {
            var zone = _zones[side];
            zone.Visible = zone.Panels.Count > 0;
            if (!zone.Visible) continue;
            var size = Math.Clamp(Size_(side), S(80), Math.Max(S(80), rect.Width - minCenter));
            if (side == DockPosition.Left)
            {
                zone.Bounds = new Rectangle(rect.X, rect.Y, size, rect.Height);
                _splitters.Add((side, new Rectangle(rect.X + size, rect.Y, gap, rect.Height)));
                rect = new Rectangle(rect.X + size + gap, rect.Y, Math.Max(0, rect.Width - size - gap), rect.Height);
            }
            else
            {
                zone.Bounds = new Rectangle(rect.Right - size, rect.Y, size, rect.Height);
                _splitters.Add((side, new Rectangle(rect.Right - size - gap, rect.Y, gap, rect.Height)));
                rect = new Rectangle(rect.X, rect.Y, Math.Max(0, rect.Width - size - gap), rect.Height);
            }
        }

        foreach (var side in new[] { DockPosition.Top, DockPosition.Bottom })
        {
            var zone = _zones[side];
            zone.Visible = zone.Panels.Count > 0;
            if (!zone.Visible) continue;
            var size = Math.Clamp(Size_(side), S(60), Math.Max(S(60), rect.Height - S(80)));
            if (side == DockPosition.Top)
            {
                zone.Bounds = new Rectangle(rect.X, rect.Y, rect.Width, size);
                _splitters.Add((side, new Rectangle(rect.X, rect.Y + size, rect.Width, gap)));
                rect = new Rectangle(rect.X, rect.Y + size + gap, rect.Width, Math.Max(0, rect.Height - size - gap));
            }
            else
            {
                zone.Bounds = new Rectangle(rect.X, rect.Bottom - size, rect.Width, size);
                _splitters.Add((side, new Rectangle(rect.X, rect.Bottom - size - gap, rect.Width, gap)));
                rect = new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(0, rect.Height - size - gap));
            }
        }

        var document = _zones[DockPosition.Document];
        document.Visible = document.Panels.Count > 0;
        if (document.Visible) document.Bounds = rect;

        foreach (var side in autoSides)
        {
            var bounds = StripBounds(side);
            var vertical = side is DockPosition.Left or DockPosition.Right;
            var x = vertical ? bounds.X : bounds.X + S(4);
            var y = vertical ? bounds.Y + S(4) : bounds.Y;
            foreach (var panel in _panels.Where(p => IsAutoHidden(p) && p.AutoHideSide == side))
            {
                var length = Chrome.MeasureText(panel.Title, Font) + S(22);
                var tab = vertical ? new Rectangle(bounds.X, y, bounds.Width, length) : new Rectangle(x, bounds.Y, length, bounds.Height);
                _stripTabs.Add((side, panel, tab));
                if (vertical) y += length + S(2);
                else x += length + S(2);
            }
        }

        foreach (var zone in _zones.Values)
        {
            if (zone.Visible) zone.Sync();
        }

        if (_overlay is not null) PositionOverlay();
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
        foreach (var zone in _zones.Values) zone.Invalidate();
        _overlay?.Invalidate();
    }

    // ---- auto-hide overlay ----

    private void ShowOverlay(EnterpriseDockPanelControl panel)
    {
        if (_overlay?.Active == panel) return;
        CloseOverlay();
        var side = panel.AutoHideSide;
        _overlay = new Zone(this, side) { IsOverlay = true };
        _overlay.Panels.Add(panel);
        _overlay.Active = panel;
        Controls.Add(_overlay);
        _overlay.BringToFront();
        PositionOverlay();
        _overlay.Sync();
        OnActivePanelChanged(panel);
        _overlayFilter = PopupDismissFilter.Attach(() => _overlay is null ? Rectangle.Empty : RectangleToScreen(_overlay.Bounds), CloseOverlay, OnOverlayOutsideClick);
    }

    private void OnOverlayOutsideClick()
    {
        // Clicks on the strip tabs are handled by the host itself; everything else dismisses the overlay.
        var point = PointToClient(MousePosition);
        if (_stripTabs.Any(t => t.Bounds.Contains(point))) return;
        CloseOverlay();
    }

    private void PositionOverlay()
    {
        if (_overlay?.Active is null) return;
        var side = _overlay.Active.AutoHideSide;
        var strip = S(26);
        var size = Size_(side is DockPosition.Left or DockPosition.Right ? side : side);
        var area = new Rectangle(HasAutoHide(DockPosition.Left) ? strip : 0, HasAutoHide(DockPosition.Top) ? strip : 0, 0, 0);
        area.Width = Width - area.X - (HasAutoHide(DockPosition.Right) ? strip : 0);
        area.Height = Height - area.Y - (HasAutoHide(DockPosition.Bottom) ? strip : 0);
        _overlay.Bounds = side switch
        {
            DockPosition.Left => new Rectangle(area.X, area.Y, Math.Min(size, area.Width), area.Height),
            DockPosition.Right => new Rectangle(area.Right - Math.Min(size, area.Width), area.Y, Math.Min(size, area.Width), area.Height),
            DockPosition.Top => new Rectangle(area.X, area.Y, area.Width, Math.Min(size, area.Height)),
            _ => new Rectangle(area.X, area.Bottom - Math.Min(size, area.Height), area.Width, Math.Min(size, area.Height))
        };
    }

    private void CloseOverlay()
    {
        _overlayFilter?.Dispose();
        _overlayFilter = null;
        if (_overlay is null) return;
        var overlay = _overlay;
        _overlay = null;
        foreach (var panel in overlay.Panels.ToList())
        {
            overlay.Panels.Remove(panel);
            if (panel.Parent == overlay) panel.Parent = null;
            panel.Visible = false;
        }

        Controls.Remove(overlay);
        overlay.Dispose();
    }

    // ---- splitters and strips ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging is { } position)
        {
            var size = position switch
            {
                DockPosition.Left => e.X - (HasAutoHide(DockPosition.Left) ? S(26) : 0),
                DockPosition.Right => Width - e.X - (HasAutoHide(DockPosition.Right) ? S(26) : 0),
                DockPosition.Top => e.Y - (HasAutoHide(DockPosition.Top) ? S(26) : 0),
                _ => Height - e.Y - (HasAutoHide(DockPosition.Bottom) ? S(26) : 0)
            };
            _sizes[position] = (int)Math.Max(60, size / DpiScale);
            Relayout();
            return;
        }

        var hot = _splitters.Where(s => s.Bounds.Contains(e.Location)).Select(s => (DockPosition?)s.Position).FirstOrDefault();
        if (hot != _hotSplitter)
        {
            _hotSplitter = hot;
            Cursor = hot is DockPosition.Left or DockPosition.Right ? Cursors.VSplit : hot is not null ? Cursors.HSplit : Cursors.Default;
            Invalidate();
        }

        var tab = _stripTabs.FirstOrDefault(t => t.Bounds.Contains(e.Location));
        if (tab.Panel is not null && _overlay?.Active != tab.Panel) ShowOverlay(tab.Panel);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var splitter = _splitters.FirstOrDefault(s => s.Bounds.Contains(e.Location));
        if (splitter.Bounds != Rectangle.Empty)
        {
            _dragging = splitter.Position;
            Capture = true;
            return;
        }

        var tab = _stripTabs.FirstOrDefault(t => t.Bounds.Contains(e.Location));
        if (tab.Panel is not null)
        {
            if (_overlay?.Active == tab.Panel) CloseOverlay();
            else ShowOverlay(tab.Panel);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging is null) return;
        _dragging = null;
        Capture = false;
        RaiseLayoutChanged();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotSplitter is not null) { _hotSplitter = null; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = Colors;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Chrome.Blend(c.Header, c.Border, 0.35));
        foreach (var (position, bounds) in _splitters)
        {
            if (_hotSplitter != position && _dragging != position) continue;
            using var brush = new SolidBrush(Color.FromArgb(150, c.Primary));
            g.FillRectangle(brush, bounds);
        }

        foreach (var side in new[] { DockPosition.Left, DockPosition.Right, DockPosition.Top, DockPosition.Bottom })
        {
            if (!HasAutoHide(side)) continue;
            using var brush = new SolidBrush(c.Header);
            g.FillRectangle(brush, StripBounds(side));
        }

        foreach (var (side, panel, bounds) in _stripTabs)
        {
            var active = _overlay?.Active == panel;
            Chrome.FillRounded(g, Rectangle.Inflate(bounds, -S(2), -S(2)), active ? c.SelectionBack : c.Surface, Chrome.CornerRadius);
            var vertical = side is DockPosition.Left or DockPosition.Right;
            if (!vertical)
            {
                Chrome.DrawText(g, panel.Title, Font, bounds, c.Text, ContentAlignment.MiddleCenter);
                continue;
            }

            var state = g.Save();
            g.TranslateTransform(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            g.RotateTransform(90);
            Chrome.DrawText(g, panel.Title, Font, new Rectangle(-bounds.Height / 2, -bounds.Width / 2, bounds.Height, bounds.Width), c.Text, ContentAlignment.MiddleCenter);
            g.Restore(state);
        }
    }

    // ---- persistence ----

    private sealed class LayoutState
    {
        public Dictionary<string, int> Sizes { get; set; } = new();
        public List<PanelState> Panels { get; set; } = new();
        public string? Active { get; set; }
    }

    private sealed class PanelState
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = nameof(DockPosition.Left);
        public string AutoHideSide { get; set; } = nameof(DockPosition.Left);
        public bool Visible { get; set; } = true;
        public bool IsActiveInZone { get; set; }
    }

    private static string KeyOf(EnterpriseDockPanelControl panel) => string.IsNullOrEmpty(panel.Name) ? panel.Title : panel.Name;

    /// <summary>Serializes sizes, panel positions, visibility and the active panel of each zone to JSON.</summary>
    public string SaveLayout()
    {
        var state = new LayoutState { Active = _active is null ? null : KeyOf(_active) };
        foreach (var pair in _sizes) state.Sizes[pair.Key.ToString()] = pair.Value;
        foreach (var panel in _panels)
        {
            var zone = _zones.Values.FirstOrDefault(z => z.Panels.Contains(panel));
            state.Panels.Add(new PanelState
            {
                Name = KeyOf(panel),
                Position = (panel.Model.IsVisible ? panel.Position : _lastDocked.TryGetValue(panel, out var last) ? last : panel.Position).ToString(),
                AutoHideSide = panel.AutoHideSide.ToString(),
                Visible = panel.Model.IsVisible,
                IsActiveInZone = zone?.Active == panel
            });
        }

        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Restores a layout produced by <see cref="SaveLayout"/>; panels are matched by name or title and unknown entries are ignored.</summary>
    public void LoadLayout(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var state = JsonSerializer.Deserialize<LayoutState>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LayoutState();
        _suspend = true;
        try
        {
            foreach (var pair in state.Sizes)
            {
                if (Enum.TryParse<DockPosition>(pair.Key, out var position) && _sizes.ContainsKey(position)) _sizes[position] = Math.Clamp(pair.Value, 40, 4000);
            }

            foreach (var entry in state.Panels)
            {
                var panel = FindPanel(entry.Name);
                if (panel is null) continue;
                if (Enum.TryParse<DockPosition>(entry.AutoHideSide, out var side)) panel.AutoHideSide = side;
                if (!Enum.TryParse<DockPosition>(entry.Position, out var position)) continue;
                if (!entry.Visible)
                {
                    if (position is not (DockPosition.Floating or DockPosition.AutoHide)) _lastDocked[panel] = position;
                    if (panel.Model.IsVisible) HidePanel(panel);
                    continue;
                }

                DockTo(panel, position == DockPosition.Floating ? DockPosition.Left : position);
            }

            foreach (var entry in state.Panels.Where(p => p.IsActiveInZone && p.Visible))
            {
                var panel = FindPanel(entry.Name);
                if (panel is not null && panel.Position != DockPosition.AutoHide) Activate(panel);
            }
        }
        finally
        {
            _suspend = false;
        }

        Relayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseOverlay();
            foreach (var window in _floating.Values.ToList())
            {
                window.Redocking = true;
                window.Dispose();
            }

            _floating.Clear();
        }

        base.Dispose(disposing);
    }
}
