using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo;

public partial class MainForm : Form, IThemeableControl
{
    private readonly EnterpriseRibbonControl _ribbon=new EnterpriseRibbonControl();
    private readonly SplitContainer _mainSplit=new SplitContainer();
    private readonly SplitContainer _centerSplit=new SplitContainer();
    private readonly Panel _leftNavPanel=new Panel();
    private readonly TextBox _searchBox=new TextBox();
    private readonly TreeView _navTree=new TreeView();
    private readonly Panel _demoWorkspaceContainer=new Panel();
    private readonly Panel _breadcrumbBar=new Panel();
    private readonly Label _lblBreadcrumb=new Label();
    private readonly Button _btnFavorite=new Button();
    private readonly ComboBox _cmbRecent=new ComboBox();
    private readonly ComboBox _cmbThemes=new ComboBox();
    private readonly Panel _demoHost=new Panel();
    private readonly EnterprisePropertyInspectorControl _inspector=new EnterprisePropertyInspectorControl();
    private readonly EnterpriseStatusStrip _statusStrip=new EnterpriseStatusStrip();
    private readonly EnterpriseStatusPanel _statusTheme;
    private readonly EnterpriseStatusPanel _statusDemo;
    private readonly EnterpriseStatusPanel _statusMemory;
    private readonly EnterpriseStatusPanel _statusLatency;
    private readonly EnterpriseStatusPanel _statusA11y;
    private readonly System.Windows.Forms.Timer _memoryMonitorTimer=new System.Windows.Forms.Timer();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private DemoItem? _currentDemo;

    private readonly HashSet<string> _favoriteIds = new();
    private readonly List<DemoItem> _recentDemos = new();

    public string? ThemeOverride { get; set; }

    public MainForm()
    {
        Text = "EnterpriseUI Demo Center";
        
        // 1080p target sizing & responsive constraints
        Size = new Size(1920, 1040);
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;
        _ribbon.BackstageClicked += ShowBackstageDialog;
        _ribbon.TabChanged += OnRibbonTabChanged;
        _statusTheme = new EnterpriseStatusPanel("theme", $"Theme: {_theme.Name}") { Width = 210 };
        _statusDemo = new EnterpriseStatusPanel("demo", "Demo: Ready") { Spring = true };
        _statusMemory = new EnterpriseStatusPanel("memory", "RAM: 0.0 MB") { Width = 150 };
        _statusLatency = new EnterpriseStatusPanel("latency", "Latency: 0 ms") { Width = 150 };
        _statusA11y = new EnterpriseStatusPanel("a11y", "Accessibility: verification required") { Width = 290 };

        foreach (var panel in new[] { _statusTheme, _statusDemo, _statusMemory, _statusLatency, _statusA11y }) _statusStrip.Panels.Add(panel);

        // 3. Main Split Container (Left Nav | Center & Right Inspector)
        _mainSplit.            Dock = DockStyle.Fill;

        // 3. Main Split Container (Left Nav | Center & Right Inspector)
        _mainSplit.            Orientation = Orientation.Vertical;

        // 3. Main Split Container (Left Nav | Center & Right Inspector)
        _mainSplit.            FixedPanel = FixedPanel.Panel1;

        // 3. Main Split Container (Left Nav | Center & Right Inspector)
        _mainSplit.            SplitterWidth = 6
;

        // Left Navigation Panel
        _leftNavPanel.Dock = DockStyle.Fill ;

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        _searchBox.            Dock = DockStyle.Fill;
        _searchBox.            Font = new Font("Segoe UI", 9.5f);
        _searchBox.            PlaceholderText = "Search controls, features, scenarios..."
;
        _searchBox.TextChanged += (_, _) => ApplySearchFilter(_searchBox.Text);
        searchPanel.Controls.Add(_searchBox);

        _navTree.            Dock = DockStyle.Fill;

        _navTree.            Font = new Font("Segoe UI", 9.5f);

        _navTree.            ShowLines = true;

        _navTree.            ShowPlusMinus = true;

        _navTree.            HideSelection = false
;
        _navTree.AfterSelect += OnNavNodeSelected;

        _leftNavPanel.Controls.Add(_navTree);
        _leftNavPanel.Controls.Add(searchPanel);
        _mainSplit.Panel1.Controls.Add(_leftNavPanel);

        // Center Split (Center Demo Host | Right Property Inspector)
        _centerSplit.            Dock = DockStyle.Fill;

        // Center Split (Center Demo Host | Right Property Inspector)
        _centerSplit.            Orientation = Orientation.Vertical;

        // Center Split (Center Demo Host | Right Property Inspector)
        _centerSplit.            FixedPanel = FixedPanel.Panel2;

        // Center Split (Center Demo Host | Right Property Inspector)
        _centerSplit.            SplitterWidth = 6
;

        // Workspace Container with EnterpriseBreadcrumb & Action Toolbar
        _demoWorkspaceContainer.Dock = DockStyle.Fill ;

        _breadcrumbBar.            Dock = DockStyle.Top;

        _breadcrumbBar.            Height = 40;

        _breadcrumbBar.            Padding = new Padding(12, 6, 12, 6);

        _breadcrumbBar.            BackColor = Color.FromArgb(246, 248, 250)
;

        _lblBreadcrumb.            Text = "🏠 Home > Control Explorer > Ready";

        _lblBreadcrumb.            Dock = DockStyle.Left;

        _lblBreadcrumb.            AutoSize = true;

        _lblBreadcrumb.            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        _lblBreadcrumb.            ForeColor = Color.FromArgb(60, 64, 70);

        _lblBreadcrumb.            TextAlign = ContentAlignment.MiddleLeft
;

        var rightToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnFavorite.            Text = "⭐ Favorite";

        _btnFavorite.            Size = new Size(100, 28);

        _btnFavorite.            FlatStyle = FlatStyle.Flat;

        _btnFavorite.            Font = new Font("Segoe UI", 10f)
;
        _btnFavorite.Click += OnFavoriteToggle;

        var lblRecent = new Label
        {
            Text = "Recent:",
            AutoSize = true,
            Padding = new Padding(8, 6, 2, 0),
            Font = new Font("Segoe UI", 10f)
        };

        _cmbRecent.            DropDownStyle = ComboBoxStyle.DropDownList;

        _cmbRecent.            Size = new Size(170, 28);

        _cmbRecent.            Font = new Font("Segoe UI", 10f)
;
        _cmbRecent.SelectedIndexChanged += OnRecentSelected;

        var lblThemes = new Label
        {
            Text = "Theme:",
            AutoSize = true,
            Padding = new Padding(8, 6, 2, 0),
            Font = new Font("Segoe UI", 10f)
        };

        _cmbThemes.            DropDownStyle = ComboBoxStyle.DropDownList;

        _cmbThemes.            Size = new Size(160, 28);

        _cmbThemes.            Font = new Font("Segoe UI", 10f)
;
        PopulateThemeSelector();
        _cmbThemes.SelectedIndexChanged += OnThemeComboChanged;

        rightToolbar.Controls.Add(_btnFavorite);
        rightToolbar.Controls.Add(lblRecent);
        rightToolbar.Controls.Add(_cmbRecent);
        rightToolbar.Controls.Add(lblThemes);
        rightToolbar.Controls.Add(_cmbThemes);

        _breadcrumbBar.Controls.Add(_lblBreadcrumb);
        _breadcrumbBar.Controls.Add(rightToolbar);

        _demoHost.            Dock = DockStyle.Fill;

        _demoHost.            BackColor = Color.White
;

        _demoWorkspaceContainer.Controls.Add(_demoHost);
        _demoWorkspaceContainer.Controls.Add(_breadcrumbBar);

        _centerSplit.Panel1.Controls.Add(_demoWorkspaceContainer);

        _inspector.Dock = DockStyle.Fill ;
        _inspector.PropertyMutated += rec =>
        {
            if (_demoHost.Controls.Count > 0)
            {
                var root = _demoHost.Controls[0];
                if (root is BaseDemoView bdv)
                {
                    bdv.RecordExternalMutation(rec);
                }
                else if (root.Controls.Count > 0 && root.Controls[0] is BaseDemoView childBdv)
                {
                    childBdv.RecordExternalMutation(rec);
                }
            }
        };
        _inspector.ResetRequested += () =>
        {
            if (_currentDemo != null)
            {
                LoadDemo(_currentDemo);
            }
        };
        _centerSplit.Panel2.Controls.Add(_inspector);

        _mainSplit.Panel2.Controls.Add(_centerSplit);

        // Assemble Main Form Controls
        Controls.Add(_mainSplit);
        Controls.Add(_ribbon);
        Controls.Add(_statusStrip);

        // Listen for Theme Changes
        ThemeManager.Instance.ThemeChanged += HandleThemeChanged;

        // Listen for Navigation Changes
        DemoNavigationManager.Instance.DemoSelected += LoadDemo;

        // Build Nav Hierarchy
        PopulateNavigationTree();

        // Memory Monitor Timer
        _memoryMonitorTimer.Interval = 1000 ;
        _memoryMonitorTimer.Tick += (_, _) =>
        {
            _statusMemory.Text = $"RAM: {DemoAnalyticsTracker.Instance.GetFormattedMemory()}";
        };
        _memoryMonitorTimer.Start();
        RegisterRibbonActions();

        // Load Default Demo & Apply Optimal Left/Right Panel Widths
        SizeChanged += (_, _) => ApplyOptimalPanelWidths();
        Load += (_, _) => ApplyOptimalPanelWidths();
        Shown += (_, _) =>
        {
            ApplyOptimalPanelWidths();
            ApplyTheme(BuiltInThemes.CreateFluent(ColorScheme.Light));
            if (_navTree.Nodes.Count > 0 && _navTree.Nodes[0].Nodes.Count > 0)
            {
                _navTree.SelectedNode = _navTree.Nodes[0].Nodes[0];
            }
        };
    }

    private void ApplyOptimalPanelWidths()
    {
        try
        {
            var narrow = ClientSize.Width < 1440;
            var targetLeftWidth = narrow ? 260 : 380;
            var targetRightWidth = narrow ? 320 : 480;

            _mainSplit.Panel1MinSize = 220;
            _mainSplit.Panel2MinSize = 500;
            if (_mainSplit.Width > _mainSplit.Panel1MinSize + _mainSplit.Panel2MinSize)
            {
                _mainSplit.SplitterDistance = Math.Clamp(targetLeftWidth, _mainSplit.Panel1MinSize,
                    _mainSplit.Width - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth);
            }

            _centerSplit.Panel1MinSize = 320;
            _centerSplit.Panel2MinSize = 260;
            int centerTotalWidth = _centerSplit.Width;
            if (centerTotalWidth > _centerSplit.Panel1MinSize + _centerSplit.Panel2MinSize)
            {
                var desiredCenterDistance = Math.Max(
                    _centerSplit.Panel1MinSize,
                    centerTotalWidth - targetRightWidth - _centerSplit.SplitterWidth);
                _centerSplit.SplitterDistance = Math.Min(desiredCenterDistance,
                    centerTotalWidth - _centerSplit.Panel2MinSize - _centerSplit.SplitterWidth);
            }
        }
        catch
        {
            // Guard against intermediate layout states during initialization
        }
    }

    private void PopulateThemeSelector()
    {
        _cmbThemes.Items.Clear();
        foreach (var theme in ThemeManager.Instance.AvailableThemes)
        {
            _cmbThemes.Items.Add(theme.Name);
        }
        _cmbThemes.SelectedItem = ThemeManager.Instance.CurrentTheme.Name;
    }

    private void OnThemeComboChanged(object? sender, EventArgs e)
    {
        if (_cmbThemes.SelectedItem is string themeName)
        {
            try
            {
                ThemeManager.Instance.ApplyTheme(themeName);
            }
            catch
            {
                // fallback
            }
        }
    }

    private void OnFavoriteToggle(object? sender, EventArgs e)
    {
        if (_currentDemo == null) return;

        if (_favoriteIds.Contains(_currentDemo.Id))
        {
            _favoriteIds.Remove(_currentDemo.Id);
            _btnFavorite.Text = "⭐ Favorite";
        }
        else
        {
            _favoriteIds.Add(_currentDemo.Id);
            _btnFavorite.Text = "★ Favorited";
        }

        PopulateNavigationTree();
    }

    private void OnRecentSelected(object? sender, EventArgs e)
    {
        if (_cmbRecent.SelectedItem is DemoItem demo)
        {
            LoadDemo(demo);
        }
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        _statusTheme.Text = $"Theme: {newTheme.Name}";
        if (_cmbThemes.SelectedItem?.ToString() != newTheme.Name)
        {
            _cmbThemes.SelectedIndexChanged -= OnThemeComboChanged;
            try
            {
                if (!_cmbThemes.Items.Contains(newTheme.Name)) _cmbThemes.Items.Add(newTheme.Name);
                _cmbThemes.SelectedItem = newTheme.Name;
            }
            finally { _cmbThemes.SelectedIndexChanged += OnThemeComboChanged; }
        }
        EnterpriseThemeEngine.ApplyToControl(this, newTheme);
    }

    private void HandleThemeChanged(object? sender, ITheme theme) => OnThemeChanged(theme);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= HandleThemeChanged;
            DemoNavigationManager.Instance.DemoSelected -= LoadDemo;
            _memoryMonitorTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ApplyTheme(ITheme theme)
    {
        ThemeManager.Instance.ApplyTheme(theme);
        OnThemeChanged(theme);
    }

    private void PopulateNavigationTree()
    {
        _navTree.Nodes.Clear();

        // 0. Favorites Node
        if (_favoriteIds.Count > 0)
        {
            var favNode = new TreeNode("⭐ Favorites") { Tag = "Category_Favorites" };
            foreach (var favId in _favoriteIds)
            {
                var demo = DemoMetadataRegistry.GetById(favId);
                if (demo != null)
                {
                    favNode.Nodes.Add(new TreeNode(demo.Title) { Tag = demo });
                }
            }
            _navTree.Nodes.Add(favNode);
        }

        // 1. Browse by Control
        var suites = DemoMetadataRegistry.AllDemos.Where(demo =>
            demo.SubCategory != "DataGrid Workshop" && demo.SubCategory != "Data Management Controls" && demo.SubCategory != "Interactive Controls" && !_favoriteIds.Contains(demo.Id)).ToList();
        var rootControls = new TreeNode($"Control Demos & Tools ({suites.Count} Pages)") { Tag = "Category_Controls" };
        var groups = suites.GroupBy(d => d.SubCategory);
        foreach (var grp in groups)
        {
            var subNode = new TreeNode(grp.Key);
            foreach (var demo in grp)
            {
                subNode.Nodes.Add(new TreeNode(demo.Title) { Tag = demo });
            }
            subNode.Expand();
            rootControls.Nodes.Add(subNode);
        }

        // 7. DataGrid workshop: one page per feature area
        var rootGridWorkshop = new TreeNode($"DataGrid Workshop ({GridScenarios.All.Count} Pages)") { Tag = "Category_GridWorkshop" };
        foreach (var scenario in GridScenarios.All)
        {
            if (_favoriteIds.Contains(scenario.Id)) continue;
            rootGridWorkshop.Nodes.Add(new TreeNode(scenario.Title) { Tag = DemoMetadataRegistry.GetById(scenario.Id) });
        }

        // 8. Data management controls: one page per control
        var rootDataControls = new TreeNode($"Data Management Controls ({DataControlScenarios.All.Count} Pages)") { Tag = "Category_DataControls" };
        foreach (var scenario in DataControlScenarios.All)
        {
            if (_favoriteIds.Contains(scenario.Id)) continue;
            rootDataControls.Nodes.Add(new TreeNode(scenario.Title) { Tag = DemoMetadataRegistry.GetById(scenario.Id) });
        }

        // 9. Interactive and AI controls: one page per control family
        var rootInteractive = new TreeNode($"Interactive & AI Controls ({InteractiveControlScenarios.All.Count} Pages)") { Tag = "Category_InteractiveControls" };
        foreach (var scenario in InteractiveControlScenarios.All)
        {
            if (_favoriteIds.Contains(scenario.Id)) continue;
            rootInteractive.Nodes.Add(new TreeNode(scenario.Title) { Tag = DemoMetadataRegistry.GetById(scenario.Id) });
        }

        _navTree.Nodes.AddRange(new[]
        {
            rootGridWorkshop,
            rootDataControls,
            rootInteractive,
            rootControls
        });

        rootGridWorkshop.Expand();
        rootDataControls.Expand();
        rootInteractive.Expand();
        rootControls.Expand();
    }

    internal void ApplySearchFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            PopulateNavigationTree();
            return;
        }

        var results = DemoSearchEngine.Search(DemoMetadataRegistry.AllDemos, query);
        var catalogMatches = EnterpriseUI.Editors.EnterpriseControlRegistry.AllControls
            .Where(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.EnterpriseUIName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.KeyFeatures.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var matchingDemos = results.Concat(catalogMatches.Select(DemoMetadataRegistry.GetCatalogDemo))
            .DistinctBy(demo => demo.Id).ToList();
        _navTree.Nodes.Clear();

        var searchRoot = new TreeNode($"Demo Pages ({matchingDemos.Count} matches)");
        foreach (var d in matchingDemos)
        {
            searchRoot.Nodes.Add(new TreeNode($"{d.Title} [{d.SubCategory}]") { Tag = d });
        }
        _navTree.Nodes.Add(searchRoot);
        searchRoot.Expand();

        if (matchingDemos.Count > 0)
        {
            LoadDemo(matchingDemos[0]);
        }
    }

    private void OnNavNodeSelected(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is DemoItem item)
        {
            LoadDemo(item);
        }
    }

    private void LoadDemo(DemoItem demo)
    {
        _currentDemo = demo;
        DemoAnalyticsTracker.Instance.BeginTrack(demo.Id);

        foreach (Control previous in _demoHost.Controls.Cast<Control>().ToArray()) previous.Dispose();
        _demoHost.Controls.Clear();
        var view = demo.ViewFactory();
        view.Dock = DockStyle.Fill;
        _demoHost.Controls.Add(view);

        DemoAnalyticsTracker.Instance.EndTrack();

        // Update EnterpriseBreadcrumb & Favorite state
        _lblBreadcrumb.Text = $"🏠 Home > {demo.Category} > {demo.SubCategory} > {demo.Title}";
        _btnFavorite.Text = _favoriteIds.Contains(demo.Id) ? "★ Favorited" : "⭐ Favorite";

        // Update Recently Opened
        _recentDemos.RemoveAll(d => d.Id == demo.Id);
        _recentDemos.Insert(0, demo);
        if (_recentDemos.Count > 10) _recentDemos.RemoveAt(10);

        _cmbRecent.SelectedIndexChanged -= OnRecentSelected;
        _cmbRecent.DataSource = null;
        _cmbRecent.DisplayMember = "Title";
        _cmbRecent.DataSource = new List<DemoItem>(_recentDemos);
        _cmbRecent.SelectedIndex = 0;
        _cmbRecent.SelectedIndexChanged += OnRecentSelected;

        _statusDemo.Text = $"Demo: {demo.Title}";
        _statusLatency.Text = $"Latency: {DemoAnalyticsTracker.Instance.LastRenderTimeMs} ms";

        _inspector.InspectObject(view, demo);
        _inspector.LogEvent($"Loaded demo view: {demo.Id}");
    }

    private void OnRibbonTabChanged(string tabName)
    {
        switch (tabName)
        {
            case "Data & Grids":
                LoadDemo(DemoMetadataRegistry.GetById("grid-virtualization-1m"));
                break;
            case "Visualizations":
                LoadDemo(DemoMetadataRegistry.GetById("charts-financial-realtime"));
                break;
            case "Navigation & Layout":
                LoadDemo(DemoMetadataRegistry.GetById("navigation-layout-alerts-suite"));
                break;
            case "Editors & Docs":
                LoadDemo(DemoMetadataRegistry.GetById("editors-fluent-validation"));
                break;
            case "Component Catalog":
                LoadDemo(DemoMetadataRegistry.GetById("component-catalog"));
                break;
            case "Intelligence":
                LoadDemo(DemoMetadataRegistry.GetById("ai-copilot-assistant"));
                break;
            case "Theming":
                LoadDemo(DemoMetadataRegistry.GetById("theming-design-tokens"));
                break;
            case "QA & Lab":
                LoadDemo(DemoMetadataRegistry.GetById("qa-validation-suite"));
                break;
            case "Developer Tools":
                break;
            default:
                LoadDemo(DemoMetadataRegistry.GetById("dashboard-executive-bi"));
                break;
        }
    }

    private void ShowBackstageDialog()
    {
        using var dlg = new EnterpriseForm
        {
            Text = "EnterpriseUI - System Backstage & Architecture Diagnostics",
            ClientSize = new Size(680, 520 + 36),
            StartPosition = FormStartPosition.CenterParent,
            AllowResize = false,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9.5f)
        };

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };

        // Tab 1: System Info
        var tabInfo = new TabPage("System Diagnostics");
        var rtbInfo = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9.5f),
            Text =
                "============================================================\n" +
                "ENTERPRISEUI SHOWCASE RUNTIME DIAGNOSTICS\n" +
                "============================================================\n" +
                $"OS Version:           {Environment.OSVersion}\n" +
                $"CLR Runtime:          .NET {Environment.Version}\n" +
                $"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}\n" +
                $"Total Memory (RAM):   {DemoAnalyticsTracker.Instance.GetFormattedMemory()}\n" +
                $"Total Demos Loaded:   {DemoMetadataRegistry.AllDemos.Count} interactive showcases\n" +
                $"Demos Viewed Session: {DemoAnalyticsTracker.Instance.TotalDemosViewed}\n" +
                $"Active Theme:         {ThemeManager.Instance.CurrentTheme.Name}\n" +
                $"Accessibility:       Requires control-by-control validation\n" +
                $"High-DPI Behavior:   Validate on each supported display configuration\n" +
                "============================================================"
        };
        tabInfo.Controls.Add(rtbInfo);

        // Tab 2: Licensing & Governance
        var tabLic = new TabPage("Licensing & Governance");
        var rtbLic = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Segoe UI", 9.5f),
            Text =
                "EnterpriseUI License & Support\n\n" +
                "License: See the repository LICENSE file.\n" +
                "Support: Refer to the project's published support and contribution channels.\n" +
                "Package, redistribution, and support terms depend on the applicable project policies.\n\n" +
                "Accessibility, performance, and production-readiness claims must be validated for the specific control and scenario."
        };
        tabLic.Controls.Add(rtbLic);

        tabs.TabPages.AddRange(new[] { tabInfo, tabLic });

        var btnClose = new Button
        {
            Text = "Close Backstage",
            Dock = DockStyle.Bottom,
            Height = 36,
            DialogResult = DialogResult.OK
        };

        var btnAbout = new EnterpriseButtonControl { Text = "About EnterpriseUI", Variant = EnterpriseButtonVariant.Outline, Dock = DockStyle.Bottom, Height = 40 };
        btnAbout.Click += (_, _) =>
        {
            using var about = new EnterpriseAboutBox { ProductName = "EnterpriseUI Demo Center", Description = "Runnable demonstrations of the EnterpriseUI Windows Forms suite.", LinkUrl = "https://github.com/muhammad-aftab93/EnterpriseUI" };
            about.LoadFromAssembly(typeof(MainForm).Assembly);
            about.ProductName = "EnterpriseUI Demo Center";
            about.ShowDialog(dlg);
        };

        dlg.Controls.Add(tabs);
        dlg.Controls.Add(btnAbout);
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog(this);
    }
}
