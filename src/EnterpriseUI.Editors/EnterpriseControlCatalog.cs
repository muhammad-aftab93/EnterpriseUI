using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Editors;

/// <summary>
/// mapped to its clean-room EnterpriseUI implementation.
/// </summary>
public sealed record ControlCatalogEntry(
    string EnterpriseUIName,
    string Category,
    string DocumentationPath,
    string KeyFeatures,
    string DemoSuiteId)
{
    public string Name => EnterpriseUIName.Split('/')[0].Trim();
}

/// <summary>
/// </summary>
public static class EnterpriseControlRegistry
{
    public static IReadOnlyList<ControlCatalogEntry> AllControls { get; } = new List<ControlCatalogEntry>
    {
        // 1. AI & Conversational Controls (4)
        new("AIPromptControl / EnterpriseAIPromptControl", "AI & Smart Controls", "docs/Controls/UnifiedControlsCatalog.md", "Prompt View, Output View, Commands View, Rating, Copy/Insert/Retry", "ai-prompt-smart-controls-suite"),
        new("AIPromptButton", "AI & Smart Controls", "docs/Controls/UnifiedControlsCatalog.md", "Popup AI Prompt attachable to any editor or grid", "ai-prompt-smart-controls-suite"),
        new("SmartPasteButton", "AI & Smart Controls", "docs/Controls/UnifiedControlsCatalog.md", "AI Clipboard field extraction into bound form controls", "ai-prompt-smart-controls-suite"),
        new("SpeechToTextButton", "AI & Smart Controls", "docs/Controls/UnifiedControlsCatalog.md", "Voice dictation, waveform pulse, custom recognizer states", "ai-prompt-smart-controls-suite"),
        new("AIChatView / ConversationalChatControl", "AI & Smart Controls", "docs/Controls/UnifiedControlsCatalog.md", "Suggested Actions, Adaptive Cards, Typing Indicator, Avatars", "ai-copilot-assistant"),

        // 2. Data Grids, Cards, Filter & List Controls (9)
        new("DataGrid<T> / EnterpriseDataGrid<T>", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "1M-Row Virtualization, Grouping, Multi-Sort, Filter Row, Master-Detail, Bands", "grid-virtualization-1m"),
        new("EnterpriseVirtualGrid / EnterpriseVirtualGridControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "CellValueNeeded / CellValuePushed 1M x 12 on-demand matrix", "component-catalog"),
        new("EnterpriseTreeList<T> / EnterpriseTreeListControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Self-referencing hierarchy, Tri-state checkboxes, Drag & Drop, Multi-column", "grid-treelist-hierarchy"),
        new("EnterprisePivotGrid<T> / EnterprisePivotGridControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "OLAP Cross-Tab, Calculated Fields, DrillDown, PivotFieldList", "grid-pivot-olap"),
        new("EnterpriseCardView<TItem> / EnterpriseCardViewControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Templated cards, Group/Sort/Filter cards, Inline card editing", "dataentry-filter-cards-suite"),
        new("ListViewControl<TItem>", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "DetailsView, IconsView, ListView, Grouping, CheckBoxes, Drag-Drop", "dataentry-filter-cards-suite"),
        new("DataFilterControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Hierarchical AND/OR/NOT expression tree builder & DataFilterDialog", "dataentry-filter-cards-suite"),
        new("FilterViewControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Faceted category checklist & numeric range filter panel", "dataentry-filter-cards-suite"),
        new("DataEntryControl<TEntity>", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Auto-generated bound entity form, multi-column layout, validation", "dataentry-filter-cards-suite"),

        // 3. Buttons & Toggles (8)
        new("EnterpriseButtonControl / EnterpriseActionButton", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Themed vector icon, ripple, badge, command binding, mnemonics", "editors-fluent-validation"),
        new("CheckBoxControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Tri-state Checked/Unchecked/Indeterminate, custom glyphs", "editors-fluent-validation"),
        new("RadioButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Exclusive option group, vector radio glyph, WCAG focus ring", "editors-fluent-validation"),
        new("ToggleButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Two-state / Three-state latch button with active token styling", "editors-fluent-validation"),
        new("EnterpriseToggleSwitchControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Animated pill On/Off switch with custom state labels", "editors-fluent-validation"),
        new("EnterpriseSplitButtonControl (DropDownMode)", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Primary button with popup menu arrow and item hierarchy", "editors-fluent-validation"),
        new("EnterpriseSplitButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Dual-zone primary action + dropdown arrow menu", "editors-fluent-validation"),
        new("EnterpriseRepeatButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Continuous fire on press-and-hold with configurable interval", "editors-fluent-validation"),

        // 4. Editors, Pickers & DropDown Lists (22)
        new("EnterpriseTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "NullText placeholder, Clear button (✕), Prefix glyph, Validation border", "editors-fluent-validation"),
        new("EnterpriseTextBoxControl / SyntaxTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Hardware-accelerated text surface with custom selection painting", "editors-fluent-validation"),
        new("EnterpriseButtonTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "TextBox with embedded left/right command buttons", "editors-fluent-validation"),
        new("EnterpriseButtonTextBoxControl (BrowseMode)", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "File/Folder path editor with integrated browse dialog trigger", "editors-fluent-validation"),
        new("EnterpriseMaskedTextBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Phone, SSN, IP, Currency, Regex, and Standard masks", "editors-fluent-validation"),
        new("SpinEditor / NumericUpDown", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Decimal/Hexadecimal numeric spinner with step & min/max", "editors-fluent-validation"),
        new("DomainUpDownControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "String collection up/down spinner with wrap-around", "editors-fluent-validation"),
        new("EnterpriseAutoCompleteBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Tokenized multi-value chips with prefix/contains search", "editors-fluent-validation"),
        new("PopupEditorControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Resizable dropdown container hosting arbitrary custom controls", "editors-fluent-validation"),
        new("EnterpriseCalculatorDropDown", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Dropdown arithmetic calculator with memory (MC/MR/MS/M+) & history", "component-catalog"),
        new("CalculatorControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Standalone scientific/standard calculator pad with expression parser", "component-catalog"),
        new("ColorPicker / ColorBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "HEX/ARGB input, Web/System/Professional palettes, Screen eyedropper", "editors-fluent-validation"),
        new("DateTimePickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Calendar dropdown, Free-form date parsing, Null date support", "editors-fluent-validation"),
        new("DateOnlyPickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Dedicated .NET 8 DateOnly picker with ISO-8601 calendar popup", "editors-fluent-validation"),
        new("TimePickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Clock face & hour/minute table popup with 12h/24h formats", "editors-fluent-validation"),
        new("TimeOnlyPickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Dedicated .NET 8 TimeOnly picker with precision interval selector", "editors-fluent-validation"),
        new("EnterpriseTimeSpanPicker", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Days, Hours, Minutes, Seconds, Milliseconds duration picker", "editors-fluent-validation"),
        new("DropDownListControl<T>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Virtualized dropdown with auto-complete suggest & append", "editors-fluent-validation"),
        new("ListControl<T>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Virtualized scrollable list with custom item formatting", "editors-fluent-validation"),
        new("EnterpriseCheckedDropDownList<TItem>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Multi-select checkable dropdown with summary token header", "editors-fluent-validation"),
        new("CheckedListBoxControl<TItem>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Data-bound checkable list with Spacebar toggle & Check All", "editors-fluent-validation"),
        new("EnterpriseFontDropDownControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Installed font family selector rendered in live font preview", "editors-fluent-validation"),
        new("EnterpriseMultiColumnComboBox<TItem>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Embedded DataGrid dropdown with multi-column filtering", "editors-fluent-validation"),
        new("PropertyGridControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Categorized/Alphabetical object inspector, search, custom type converters", "editors-fluent-validation"),

        // 5. Data Visualization: Charts, Gauges, Maps & Diagrams (13)
        new("CartesianChart / PieChart / CandlestickChart", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Bar, Line, Spline, Area, Pie, Donut, Candlestick, Radar, Funnel, DrillDown", "charts-financial-realtime"),
        new("EnterpriseSparkline", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Compact inline Line, Column, Win/Loss sparkline with High/Low/First/Last markers", "charts-financial-realtime"),
        new("EnterpriseRangeSelectorControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Dual-thumb zoom/pan range selector linked to ChartView", "clock-callout-rangeselector-suite"),
        new("EnterpriseHeatMap", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "2D color-gradient matrix with categorical axes, legend & tooltips", "charts-financial-realtime"),
        new("EnterpriseTreeMap / EnterpriseTreeMapControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Squarified/Slice-and-Dice hierarchical area proportional visualization", "specialized-gantt-barcode-kanban"),
        new("CircularGauge / EnterpriseGaugeControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Sweep arcs, needles, scale ticks, color ranges, animated value transitions", "gauges-radial-linear"),
        new("EnterpriseLinearGauge", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Horizontal/Vertical thermometer & bar indicator with threshold markers", "gauges-radial-linear"),
        new("EnterpriseBulletGraph / EnterpriseBulletGraphControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Stephen Few KPI EnterpriseBulletGraph with featured measure, target & qualitative ranges", "gauges-radial-linear"),
        new("EnterpriseDigitalGauge", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "7-Segment and 14-Segment LED/LCD digital telemetry readout", "gauges-radial-linear"),
        new("GeoMapControl / EnterpriseMapControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Vector choropleth polygons, pins, routes, clustering, mini-map, zoom/pan", "maps-gis-fleet"),
        new("DiagramCanvas / EnterpriseDiagramControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Nodes, Bezier/Orthogonal connectors, Auto-layout, Snap grid, Undo/Redo", "diagrams-node-link"),
        new("EnterpriseBarcodeGenerator / EnterpriseBarcodeControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "QR Code, Code128, EAN-13, DataMatrix, PDF417 vector generator & reader", "specialized-gantt-barcode-kanban"),

        // 6. Scheduling, Gantt & TaskBoard (4)
        new("SchedulerControl / EnterpriseSchedulerControl", "Scheduling & Project Management", "docs/Controls/UnifiedControlsCatalog.md", "Day, WorkWeek, Week, Month, Timeline & Agenda views, Recurrence, Resources", "scheduler-appointments"),
        new("Calendar", "Scheduling & Project Management", "docs/Controls/UnifiedControlsCatalog.md", "Multi-month view, Special days, Week numbers, Multi-date selection, RTL", "scheduler-appointments"),
        new("EnterpriseGanttView / EnterpriseGanttControl", "Scheduling & Project Management", "docs/Controls/UnifiedControlsCatalog.md", "Split TreeGrid + Timeline, Task bars, Dependencies, Critical Path, Milestones", "specialized-gantt-barcode-kanban"),
        new("EnterpriseKanbanBoardControl", "Scheduling & Project Management", "docs/Controls/UnifiedControlsCatalog.md", "Agile swimlanes, Task cards, Drag & Drop column transfer, WIP limits, Tags", "specialized-gantt-barcode-kanban"),

        // 7. Navigation, Containers, Layout & Docking (24)
        new("EnterpriseRibbonBar / EnterpriseRibbonControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Tabs, Contextual Groups, Backstage View, Quick Access Toolbar, Simplified Mode", "navigation-layout-alerts-suite"),
        new("EnterpriseDockManager", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "ToolWindows, DocumentWindows, Floating, Auto-Hide pins, Compass indicators, Layout XML", "navigation-layout-alerts-suite"),
        new("EnterprisePageView", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "StripView, StackView, OutlookView, ExplorerBarView, BackstageView, NavigationView", "navigation-layout-alerts-suite"),
        new("NavigationViewControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Hamburger menu with Expanded, Compact, and Minimal modes, Hierarchy & Footer", "navigation-forms-overlays-suite"),
        new("OfficeNavigationBarControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Outlook bottom navigation bar with hover Peek Window & Options dialog", "navigation-forms-overlays-suite"),
        new("EnterpriseBreadcrumb", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Path hierarchy selector with dropdown node children & history", "navigation-layout-alerts-suite"),
        new("EnterpriseBindingNavigator<TItem>", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "First, Previous, Record Position, Next, Last, AddNew, Delete, Save", "navigation-layout-alerts-suite"),
        new("EnterpriseCommandBar", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Dockable/Floating multi-row toolstrips with overflow chevron", "navigation-layout-alerts-suite"),
        new("EnterprisePanorama", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Windows Live Tiles (Single/Double/Quad) with live flip animation & groups", "navigation-layout-alerts-suite"),
        new("EnterpriseCarousel / EnterpriseCarouselControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "3D Elliptical path item reflection, depth scaling, smooth animation", "component-catalog"),
        new("RotatorControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Automated frame/banner rotator with directional slide transitions", "slide-rotator-pips-media-suite"),
        new("SlideViewControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Interactive slide gallery with Prev/Next navigation & infinite loop", "slide-rotator-pips-media-suite"),
        new("PipsPagerControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Dot/Number pagination indicator with horizontal/vertical orientation", "slide-rotator-pips-media-suite"),
        new("EnterpriseWizardControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Welcome, Interior, Completion pages, Per-step validation, Aero/Wizard97", "navigation-layout-alerts-suite"),
        new("LayoutControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Runtime drag-and-drop layout customization, layout groups, JSON/XML persistence", "navigation-layout-alerts-suite"),
        new("EnterpriseDataLayout<TEntity>", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Attribute-driven auto-form generation from [Display] & [Required] metadata", "navigation-layout-alerts-suite"),
        new("SplitContainer", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Multi-panel nested splitter with collapse/expand buttons & proportional sizing", "navigation-layout-alerts-suite"),
        new("EnterpriseCollapsiblePanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Animated expand/collapse header container (Vertical & Horizontal)", "navigation-layout-alerts-suite"),
        new("GroupBoxControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Themed header group box with header alignment & custom border styles", "navigation-layout-alerts-suite"),
        new("PanelControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Bordered theme-aware container panel with shadow & corner radius", "navigation-layout-alerts-suite"),
        new("ScrollablePanelControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Themed custom scrollbar container with touch kinetic scrolling", "navigation-layout-alerts-suite"),
        new("LabelControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "HTML-like text markup, Auto-ellipsis, Image-text alignment", "navigation-layout-alerts-suite"),
        new("SeparatorControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Horizontal/Vertical visual divider with optional shadow line", "navigation-layout-alerts-suite"),
        new("MenuControl / ContextMenuControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Multi-column drop-down menus, checkable items, keyboard shortcuts, icons", "navigation-forms-overlays-suite"),

        // 8. Documents, RichText, Spreadsheet, PDF & Syntax (6)
        new("RichTextDocument / RichTextEditorControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Formatted paragraphs, tables, inline images, ruler, DOCX/RTF/HTML export", "documents-multiformat"),
        new("SpreadsheetDocument / EnterpriseSpreadsheetControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Worksheets, Cell A1 formulas (=SUM, =AVERAGE, =IF), Number formatting, CSV/XLSX", "documents-multiformat"),
        new("PdfDocumentGenerator / PdfViewerControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Page rendering, Thumbnails, Text search, Zoom/Rotate, PdfViewerNavigator", "documents-multiformat"),
        new("EnterpriseSyntaxEditor / EnterpriseSyntaxEditorControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Lexical AST tokenizer (C#, SQL, JSON, XML), Line numbers, Code folding", "component-catalog"),
        new("SpellChecker", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Levenshtein distance dictionary suggestions, Custom dictionary, As-you-type", "component-catalog"),
        new("DocumentProcessingSuite", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Headless WordsProcessing, SpreadProcessing, PdfProcessing & ZipLibrary", "documents-multiformat"),

        // 9. Track, Status, Alerts, Overlays & Notifications (14)
        new("EnterpriseProgressBarControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Determinate/Indeterminate progress, Dash/Solid fill, Text percentage overlay", "editors-fluent-validation"),
        new("EnterpriseStepProgressBar", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Multi-stage workflow tracker with step icons, titles & completion status", "navigation-layout-alerts-suite"),
        new("EnterpriseWaitingBar / EnterpriseRatingAndWaitingControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Marquee blocks, Rotating ring, Segmented dots, Fading bar busy indicators", "component-catalog"),
        new("EnterpriseRatingControl / EnterpriseRatingAndWaitingControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Full, Half, and Continuous precision star rating with hover preview", "component-catalog"),
        new("RangeSlider / TrackBarControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Single and Dual-thumb range trackbar with tick marks & value tooltip", "editors-fluent-validation"),
        new("ScrollBarControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Themed horizontal/vertical scrollbar with thumb grip & smooth step", "navigation-layout-alerts-suite"),
        new("EnterpriseDesktopAlert", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Screen-corner popup alert with auto-close timer, pin button & action links", "navigation-layout-alerts-suite"),
        new("ToastNotificationManager", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "OS Toast notifications with XML templates, action buttons & reply box", "navigation-forms-overlays-suite"),
        new("NotifyIconControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "System tray icon with badge counter, popup flyout & themed context menu", "navigation-forms-overlays-suite"),
        new("CalloutControl", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Vector callout balloon with directional pointer arrow & target control anchor", "clock-callout-rangeselector-suite"),
        new("SplashScreenManager", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Startup splash screen with animated progress bar, logo & status text", "navigation-forms-overlays-suite"),
        new("OverlayManager", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Semi-transparent busy dimmer overlay over any host control or form", "navigation-forms-overlays-suite"),
        new("FlyoutManager", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Interactive modal/non-modal slide-in flyout dialog overlay", "navigation-forms-overlays-suite"),
        new("TaskbarButtonManager", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Windows taskbar progress state, badge icon overlay & jump list actions", "navigation-forms-overlays-suite"),

        // 10. Forms, Dialogs, Media & System Utilities (19)
        new("ThemedForm", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Custom non-client titlebar, border glow, DPI scaling & full theme token sync", "navigation-forms-overlays-suite"),
        new("RibbonForm", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Integrated titlebar QAT and Contextual Tab headers in form frame", "navigation-forms-overlays-suite"),
        new("TabbedForm", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Modern browser-style tabs in titlebar with New Tab (+), pin, close & drag", "navigation-forms-overlays-suite"),
        new("ToolbarForm", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Compact application window with menus, search & action buttons in titlebar", "navigation-forms-overlays-suite"),
        new("ShapedForm", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Custom vector polygon/rounded window geometry without standard OS chrome", "navigation-forms-overlays-suite"),
        new("TitleBarControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Draggable caption bar with minimize, maximize/restore, close & custom buttons", "navigation-forms-overlays-suite"),
        new("StatusStripControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Bottom application status bar with spring panels, progress & grip", "navigation-forms-overlays-suite"),
        new("ThemedMessageBox", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Themed replacement for MessageBox.Show with Details expander & copy support", "navigation-forms-overlays-suite"),
        new("EnterpriseTaskDialog", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Command links, verification checkbox, expandable footer, progress bar", "navigation-layout-alerts-suite"),
        new("ColorDialogControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "HSV color wheel, RGB/CMYK/HEX sliders, screen eyedropper & palette swatches", "editors-fluent-validation"),
        new("FileDialogsSuite", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Themed Explorer file/folder dialogs with breadcrumb, search, details/icons & preview", "navigation-forms-overlays-suite"),
        new("ClockControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Vector Analog & Digital clock with timezone offset & smooth second hand", "clock-callout-rangeselector-suite"),
        new("ImageEditorControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Crop, Resize, Rotate, Flip, Brightness/Contrast, Filters & Annotations", "slide-rotator-pips-media-suite"),
        new("PictureBoxControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Raster & SVG viewer with Zoom, Pan, Fit-to-View & mini toolbar", "slide-rotator-pips-media-suite"),
        new("EnterpriseWebCam / EnterpriseWebCamControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Camera feed, Snapshot capture, Video recording state & device selector", "slide-rotator-pips-media-suite"),
        new("EnterpriseVirtualKeyboardControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Touch/Kiosk on-screen QWERTY & Numpad keyboard with Shift/Caps & sound", "specialized-gantt-barcode-kanban"),
        new("ValidationProviderControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Declarative validation rules attachable to any control with error icons", "dataentry-filter-cards-suite"),
        new("ShortcutsManager", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Global and local keyboard chord/shortcut dispatcher (Ctrl+K, Ctrl+S)", "navigation-forms-overlays-suite"),
        new("PresentationToolsSuite", "Forms, Dialogs & Media", "/tools/controlspy/controlspy", "Runtime Visual Tree Spy, Vector Shape Editor, Theme Builder & HTML Markup Editor", "navigation-forms-overlays-suite")
    };
}

/// <summary>
/// Headless engine for SmartPasteButton: parses unstructured clipboard text into structured form fields.
/// </summary>
public static class SmartPasteEngine
{
    private static readonly char[] LineSeparators = { '\r', '\n' };
    public static Dictionary<string, string> ExtractFieldsFromText(string rawClipboardText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawClipboardText)) return result;

        var emailMatch = Regex.Match(rawClipboardText, @"[\w\.-]+@[\w\.-]+\.\w+");
        if (emailMatch.Success) result["Email"] = emailMatch.Value;

        var phoneMatch = Regex.Match(rawClipboardText, @"(\+?\d{1,2}[\s-]?)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}");
        if (phoneMatch.Success) result["Phone"] = phoneMatch.Value;

        var amountMatch = Regex.Match(rawClipboardText, @"\$?\d{1,3}(,\d{3})*(\.\d{2})?");
        if (amountMatch.Success) result["Amount"] = amountMatch.Value;

        var lines = rawClipboardText.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var kv = line.Split(':', 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var val = kv[1].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                {
                    result[key] = val;
                }
            }
        }

        if (!result.ContainsKey("CustomerName") && lines.Length > 0)
        {
            result["CustomerName"] = lines[0].Trim();
        }

        return result;
    }
}

