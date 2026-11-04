using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class LearningCenterDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly TreeView _topicsTree;
    private readonly RichTextBox _contentBox;

    public LearningCenterDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 260
        };

        _topicsTree = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            ShowLines = true,
            ShowPlusMinus = true
        };
        _topicsTree.AfterSelect += (_, e) => ShowTopicContent(e.Node?.Tag?.ToString() ?? string.Empty);

        _contentBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.White,
            Padding = new Padding(16)
        };

        split.Panel1.Controls.Add(_topicsTree);
        split.Panel2.Controls.Add(_contentBox);

        _baseView.DemoContainer.Controls.Add(split);

        PopulateLearningTopics();
        ShowTopicContent("Overview");
    }

    private void PopulateLearningTopics()
    {
        var rootCore = new TreeNode("1. Framework Architecture") { Tag = "Architecture" };
        rootCore.Nodes.Add(new TreeNode("Platform Abstractions & MVVM") { Tag = "Mvvm" });
        rootCore.Nodes.Add(new TreeNode("Design Token System & Theming") { Tag = "Theming" });
        rootCore.Nodes.Add(new TreeNode("High-Performance GDI+ Rendering") { Tag = "Rendering" });

        var rootPatterns = new TreeNode("2. Best Practices & Anti-Patterns") { Tag = "Patterns" };
        rootPatterns.Nodes.Add(new TreeNode("DataGrid Virtualization Best Practices") { Tag = "GridVirtualization" });
        rootPatterns.Nodes.Add(new TreeNode("Memory Optimization & GC Management") { Tag = "Memory" });
        rootPatterns.Nodes.Add(new TreeNode("Common Anti-Patterns to Avoid") { Tag = "AntiPatterns" });

        var rootMigration = new TreeNode("3. Enterprise Migration Guides") { Tag = "Migration" };
        rootMigration.Nodes.Add(new TreeNode("Migrating from DevExpress WinForms") { Tag = "DevExpressMigration" });
        rootMigration.Nodes.Add(new TreeNode("Migrating from Telerik UI for WinForms") { Tag = "TelerikMigration" });
        rootMigration.Nodes.Add(new TreeNode("Migrating from Legacy System.Windows.Forms") { Tag = "LegacyWinForms" });

        var rootAccess = new TreeNode("4. Accessibility & Compliance") { Tag = "Accessibility" };
        rootAccess.Nodes.Add(new TreeNode("WCAG 2.2 AA Checklist") { Tag = "WcagChecklist" });
        rootAccess.Nodes.Add(new TreeNode("Screen Reader Live Regions") { Tag = "ScreenReaders" });

        _topicsTree.Nodes.AddRange(new[] { rootCore, rootPatterns, rootMigration, rootAccess });
        _topicsTree.ExpandAll();
    }

    private void ShowTopicContent(string tag)
    {
        switch (tag)
        {
            case "Architecture":
            case "Overview":
                _contentBox.Text =
                    "ENTERPRISEUI FRAMEWORK ARCHITECTURE\n\n" +
                    "EnterpriseUI is engineered from the ground up as a unified, platform-agnostic enterprise UI suite.\n\n" +
                    "Core Tenets:\n" +
                    "• Headless Core Models: Pure C# business logic, virtualization algorithms, query compilation, and validation live in src/EnterpriseUI.* with zero UI framework dependencies.\n" +
                    "• Native Platform Renderers: WinForms, WPF, Blazor, MAUI, and ASP.NET Core provide platform-native double-buffered rendering while sharing 100% of state models.\n" +
                    "• Design Token Theming: 1-to-1 parity across Fluent, Material, Bootstrap, Office, and High-Contrast palettes.\n" +
                    "• Zero-Allocation Querying: QueryEngine compiles dynamic LINQ expression trees at runtime for sub-millisecond filtering and sorting.";
                break;
            case "Mvvm":
                _contentBox.Text =
                    "MVVM & REACTIVE PROPERTY BINDING\n\n" +
                    "All EnterpriseUI controls inherit from ObservableObject, providing thread-safe INotifyPropertyChanged dispatch, SetProperty<T> mutators, and RelayCommand abstractions.\n\n" +
                    "Pattern Example:\n" +
                    "public class OrderViewModel : ObservableObject\n" +
                    "{\n" +
                    "    private decimal _total;\n" +
                    "    public decimal Total { get => _total; set => SetProperty(ref _total, value); }\n" +
                    "}";
                break;
            case "GridVirtualization":
                _contentBox.Text =
                    "DATAGRID VIRTUALIZATION BEST PRACTICES\n\n" +
                    "1. Always bind to IQueryable<T> or IDataSource<T> when datasets exceed 10,000 rows.\n" +
                    "2. Enable EnableVirtualization = true on the DataGrid.\n" +
                    "3. Avoid cell formatting inside OnPaint loops; utilize GridColumn.FormatString.\n" +
                    "4. Use batch editing instead of per-cell notifications for heavy data entry workflows.\n" +
                    "5. Keep ItemHeight fixed for O(1) viewport index computation.";
                break;
            case "AntiPatterns":
                _contentBox.Text =
                    "COMMON ENTERPRISE ANTI-PATTERNS TO AVOID\n\n" +
                    "❌ Anti-Pattern 1: Calling Refresh() or Invalidate() inside tight loops.\n" +
                    "   → Fix: Batch updates or dispatch via throttled timer.\n\n" +
                    "❌ Anti-Pattern 2: Hardcoding SystemColors in custom control rendering.\n" +
                    "   → Fix: Resolve all colors through ThemeManager and DesignTokens.\n\n" +
                    "❌ Anti-Pattern 3: Polling background services on UI threads.\n" +
                    "   → Fix: Use async Task or reactive event aggregators.\n\n" +
                    "❌ Anti-Pattern 4: Allocating new Pens and Brushes on every OnPaint pass.\n" +
                    "   → Fix: Cache GDI+ drawing resources or use using blocks efficiently.";
                break;
            case "DevExpressMigration":
                _contentBox.Text =
                    "MIGRATING FROM DEVEXPRESS WINFORMS\n\n" +
                    "DevExpress Concept → EnterpriseUI Equivalent:\n" +
                    "• DevExpress GridControl / GridView → WinFormsDataGrid<T> / DataGrid<T>\n" +
                    "• DevExpress TreeList → TreeList<T> / WinFormsTreeListControl\n" +
                    "• DevExpress PivotGridControl → PivotGrid<T> / WinFormsPivotGridControl\n" +
                    "• DevExpress XtraCharts → Chart / WinFormsChartControl\n" +
                    "• DevExpress SchedulerControl → Scheduler / WinFormsSchedulerControl\n" +
                    "• DevExpress XtraReports → ReportDefinition / ReportViewer\n" +
                    "• DevExpress RibbonControl → RibbonBar / WinFormsRibbonControl\n" +
                    "• DevExpress LookUpEdit → ComboBox<T>\n" +
                    "• DevExpress SpinEdit → NumericTextBox";
                break;
            case "TelerikMigration":
                _contentBox.Text =
                    "MIGRATING FROM TELERIK UI FOR WINFORMS\n\n" +
                    "Telerik Concept → EnterpriseUI Equivalent:\n" +
                    "• RadGridView → WinFormsDataGrid<T>\n" +
                    "• RadChartView → WinFormsChartControl\n" +
                    "• RadScheduler → WinFormsSchedulerControl\n" +
                    "• RadDiagram → WinFormsDiagramControl\n" +
                    "• RadMap → WinFormsMapControl\n" +
                    "• RadPdfViewer → PdfViewer / DocumentsDemoView\n" +
                    "• RadRibbonBar → WinFormsRibbonControl\n" +
                    "• RadThemeManager → ThemeManager.Instance";
                break;
            default:
                _contentBox.Text = $"Documentation for {tag} loaded. Select a subtopic from the navigation tree.";
                break;
        }
    }
}

