using System.ComponentModel;
using EnterpriseUI.Framework;
using EnterpriseUI.Layout;
using EnterpriseUI.Navigation;
using EnterpriseUI.WinFormsDemo.Infrastructure;
using Sparkline = EnterpriseUI.Charts.EnterpriseSparkline.SparklineKind;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>Navigation panes, menus, command bars, containers, docking, page views, live tiles, customizable layout, sparklines and the style engine.</summary>
public class NavigationContainersDemoView : UserControl
{
    private sealed record StudentRow(int Id, string Name, string Course);

    private readonly BaseDemoView _baseView = new();
    private readonly TabControl _tabs = new();

    // navigation
    private readonly EnterpriseNavigationPane _pane = new();
    private readonly EnterpriseBreadcrumbControl _breadcrumb = new();
    private readonly EnterpriseCompactNavBar _compactBar = new();
    private readonly Label _navStatus = new();

    // menus and commands
    private readonly EnterpriseMenuBar _menuBar = new();
    private readonly EnterpriseCommandBarControl _commandBar = new();
    private readonly EnterpriseContextMenu _context = new();
    private readonly EnterpriseBindingNavigatorControl _navigator = new();
    private readonly EnterpriseBackstageMenu _backstage = new();
    private readonly Label _menuStatus = new();

    // docking and pages
    private readonly EnterpriseDockHost _dock = new();
    private readonly EnterprisePageViewControl _pageView = new();
    private readonly EnterprisePanoramaControl _panorama = new();

    // layout
    private readonly EnterpriseCustomizableLayout _layout = new();
    private readonly EnterpriseSparklineControl[] _sparklines = new EnterpriseSparklineControl[4];

    public NavigationContainersDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 10f);
        _baseView.Dock = DockStyle.Fill;
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = new Font("Segoe UI", 9.5f);
        _tabs.TabPages.Add(BuildNavigationTab());
        _tabs.TabPages.Add(BuildMenusTab());
        _tabs.TabPages.Add(BuildContainersTab());
        _tabs.TabPages.Add(BuildDockingTab());
        _tabs.TabPages.Add(BuildPagesTab());
        _tabs.TabPages.Add(BuildLayoutTab());
        _tabs.TabPages.Add(BuildStyleTab());
        _baseView.DemoContainer.Controls.Add(_tabs);
    }

    private static FlowLayoutPanel Toolbar() => new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 4), WrapContents = false };

    private static Button Command(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 0) };
        button.Click += onClick;
        return button;
    }

    // ---- 1. navigation ----

    private TabPage BuildNavigationTab()
    {
        var tab = new TabPage("Navigation");
        _pane.Title = "School";
        _pane.Items.Add(EnterpriseNavigationItem.Header("Workspace"));
        _pane.Items.Add(new EnterpriseNavigationItem("Overview", "\u2302") { Key = "overview" });
        var people = new EnterpriseNavigationItem("People", "\u263A") { Key = "people", IsExpanded = true };
        people.Children.Add("Students", null, null, "128");
        people.Children.Add("Teachers", null, null, "14");
        people.Children.Add("Guardians");
        _pane.Items.Add(people);
        _pane.Items.Add(new EnterpriseNavigationItem("Reports", "\u25A4", null, "New") { Key = "reports" });
        _pane.Items.Add(EnterpriseNavigationItem.Separator());
        _pane.Items.Add(new EnterpriseNavigationItem("Archive", "\u2750") { IsEnabled = false });
        _pane.FooterItems.Add(new EnterpriseNavigationItem("Settings", "\u2699") { Key = "settings" });
        _pane.SelectedItemChanged += (_, _) =>
        {
            var text = _pane.SelectedItem?.Text ?? string.Empty;
            _navStatus.Text = "Selected: " + text;
            _breadcrumb.SetPath("School/" + (_pane.SelectedItem?.Parent is { } parent ? parent.Text + "/" : string.Empty) + text);
            _baseView.LogEvent("NavigationPane.SelectedItemChanged", text);
        };
        _pane.DisplayModeChanged += (_, _) => _baseView.LogEvent("NavigationPane.DisplayMode", _pane.IsCompact ? "Compact" : "Expanded");

        _breadcrumb.Dock = DockStyle.Top;
        _breadcrumb.Height = 36;
        _breadcrumb.SetPath("School/Overview");
        _breadcrumb.ItemClicked += (_, e) => _baseView.LogEvent("Breadcrumb.ItemClicked", e.Item.Text);

        _compactBar.Items.Add("Mail", "\u2709", null, "3");
        _compactBar.Items.Add("Calendar", "\u25A6");
        _compactBar.Items.Add("People", "\u263A");
        _compactBar.Items.Add("Tasks", "\u2714");
        _compactBar.Items.Add("Notes", "\u270E");
        _compactBar.SelectedItemChanged += (_, _) => _baseView.LogEvent("CompactNavBar.SelectedItemChanged", _compactBar.SelectedItem?.Text ?? string.Empty);

        _navStatus.Dock = DockStyle.Fill;
        _navStatus.Padding = new Padding(16);
        _navStatus.Text = "Selected: (none)";

        var toolbar = Toolbar();
        toolbar.Controls.Add(Command("Toggle compact", (_, _) => _pane.ToggleCompact()));
        toolbar.Controls.Add(Command("Auto mode", (_, _) => _pane.DisplayMode = EnterpriseNavigationDisplayMode.Auto));
        toolbar.Controls.Add(Command("Icons only bar", (_, _) => _compactBar.IconsOnly = !_compactBar.IconsOnly));

        var center = new Panel { Dock = DockStyle.Fill };
        center.Controls.Add(_navStatus);
        center.Controls.Add(_breadcrumb);
        center.Controls.Add(toolbar);
        center.Controls.Add(_compactBar);
        tab.Controls.Add(center);
        tab.Controls.Add(_pane);
        return tab;
    }

    // ---- 2. menus, commands, navigator, backstage ----

    private TabPage BuildMenusTab()
    {
        var tab = new TabPage("Menus & Commands");
        _menuBar.AddItem("&File/&New", () => Log("File/New"), Keys.Control | Keys.N);
        _menuBar.AddItem("&File/&Open...", () => Log("File/Open"), Keys.Control | Keys.O);
        _menuBar.AddItem("&File/Export/PDF", () => Log("File/Export/PDF"));
        _menuBar.AddItem("&File/Export/Word", () => Log("File/Export/Word"));
        _menuBar.AddSeparator("File");
        _menuBar.AddItem("&File/Backstage", () => _backstage.Open(tab), Keys.Control | Keys.B);
        _menuBar.AddItem("&View/Compact", () => { }, Keys.None, true);
        _menuBar.AddItem("&Help/About", () => Log("Help/About"));

        var row = _commandBar.AddRow("Main");
        _commandBar.AddCommand(row, "Save", "\u2713", () => Log("Save"));
        _commandBar.AddCommand(row, "Print", "\u2399", () => Log("Print"));
        _commandBar.AddSeparator(row);
        _commandBar.AddCommand(row, "Undo", "\u21B6", () => Log("Undo"));
        _commandBar.AddCommand(row, "Redo", "\u21B7", () => Log("Redo"));
        _commandBar.AddCommand(row, "Refresh", "\u21BB", () => Log("Refresh"));

        _context.AddItem("Copy", () => Log("Context/Copy"), Keys.Control | Keys.C);
        _context.AddItem("Paste", () => Log("Context/Paste"), Keys.Control | Keys.V);
        _context.AddSeparator();
        _context.AddItem("Send to/Printer", () => Log("Context/Printer"));
        _context.AddItem("Send to/Email", () => Log("Context/Email"));

        var rows = new BindingList<StudentRow>
        {
            new(1, "Amina Yusuf", "Biology"), new(2, "Daniel Okoye", "Chemistry"), new(3, "Mei Tanaka", "Physics"), new(4, "Lucas Silva", "History")
        };
        var source = new BindingSource { DataSource = rows };
        _navigator.BindingSource = source;
        _navigator.Dock = DockStyle.Bottom;
        _navigator.Height = 36;
        var current = new Label { Dock = DockStyle.Fill, Padding = new Padding(16), Text = "Right-click here for the context menu" };
        _context.AttachTo(current);
        source.PositionChanged += (_, _) => current.Text = source.Current is StudentRow row2 ? $"{row2.Name} - {row2.Course}" : "No record";
        _navigator.ItemAdding += (_, e) => { if (rows.Count >= 10) e.Cancel = true; };
        source.AddingNew += (_, e) => e.NewObject = new StudentRow(rows.Count + 1, "New student", "Undecided");

        _backstage.AddPage("Info", new Label { Text = "Document information appears here.", Padding = new Padding(24) }, "\u2139");
        _backstage.AddPage("Options", new Label { Text = "Application options appear here.", Padding = new Padding(24) }, "\u2699");
        _backstage.AddCommand("Exit demo", () => Log("Backstage/Exit"), "\u2716");

        _menuStatus.Dock = DockStyle.Bottom;
        _menuStatus.Height = 28;
        _menuStatus.Padding = new Padding(8, 4, 0, 0);
        _menuStatus.Text = "Use the menu, the command bar, Ctrl+B for the backstage or the record navigator.";

        _menuBar.Dock = DockStyle.Top;
        tab.Controls.Add(current);
        tab.Controls.Add(_menuStatus);
        tab.Controls.Add(_navigator);
        tab.Controls.Add(_commandBar);
        tab.Controls.Add(_menuBar);
        return tab;
    }

    private void Log(string action)
    {
        _menuStatus.Text = "Last action: " + action;
        _baseView.LogEvent("Menu.Command", action);
    }

    // ---- 3. containers ----

    private TabPage BuildContainersTab()
    {
        var tab = new TabPage("Containers");
        var split = new EnterpriseSplitPanel { Dock = DockStyle.Fill };
        var distanceSet = false;
        split.SizeChanged += (_, _) =>
        {
            if (distanceSet || split.Width < 500) return;
            distanceSet = true;
            split.SplitterDistance = split.Width / 2;
        };

        var left = new EnterpriseScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var y = 8;
        var surface = new EnterpriseSurfacePanel { Location = new Point(8, 48), Size = new Size(300, 110) };
        var surfaceLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        surface.Controls.Add(surfaceLabel);
        var surfaceStyles = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(8, 12), Width = 200 };
        surfaceStyles.Items.AddRange(Enum.GetNames<EnterpriseSurfaceStyle>());
        void ApplySurface()
        {
            var style = Enum.Parse<EnterpriseSurfaceStyle>((string)surfaceStyles.SelectedItem!);
            surface.SurfaceStyle = style;
            surfaceLabel.Text = style + " surface";
        }

        surfaceStyles.SelectedIndexChanged += (_, _) => ApplySurface();
        surfaceStyles.SelectedItem = nameof(EnterpriseSurfaceStyle.Raised);
        ApplySurface();
        left.Controls.Add(surfaceStyles);
        left.Controls.Add(surface);
        y = 176;
        left.Controls.Add(new EnterpriseDivider { Text = "Scrolls vertically", Location = new Point(8, y), Width = 300 });
        y += 28;
        for (var i = 1; i <= 8; i++, y += 26) left.Controls.Add(new Label { Text = "Scrollable row " + i, Location = new Point(8, y), AutoSize = true });
        split.Panel1.Controls.Add(left);

        var group = new EnterpriseGroupPanel { Dock = DockStyle.Top, Height = 110 };
        group.Controls.Add(new CheckBox { Text = "Option A", Location = new Point(12, 44), AutoSize = true });
        group.Controls.Add(new CheckBox { Text = "Option B", Location = new Point(120, 44), AutoSize = true });
        var groupStyles = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
        groupStyles.Items.AddRange(Enum.GetNames<EnterpriseGroupStyle>());
        void ApplyGroup()
        {
            var style = Enum.Parse<EnterpriseGroupStyle>((string)groupStyles.SelectedItem!);
            group.GroupStyle = style;
            group.Text = style + " group";
        }

        groupStyles.SelectedIndexChanged += (_, _) => ApplyGroup();
        groupStyles.SelectedItem = nameof(EnterpriseGroupStyle.Header);
        ApplyGroup();
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        right.Controls.Add(new EnterpriseDivider { Dock = DockStyle.Top, Height = 20, Text = "Fading rule", LineStyle = EnterpriseDividerStyle.Fade });
        right.Controls.Add(group);
        right.Controls.Add(groupStyles);
        split.Panel2.Controls.Add(right);
        split.CollapsedChanged += (_, _) => _baseView.LogEvent("SplitPanel.Collapsed", split.Collapsed.ToString());
        tab.Controls.Add(split);
        return tab;
    }

    // ---- 4. docking ----

    private TabPage BuildDockingTab()
    {
        var tab = new TabPage("Docking");
        _dock.Dock = DockStyle.Fill;

        var explorer = _dock.AddPanel("Explorer", DockPosition.Left, 220);
        explorer.Name = "explorer";
        explorer.Controls.Add(new ListBox { Dock = DockStyle.Fill, DataSource = new[] { "Students", "Courses", "Rooms", "Timetable" } });
        var properties = _dock.AddPanel("Properties", DockPosition.Right, 220);
        properties.Name = "properties";
        properties.Controls.Add(new Label { Text = "Properties of the selection", Dock = DockStyle.Fill });
        var output = _dock.AddPanel("Output", DockPosition.Bottom, 130);
        output.Name = "output";
        output.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = "Dock panels can be tabbed, floated, auto-hidden and closed." });
        var errors = _dock.AddPanel("Errors", DockPosition.Bottom);
        errors.Name = "errors";
        errors.Controls.Add(new Label { Text = "No errors", Dock = DockStyle.Fill });
        for (var i = 1; i <= 2; i++)
        {
            var doc = _dock.AddPanel("Document " + i, DockPosition.Document);
            doc.Name = "doc" + i;
            doc.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = "Content of document " + i });
        }

        string? saved = null;
        var toolbar = Toolbar();
        toolbar.Controls.Add(Command("Save layout", (_, _) => { saved = _dock.SaveLayout(); _baseView.LogEvent("DockHost.SaveLayout", saved.Length + " characters"); }));
        toolbar.Controls.Add(Command("Load layout", (_, _) => { if (saved is not null) _dock.LoadLayout(saved); }));
        toolbar.Controls.Add(Command("Show closed panels", (_, _) => { foreach (var p in _dock.Panels) _dock.ShowPanel(p); }));
        toolbar.Controls.Add(Command("Auto-hide Explorer", (_, _) => _dock.ToggleAutoHide(explorer)));
        _dock.LayoutChanged += (_, _) => _baseView.LogEvent("DockHost.LayoutChanged", string.Join(", ", _dock.Panels.Select(p => $"{p.Title}:{p.Position}")));
        tab.Controls.Add(_dock);
        tab.Controls.Add(toolbar);
        return tab;
    }

    // ---- 5. page views and tiles ----

    private TabPage BuildPagesTab()
    {
        var tab = new TabPage("Pages & Tiles");
        var split = new System.Windows.Forms.SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        var pagesSplitSet = false;
        split.SizeChanged += (_, _) =>
        {
            if (pagesSplitSet || split.Height < 300) return;
            pagesSplitSet = true;
            split.SplitterDistance = split.Height / 2;
        };

        foreach (var (title, glyph) in new[] { ("General", "\u2302"), ("Security", "\u26BF"), ("Appearance", "\u25D0"), ("About", "\u2139") })
        {
            var page = _pageView.AddPage(title, glyph);
            page.Controls.Add(new Label { Text = title + " settings", Dock = DockStyle.Top, Height = 40 });
        }

        _pageView.Dock = DockStyle.Fill;
        _pageView.SelectedPageChanged += (_, _) => _baseView.LogEvent("PageView.SelectedPageChanged", _pageView.SelectedPage?.Title ?? string.Empty);
        var modes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<PageViewMode>(), Width = 160 };
        modes.SelectedIndexChanged += (_, _) => { if (modes.SelectedItem is PageViewMode mode) _pageView.ViewMode = mode; };
        var pageToolbar = Toolbar();
        pageToolbar.Controls.Add(new Label { Text = "View mode", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        pageToolbar.Controls.Add(modes);
        var top = new Panel { Dock = DockStyle.Fill };
        top.Controls.Add(_pageView);
        top.Controls.Add(pageToolbar);
        split.Panel1.Controls.Add(top);

        foreach (var (group, title, span, frames) in new (string, string, int, string[])[]
        {
            ("Today", "Attendance", 2, new[] { "96%", "94% yesterday" }),
            ("Today", "Absent", 1, new[] { "7" }),
            ("Today", "Late", 1, new[] { "3", "2 excused" }),
            ("Finance", "Fees due", 2, new[] { "$12,400", "41 invoices" }),
            ("Finance", "Collected", 1, new[] { "$38,200" }),
            ("Staff", "Teachers", 1, new[] { "14" }),
            ("Staff", "Substitutes", 1, new[] { "2", "Maths, Art" })
        })
        {
            _panorama.AddTile(group, title, span, null, frames);
        }

        _panorama.HeaderTitle = "School hub";
        _panorama.AutoCycle = true;
        _panorama.FrameInterval = 3000;
        _panorama.Dock = DockStyle.Fill;
        _panorama.TileClicked += (_, e) => _baseView.LogEvent("Panorama.TileClicked", e.Tile.Title);
        split.Panel2.Controls.Add(_panorama);
        tab.Controls.Add(split);
        return tab;
    }

    // ---- 6. customizable layout and sparklines ----

    private TabPage BuildLayoutTab()
    {
        var tab = new TabPage("Layout & Trends");
        var kinds = new[] { Sparkline.Line, Sparkline.Area, Sparkline.Column, Sparkline.WinLoss };
        var data = new[]
        {
            new[] { 4d, 6, 5, 8, 7, 9, 12, 10 }, new[] { 3d, 3, 4, 6, 5, 7, 7, 9 }, new[] { 5d, 2, 6, 3, 8, 4, 9, 5 }, new[] { 1d, -1, 1, 1, -1, -1, 1, 1 }
        };
        var captions = new[] { "Enrollment", "Attendance", "Grades", "Win / loss" };
        for (var i = 0; i < 4; i++)
        {
            var spark = new EnterpriseSparklineControl { Kind = kinds[i], Dock = DockStyle.Fill };
            spark.SetValues(data[i]);
            _sparklines[i] = spark;
            _layout.AddItem(captions[i], spark, i == 0 ? 2 : 1, 1);
        }

        _layout.AddItem("Notes", new TextBox { Multiline = true, Text = "Drag captions in customization mode." }, 1, 2);
        _layout.AddItem("Calendar", new MonthCalendar(), 1, 2);
        _layout.Dock = DockStyle.Fill;
        _layout.LayoutChanged += (_, _) => _baseView.LogEvent("CustomizableLayout.LayoutChanged", string.Join(", ", _layout.Items.Select(i => $"{i.Name}:{i.ColumnSpan}x{i.RowSpan}")));

        string? saved = null;
        var toolbar = Toolbar();
        var customize = new CheckBox { Text = "Customize", AutoSize = true, Padding = new Padding(0, 4, 8, 0) };
        customize.CheckedChanged += (_, _) => _layout.IsCustomizing = customize.Checked;
        toolbar.Controls.Add(customize);
        toolbar.Controls.Add(Command("Save", (_, _) => saved = _layout.SaveLayout()));
        toolbar.Controls.Add(Command("Restore", (_, _) => { if (saved is not null) _layout.LoadLayout(saved); }));
        toolbar.Controls.Add(Command("Reset", (_, _) => _layout.ResetLayout()));
        tab.Controls.Add(_layout);
        tab.Controls.Add(toolbar);
        return tab;
    }

    // ---- 7. style engine ----

    private TabPage BuildStyleTab()
    {
        var tab = new TabPage("Style Engine");
        var css = new TextBox
        {
            Multiline = true, Dock = DockStyle.Left, Width = 420, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f),
            Text = "Window { --brand: #0a64ad; color: #202020; }\r\nButton { background: var(--brand); padding: 4 8; }\r\nPanel > Button.primary { background: #107c41; }\r\n#cancel:hover { background: #c50f1f !important; }"
        };
        var output = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f) };

        var root = new EnterpriseElement("Window", "main");
        var panel = root.Children.Add("Panel", "side");
        panel.Children.Add("Button", "ok", "primary");
        var cancel = panel.Children.Add("Button", "cancel");
        var hover = new CheckBox { Text = "Cancel is hovered", AutoSize = true, Padding = new Padding(8, 8, 0, 0), Dock = DockStyle.Top };

        void Resolve()
        {
            try
            {
                var resolver = new StyleResolver();
                resolver.AddSheet(StyleSheet.Parse(css.Text));
                cancel.SetState(ElementStates.Hover, hover.Checked);
                var builder = new System.Text.StringBuilder();
                foreach (var element in root.Descendants().Prepend(root))
                {
                    builder.AppendLine(element.Path);
                    foreach (var pair in resolver.Resolve(element).Values.OrderBy(p => p.Key)) builder.AppendLine($"    {pair.Key}: {pair.Value}");
                }

                output.Text = builder.ToString();
            }
            catch (FormatException ex)
            {
                output.Text = "Style error: " + ex.Message;
            }
        }

        css.TextChanged += (_, _) => Resolve();
        hover.CheckedChanged += (_, _) => Resolve();
        Resolve();
        tab.Controls.Add(output);
        tab.Controls.Add(hover);
        tab.Controls.Add(css);
        return tab;
    }
}
