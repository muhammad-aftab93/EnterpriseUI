using System;
using System.Collections.Generic;
using EnterpriseUI.Editors;
using EnterpriseUI.WinFormsDemo.Modules;

namespace EnterpriseUI.WinFormsDemo.Infrastructure;

public static class DemoMetadataRegistry
{
    private static readonly string[] CatalogFeatures = { "Component search", "Category filter", "Demo launcher" };
    private static readonly string[] CatalogTags = { "Catalog", "Components", "Search" };
    private static readonly string[] SlideFeatures = { "Slide navigation", "Rotator", "Pagination" };
    private static readonly string[] SlideTags = { "SlideView", "Rotator", "PipsPager" };

    private static readonly string[] CachedValues57 = new[] { "Grid", "DataGrid", "Workshop" };

    private static readonly string[] CachedValues56 = new[] { "NavigationView", "OfficeNavigationBar", "Flyout", "Overlay", "Toast", "TabbedForm", "FileDialogs", "ControlSpy" };

    private static readonly string[] CachedValues55 = new[] { "NavigationView", "OfficeNavigationBar", "Flyout", "Overlay", "SplashScreen", "ToastNotification", "NotifyIcon", "TaskbarButton", "TabbedForm", "ControlSpy" };

    private static readonly string[] CachedValues54 = new[] { "Clock", "RangeSelector", "Annotation", "TimeSeries" };

    private static readonly string[] CachedValues53 = new[] { "Clock", "RangeSelector", "TimeZone", "EnterpriseSparkline Zoom" };

    private static readonly string[] CachedValues46 = new[] { "Navigation", "Layout", "EnterpriseBreadcrumb", "Wizard", "EnterpriseCarousel", "EnterprisePanorama", "Alerts" };

    private static readonly string[] CachedValues45 = new[] { "EnterpriseBreadcrumb", "EnterpriseStepProgressBar", "EnterpriseBindingNavigator", "EnterpriseCarousel", "EnterprisePanorama", "EnterpriseWizardControl", "EnterpriseDesktopAlert", "EnterpriseTaskDialog" };

    private static readonly string[] CachedValues42 = new[] { "Gantt", "Barcode", "QR", "Kanban", "TaskBoard", "ProjectManagement" };

    private static readonly string[] CachedValues41 = new[] { "Gantt EnterpriseChart", "Critical Path", "QR Code", "Code128", "Kanban Board" };

    private static readonly string[] CachedValues40 = new[] { "QA", "Testing", "Coverage", "Diagnostics", "Verification" };

    private static readonly string[] CachedValues39 = new[] { "Test Runner", "Feature Coverage", "Diagnostics", "Regression Matrix" };

    private static readonly string[] CachedValues38 = new[] { "API", "Reflection", "Inspector", "DeveloperTools" };

    private static readonly string[] CachedValues37 = new[] { "Reflection", "API Browser", "Member Inspection", "Signatures", "Type Hierarchy" };

    private static readonly string[] CachedValues36 = new[] { "Learning", "Docs", "Migration", "Patterns", "Onboarding" };

    private static readonly string[] CachedValues35 = new[] { "Tutorials", "Best Practices", "Anti-Patterns", "Migration Guides", "Architecture" };

    private static readonly string[] CachedValues34 = new[] { "Accessibility", "WCAG", "ARIA", "Section508", "ScreenReader" };

    private static readonly string[] CachedValues33 = new[] { "WCAG 2.2 AA", "Section 508", "Screen Readers", "Live Regions", "Contrast Audit" };

    private static readonly string[] CachedValues32 = new[] { "Performance", "Benchmarks", "Throughput", "Latency", "Memory" };

    private static readonly string[] CachedValues31 = new[] { "Stress Testing", "1M Rows", "Throughput", "Memory Profiling", "Micro-Benchmarks" };

    private static readonly string[] CachedValues30 = new[] { "Scenarios", "CRM", "ERP", "Healthcare", "Trading", "HR" };

    private static readonly string[] CachedValues29 = new[] { "CRM Pipeline", "ERP Manufacturing", "Clinical EHR", "Trading Order Book", "Agile Sprints" };

    private static readonly string[] CachedValues28 = new[] { "Theming", "Tokens", "DarkMode", "Material", "Fluent" };

    private static readonly string[] CachedValues27 = new[] { "Live Switching", "Dark Mode", "Light Mode", "Fluent", "Material", "Tokens" };

    private static readonly string[] CachedValues26 = new[] { "AI", "Copilot", "LLM", "NLP", "SmartGrid" };

    private static readonly string[] CachedValues25 = new[] { "AI Copilot", "Natural Language Query", "Offline Mock Engine", "Summaries", "AST Translation" };

    private static readonly string[] CachedValues24 = new[] { "Documents", "PDF", "RichText", "Office", "Spreadsheet", "ImageEditor", "WebCam", "ZipLibrary" };

    private static readonly string[] CachedValues23 = new[] { "PDF Viewer", "WYSIWYG", "RichText", "Spreadsheet", "Formulas", "ImageEditor", "WebCam", "ZipLibrary" };

    private static readonly string[] CachedValues22 = new[] { "Editors", "Validation", "Forms", "Input", "FluentValidation" };

    private static readonly string[] CachedValues21 = new[] { "Validation", "Formatting", "Numeric Stepping", "Masking", "Accessibility" };

    private static readonly string[] CachedValues20 = new[] { "Dashboard", "KPI", "Executive", "BI", "Analytics" };

    private static readonly string[] CachedValues19 = new[] { "KPI Cards", "Integrated Visuals", "Executive Overview", "Dynamic Layout" };

    private static readonly string[] CachedValues18 = new[] { "Reporting", "Bands", "Invoices", "Printing", "PDF" };

    private static readonly string[] CachedValues17 = new[] { "Banded Reports", "Invoices", "Healthcare EHR", "Print Preview", "PDF Vector" };

    private static readonly string[] CachedValues16 = new[] { "Map", "GIS", "GPS", "Fleet", "Logistics" };

    private static readonly string[] CachedValues15 = new[] { "GIS", "GeoCoordinates", "Markers", "Fleet Tracking", "Zoom & Pan" };

    private static readonly string[] CachedValues14 = new[] { "EnterpriseDiagram", "Flowchart", "Architecture", "Nodes" };

    private static readonly string[] CachedValues13 = new[] { "Shapes", "Connectors", "Orthogonal Routing", "Drag & Drop", "Zoom" };

    private static readonly string[] CachedValues12 = new[] { "EnterpriseScheduler", "Calendar", "Appointments", "Planning" };

    private static readonly string[] CachedValues11 = new[] { "WorkWeek", "Timeline", "Appointments", "Resources", "Recurrence" };

    private static readonly string[] CachedValues10 = new[] { "Gauge", "Radial", "Linear", "KPI", "Telemetry" };

    private static readonly string[] CachedValues9 = new[] { "Radial", "Linear", "Threshold Ranges", "Telemetry", "Animation" };

    private static readonly string[] CachedValues8 = new[] { "EnterpriseChart", "Financial", "OHLC", "RealTime", "Telemetry" };

    private static readonly string[] CachedValues7 = new[] { "Candlestick", "Spline", "Area", "Real-Time", "Tooltips", "Vector GDI+" };

    private static readonly string[] CachedValues4 = new[] { "EnterpriseTreeList", "Hierarchy", "Organization", "BOM" };

    private static readonly string[] CachedValues3 = new[] { "Hierarchy", "TreeGrid", "Expand/Collapse", "Multi-Column", "Aggregation" };

    private static readonly string[] CachedValues2 = new[] { "Grid", "Virtualization", "Performance", "MillionRows" };

    private static readonly string[] CachedValues1 = new[] { "Virtualization", "1M Rows", "Direct GDI+", "Low Memory", "DPI Aware" };
    private static readonly List<DemoItem> _allDemos = new();

    public static IReadOnlyList<DemoItem> AllDemos => _allDemos;

    static DemoMetadataRegistry()
    {
        RegisterDemos();
    }

    private static void RegisterDemos()
    {
        // 1. DataGrid - Million Rows & Virtualization
        _allDemos.Add(new DemoItem
        {
            Id = "grid-virtualization-1m",
            Title = "DataGrid - Million Row Windowed Rendering",
            Description = "Binds 1,000,000 in-memory rows and fetches only the rows near the viewport. Scroll, sort, filter, resize and reorder columns, and edit cells.",
            Category = DemoCategoryType.GridsAndData,
            SubCategory = "DataGrid",
            TargetControlName = "EnterpriseDataGrid<T>, DataGrid<T>",
            Features = CachedValues1,
            Tags = CachedValues2,
            Industry = "High-Throughput Financial & Logistics",
            IsFeatured = true,
            IsPopular = true,
            ViewFactory = () => new GridDemoView(GetById("grid-virtualization-1m")),
            SourceCode =
@"// High-Performance Virtualized WinForms DataGrid
var grid = new EnterpriseDataGrid<GridDemoRecord>();
grid.EnableVirtualization = true;
grid.Columns.Add(new GridColumn { Field = ""Id"", Title = ""Record ID"" });
grid.Columns.Add(new GridColumn { Field = ""Customer"", Title = ""Enterprise Customer"" });
grid.Columns.Add(new GridColumn { Field = ""UnitPrice"", Title = ""Unit Price"", FormatString = ""C2"" });
grid.DataSource = GenerateSampleData(1000000); // 1,000,000 records
await grid.RefreshDataAsync();",
            Documentation = "The EnterpriseDataGrid draws with GDI+ from the headless DataGrid<T> engine, which caches only a window of rows around the viewport. Sorting and filtering re-query the in-memory source, so measure large datasets on your own hardware before relying on a latency target.",
            BestPractices = "Always enable virtualization for datasets exceeding 1,000 items. Keep row height fixed for constant time O(1) viewport index computation.",
            Troubleshooting = "Ensure DoubleBuffered is true to avoid flicker. Use FormatString rather than custom cell events for high-throughput numeric columns.",
            ApiReference = "DataGrid<T> -> EnableVirtualization, Columns, DataSource, RenderedItems, RefreshDataAsync()"
        });

        // 2. EnterpriseTreeList - Hierarchical Data
        _allDemos.Add(new DemoItem
        {
            Id = "grid-treelist-hierarchy",
            Title = "EnterpriseTreeList - Multi-Column Hierarchical Organization & Budgets",
            Description = "Combines tree-view node expansion with multi-column grid features for organizational structures, bill of materials, and financial roll-ups.",
            Category = DemoCategoryType.GridsAndData,
            SubCategory = "EnterpriseTreeList",
            TargetControlName = "EnterpriseTreeList<T>, EnterpriseTreeListControl",
            Features = CachedValues3,
            Tags = CachedValues4,
            Industry = "Corporate Governance & Manufacturing",
            ViewFactory = () => new TreeGridDemoView(GetById("grid-treelist-hierarchy")),
            SourceCode =
@"var treeList = new EnterpriseTreeListControl();
treeList.Items.Add(new TreeListDemoItem { Id = 1, ParentId = null, Name = ""Corporate HQ"", Budget = 4500000m });
treeList.Items.Add(new TreeListDemoItem { Id = 2, ParentId = 1, Name = ""Engineering"", Budget = 2800000m });",
            Documentation = "EnterpriseTreeList binds parent-child hierarchical relations without recursive UI controls, minimizing GDI handles.",
            BestPractices = "Use self-referencing ID/ParentID schemas for dynamic lazy loading.",
            Troubleshooting = "Check for cyclic parent references in raw datasets.",
            ApiReference = "EnterpriseTreeList<T> -> KeyFieldName, ParentFieldName, Columns, DataSource"
        });

        // 4. Charts - Financial & Real-Time
        _allDemos.Add(new DemoItem
        {
            Id = "charts-financial-realtime",
            Title = "Charts - Multi-Series Financial & OHLC Candlestick Analysis",
            Description = "High-performance vector charts supporting Bar, Column, Line, Spline, Area, Pie, Donut, and Candlestick series with live real-time tick streaming.",
            Category = DemoCategoryType.ChartsAndGauges,
            SubCategory = "Charts",
            TargetControlName = "EnterpriseChart, EnterpriseChartControl",
            Features = CachedValues7,
            Tags = CachedValues8,
            Industry = "Capital Markets & IoT Telemetry",
            IsFeatured = true,
            ViewFactory = () => new ChartsDemoView(GetById("charts-financial-realtime")),
            SourceCode =
@"var chart = new EnterpriseChartControl();
var series = new ChartSeries { Name = ""Equity"", Type = SeriesType.Candlestick };
series.DataPoints.Add(new ChartDataPoint { Category = ""Day 1"", Open = 150, Close = 158, High = 162, Low = 148 });
chart.Series.Add(series);",
            Documentation = "Charts support hardware-accelerated GDI+ rendering with automated axis normalization and marker decimation.",
            BestPractices = "Use LTTB (Largest Triangle Three Buckets) or bucket decimation when visualizing datasets over 100,000 points.",
            Troubleshooting = "Set EnableZoomPan = true to let users interactively drill down into localized volatility ranges.",
            ApiReference = "EnterpriseChart -> Series, XAxis, YAxis, EnableTooltips, EnableZoomPan"
        });

        // 5. Gauges - Radial & Linear
        _allDemos.Add(new DemoItem
        {
            Id = "gauges-radial-linear",
            Title = "Gauges - Radial & Linear Telemetry Indicators",
            Description = "Precision circular and bar gauges with colored threshold ranges, smooth needle tracking, and live server telemetry feeds.",
            Category = DemoCategoryType.ChartsAndGauges,
            SubCategory = "Gauges",
            TargetControlName = "EnterpriseRadialGauge, EnterpriseLinearGauge, EnterpriseGaugeControl",
            Features = CachedValues9,
            Tags = CachedValues10,
            Industry = "Infrastructure Monitoring & Automotive",
            ViewFactory = () => new GaugesDemoView(GetById("gauges-radial-linear")),
            SourceCode =
@"var gauge = new EnterpriseGaugeControl { IsLinear = false };
gauge.Min = 0;
gauge.Max = 100;
gauge.Value = 68;
gauge.Ranges.Add(new GaugeRange { From = 0, To = 50, Color = ""#107c10"" });",
            Documentation = "Radial and Linear gauges provide instant visual feedback for server health, CPU/RAM utilization, and industrial metrics.",
            BestPractices = "Limit redraws to value changes exceeding 0.5 units to minimize unnecessary paint cycles.",
            Troubleshooting = "Configure StartAngle and EndAngle to align with standard 270-degree industrial gauges.",
            ApiReference = "EnterpriseRadialGauge -> Min, Max, Value, StartAngle, EndAngle, Ranges"
        });

        // 6. EnterpriseScheduler - Calendar & Appointments
        _allDemos.Add(new DemoItem
        {
            Id = "scheduler-appointments",
            Title = "EnterpriseScheduler - Work Week Calendar & Resource Scheduling",
            Description = "Enterprise scheduling engine supporting Day, Work Week, Month, and Timeline views with RFC 5545 recurrence rules.",
            Category = DemoCategoryType.NavigationAndLayout,
            SubCategory = "EnterpriseScheduler",
            TargetControlName = "EnterpriseScheduler, EnterpriseSchedulerControl",
            Features = CachedValues11,
            Tags = CachedValues12,
            Industry = "Healthcare & Enterprise Resource Planning",
            ViewFactory = () => new SchedulerDemoView(GetById("scheduler-appointments")),
            SourceCode =
@"var scheduler = new EnterpriseSchedulerControl();
scheduler.ActiveView = SchedulerViewType.Week;
scheduler.Appointments.Add(new Appointment
{
    Subject = ""Sprint Review"",
    Start = DateTime.Today.AddHours(9),
    End = DateTime.Today.AddHours(10.5),
    Color = ""#0078d4""
});",
            Documentation = "EnterpriseScheduler maps appointments to customizable time slots with collision detection and multi-resource lanes.",
            BestPractices = "Filter appointments by active date window before binding to conserve memory.",
            Troubleshooting = "Ensure Appointment.End is strictly greater than Appointment.Start.",
            ApiReference = "EnterpriseScheduler -> ActiveView, SelectedDate, Appointments, Resources, TimeSlotMinutes"
        });

        // 7. Diagrams - Node-Link Canvas
        _allDemos.Add(new DemoItem
        {
            Id = "diagrams-node-link",
            Title = "Diagrams - Interactive Architecture & Workflow Canvas",
            Description = "Interactive node-link diagramming surface supporting shapes, orthogonal routing, arrowheads, and drag-and-drop manipulation.",
            Category = DemoCategoryType.DiagramsAndWorkflow,
            SubCategory = "Diagrams",
            TargetControlName = "EnterpriseDiagram, EnterpriseDiagramControl",
            Features = CachedValues13,
            Tags = CachedValues14,
            Industry = "DevOps Architecture & Process Modeling",
            ViewFactory = () => new DiagramsDemoView(GetById("diagrams-node-link")),
            SourceCode =
@"var diagram = new EnterpriseDiagramControl();
var n1 = new DiagramNode { Id = ""n1"", Text = ""Gateway"", X = 40, Y = 100 };
var n2 = new DiagramNode { Id = ""n2"", Text = ""Auth"", X = 240, Y = 40 };
diagram.Nodes.AddRange(new[] { n1, n2 });
diagram.Connectors.Add(new DiagramConnector { SourceNodeId = ""n1"", TargetNodeId = ""n2"" });",
            Documentation = "Diagrams provide an extensible canvas for process workflows, state machines, and system topologies.",
            BestPractices = "Use orthogonal connector pathing for enterprise architecture and network flow diagrams.",
            Troubleshooting = "Verify all connector source and target IDs exist in the Nodes collection.",
            ApiReference = "EnterpriseDiagram -> Nodes, Connectors, EnableGridSnapping, ZoomFactor"
        });

        // 8. GIS Maps - Fleet Tracking
        _allDemos.Add(new DemoItem
        {
            Id = "maps-gis-fleet",
            Title = "Maps - GIS Geographic Asset & Logistics Tracking",
            Description = "Geographic map display with latitude/longitude coordinate projections, zoom/pan controls, and interactive pin markers.",
            Category = DemoCategoryType.NavigationAndLayout,
            SubCategory = "Maps",
            TargetControlName = "EnterpriseGeoMap, EnterpriseMapControl",
            Features = CachedValues15,
            Tags = CachedValues16,
            Industry = "Global Supply Chain & Transportation",
            ViewFactory = () => new MapsDemoView(GetById("maps-gis-fleet")),
            SourceCode =
@"var map = new EnterpriseMapControl();
map.Center = new GeoLocation(37.7749, -122.4194);
map.ZoomLevel = 8;
map.Markers.Add(new GeoMarker
{
    Title = ""HQ Data Center"",
    Location = new GeoLocation(37.7749, -122.4194),
    Color = ""#0078d4""
});",
            Documentation = "EnterpriseGeoMap transforms GPS coordinates into screen space via equirectangular and Mercator projection algorithms.",
            BestPractices = "Cluster close markers when zoom level is under 5 to prevent marker overlap.",
            Troubleshooting = "Clamp latitude to [-85, 85] to prevent projection singularities near poles.",
            ApiReference = "EnterpriseGeoMap -> Center, ZoomLevel, Markers"
        });

        // 9. Reporting - Banded Documents
        _allDemos.Add(new DemoItem
        {
            Id = "reporting-banded-invoices",
            Title = "Reporting - Banded Invoice & Healthcare Statements",
            Description = "Report generator featuring Header, Detail, Group and Footer bands, data binding expressions, and print/PDF pipeline.",
            Category = DemoCategoryType.ReportingAndDashboard,
            SubCategory = "Reporting",
            TargetControlName = "ReportDefinition, EnterpriseReportViewer",
            Features = CachedValues17,
            Tags = CachedValues18,
            Industry = "Billing, Finance & Clinical EHR",
            ViewFactory = () => new ReportingDemoView(GetById("reporting-banded-invoices")),
            SourceCode =
@"var report = new ReportDefinition { ReportName = ""Invoice"" };
var header = new ReportBand { Type = BandType.PageHeader, Height = 40 };
header.Controls.Add(new EnterpriseReportControl { Text = ""Commercial Invoice"", Width = 300 });
report.Bands.Add(header);",
            Documentation = "ReportDefinition models banded layouts independently of UI controls.",
            BestPractices = "Use group bands for multi-level customer or department subtotals.",
            Troubleshooting = "Ensure total band height does not exceed page height to avoid unwanted blank overflow pages.",
            ApiReference = "ReportDefinition -> Bands, PageWidth, PageHeight, DataSource"
        });

        // 10. Dashboard - Executive BI
        _allDemos.Add(new DemoItem
        {
            Id = "dashboard-executive-bi",
            Title = "Dashboard - Executive BI Overview & KPI Cards",
            Description = "Business intelligence dashboard integrating high-impact KPI summary cards, trend forecast charts, and live tabular grids.",
            Category = DemoCategoryType.ReportingAndDashboard,
            SubCategory = "Dashboard",
            TargetControlName = "Dashboard, DashboardWidget",
            Features = CachedValues19,
            Tags = CachedValues20,
            Industry = "Executive Leadership & Board Reporting",
            IsFeatured = true,
            ViewFactory = () => new DashboardDemoView(GetById("dashboard-executive-bi")),
            SourceCode =
@"var dashboard = new Dashboard { Title = ""Executive Overview"" };
dashboard.AddKpi(""ARR Revenue"", ""$48.6M"", 18.4);
dashboard.Widgets.Add(new DashboardWidget { Title = ""Revenue Trend"", Type = WidgetType.EnterpriseChart });",
            Documentation = "Dashboard provides a responsive grid container for KPI metric cards, charts, and data grids.",
            BestPractices = "Place top-level actionable KPIs along the top row for immediate executive clarity.",
            Troubleshooting = "Keep column count between 3 and 4 for optimal readability on standard 1080p and 4K displays.",
            ApiReference = "Dashboard -> Title, ColumnsCount, Widgets, AddKpi()"
        });

        // 11. Form Editors & Validation
        _allDemos.Add(new DemoItem
        {
            Id = "editors-fluent-validation",
            Title = "Editors - Enterprise Form Input & Fluent Validation",
            Description = "Text, Numeric, Date, ComboBox, and Switch editors wired to fluent async validation rules with instant visual compliance feedback.",
            Category = DemoCategoryType.EditorsAndForms,
            SubCategory = "Editors",
            TargetControlName = "TextBox, EnterpriseNumericTextBox, EnterpriseDatePicker, Validator<T>",
            Features = CachedValues21,
            Tags = CachedValues22,
            Industry = "Cross-Industry Data Entry",
            ViewFactory = () => new EditorsDemoView(GetById("editors-fluent-validation")),
            SourceCode =
@"var validator = new Validator<DepartmentModel>()
    .RuleFor(m => m.Name, s => !string.IsNullOrEmpty(s), ""Name required"")
    .RuleFor(m => m.Budget, b => b >= 1000, ""Budget must be at least $1,000"");
var result = validator.Validate(model);",
            Documentation = "The Validation engine provides fluent type-safe predicate builder with synchronous and asynchronous verification pipelines.",
            BestPractices = "Combine client-side field validation with debounced asynchronous backend uniqueness checks.",
            Troubleshooting = "Check ValidationError.PropertyName to bind validation state directly to corresponding input controls.",
            ApiReference = "Validator<T> -> RuleFor, RuleForAsync, Validate, ValidateAsync"
        });

        // 12. Documents - PDF, RichText & Spreadsheet
        _allDemos.Add(new DemoItem
        {
            Id = "documents-multiformat",
            Title = "Documents & Media - PDF, RichText, Spreadsheet, ImageEditor & WebCam",
            Description = "Document and media workstation with PDF viewing, rich text, spreadsheet calculations, image transforms, webcam snapshots and ZIP processing.",
            Category = DemoCategoryType.DocumentsAndMedia,
            SubCategory = "Documents",
            TargetControlName = "EnterprisePdfViewer, EnterpriseRichTextEditor, EnterpriseSpreadsheetControl",
            Features = CachedValues23,
            Tags = CachedValues24,
            Industry = "Legal, Contract Management & Office Productivity",
            ViewFactory = () => new DocumentsDemoView(GetById("documents-multiformat")),
            SourceCode =
@"var docViewer = new DocumentsDemoView(meta);
// PDF rendering with vector kerning
// RichText with font styling and text runs
// Spreadsheet with formula parsing",
            Documentation = "Unified documents suite eliminating the need for bulky external Office interop or third-party viewer plugins.",
            BestPractices = "Use stream-based loading for large PDFs and documents to maintain low memory overhead.",
            Troubleshooting = "Ensure font collections match system DPI settings for crisp ClearType text rendering.",
            ApiReference = "EnterprisePdfViewer -> Document, CurrentPageIndex, ZoomFactor; EnterpriseRichTextEditor -> Document"
        });

        // 13. AI & Intelligence
        _allDemos.Add(new DemoItem
        {
            Id = "ai-copilot-assistant",
            Title = "AI Assistant - Copilot Chat & NL Query Translation",
            Description = "Offline-first AI copilot translating natural language prompts into compiled QueryEngine AST filters and generating automated BI summaries.",
            Category = DemoCategoryType.AIAndIntelligence,
            SubCategory = "AI Copilot",
            TargetControlName = "EnterpriseAIAssistant, EnterpriseAICopilotControl",
            Features = CachedValues25,
            Tags = CachedValues26,
            Industry = "Next-Generation Intelligent Enterprise",
            IsFeatured = true,
            ViewFactory = () => new AIControlsDemoView(GetById("ai-copilot-assistant")),
            SourceCode =
@"var assistant = new EnterpriseAIAssistant();
// Translates natural language directly to compiled QueryEngine filters
await assistant.SendPromptAsync(""Show pending high-priority orders above $2,500"");",
            Documentation = "EnterpriseAIAssistant provides an offline-first provider architecture that can swap between local deterministic mock providers and Azure OpenAI.",
            BestPractices = "Sanitize and validate compiled AST expressions before querying sensitive production backends.",
            Troubleshooting = "Verify that entity property names map accurately during natural language translation.",
            ApiReference = "EnterpriseAIAssistant -> SendPromptAsync, History, ServiceProvider"
        });

        // 14. Theming & Design Tokens
        _allDemos.Add(new DemoItem
        {
            Id = "theming-design-tokens",
            Title = "Theming - Live Theme Switching & Design Token Explorer",
            Description = "Instant live theme switching across Fluent, Material, Bootstrap, Office, Modern, and High-Contrast palettes with design tokens browser.",
            Category = DemoCategoryType.ThemingAndStyling,
            SubCategory = "Theming",
            TargetControlName = "ThemeManager, DesignTokens, BuiltInThemes",
            Features = CachedValues27,
            Tags = CachedValues28,
            Industry = "Design Systems & Corporate Branding",
            ViewFactory = () => new ThemePlaygroundView(GetById("theming-design-tokens")),
            SourceCode =
@"// Switch application theme dynamically
var theme = BuiltInThemes.CreateFluent(ColorScheme.Dark);
ThemeManager.Instance.ApplyTheme(theme);",
            Documentation = "EnterpriseUI theming relies on a centralized dictionary of DesignTokens (Surface, Primary, Border, Text) resolved dynamically by controls.",
            BestPractices = "Never hardcode Color.White or Color.Black; always fetch via EnterpriseThemeEngine.GetColor(theme, token, fallback).",
            Troubleshooting = "Implement IThemeableControl on custom controls to receive ThemeChanged notifications automatically.",
            ApiReference = "ThemeManager.Instance -> ApplyTheme, CurrentTheme, ThemeChanged"
        });

        // 15. End-to-End Business Scenarios
        _allDemos.Add(new DemoItem
        {
            Id = "business-scenarios-suite",
            Title = "Business Scenarios - CRM, ERP, Healthcare & Trading Suites",
            Description = "Comprehensive industry-specific enterprise applications demonstrating end-to-end workflows in CRM, ERP, Healthcare, and Trading.",
            Category = DemoCategoryType.BusinessScenarios,
            SubCategory = "Industry Suites",
            TargetControlName = "Integrated Ecosystem",
            Features = CachedValues29,
            Tags = CachedValues30,
            Industry = "Full Enterprise Ecosystem",
            IsFeatured = true,
            ViewFactory = () => new ScenariosDemoView(GetById("business-scenarios-suite")),
            SourceCode =
@"// End-to-end CRM and ERP composite controls
// Combines DataGrid, Charts, Gauges, and Status tracking",
            Documentation = "Real-world composite architectures illustrating how all EnterpriseUI framework modules collaborate seamlessly.",
            BestPractices = "Decompose complex enterprise forms into reusable user controls linked via EventAggregator.",
            Troubleshooting = "Use shared observable state models to synchronize grid edits with dashboard KPI summaries.",
            ApiReference = "Cross-module integration across EnterpriseUI.* assemblies"
        });

        // 16. Performance Laboratory
        _allDemos.Add(new DemoItem
        {
            Id = "performance-laboratory",
            Title = "Performance Laboratory - 1M Virtualization & Micro-Benchmarks",
            Description = "Reproducible micro-benchmarking suite measuring virtualization throughput, sub-millisecond query compilation, and zero-allocation rendering.",
            Category = DemoCategoryType.PerformanceLab,
            SubCategory = "Benchmarking",
            TargetControlName = "QueryEngine, VirtualizationManager",
            Features = CachedValues31,
            Tags = CachedValues32,
            Industry = "High-Performance Systems & Benchmarking",
            IsFeatured = true,
            ViewFactory = () => new PerformanceDemoView(GetById("performance-laboratory")),
            SourceCode =
@"// Reproducible micro-benchmark execution
var sw = Stopwatch.StartNew();
var result = QueryEngine.Execute(source, request);
sw.Stop();",
            Documentation = "The Performance Laboratory provides verifiable empirical metrics proving EnterpriseUI meets strict enterprise SLAs.",
            BestPractices = "Run benchmarks under Release builds with GC.Collect(2) preceding baseline measurements.",
            Troubleshooting = "Ensure SystemAware or PerMonitorV2 DPI awareness is declared in app.manifest.",
            ApiReference = "QueryEngine -> Execute, ApplyFiltering, ApplySorting"
        });

        // 17. Accessibility Center
        _allDemos.Add(new DemoItem
        {
            Id = "accessibility-center",
            Title = "Accessibility Center - WCAG 2.2 AA & Live Screen Reader Audit",
            Description = "Dedicated accessibility validation portal showing contrast compliance, ARIA role mapping, and assistive screen reader live region announcements.",
            Category = DemoCategoryType.AccessibilityCenter,
            SubCategory = "Accessibility",
            TargetControlName = "AccessibilityManager, IAccessibleControl",
            Features = CachedValues33,
            Tags = CachedValues34,
            Industry = "Federal, Healthcare & Financial Compliance",
            ViewFactory = () => new AccessibilityDemoView(GetById("accessibility-center")),
            SourceCode =
@"// Dispatch screen reader announcements
AccessibilityManager.Instance.Announce(
    ""Filter applied. 14 records visible."", 
    LiveRegionPoliteness.Polite);",
            Documentation = "AccessibilityManager exposes Section 508 and WCAG 2.2 Level AA accessibility hooks across all controls.",
            BestPractices = "Announce state mutations politely so screen readers do not interrupt ongoing user speech.",
            Troubleshooting = "Validate that all clickable icon buttons specify a non-empty AriaLabel.",
            ApiReference = "AccessibilityManager.Instance -> Announce, SetHighContrastMode, GenerateAriaAttributes"
        });

        // 18. Learning Center
        _allDemos.Add(new DemoItem
        {
            Id = "learning-center-hub",
            Title = "Learning Center - Architecture Guides, Tutorials & Anti-Patterns",
            Description = "Integrated developer onboarding portal with architectural concepts, design patterns, anti-pattern remediation, and migration guides.",
            Category = DemoCategoryType.LearningCenter,
            SubCategory = "Developer Onboarding",
            TargetControlName = "Educational Suite",
            Features = CachedValues35,
            Tags = CachedValues36,
            Industry = "Developer Experience & Training",
            ViewFactory = () => new LearningCenterDemoView(GetById("learning-center-hub")),
            SourceCode =
@"// Review framework design rules and patterns
// 1. Decoupled headless models
// 2. Platform native renderers
// 3. Centralized design tokens",
            Documentation = "The Learning Center offers an authoritative reference for software architects and senior engineers building on EnterpriseUI.",
            BestPractices = "Study the anti-pattern remediation section to avoid common GDI+ performance pitfalls.",
            Troubleshooting = "Review setup guides and API examples when integrating components into an existing application.",
            ApiReference = "Full conceptual hierarchy and API cross-references"
        });

        // 19. Interactive API Explorer
        _allDemos.Add(new DemoItem
        {
            Id = "api-interactive-explorer",
            Title = "API Explorer - Assembly Type Browser & Member Inspector",
            Description = "Reflection-powered API browser inspecting types, public properties, methods, events, and signatures across all 20+ EnterpriseUI libraries.",
            Category = DemoCategoryType.Overview,
            SubCategory = "Developer Tools",
            TargetControlName = "All EnterpriseUI Assemblies",
            Features = CachedValues37,
            Tags = CachedValues38,
            Industry = "Developer Tooling",
            ViewFactory = () => new ApiExplorerDemoView(GetById("api-interactive-explorer")),
            SourceCode =
@"// Inspect any EnterpriseUI assembly at runtime
var asm = Assembly.Load(""EnterpriseUI.Grids"");
var exported = asm.GetExportedTypes();",
            Documentation = "Provides instant runtime introspection into all public types, methods, and properties.",
            BestPractices = "Use the API Explorer to discover newly shipped extension points and interface contracts.",
            Troubleshooting = "Ensure referenced assemblies are loaded in the current AppDomain.",
            ApiReference = "System.Reflection based inspection engine"
        });

        // 20. QA Validation Mode
        _allDemos.Add(new DemoItem
        {
            Id = "qa-validation-suite",
            Title = "QA Verification Mode - Automated Test Coverage & Diagnostics",
            Description = "Quality assurance validation platform executing automated test suites, verifying feature coverage, and generating diagnostics bundles.",
            Category = DemoCategoryType.QAValidation,
            SubCategory = "Quality Assurance",
            TargetControlName = "QA Harness",
            Features = CachedValues39,
            Tags = CachedValues40,
            Industry = "Quality Assurance & Certification",
            ViewFactory = () => new QaValidationDemoView(GetById("qa-validation-suite")),
            SourceCode =
@"// Execute QA test suites across all 14 shipped modules
var testRunner = new QaValidationDemoView(meta);",
            Documentation = "QA Verification Mode gives quality engineers and release architects full transparency into automated test status.",
            BestPractices = "Run this verification suite prior to committing changes or approving release candidates.",
            Troubleshooting = "Check diagnostics logs for any failing assertions or missing theme tokens.",
            ApiReference = "EnterpriseUI automated QA and verification matrices"
        });

        // 21. Gantt, Barcode & Kanban TaskBoard Suite (Component Catalog Suite)
        _allDemos.Add(new DemoItem
        {
            Id = "specialized-gantt-barcode-kanban",
            Title = "Gantt, Barcode/QR & Kanban TaskBoard Suite",
            Description = "Project Gantt EnterpriseChart with critical path dependencies, Vector QR/Code128/DataMatrix Barcode generator, and interactive Agile Kanban TaskBoard.",
            Category = DemoCategoryType.DiagramsAndWorkflow,
            SubCategory = "Gantt, Barcode & Kanban",
            TargetControlName = "EnterpriseGanttControl, EnterpriseBarcodeControl, EnterpriseKanbanBoardControl",
            Features = CachedValues41,
            Tags = CachedValues42,
            Industry = "Project Management, Supply Chain & Agile Engineering",
            IsFeatured = true,
            IsPopular = true,
            ViewFactory = () => new SpecializedControlsDemoView(GetById("specialized-gantt-barcode-kanban")),
            SourceCode =
@"var gantt = new EnterpriseGanttControl { ShowCriticalPath = true };
var barcode = new EnterpriseBarcodeControl { Symbology = BarcodeSymbology.QRCode, Value = ""EUI-2026"" };
var kanban = new EnterpriseKanbanBoardControl();",
            Documentation = "Provides native GDI+ vector implementations of EnterpriseGanttView (with task dependencies and critical path), Barcode (QR, Code128, DataMatrix), and Kanban TaskBoard.",
            BestPractices = "Highlight critical path milestones in Gantt charts to surface schedule bottlenecks early.",
            Troubleshooting = "Ensure Barcode quiet-zone padding is preserved when printing labels.",
            ApiReference = "EnterpriseGanttControl -> Tasks, ShowCriticalPath; EnterpriseBarcodeControl -> Symbology, Value; EnterpriseKanbanBoardControl -> Cards"
        });

        _allDemos.Add(new DemoItem
        {
            Id = "component-catalog",
            Title = "Component Catalog",
            Description = "Search component metadata and open each control's dedicated demonstration.",
            Category = DemoCategoryType.Overview,
            SubCategory = "Component Catalog Suite",
            TargetControlName = "EnterpriseControlRegistry",
            Features = CatalogFeatures,
            Tags = CatalogTags,
            Industry = "Enterprise Desktop & Developer Productivity",
            IsFeatured = true,
            IsPopular = true,
            ViewFactory = () => new CompleteControlCatalogDemoView(GetById("component-catalog")),
            SourceCode =
@"var controls = EnterpriseControlRegistry.AllControls;
var demo = DemoMetadataRegistry.GetCatalogDemo(controls[0]);
DemoNavigationManager.Instance.NavigateTo(demo);",
            Documentation = "The catalog links to canonical demo pages without embedding duplicate demonstrations.",
            BestPractices = "Use the catalog to locate a control's dedicated page.",
            Troubleshooting = "Clear the search and category filter to show all components.",
            ApiReference = "EnterpriseControlRegistry.AllControls, DemoMetadataRegistry.GetCatalogDemo"
        });

        // 23. Navigation, Layout, Wizard, EnterpriseCarousel, EnterprisePanorama & DesktopAlerts Suite
        _allDemos.Add(new DemoItem
        {
            Id = "navigation-layout-alerts-suite",
            Title = "Navigation & Layout - EnterpriseBreadcrumb, StepProgress, EnterpriseCarousel, EnterprisePanorama & Wizard",
            Description = "Interactive EnterpriseBreadcrumb hierarchy bar, EnterpriseStepProgressBar pipeline, EnterpriseBindingNavigator, 3D Elliptical EnterpriseCarousel, EnterprisePanorama live tiles, multi-step EnterpriseWizardControl, EnterpriseDesktopAlert, and EnterpriseTaskDialog.",
            Category = DemoCategoryType.NavigationAndLayout,
            SubCategory = "Navigation, Layout & Alerts",
            TargetControlName = "EnterpriseBreadcrumb, EnterpriseStepProgressBar, EnterpriseBindingNavigator<T>, EnterpriseCarousel, EnterprisePanorama, EnterpriseWizardControl, EnterpriseDesktopAlert, EnterpriseTaskDialog",
            Features = CachedValues45,
            Tags = CachedValues46,
            Industry = "Enterprise Application Shell & Workflow UX",
            IsFeatured = true,
            IsPopular = true,
            ViewFactory = () => new NavigationAndLayoutDemoView(GetById("navigation-layout-alerts-suite")),
            SourceCode =
@"var breadcrumb = new EnterpriseBreadcrumb();
var stepProgress = new EnterpriseStepProgressBar();
var wizard = new EnterpriseWizardControl();
var panorama = new EnterprisePanorama();
var carousel = new EnterpriseCarouselControl();",
            Documentation = "Demonstrates the complete Navigation, Layout, Wizard, Live Tile EnterprisePanorama, 3D EnterpriseCarousel, and Notification Dialog suite.",
            BestPractices = "Use EnterpriseStepProgressBar and EnterpriseWizardControl together for multi-stage enterprise onboarding and deployment pipelines.",
            Troubleshooting = "Ensure WizardPage.ValidateStep returns true before calling MoveNext().",
            ApiReference = "EnterpriseBreadcrumb, EnterpriseStepProgressBar, EnterpriseBindingNavigator<T>, EnterpriseCarousel, EnterprisePanorama, EnterpriseWizardControl, EnterpriseDesktopAlert, EnterpriseTaskDialog"
        });

        // 26. SlideView, Rotator, PipsPager, EnterpriseImageEditor, PictureBox & EnterpriseWebCam Suite
        _allDemos.Add(new DemoItem
        {
            Id = "slide-rotator-pips-media-suite",
            Title = "SlideView, Rotator & PipsPager",
            Description = "Interactive SlideView deck, animated Rotator banner and PipsPager pagination indicator.",
            Category = DemoCategoryType.NavigationAndLayout,
            SubCategory = "SlideView, PipsPager & Media",
            TargetControlName = "EnterpriseSlideRotatorPipsControl",
            Features = SlideFeatures,
            Tags = SlideTags,
            Industry = "Digital Signage, Kiosks, Security Badging & Media",
            IsFeatured = true,
            ViewFactory = () => new CompleteControlCatalogDemoView(GetById("slide-rotator-pips-media-suite")),
            SourceCode =
@"var slideView = new EnterpriseSlideRotatorPipsControl();",
            Documentation = "Demonstrates EnterpriseSlideView, EnterpriseRotator and EnterprisePipsPager. Image editing and webcam demonstrations live in Documents & Media.",
            BestPractices = "Use PipsPager paired with SlideView for touch-friendly executive presentation decks.",
            Troubleshooting = "Use slide navigation to change the selected page.",
            ApiReference = "EnterpriseSlideRotatorPipsControl"
        });

        // 27. Clock, Callout & RangeSelector Suite
        _allDemos.Add(new DemoItem
        {
            Id = "clock-callout-rangeselector-suite",
            Title = "Clock & RangeSelector Suite",
            Description = "Vector GDI+ Analog & Digital Clock with timezone offsets and a dual-thumb RangeSelector with sparkline preview.",
            Category = DemoCategoryType.ChartsAndGauges,
            SubCategory = "Clock & RangeSelector",
            TargetControlName = "EnterpriseClockControl, EnterpriseRangeSelectorControl",
            Features = CachedValues53,
            Tags = CachedValues54,
            Industry = "Global Trading Desks & Operations Centers",
            IsFeatured = true,
            ViewFactory = () => new CompleteControlCatalogDemoView(GetById("clock-callout-rangeselector-suite")),
            SourceCode =
@"var clock = new EnterpriseClockControl { TimeZoneOffsetHours = -5, TimeZoneLabel = ""UTC-5 (New York)"" };
var rangeSelector = new EnterpriseRangeSelectorControl { SelectionStartPercent = 20, SelectionEndPercent = 80 };",
            Documentation = "Implements EnterpriseClock and EnterpriseRangeSelector using anti-aliased GDI+ vector geometry.",
            BestPractices = "Link EnterpriseRangeSelectorControl.RangeChanged to your primary ChartView time axis for synchronized zooming.",
            Troubleshooting = "Ensure EnterpriseClockControl is disposed when its parent container closes to release the 1-second tick timer.",
            ApiReference = "EnterpriseClockControl, EnterpriseRangeSelectorControl"
        });

        // 28. NavigationView, OfficeNavigationBar, Forms, Overlays, Toast & Tools Suite
        _allDemos.Add(new DemoItem
        {
            Id = "navigation-forms-overlays-suite",
            Title = "NavigationView, OfficeNavBar, Forms, Overlays, Toast & Tools",
            Description = "Hamburger NavigationView, Outlook OfficeNavigationBar with Peek Window, SplashScreen/Overlay/Flyout managers, ToastNotification & NotifyIcon, TaskbarButton, TabbedForm/ToolbarForm/ShapedForm, FileDialogs, ControlSpy & ShapeEditor.",
            Category = DemoCategoryType.NavigationAndLayout,
            SubCategory = "Forms, Overlays & Shell Tools",
            TargetControlName = "NavigationView, OfficeNavigationBar, Flyout, Overlay, SplashScreen, ToastNotification, TabbedForm, ControlSpy",
            Features = CachedValues55,
            Tags = CachedValues56,
            Industry = "Modern Desktop Application Shells & IDE Tooling",
            IsFeatured = true,
            ViewFactory = () => new CompleteControlCatalogDemoView(GetById("navigation-forms-overlays-suite")),
            SourceCode =
@"// Complete 118-Control reference WinForms coverage Registry
var allControls = EnterpriseControlRegistry.AllControls;",
            Documentation = "Browse component metadata and open the associated EnterpriseUI demonstrations.",
            BestPractices = "Use NavigationView in Compact mode on smaller viewports and Expanded mode on 1080p+ monitors.",
            Troubleshooting = "Open Component Catalog to search and jump to any control demonstration.",
            ApiReference = "EnterpriseControlRegistry.AllControls (118 Controls)"
        });
        RegisterGridWorkshop();
        RegisterDataControlWorkshop();
        RegisterInteractiveWorkshop();
    }

    private static void RegisterInteractiveWorkshop()
    {
        foreach (var scenario in InteractiveControlScenarios.All)
        {
            var captured = scenario;
            var target = captured.Api.Contains(" -> ", StringComparison.Ordinal) ? captured.Api[..captured.Api.IndexOf(" -> ", StringComparison.Ordinal)] : captured.Title;
            _allDemos.Add(new DemoItem
            {
                Id = captured.Id,
                Title = captured.Title,
                Description = captured.Description,
                Category = captured.Id is "interactive-aiprompt" or "interactive-inlineai" or "interactive-chat" or "interactive-smartpaste" or "interactive-speechtotext" ? DemoCategoryType.AIAndIntelligence : captured.Id is "interactive-carousel" or "interactive-rotator" or "interactive-stepprogress" or "interactive-callout" or "interactive-notifyicon" ? DemoCategoryType.NavigationAndLayout : DemoCategoryType.EditorsAndForms,
                SubCategory = "Interactive Controls",
                TargetControlName = target,
                Features = captured.Features,
                Tags = new[] { "Interactive", "Animation", "Accessibility", "Theming" },
                Industry = "Cross-Industry",
                IsFeatured = captured.Id is "interactive-aiprompt" or "interactive-chat" or "interactive-carousel",
                IsNew = true,
                ViewFactory = () => new GridScenarioView(GetById(captured.Id), captured),
                SourceCode = captured.Usage,
                Documentation = captured.Documentation,
                BasicUsage = captured.Usage,
                ArchitectureNotes = captured.Architecture,
                PerformanceNotes = captured.Performance,
                AccessibilityNotes = captured.Accessibility,
                BestPractices = captured.BestPractices,
                Troubleshooting = captured.Troubleshooting,
                ApiReference = captured.Api,
                RelatedControls = "EnterpriseButtonControl, EnterpriseTrackBarControl, EnterpriseProgressBarControl, EnterpriseChatControl, EnterpriseAIPromptControl"
            });
        }
    }

    private static void RegisterDataControlWorkshop()
    {
        foreach (var scenario in DataControlScenarios.All)
        {
            var captured = scenario;
            var target = captured.Api.Contains(" -> ", StringComparison.Ordinal) ? captured.Api[..captured.Api.IndexOf(" -> ", StringComparison.Ordinal)] : captured.Title;
            _allDemos.Add(new DemoItem
            {
                Id = captured.Id,
                Title = captured.Title,
                Description = captured.Description,
                Category = DemoCategoryType.GridsAndData,
                SubCategory = "Data Management Controls",
                TargetControlName = target,
                Features = captured.Features,
                Tags = CachedValues57,
                Industry = "Cross-Industry",
                IsFeatured = captured.Id is "data-propertygrid" or "data-treemap" or "data-pivotgrid",
                IsNew = true,
                ViewFactory = () => new GridScenarioView(GetById(captured.Id), captured),
                SourceCode = captured.Usage,
                Documentation = captured.Documentation,
                BasicUsage = captured.Usage,
                ArchitectureNotes = captured.Architecture,
                PerformanceNotes = captured.Performance,
                AccessibilityNotes = captured.Accessibility,
                BestPractices = captured.BestPractices,
                Troubleshooting = captured.Troubleshooting,
                ApiReference = captured.Api,
                RelatedControls = "EnterpriseDataGrid<T>, EnterpriseListControl, EnterpriseTreeViewControl, EnterprisePropertyGridControl, EnterprisePivotGridControl"
            });
        }
    }

    private static void RegisterGridWorkshop()
    {
        foreach (var scenario in GridScenarios.All)
        {
            var captured = scenario;
            _allDemos.Add(new DemoItem
            {
                Id = captured.Id,
                Title = captured.Title,
                Description = captured.Description,
                Category = DemoCategoryType.GridsAndData,
                SubCategory = "DataGrid Workshop",
                TargetControlName = "EnterpriseDataGrid<T>",
                Features = captured.Features,
                Tags = CachedValues57,
                Industry = "Cross-Industry",
                IsFeatured = captured.Id is "grid-enterprise" or "grid-basics",
                IsNew = true,
                ViewFactory = () => new GridScenarioView(GetById(captured.Id), captured),
                SourceCode = "// Open the Generated Code tab: C#, Designer, Fluent API, JSON and a complete exportable example are generated from the live grid state.",
                Documentation = captured.Documentation,
                BasicUsage = captured.Usage,
                ArchitectureNotes = captured.Architecture,
                PerformanceNotes = captured.Performance,
                AccessibilityNotes = captured.Accessibility,
                BestPractices = captured.BestPractices,
                Troubleshooting = captured.Troubleshooting,
                ApiReference = captured.Api,
                RelatedControls = string.IsNullOrEmpty(captured.Related) ? Related : captured.Related
            });
        }
    }

    private const string Related = "EnterpriseDataGrid<T>, DataGrid<T>, GridBuilder<T>, EnterpriseTreeList, EnterprisePivotGrid";

    public static DemoItem GetCatalogDemo(ControlCatalogEntry entry)
    {
        var id = entry.Name switch
        {
            "EnterpriseCalculatorDropDown" or "CalculatorControl" or "EnterpriseSyntaxEditor" or "SpellChecker" => "editors-fluent-validation",
            "EnterpriseBulletGraph" => "gauges-radial-linear",
            "ImageEditorControl" or "PictureBoxControl" or "EnterpriseWebCam" => "documents-multiformat",
            _ => entry.DemoSuiteId
        };
        id = ResolveDemoId(id);
        return _allDemos.Find(demo => demo.Id == id)
            ?? throw new InvalidOperationException($"Missing catalog demo target: {entry.Name} / {id}");
    }

    private static string ResolveDemoId(string id) => id switch
        {
            "grid-pivot-olap" => "data-pivotgrid",
            "dataentry-filter-cards-suite" => "data-dataentry",
            "data-gridview" => "grid-basics",
            _ => id
        };

    public static DemoItem GetById(string id)
    {
        id = ResolveDemoId(id);
        return _allDemos.Find(d => d.Id == id) ?? _allDemos[0];
    }
}

