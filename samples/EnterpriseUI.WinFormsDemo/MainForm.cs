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

public class MainForm : Form, IThemeableControl
{
    private readonly EnterpriseRibbonControl _ribbon;
    private readonly SplitContainer _mainSplit;
    private readonly SplitContainer _centerSplit;
    private readonly Panel _leftNavPanel;
    private readonly TextBox _searchBox;
    private readonly TreeView _navTree;
    private readonly Panel _demoWorkspaceContainer;
    private readonly Panel _breadcrumbBar;
    private readonly Label _lblBreadcrumb;
    private readonly Button _btnFavorite;
    private readonly ComboBox _cmbRecent;
    private readonly ComboBox _cmbThemes;
    private readonly Panel _demoHost;
    private readonly EnterprisePropertyInspectorControl _inspector;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusTheme;
    private readonly ToolStripStatusLabel _statusDemo;
    private readonly ToolStripStatusLabel _statusMemory;
    private readonly ToolStripStatusLabel _statusLatency;
    private readonly ToolStripStatusLabel _statusA11y;
    private readonly System.Windows.Forms.Timer _memoryMonitorTimer;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private DemoItem? _currentDemo;

    private readonly HashSet<string> _favoriteIds = new();
    private readonly List<DemoItem> _recentDemos = new();

    public string? ThemeOverride { get; set; }

    public MainForm()
    {
        Text = "EnterpriseUI Demonstration Platform - World-Class Showcase, Learning & Validation Center";
        
        // 1080p target sizing & responsive constraints
        Size = new Size(1920, 1040);
        MinimumSize = new Size(1280, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        // 1. Top Ribbon Control
        _ribbon = new EnterpriseRibbonControl();
        _ribbon.BackstageClicked += ShowBackstageDialog;
        _ribbon.TabChanged += OnRibbonTabChanged;

        // 2. Bottom Status Strip
        _statusStrip = new StatusStrip();
        _statusTheme = new ToolStripStatusLabel { Text = $"Theme: {_theme.Name}  |" };
        _statusDemo = new ToolStripStatusLabel { Text = "Demo: Ready  |" };
        _statusMemory = new ToolStripStatusLabel { Text = "RAM: 0.0 MB  |" };
        _statusLatency = new ToolStripStatusLabel { Text = "Latency: 0 ms  |" };
        _statusA11y = new ToolStripStatusLabel { Text = "WCAG 2.2 AA: Active" };

        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _statusTheme,
            _statusDemo,
            _statusMemory,
            _statusLatency,
            _statusA11y
        });

        // 3. Main Split Container (Left Nav | Center & Right Inspector)
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6
        };

        // Left Navigation Panel
        _leftNavPanel = new Panel { Dock = DockStyle.Fill };

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Search controls, features, scenarios..."
        };
        _searchBox.TextChanged += (_, _) => ApplySearchFilter(_searchBox.Text);
        searchPanel.Controls.Add(_searchBox);

        _navTree = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            ShowLines = true,
            ShowPlusMinus = true,
            HideSelection = false
        };
        _navTree.AfterSelect += OnNavNodeSelected;

        _leftNavPanel.Controls.Add(_navTree);
        _leftNavPanel.Controls.Add(searchPanel);
        _mainSplit.Panel1.Controls.Add(_leftNavPanel);

        // Center Split (Center Demo Host | Right Property Inspector)
        _centerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6
        };

        // Workspace Container with EnterpriseBreadcrumb & Action Toolbar
        _demoWorkspaceContainer = new Panel { Dock = DockStyle.Fill };

        _breadcrumbBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(12, 6, 12, 6),
            BackColor = Color.FromArgb(246, 248, 250)
        };

        _lblBreadcrumb = new Label
        {
            Text = "🏠 Home > Control Explorer > Ready",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 64, 70),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var rightToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnFavorite = new Button
        {
            Text = "⭐ Favorite",
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        _btnFavorite.Click += OnFavoriteToggle;

        var lblRecent = new Label
        {
            Text = "Recent:",
            AutoSize = true,
            Padding = new Padding(8, 6, 2, 0),
            Font = new Font("Segoe UI", 9f)
        };

        _cmbRecent = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(170, 28),
            Font = new Font("Segoe UI", 9f)
        };
        _cmbRecent.SelectedIndexChanged += OnRecentSelected;

        var lblThemes = new Label
        {
            Text = "Theme:",
            AutoSize = true,
            Padding = new Padding(8, 6, 2, 0),
            Font = new Font("Segoe UI", 9f)
        };

        _cmbThemes = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(160, 28),
            Font = new Font("Segoe UI", 9f)
        };
        PopulateThemeSelector();
        _cmbThemes.SelectedIndexChanged += OnThemeComboChanged;

        rightToolbar.Controls.Add(_btnFavorite);
        rightToolbar.Controls.Add(lblRecent);
        rightToolbar.Controls.Add(_cmbRecent);
        rightToolbar.Controls.Add(lblThemes);
        rightToolbar.Controls.Add(_cmbThemes);

        _breadcrumbBar.Controls.Add(_lblBreadcrumb);
        _breadcrumbBar.Controls.Add(rightToolbar);

        _demoHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        _demoWorkspaceContainer.Controls.Add(_demoHost);
        _demoWorkspaceContainer.Controls.Add(_breadcrumbBar);

        _centerSplit.Panel1.Controls.Add(_demoWorkspaceContainer);

        _inspector = new EnterprisePropertyInspectorControl { Dock = DockStyle.Fill };
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
        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);

        // Listen for Navigation Changes
        DemoNavigationManager.Instance.DemoSelected += LoadDemo;

        // Build Nav Hierarchy
        PopulateNavigationTree();

        // Memory Monitor Timer
        _memoryMonitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _memoryMonitorTimer.Tick += (_, _) =>
        {
            _statusMemory.Text = $"RAM: {DemoAnalyticsTracker.Instance.GetFormattedMemory()}  |";
        };
        _memoryMonitorTimer.Start();

        // Load Default Demo & Apply Optimal Left/Right Panel Widths
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
            // Left navigation panel: 380px wide so long demo and feature titles never clip
            const int targetLeftWidth = 380;
            // Right inspector & playground panel: 480px wide so all 5 tabs & PropertyGrid values fit cleanly
            const int targetRightWidth = 480;

            _mainSplit.Panel1MinSize = 320;
            _mainSplit.Panel2MinSize = 600;
            if (_mainSplit.Width > _mainSplit.Panel1MinSize + _mainSplit.Panel2MinSize)
            {
                _mainSplit.SplitterDistance = targetLeftWidth;
            }

            _centerSplit.Panel1MinSize = 500;
            _centerSplit.Panel2MinSize = 420;
            int centerTotalWidth = _centerSplit.Width;
            if (centerTotalWidth > _centerSplit.Panel1MinSize + _centerSplit.Panel2MinSize)
            {
                int desiredCenterDistance = Math.Max(
                    _centerSplit.Panel1MinSize,
                    centerTotalWidth - targetRightWidth - _centerSplit.SplitterWidth);
                _centerSplit.SplitterDistance = desiredCenterDistance;
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
        _statusTheme.Text = $"Theme: {newTheme.Name}  |";
        if (_cmbThemes.SelectedItem?.ToString() != newTheme.Name)
        {
            _cmbThemes.SelectedItem = newTheme.Name;
        }
        EnterpriseThemeEngine.ApplyToControl(this, newTheme);
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
        var rootControls = new TreeNode($"Browse by Control ({DemoMetadataRegistry.AllDemos.Count} Suites)") { Tag = "Category_Controls" };
        var groups = DemoMetadataRegistry.AllDemos.GroupBy(d => d.SubCategory);
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

        // 1B. Browse All 118 Official Controls (Telerik Documentation Catalog)
        var rootAll118 = new TreeNode($"Browse All {EnterpriseUI.Editors.TelerikWinFormsControlRegistry.AllControls.Count} Controls (Official Catalog)")
        {
            Tag = DemoMetadataRegistry.GetById("complete-telerik-parity-catalog")
        };
        foreach (var catGrp in EnterpriseUI.Editors.TelerikWinFormsControlRegistry.AllControls.GroupBy(c => c.Category))
        {
            var catNode = new TreeNode($"{catGrp.Key} ({catGrp.Count()})");
            foreach (var ctrl in catGrp)
            {
                var targetDemo = DemoMetadataRegistry.GetById(ctrl.DemoSuiteId);
                catNode.Nodes.Add(new TreeNode($"{ctrl.TelerikName} → {ctrl.EnterpriseUIName}") { Tag = targetDemo });
            }
            rootAll118.Nodes.Add(catNode);
        }

        // 2. Browse by Feature
        var rootFeatures = new TreeNode("Browse by Feature") { Tag = "Category_Features" };
        rootFeatures.Nodes.Add(new TreeNode("Million-Row Virtualization") { Tag = DemoMetadataRegistry.GetById("grid-virtualization-1m") });
        rootFeatures.Nodes.Add(new TreeNode("Event-Driven EnterpriseVirtualGrid (1M×12), EnterpriseSyntaxEditor & EnterpriseCarousel") { Tag = DemoMetadataRegistry.GetById("complete-telerik-parity-catalog") });
        rootFeatures.Nodes.Add(new TreeNode("EnterpriseGanttView, QR/Code128 Barcode, Kanban, EnterpriseTreeMap & SignaturePad") { Tag = DemoMetadataRegistry.GetById("specialized-gantt-barcode-kanban") });
        rootFeatures.Nodes.Add(new TreeNode("EnterpriseBreadcrumb, StepProgress, EnterprisePanorama, Wizard & DesktopAlerts") { Tag = DemoMetadataRegistry.GetById("navigation-layout-alerts-suite") });
        rootFeatures.Nodes.Add(new TreeNode("Multi-Sort, Grouping & Bands") { Tag = DemoMetadataRegistry.GetById("grid-virtualization-1m") });
        rootFeatures.Nodes.Add(new TreeNode("Self-Referencing Tree Hierarchies") { Tag = DemoMetadataRegistry.GetById("grid-treelist-hierarchy") });
        rootFeatures.Nodes.Add(new TreeNode("OLAP Cross-Tab Pivot Aggregations") { Tag = DemoMetadataRegistry.GetById("grid-pivot-olap") });
        rootFeatures.Nodes.Add(new TreeNode("Real-Time Financial Candlesticks") { Tag = DemoMetadataRegistry.GetById("charts-financial-realtime") });
        rootFeatures.Nodes.Add(new TreeNode("Offline AI Natural Language Copilot") { Tag = DemoMetadataRegistry.GetById("ai-copilot-assistant") });
        rootFeatures.Nodes.Add(new TreeNode("MaskedEdit, Calculator, EnterpriseSyntaxEditor & Fluent Validation") { Tag = DemoMetadataRegistry.GetById("editors-fluent-validation") });
        rootFeatures.Nodes.Add(new TreeNode("Live Design Token Hot-Swapping (12 Themes)") { Tag = DemoMetadataRegistry.GetById("theming-design-tokens") });

        // 3. Browse by Industry Scenario
        var rootScenarios = new TreeNode("Browse by Industry Scenario") { Tag = "Category_Scenarios" };
        rootScenarios.Nodes.Add(new TreeNode("All Industry Suites (CRM, ERP, Healthcare, FinTech)") { Tag = DemoMetadataRegistry.GetById("business-scenarios-suite") });
        foreach (var indGrp in DemoMetadataRegistry.AllDemos.GroupBy(d => d.Industry))
        {
            var indNode = new TreeNode(indGrp.Key);
            foreach (var d in indGrp)
            {
                indNode.Nodes.Add(new TreeNode(d.Title) { Tag = d });
            }
            rootScenarios.Nodes.Add(indNode);
        }

        // 4. Browse by API & Architecture
        var rootApi = new TreeNode("Browse by API & Architecture") { Tag = "Category_Api" };
        rootApi.Nodes.Add(new TreeNode("Interactive Reflection API Explorer") { Tag = DemoMetadataRegistry.GetById("api-interactive-explorer") });
        rootApi.Nodes.Add(new TreeNode("Learning Center & Blueprints") { Tag = DemoMetadataRegistry.GetById("learning-center-hub") });

        // 5. Browse by Performance Category
        var rootPerf = new TreeNode("Browse by Performance Category") { Tag = "Category_Perf" };
        rootPerf.Nodes.Add(new TreeNode("Performance & Stress Laboratory") { Tag = DemoMetadataRegistry.GetById("performance-laboratory") });
        rootPerf.Nodes.Add(new TreeNode("1M Row O(1) Viewport Grid") { Tag = DemoMetadataRegistry.GetById("grid-virtualization-1m") });
        rootPerf.Nodes.Add(new TreeNode("High-Frequency Financial Charting") { Tag = DemoMetadataRegistry.GetById("charts-financial-realtime") });

        // 6. Browse by Accessibility Category
        var rootA11y = new TreeNode("Browse by Accessibility (WCAG 2.2)") { Tag = "Category_A11y" };
        rootA11y.Nodes.Add(new TreeNode("Accessibility & RTL Validation Center") { Tag = DemoMetadataRegistry.GetById("accessibility-center") });
        rootA11y.Nodes.Add(new TreeNode("High Contrast & Theme Compliance") { Tag = DemoMetadataRegistry.GetById("theming-design-tokens") });
        rootA11y.Nodes.Add(new TreeNode("Automated QA Verification Suite") { Tag = DemoMetadataRegistry.GetById("qa-validation-suite") });

        // 7. DataGrid workshop: one page per feature area
        var rootGridWorkshop = new TreeNode($"DataGrid Workshop ({GridScenarios.All.Count} Pages)") { Tag = "Category_GridWorkshop" };
        foreach (var scenario in GridScenarios.All)
        {
            rootGridWorkshop.Nodes.Add(new TreeNode(scenario.Title) { Tag = DemoMetadataRegistry.GetById(scenario.Id) });
        }

        _navTree.Nodes.AddRange(new[]
        {
            rootGridWorkshop,
            rootControls,
            rootAll118,
            rootFeatures,
            rootScenarios,
            rootApi,
            rootPerf,
            rootA11y
        });

        rootGridWorkshop.Expand();
        rootControls.Expand();
        rootAll118.Expand();
        rootFeatures.Expand();
        rootPerf.Expand();
        rootA11y.Expand();
    }

    private void ApplySearchFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            PopulateNavigationTree();
            return;
        }

        var results = DemoSearchEngine.Search(DemoMetadataRegistry.AllDemos, query);
        var catalogMatches = EnterpriseUI.Editors.TelerikWinFormsControlRegistry.AllControls
            .Where(c =>
                c.TelerikName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.EnterpriseUIName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.KeyFeatures.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _navTree.Nodes.Clear();

        var searchRoot = new TreeNode($"Demo Suites ({results.Count} matches)");
        foreach (var d in results)
        {
            searchRoot.Nodes.Add(new TreeNode($"{d.Title} [{d.SubCategory}]") { Tag = d });
        }
        _navTree.Nodes.Add(searchRoot);
        searchRoot.Expand();

        if (catalogMatches.Count > 0)
        {
            var ctrlRoot = new TreeNode($"Individual Controls ({catalogMatches.Count} matches)");
            foreach (var c in catalogMatches)
            {
                var targetDemo = DemoMetadataRegistry.GetById(c.DemoSuiteId);
                ctrlRoot.Nodes.Add(new TreeNode($"{c.TelerikName} → {c.EnterpriseUIName}") { Tag = targetDemo });
            }
            _navTree.Nodes.Add(ctrlRoot);
            ctrlRoot.Expand();
        }

        if (results.Count > 0)
        {
            LoadDemo(results[0]);
        }
        else if (catalogMatches.Count > 0)
        {
            LoadDemo(DemoMetadataRegistry.GetById(catalogMatches[0].DemoSuiteId));
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

        _statusDemo.Text = $"Demo: {demo.Title}  |";
        _statusLatency.Text = $"Latency: {DemoAnalyticsTracker.Instance.LastRenderTimeMs} ms  |";

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
            case "Telerik Parity":
                LoadDemo(DemoMetadataRegistry.GetById("complete-telerik-parity-catalog"));
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
            default:
                LoadDemo(DemoMetadataRegistry.GetById("dashboard-executive-bi"));
                break;
        }
    }

    private void ShowBackstageDialog()
    {
        using var dlg = new Form
        {
            Text = "EnterpriseUI - System Backstage & Architecture Diagnostics",
            Size = new Size(680, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
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

        dlg.Controls.Add(tabs);
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog(this);
    }
}
