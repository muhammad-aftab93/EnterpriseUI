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
        // 1. AI & Conversational Controls (5)
        new("EnterpriseAIPromptControl", "AI & Smart Controls", "docs/Controls/InteractiveControls.md", "Prompt, Output, Commands and History views, streaming output, custom commands, retry, rating, token usage", "interactive-aiprompt"),
        new("EnterpriseInlineAIAssistant", "AI & Smart Controls", "docs/Controls/InteractiveControls.md", "Selection button or Ctrl+Space inside a text box: summarize, rewrite, expand, tone, free instruction, preview before apply", "interactive-inlineai"),
        new("EnterpriseSmartPasteButton", "AI & Smart Controls", "docs/Controls/InteractiveControls.md", "Clipboard text classified (address, labeled lines, JSON, table row, code) and mapped into bound fields with undo", "interactive-smartpaste"),
        new("EnterpriseSpeechToTextButton", "AI & Smart Controls", "docs/Controls/InteractiveControls.md", "Microphone toggle, streaming recognizer interface, live waveform, partial and final transcripts", "interactive-speechtotext"),
        new("EnterpriseChatControl", "AI & Smart Controls", "docs/Controls/InteractiveControls.md", "Grouped bubbles, avatars, timestamps, delivery state, attachments, quick replies, typing indicator", "interactive-chat"),

        // 2. Data Grids, Cards, Filter & List Controls (9)
        new("DataGrid<T> / EnterpriseDataGrid<T>", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "1M-Row Virtualization, Grouping, Multi-Sort, Filter Row, Master-Detail, Bands", "grid-virtualization-1m"),
        new("EnterpriseVirtualGrid / EnterpriseVirtualGridControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "CellValueNeeded / CellValuePushed on-demand cells, 1M+ rows, headers, inline editing, keyboard navigation", "data-virtualgrid"),
        new("EnterpriseTreeList<T> / EnterpriseTreeListControl", "Grids & Data", "docs/Controls/UnifiedControlsCatalog.md", "Self-referencing hierarchy, Tri-state checkboxes, Drag & Drop, Multi-column", "grid-treelist-hierarchy"),
        new("EnterprisePivotGridControl (PivotEngine)", "Grids & Data", "docs/Controls/DataManagementControls.md", "Cross-tab with aggregates, subtotals, grand totals, collapsible groups, frozen headers", "data-pivotgrid"),
        new("EnterpriseCardViewControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Wrapping virtualized cards, badges, group/sort/filter, check boxes, keyboard navigation", "data-cardview"),
        new("EnterpriseListViewControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Details, List and Tiles views, sortable resizable columns, groups, check boxes, multi-selection", "data-listview"),
        new("EnterpriseTreeViewControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Chevron expanders, tri-state cascading check boxes, lazy loading, guides, keyboard navigation", "data-treeview"),
        new("EnterpriseDataFilterControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Visual AND/OR/NOT expression builder, typed editors, descriptor and text output, dialog", "data-datafilter"),
        new("EnterpriseFilterViewControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Faceted checklists with live counts, range filters, clear all, filter descriptors", "data-filterview"),
        new("EnterpriseDataEntryControl", "Grids & Data", "docs/Controls/DataManagementControls.md", "Entity form generated from DataAnnotations, multi-column layout, validation, save/reset", "data-dataentry"),

        // 3. Buttons & Toggles (8)
        new("EnterpriseButtonControl", "Buttons & Switches", "docs/Controls/InteractiveControls.md", "Five variants, glyph or image, badge counter, access keys, DialogResult, animated hover, press and focus", "interactive-buttons"),
        new("CheckBoxControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Tri-state Checked/Unchecked/Indeterminate, custom glyphs", "editors-fluent-validation"),
        new("RadioButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Exclusive option group, vector radio glyph, WCAG focus ring", "editors-fluent-validation"),
        new("EnterpriseToggleButtonControl", "Buttons & Switches", "docs/Controls/InteractiveControls.md", "Two-state and three-state latch button, exclusive groups, checked styling", "interactive-buttons"),
        new("EnterpriseToggleSwitchControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Animated pill On/Off switch with custom state labels", "editors-fluent-validation"),
        new("EnterpriseDropDownButtonControl", "Buttons & Switches", "docs/Controls/InteractiveControls.md", "Whole-button menu with themed items, separators, glyphs and keyboard opening", "interactive-buttons"),
        new("EnterpriseSplitButtonControl", "Buttons & Switches", "docs/Controls/InteractiveControls.md", "Dual-zone primary action + dropdown arrow menu", "interactive-buttons"),
        new("EnterpriseRepeatButtonControl", "Buttons & Switches", "docs/Controls/UnifiedControlsCatalog.md", "Continuous fire on press-and-hold with configurable interval", "editors-fluent-validation"),

        // 4. Editors, Pickers & DropDown Lists (22)
        new("EnterpriseTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "NullText placeholder, Clear button (✕), Prefix glyph, Validation border", "editors-fluent-validation"),
        new("EnterpriseTextBoxControl / SyntaxTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Hardware-accelerated text surface with custom selection painting", "editors-fluent-validation"),
        new("EnterpriseButtonTextBoxControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "TextBox with embedded left/right command buttons", "editors-fluent-validation"),
        new("EnterpriseBrowseBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "File open, file save and folder path editor with an integrated browse dialog, start directory and PathSelected event", "editors-fluent-validation"),
        new("EnterpriseMaskedTextBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Phone, SSN, IP, Currency, Regex, and Standard masks", "editors-fluent-validation"),
        new("EnterpriseNumericTextBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Numeric spin editor with step, min/max, decimals and standard, currency, percentage or scientific format", "editors-fluent-validation"),
        new("EnterpriseItemSpinner", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "String list up/down editor with wrap-around, press-and-hold repeat, type-ahead and mouse wheel", "editors-fluent-validation"),
        new("EnterpriseAutoCompleteBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Tokenized multi-value chips with prefix/contains search", "editors-fluent-validation"),
        new("EnterprisePopupBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Editor whose resizable pop-up hosts any custom control that survives closing", "editors-fluent-validation"),
        new("EnterpriseCalculatorDropDown", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Dropdown arithmetic calculator with memory (MC/MR/MS/M+) & history", "component-catalog"),
        new("EnterpriseCalculatorPad", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Standalone calculator keypad with memory keys, keyboard input and expression evaluation shared with the calculator drop-down", "editors-fluent-validation"),
        new("ColorPicker / ColorBox", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "HEX/ARGB input, Web/System/Professional palettes, Screen eyedropper", "editors-fluent-validation"),
        new("EnterpriseDatePicker", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Date and time editor model with range, format and optional time part", "editors-fluent-validation"),
        new("EnterpriseDateOnlyPicker / EnterpriseDateOnlyPickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "DateOnly editor with typed entry, month grid pop-up, week numbers, range limits and keyboard navigation", "editors-fluent-validation"),
        new("EnterpriseTimePicker / EnterpriseTimePickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Edits the time part of a DateTime with hour, minute, second and AM/PM columns, 12h/24h format and minute step", "editors-fluent-validation"),
        new("EnterpriseTimeOnlyPicker / EnterpriseTimeOnlyPickerControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "TimeOnly editor with minute, second or millisecond precision, range limits and wrap-around stepping", "editors-fluent-validation"),
        new("EnterpriseTimeSpanPicker", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Days, Hours, Minutes, Seconds, Milliseconds duration picker", "editors-fluent-validation"),
        new("EnterpriseDropDownControl / ComboBox<T>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Themed single-choice drop-down with keyboard navigation and data-bound combo box model", "editors-fluent-validation"),
        new("EnterpriseListControl", "Editors & Pickers", "docs/Controls/DataManagementControls.md", "Virtualized list with DisplayMember/ValueMember, selection modes, filter, sort, type-ahead", "data-listcontrol"),
        new("EnterpriseCheckedDropDownList<TItem>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Multi-select checkable dropdown with summary token header", "editors-fluent-validation"),
        new("EnterpriseCheckedListBoxControl", "Editors & Pickers", "docs/Controls/DataManagementControls.md", "Check boxes, CheckOnClick, check all/uncheck all/invert, ItemCheck event, Space toggle", "data-checkedlistbox"),
        new("EnterpriseFontDropDownControl", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Installed font family selector rendered in live font preview", "editors-fluent-validation"),
        new("EnterpriseMultiColumnComboBox<TItem>", "Editors & Pickers", "docs/Controls/UnifiedControlsCatalog.md", "Embedded DataGrid dropdown with multi-column filtering", "editors-fluent-validation"),
        new("EnterprisePropertyGridControl", "Editors & Pickers", "docs/Controls/DataManagementControls.md", "Categorized/alphabetical object inspector, search, nested objects, typed inline editors, description pane", "data-propertygrid"),

        // 5. Data Visualization: Charts, Gauges, Maps & Diagrams (13)
        new("CartesianChart / PieChart / CandlestickChart", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Bar, Line, Spline, Area, Pie, Donut, Candlestick, Radar, Funnel, DrillDown", "charts-financial-realtime"),
        new("EnterpriseSparkline / EnterpriseSparklineControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Line, area, column and win/loss sparkline with high, low, first and last markers and hover tooltips", "navigation-containers-suite"),
        new("EnterpriseRangeSelectorControl", "Charts, Gauges & Maps", "docs/Controls/UnifiedControlsCatalog.md", "Dual-thumb zoom/pan range selector linked to ChartView", "clock-callout-rangeselector-suite"),
        new("EnterpriseHeatMap / EnterpriseHeatMapControl", "Charts, Gauges & Maps", "docs/Controls/DataManagementControls.md", "2D color-scale matrix with legend, aggregation, header sorting, tooltips and selection", "data-heatmap"),
        new("EnterpriseTreeMap / EnterpriseTreeMapControl", "Charts, Gauges & Maps", "docs/Controls/DataManagementControls.md", "Squarified tiles, group headers, color by category or value, drill-down breadcrumb, tooltips", "data-treemap"),
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
        new("EnterpriseRibbonBar / EnterpriseRibbonBarControl", "Navigation, Layout & Docking", "docs/Controls/FormsAndDialogs.md", "Tabs, contextual tab groups, quick access toolbar, grouped large and small commands, collapsible body", "navigation-forms-overlays-suite"),
        new("EnterpriseDockManager / EnterpriseDockHost", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Tool windows on four sides, tabbed document area, splitters, auto-hide strips, floating windows, panel menu, JSON layout save and restore", "navigation-containers-suite"),
        new("EnterprisePageView / EnterprisePageViewControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Tab strip, stack, Outlook bar, accordion, backstage list or navigation pane over the same pages, keyboard navigation", "navigation-containers-suite"),
        new("EnterpriseNavigationPane", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Collapsible icon rail, sections, nested groups, badges, pinned footer items, fly-outs, auto-compact breakpoint, keyboard and screen reader support", "navigation-containers-suite"),
        new("EnterpriseCompactNavBar", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Labelled destination buttons that collapse to icons and an overflow menu, horizontal or vertical, badges", "navigation-containers-suite"),
        new("EnterpriseBreadcrumb / EnterpriseBreadcrumbControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Clickable path segments, chevron separators, overflow menu for leading segments, keyboard navigation, truncate on click", "navigation-containers-suite"),
        new("EnterpriseBindingNavigator<TItem> / EnterpriseBindingNavigatorControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "First, previous, position, next, last, add and delete over a BindingSource, cancelable add and delete events, keyboard shortcuts", "navigation-containers-suite"),
        new("EnterpriseCommandBar / EnterpriseCommandBarControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Multi-row themed tool strips, glyph icons, overflow chevron, end-user show/hide of commands", "navigation-containers-suite"),
        new("EnterpriseBackstageMenu", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Full-window application menu with page list, command entries, back button and Escape to close", "navigation-containers-suite"),
        new("StyleSheet / StyleResolver (EnterpriseUI.Framework)", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Retained element tree with type, id, class, state and attribute selectors, specificity cascade, inheritance and var() substitution", "navigation-containers-suite"),
        new("EnterprisePanorama / EnterprisePanoramaControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Live tiles in named groups, multi-frame tiles that cycle with a slide transition, horizontal scrolling, keyboard navigation", "navigation-containers-suite"),
        new("EnterpriseCarouselControl", "Navigation, Layout & Docking", "docs/Controls/InteractiveControls.md", "Flat, cover-flow and ring layouts, infinite loop, autoplay, swipe, custom item painting", "interactive-carousel"),
        new("EnterpriseRotatorControl", "Navigation, Layout & Docking", "docs/Controls/InteractiveControls.md", "Timed or manual panel ticker with fade, slide and flip transitions, HTML and Markdown content", "interactive-rotator"),
        new("SlideViewControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Interactive slide gallery with Prev/Next navigation & infinite loop", "slide-rotator-pips-media-suite"),
        new("PipsPagerControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Dot/Number pagination indicator with horizontal/vertical orientation", "slide-rotator-pips-media-suite"),
        new("EnterpriseWizardControl", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Welcome, Interior, Completion pages, Per-step validation, Aero/Wizard97", "navigation-layout-alerts-suite"),
        new("EnterpriseCustomizableLayout", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Grid of captioned controls with drag to reorder, corner resize, hide and restore, locked items, JSON save and restore", "navigation-containers-suite"),
        new("EnterpriseDataLayout<TEntity> / EnterpriseDataLayoutControl", "Navigation, Layout & Docking", "docs/Controls/DataManagementControls.md", "Grouped card layout of any controls, responsive columns, column span, caption left/top, collapsible groups", "data-datalayout"),
        new("EnterpriseSplitPanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Themed splitter with grip, collapse buttons, double-click toggle, minimum sizes and fixed panel", "navigation-containers-suite"),
        new("EnterpriseCollapsiblePanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Animated expand/collapse header container (Vertical & Horizontal)", "navigation-layout-alerts-suite"),
        new("EnterpriseGroupPanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Outlined, header band or underlined caption styles, caption alignment, DPI-aware layout", "navigation-containers-suite"),
        new("EnterpriseSurfacePanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Flat, outlined, filled or raised surface with corner radius and shadow depth", "navigation-containers-suite"),
        new("EnterpriseScrollPanel", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Vertical, horizontal or both scrolling with themed scroll bars, wheel step, scroll-into-view", "navigation-containers-suite"),
        new("EnterpriseLabelControl", "Navigation, Layout & Docking", "docs/Controls/InteractiveControls.md", "Auto-ellipsis, access keys, rotation, Markdown and HTML formatting with links, image alignment", "interactive-label"),
        new("EnterpriseDivider", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Horizontal or vertical rule, solid, dashed or fading, optional caption and inset", "navigation-containers-suite"),
        new("EnterpriseMenuBar / EnterpriseContextMenu", "Navigation, Layout & Docking", "docs/Controls/UnifiedControlsCatalog.md", "Themed menu bar and context menu, path based item creation, shortcuts, check marks, public EnterpriseMenuRenderer for any tool strip", "navigation-containers-suite"),

        // 8. Documents, RichText, Spreadsheet, PDF & Syntax (6)
        new("RichTextDocument / RichTextEditorControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Formatted paragraphs, tables, inline images, ruler, DOCX/RTF/HTML export", "documents-multiformat"),
        new("SpreadsheetDocument / EnterpriseSpreadsheetControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Worksheets, Cell A1 formulas (=SUM, =AVERAGE, =IF), Number formatting, CSV/XLSX", "documents-multiformat"),
        new("PdfDocumentGenerator / PdfViewerControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Page rendering, Thumbnails, Text search, Zoom/Rotate, PdfViewerNavigator", "documents-multiformat"),
        new("EnterpriseSyntaxEditor / EnterpriseSyntaxEditorControl", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Lexical AST tokenizer (C#, SQL, JSON, XML), Line numbers, Code folding", "component-catalog"),
        new("SpellChecker", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Levenshtein distance dictionary suggestions, Custom dictionary, As-you-type", "component-catalog"),
        new("DocumentProcessingSuite", "Documents & Code Editors", "docs/Controls/UnifiedControlsCatalog.md", "Headless spreadsheet formula helper and ZIP wrappers; full engines are listed under Document Processing", "documents-multiformat"),

        // 9. Track, Status, Alerts, Overlays & Notifications (14)
        new("EnterpriseProgressBarControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Determinate and indeterminate, gradient, striped and segmented fill, text overlay, states, animation speed", "interactive-progressbar"),
        new("EnterpriseStepProgressBarControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Completed, active and upcoming steps, validation markers, click navigation, horizontal and vertical", "interactive-stepprogress"),
        new("EnterpriseWaitingBarControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Spinning dots, ring, wave bars, bouncing blocks and marquee busy indicators with captions", "interactive-waitingbar"),
        new("EnterpriseStarRatingControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Full, half and free precision, hover preview and tooltip, read-only, custom symbols", "interactive-rating"),
        new("EnterpriseTrackBarControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Single and range sliders, discrete steps, ticks, value tooltip, keyboard control", "interactive-trackbar"),
        new("EnterpriseScrollBar", "Status, Alerts & Overlays", "docs/Controls/UnifiedControlsCatalog.md", "Themed horizontal/vertical scrollbar with thumb grip & smooth step", "navigation-layout-alerts-suite"),
        new("EnterpriseDesktopAlert / EnterpriseDesktopAlertWindow", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Screen-corner popup alert that never takes focus: auto-close countdown, pin and options buttons, action links, stacking", "navigation-layout-alerts-suite"),
        new("EnterpriseToastNotification", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Themed stacking toasts with actions and history, placement, auto-close and a link to the shared ToastService", "navigation-forms-overlays-suite"),
        new("EnterpriseNotifyIcon", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Tray icon with unread badge, stacked themed toasts, action buttons, notification history", "interactive-notifyicon"),
        new("EnterpriseCalloutControl", "Status, Alerts & Overlays", "docs/Controls/InteractiveControls.md", "Speech-bubble popup anchored to a control, arrow on any side, shadow, actions, close triggers", "interactive-callout"),
        new("EnterpriseSplashScreen", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Startup splash with logo, title, status text and progress; thread-safe updates, minimum duration and fade-out", "navigation-forms-overlays-suite"),
        new("EnterpriseOverlay", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Dimmer over any control or form that blocks input, with ring or dots busy indicator and caption", "navigation-forms-overlays-suite"),
        new("EnterpriseFlyout", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Panel that slides in from any edge, modal or non-modal, light dismiss, content host", "navigation-forms-overlays-suite"),
        new("EnterpriseTaskbarButton", "Status, Alerts & Overlays", "docs/Controls/FormsAndDialogs.md", "Windows taskbar progress state and value, badge icon overlay and thumbnail buttons", "navigation-forms-overlays-suite"),

        // 10. Forms, Dialogs, Media & System Utilities (19)
        new("EnterpriseForm", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Themed window with own caption bar, sizing frame, snap and maximize handling, rounded corners, DPI scaling and theme sync", "navigation-forms-overlays-suite"),
        new("EnterpriseRibbonForm", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Quick access buttons and tab headers with contextual tabs in the title bar, collapsible ribbon body below", "navigation-forms-overlays-suite"),
        new("EnterpriseTabbedForm", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Browser-style tabs in the title bar: new tab button, close, pin, drag to reorder and drag out into a new window", "navigation-forms-overlays-suite"),
        new("EnterpriseToolbarForm / EnterpriseToolbarStrip", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Compact window with menus, command buttons and a search box in the title bar, overflow menu", "navigation-forms-overlays-suite"),
        new("EnterpriseShapedForm", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Rounded, ellipse, polygon, vector path or image-mask window shape; drag by the empty body", "navigation-forms-overlays-suite"),
        new("EnterpriseTitleBarControl", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Draggable caption bar with icon, title, content area, command buttons and minimize, maximize or restore and close", "navigation-forms-overlays-suite"),
        new("EnterpriseStatusStrip", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Status bar with text, progress and link panels, spring panels and a size grip", "navigation-forms-overlays-suite"),
        new("EnterpriseMessageBox", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Theme-aware standard button sets and icons with expandable details and copy support", "navigation-forms-overlays-suite"),
        new("EnterpriseTaskDialog / EnterpriseTaskDialogForm", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Main instruction, command links, radio buttons, verification checkbox, expandable details, progress, footer, standard buttons and timer", "navigation-layout-alerts-suite"),
        new("EnterpriseAboutBox", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "About dialog filled from assembly attributes: logo, version, copyright, link, copyable system information", "navigation-forms-overlays-suite"),
        new("EnterpriseMarkupDialog", "Forms, Dialogs & Media", "docs/Controls/FormsAndDialogs.md", "Message dialog with Markdown or HTML-style markup body, links, standard buttons and icons", "navigation-forms-overlays-suite"),
        new("EnterpriseColorDialog", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "HSV, RGBA and HEX editing, screen eyedropper, preview and palette swatches", "editors-fluent-validation"),
        new("EnterpriseFileDialog", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Reusable open, save, multi-select and folder dialogs backed by native Explorer navigation", "navigation-forms-overlays-suite"),
        new("ClockControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Vector Analog & Digital clock with timezone offset & smooth second hand", "clock-callout-rangeselector-suite"),
        new("ImageEditorControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Crop, Resize, Rotate, Flip, Brightness/Contrast, Filters & Annotations", "slide-rotator-pips-media-suite"),
        new("EnterprisePictureBoxControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Raster viewer with actual-size, custom and fit zoom, panning, wheel zoom and mini toolbar", "slide-rotator-pips-media-suite"),
        new("EnterpriseWebCam / EnterpriseWebCamControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Camera feed, Snapshot capture, Video recording state & device selector", "slide-rotator-pips-media-suite"),
        new("EnterpriseVirtualKeyboardControl", "Forms, Dialogs & Media", "docs/Controls/InteractiveControls.md", "QWERTY, numeric, symbol and custom layouts, shift and caps lock, key repeat, types into a control or the focused window", "interactive-virtualkeyboard"),
        new("ValidationProviderControl", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Declarative validation rules attachable to any control with error icons", "dataentry-filter-cards-suite"),
        new("ShortcutsManager", "Forms, Dialogs & Media", "docs/Controls/UnifiedControlsCatalog.md", "Global and local keyboard chord/shortcut dispatcher (Ctrl+K, Ctrl+S)", "navigation-forms-overlays-suite"),
        new("EnterpriseControlSpy", "Developer Tools", "docs/Tools/DeveloperTools.md", "Inspect a live control tree and edit selected control properties", "developer-control-spy"),
        new("EnterpriseFormConverter", "Developer Tools", "docs/Tools/DeveloperTools.md", "Convert form types while preserving window settings and child controls", "developer-form-converter"),
        new("EnterpriseElementHierarchyEditor", "Developer Tools", "docs/Tools/DeveloperTools.md", "Navigate nested controls and edit their properties", "developer-element-hierarchy"),
        new("EnterpriseShapeEditor", "Developer Tools", "docs/Tools/DeveloperTools.md", "Create vector paths by adding, moving, removing, and curving vertices", "developer-shape-editor"),
        new("EnterpriseVisualStyleBuilder", "Developer Tools", "docs/Tools/DeveloperTools.md", "Edit inherited theme tokens and import or export theme documents", "developer-style-builder"),
        new("EnterpriseZipLibrary", "Document Processing", "docs/Tools/DeveloperTools.md", "Create, list, and extract stream-based ZIP archives with path traversal protection", "documents-zip-library"),
        new("EnterprisePdfEngine", "Document Processing", "docs/Controls/UnifiedControlsCatalog.md", "Write, read, merge and split text PDFs; classic cross-reference files with plain or Flate content streams", "documents-multiformat"),
        new("EnterpriseWordEngine", "Document Processing", "docs/Controls/UnifiedControlsCatalog.md", "Write and read .docx packages: headings, bullets, bold/italic/underline runs and tables", "documents-multiformat"),
        new("EnterpriseSheetEngine", "Document Processing", "docs/Controls/UnifiedControlsCatalog.md", "Read and write .xlsx workbooks with dates, formulas and cached results; expression evaluator with ranges and common functions", "documents-multiformat"),
        new("EnterpriseSheetStreamWriter / EnterpriseSheetStreamReader", "Document Processing", "docs/Controls/UnifiedControlsCatalog.md", "Forward-only .xlsx streaming for very large sheets with constant memory", "documents-multiformat")
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

