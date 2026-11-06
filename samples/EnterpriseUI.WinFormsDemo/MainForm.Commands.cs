namespace EnterpriseUI.WinFormsDemo;

public partial class MainForm
{
    private void RegisterRibbonActions()
    {
        void Link(string tab, string text, string demoId) => _ribbon.RegisterAction(tab, text, () => LoadDemo(Infrastructure.DemoMetadataRegistry.GetById(demoId)));
        Link("Home", "Overview\nShowcase", "dashboard-executive-bi");
        Link("Home", "Learning\nCenter", "learning-center-hub");
        Link("Home", "Feature\nMatrix", "component-catalog");
        Link("Home", "System\nMetrics", "performance-laboratory");
        Link("Data & Grids", "Virtual Grid\n1M Rows", "grid-virtualization-1m");
        Link("Data & Grids", "Hierarchical\nTreeList", "grid-treelist-hierarchy");
        Link("Data & Grids", "OLAP Multi-Dim\nPivotGrid", "grid-pivot-olap");
        Link("Data & Grids", "Streaming\nExport", "grid-exporting");
        Link("Visualizations", "Financial &\nCandlestick", "charts-financial-realtime");
        Link("Visualizations", "Radial, Linear\n& EnterpriseBulletGraph", "gauges-radial-linear");
        Link("Visualizations", "Interactive\nDiagrams", "diagrams-node-link");
        Link("Visualizations", "GIS Map\nTracking", "maps-gis-fleet");
        Link("Navigation & Layout", "EnterpriseBreadcrumb &\nStepProgress", "navigation-layout-alerts-suite");
        Link("Navigation & Layout", "3D EnterpriseCarousel &\nLive EnterprisePanorama", "component-catalog");
        Link("Navigation & Layout", "Wizard &\nDataLayout", "navigation-layout-alerts-suite");
        Link("Navigation & Layout", "EnterpriseDesktopAlert &\nTaskDialog", "navigation-forms-overlays-suite");
        Link("Editors & Docs", "Masked, Color\n& Calculator", "editors-fluent-validation");
        Link("Editors & Docs", "EnterpriseSyntaxEditor &\nSpellChecker", "component-catalog");
        Link("Editors & Docs", "SignaturePad &\nTouch Keyboard", "specialized-gantt-barcode-kanban");
        Link("Editors & Docs", "PDF, Spread &\nZipLibrary", "documents-multiformat");
        Link("Component Catalog", "EnterpriseVirtualGrid\n(1M×12 Cells)", "component-catalog");
        Link("Component Catalog", "Gantt, Barcode\n& Kanban", "specialized-gantt-barcode-kanban");
        Link("Component Catalog", "EnterpriseTreeMap &\nBulletGraph", "gauges-radial-linear");
        Link("Component Catalog", "Rating &\nWaitingBar", "component-catalog");
        Link("Intelligence", "AI Copilot\nChat", "ai-copilot-assistant");
        Link("Intelligence", "NL Query\nTranslation", "ai-prompt-smart-controls-suite");
        Link("Intelligence", "Automated\nSummary", "ai-prompt-smart-controls-suite");
        Link("Theming", "Fluent\n(Dark/Light)", "theming-design-tokens");
        Link("Theming", "Material\nDesign", "theming-design-tokens");
        Link("Theming", "Office\nTheme", "theming-design-tokens");
        Link("Theming", "High\nContrast", "theming-design-tokens");
        Link("QA & Lab", "Stress Test\nMillion Rows", "grid-performance");
        Link("QA & Lab", "WCAG 2.2\nAudit", "accessibility-center");
        Link("QA & Lab", "API Object\nInspector", "api-interactive-explorer");
        Link("QA & Lab", "Regression\nMatrix", "qa-validation-suite");
    }
}