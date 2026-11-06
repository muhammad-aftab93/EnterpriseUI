using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnterpriseUI.Grids;

public enum GridFeatureStatus
{
    /// <summary>Built, documented, demonstrated and covered by tests.</summary>
    Implemented,
    /// <summary>Works for the main scenarios; the notes list what is missing.</summary>
    Partial,
    /// <summary>Not built yet; on the roadmap.</summary>
    Planned,
    /// <summary>Deliberately out of scope for a GDI+ WinForms grid.</summary>
    OutOfScope
}
public sealed record GridFeature(
    string Area,
    string Name,
    string EnterpriseUIImplementation,
    GridFeatureStatus Status,
    int Priority,
    string Complexity,
    string? Demo,
    string? Docs,
    string? Tests,
    string? Notes = null);

/// <summary>
/// The honest, machine-readable DataGrid feature matrix. Reports and documentation are generated from it and a unit test verifies that
/// every Implemented feature points to a demo page, a documentation file and tests.
/// </summary>
public static class GridFeatureCatalog
{
    private const string Engine = "UnitTests/DataGridEngineTests";
    private const string Edit = "UnitTests/DataGridEditingAndSummaryTests";
    private const string Query = "UnitTests/QueryEngineAdvancedTests";
    private const string Export = "UnitTests/ExportingTests";
    private const string Print = "UnitTests/PrintingTests";
    private const string A11y = "AccessibilityTests/*; UnitTests/GridAccessibilityTests";
    private const string Ui = "UIAutomationTests/*";
    private const string Integration = "IntegrationTests/*";
    private const string Perf = "PerformanceTests/*";

    private static GridFeature I(string area, string name, string impl, int priority, string complexity, string demo, string docs, string tests, string? notes = null) =>
        new(area, name, impl, GridFeatureStatus.Implemented, priority, complexity, demo, docs, tests, notes);

    private static GridFeature P(string area, string name, string impl, int priority, string complexity, string? demo, string? docs, string? tests, string notes) =>
        new(area, name, impl, GridFeatureStatus.Partial, priority, complexity, demo, docs, tests, notes);

    private static GridFeature R(string area, string name, int priority, string complexity, string notes) =>
        new(area, name, "Not available", GridFeatureStatus.Planned, priority, complexity, null, null, null, notes);

    private static GridFeature O(string area, string name, string notes) =>
        new(area, name, "Not available", GridFeatureStatus.OutOfScope, 3, "L", null, null, null, notes);

    public static IReadOnlyList<GridFeature> All { get; } = new List<GridFeature>
    {
        // ---- Basics and data binding
        I("Basics", "Typed columns bound to properties", "GridColumn.Field with dotted paths, format strings, alignment, tooltips", 1, "S", "grid-basics", "Basics", Engine),
        I("Basics", "Data sources: lists, collections, async sources", "IEnumerable<T>, IList<T>, IDataSource<T>, RemoteDataSource<T>, OData request builder", 1, "M", "grid-basics", "Basics", Engine + "; " + Integration),
        I("Basics", "Alternate rows, row height, status bar", "EnableAlternateRows, ItemHeight, status bar with counts", 2, "S", "grid-basics", "Basics", Ui),
        I("Basics", "Conditional formatting", "Highlight, data bar and color scale rules plus RowStyleNeeded", 2, "M", "grid-basics", "Basics", Engine),
        P("Basics", "Icon-set conditional formatting", "Rule type exists; not rendered", 3, "M", null, null, null, "FormatRuleType.IconSet is defined but the control does not draw icons yet."),
        P("Basics", "DataTable / DataView binding", "Not supported directly; wrap rows in typed objects", 2, "M", null, null, null, "Property access is reflection on typed items; DataRow indexers are not resolved."),
        P("Basics", "Localization of built-in texts", "Texts are English strings in the control", 2, "M", null, null, null, "A localization provider hook for control strings is planned; number and date formatting follow CurrentCulture."),
        R("Basics", "Right-to-left layout", 2, "L", "The control does not mirror layout for RightToLeft yet."),
        O("Basics", "Touch gestures and inertial scrolling", "Mouse and keyboard only; WinForms touch maps to mouse messages."),

        // ---- Paging
        I("Paging", "Client paging", "PagingMode.Client with pager bar, page size menu, first/prev/next/last", 1, "M", "grid-paging", "Paging", Engine + "; " + Ui),
        I("Paging", "Server paging", "PagingMode.Server sends skip/take to the data source and reads the total", 1, "M", "grid-paging", "Paging", Integration),
        I("Paging", "Virtual scrolling over paged sources", "PagingMode.Virtual with page-aligned window fetches", 1, "M", "grid-large-data", "Paging", Engine),
        I("Paging", "Infinite scroll / load more", "PagingMode.Infinite with Load more button and scroll-triggered fetch", 2, "M", "grid-large-data", "Paging", Engine),
        I("Paging", "Keyboard paging", "Ctrl+PageUp / Ctrl+PageDown and announcements", 2, "S", "grid-paging", "Paging", Ui),

        // ---- Sorting
        I("Sorting", "Single and multi-column sorting", "Tri-state header click, Ctrl+click for more levels, programmatic SortBy", 1, "S", "grid-sorting", "Sorting", Engine + "; " + Ui),
        I("Sorting", "Custom comparer", "GridColumn.CustomComparer", 2, "S", "grid-sorting", "Sorting", Engine),
        I("Sorting", "Sort indicators and indexes", "Arrow and level number in the header", 2, "S", "grid-sorting", "Sorting", Ui),

        // ---- Filtering
        I("Filtering", "Filter row", "Filter row with Contains, per column", 1, "S", "grid-filtering", "Filtering", Edit),
        I("Filtering", "Excel-style value list", "Distinct values with search, select all/none", 1, "M", "grid-filtering", "Filtering", Engine),
        I("Filtering", "Operators and between", "18 operators including between, in, empty and null", 1, "M", "grid-filtering", "Filtering", Query),
        I("Filtering", "Filter presets", "Save, apply, export and import presets as JSON", 2, "S", "grid-filtering", "Filtering", Engine),
        P("Filtering", "Composite filter dialog / expression builder", "Descriptors and FilterBuilder exist; no dialog UI", 2, "L", "grid-filtering", "Filtering", Query, "Composite and expression filters can be built in code but there is no visual builder."),
        P("Filtering", "Date and numeric range filter menus", "Between operator exists; no dedicated menus", 2, "M", "grid-filtering", "Filtering", Query, "Use FilterBy with IsBetween or the value list."),

        // ---- Searching
        I("Searching", "Search panel with highlight and match navigation", "Ctrl+F panel, filter or highlight mode, count, next/previous, F3", 1, "M", "grid-filtering", "Searching", Engine + "; " + Ui),
        I("Searching", "Incremental type-ahead", "Typing jumps to the first matching row in the focused column", 2, "S", "grid-selection", "Searching", Ui),
        I("Searching", "Search in hierarchy and expand to reveal", "Searches child tables and expands to matches", 2, "M", "grid-hierarchy", "Searching", Integration),

        // ---- Grouping
        I("Grouping", "Multi-level grouping and group panel", "Group panel with chips, drag header to group, header menu", 1, "M", "grid-grouping", "Grouping", Engine + "; " + Ui),
        I("Grouping", "Group summaries and footers", "Header aggregates and optional group footer rows", 1, "M", "grid-grouping", "Grouping", Engine),
        I("Grouping", "Expand/collapse, default state", "Expand all, collapse all, keyboard Alt+Right/Left", 1, "S", "grid-grouping", "Grouping", Ui + "; " + A11y),
        I("Grouping", "Custom group key", "GroupBy(field, keySelector)", 2, "S", "grid-grouping", "Grouping", Engine),

        // ---- Hierarchy
        I("Hierarchy", "Multi-level master/detail", "Nested relations without a depth limit", 1, "L", "grid-hierarchy", "Hierarchy", Engine + "; " + Integration),
        I("Hierarchy", "Lazy child loading", "Relation.LoadChildrenAsync with loading row", 1, "M", "grid-master-detail", "MasterDetail", Engine),
        I("Hierarchy", "Self-reference tree (TreeGrid)", "AddSelfReference with cycle detection and filter ancestors", 1, "L", "grid-treegrid", "TreeGrid", Engine + "; " + Integration),
        I("Hierarchy", "Property summary detail row", "Parent property summary when no relation exists", 3, "S", "grid-master-detail", "MasterDetail", Engine),
        P("Hierarchy", "Tabbed child views", "Several relations render as sequential blocks", 3, "M", "grid-master-detail", "MasterDetail", Engine, "HierarchyMode.TabbedDetail is modelled but not drawn as tabs."),
        P("Hierarchy", "Child tables with full grid features", "Child tables are rows of the parent grid", 3, "L", "grid-hierarchy", "Hierarchy", Engine, "Child rows are read-only; they are not independent grids with their own sorting or editing."),

        // ---- Column management
        I("Columns", "Reorder, resize, pin, hide", "Header drag, edge drag, pin left/right menu, chooser submenu", 1, "M", "grid-columns", "Columns", Engine + "; " + Ui),
        I("Columns", "Best fit and auto-size modes", "BestFitColumn / BestFitAllColumns, AutoSizeMode Fill or BestFit", 2, "S", "grid-columns", "Columns", Engine),
        I("Columns", "Stacked header bands", "Nested bands with unlimited depth", 2, "M", "grid-columns", "Columns", Engine),
        I("Columns", "Layout persistence", "JSON layouts with sorts, groups, filters; named layout store with file persistence", 1, "M", "grid-columns", "Columns", "UnitTests/GridLayoutPersistenceTests; " + Integration),
        I("Columns", "Column types", "Boolean, ComboBox, Date, Progress, Hyperlink and Command rendering", 1, "M", "grid-columns", "Columns", Ui),
        R("Columns", "Merged cells", 3, "L", "No cell merging."),
        R("Columns", "HTML and table view definitions", 3, "L", "Only the single-line table view exists."),

        // ---- Rows
        I("Rows", "Add, delete rows with undo", "AddRow, RemoveRows, Delete key, undo/redo", 1, "S", "grid-rows", "Rows", Engine + "; " + Ui),
        I("Rows", "Row drag and drop (reorder, cross-grid)", "AllowRowDrag/AllowRowDrop with MoveRows and TransferRows", 2, "L", "grid-rows", "Rows", Engine),
        I("Rows", "Row styling", "RowStyleNeeded event and format rules", 2, "S", "grid-rows", "Rows", Engine),
        P("Rows", "New-row template", "AddRow API and toolbar; no inline template row", 2, "M", "grid-rows", "Rows", Engine, "No inline new-row UI."),
        R("Rows", "Auto row height / word wrap", 2, "L", "Rows have a fixed height."),
        R("Rows", "Pinned (frozen) rows", 3, "M", "Only columns can be pinned."),

        // ---- Editing and validation
        I("Editing", "In-cell editing with typed editors", "Text, combo, date and checkbox editors; F2, Enter, double-click, Tab", 1, "M", "grid-editing", "Editing", Ui),
        I("Editing", "Popup / form editing", "Row edit dialog with atomic commit and one undo step", 2, "M", "grid-editing", "Editing", Edit),
        I("Editing", "Batch editing", "Pending changes, commit and rollback, dirty markers", 1, "M", "grid-editing", "Editing", Edit + "; " + Ui),
        I("Editing", "Validation rules and messages", "Required, range, length, regex, delegate and async rules; row validators; error marker, tooltip and text", 1, "M", "grid-editing", "Editing", Edit + "; " + A11y),
        I("Editing", "Undo and redo", "Undo stack with transactions", 2, "M", "grid-editing", "Editing", Edit),
        P("Editing", "Inline row editing", "Behaves like in-cell editing with row validators", 3, "M", "grid-editing", "Editing", Edit, "No all-cells-at-once row editor."),
        R("Editing", "Spin, mask, color and browse editors", 3, "M", "Only text, combo, date and checkbox editors."),
        R("Editing", "Paste from clipboard", 2, "M", "Copy is supported; paste is not."),

        // ---- Summaries
        I("Summaries", "Column footer summaries", "Footer rows with 11 aggregate functions and formats", 1, "M", "grid-editing", "Summaries", Edit),
        I("Summaries", "Group summaries", "Per-group aggregates in header and footer", 1, "M", "grid-grouping", "Summaries", Engine),
        I("Summaries", "Running totals and calculated columns", "Expression columns and running totals", 2, "M", "grid-editing", "Summaries", Edit),
        R("Summaries", "Top summary rows", 3, "S", "Summaries render at the bottom only."),

        // ---- Exporting
        I("Exporting", "Excel workbook (xlsx), CSV, PDF, HTML, JSON, XML", "Streaming providers; atomic file writes; formulas sanitized for CSV", 1, "L", "grid-exporting", "Exporting", Export + "; " + Integration),
        I("Exporting", "Groups, hierarchy, summaries, formatting, bands", "Outline levels, child rows or sheets, footers, conditional colors, stacked headers", 1, "M", "grid-exporting", "Exporting", Export),
        I("Exporting", "Progress and cancellation", "IProgress<ExportProgress> and CancellationToken; dialog with cancel", 2, "S", "grid-exporting", "Exporting", Export + "; " + Integration),
        P("Exporting", "PDF typography", "Standard Latin text, no embedded fonts", 2, "L", "grid-exporting", "Exporting", Export, "Characters outside WinAnsi are not rendered faithfully."),
        R("Exporting", "Word (docx) and image export", 3, "L", "Not built."),

        // ---- Printing
        I("Printing", "Print preview and page setup", "PrintPreviewDialog and PageSetupDialog over paginated geometry", 1, "L", "grid-printing", "Printing", Print),
        I("Printing", "Scaling, repeated headers, column splitting, page order", "Fit to width/page, percent, header repeat, down-then-over or over-then-down", 1, "M", "grid-printing", "Printing", Print),
        I("Printing", "Header/footer tokens and templates", "{title} {date} {page} {pages} tokens and named templates", 2, "S", "grid-printing", "Printing", Print),
        R("Printing", "Print selected rows only", 3, "S", "Always prints the current query result."),

        // ---- Selection
        I("Selection", "Row, checkbox, cell, range, column selection", "Seven modes with mouse, keyboard and drag gestures", 1, "M", "grid-selection", "Selection", Engine + "; " + Ui),
        I("Selection", "Keyboard navigation and focus cell", "Arrows, Home/End, PageUp/Down, Ctrl variants, Tab in editing", 1, "M", "grid-selection", "Selection", Ui + "; " + A11y),
        I("Selection", "Clipboard copy", "Ctrl+C for rows, ranges and columns; copy modes", 2, "S", "grid-selection", "Selection", Ui),

        // ---- Performance
        I("Performance", "Windowed rendering over very large data", "Only the viewport rows are materialized; fixed row height", 1, "L", "grid-performance", "Performance", Perf),
        I("Performance", "Remote data with stale-request protection", "Versioned loads cancel stale responses; error state and retry", 1, "M", "grid-large-data", "LargeData", Integration),
        I("Performance", "Real-time updates", "INotifyPropertyChanged tracking for the visible window with debounced re-query", 2, "M", "grid-realtime", "RealTime", Ui),
        I("Performance", "Benchmarks", "benchmarks/EnterpriseUI.Benchmarks measures memory, render, scroll, filter, sort, grouping, export and print", 2, "S", "grid-performance", "Performance", Perf),

        // ---- Accessibility
        I("Accessibility", "UI Automation tree", "Table > Row > Cell objects with names, values, states and default actions", 1, "L", "grid-accessibility", "Accessibility", A11y),
        I("Accessibility", "Announcements", "Sort, filter, page, selection moves and errors via automation notifications", 1, "M", "grid-accessibility", "Accessibility", A11y),
        I("Accessibility", "Contrast-corrected palette and high contrast", "Theme-derived colors audited for WCAG; system colors in Windows high contrast", 1, "M", "grid-accessibility", "Accessibility", A11y),
        I("Accessibility", "Keyboard-only operation", "Every action has a key path; documented key map", 1, "M", "grid-accessibility", "Accessibility", A11y),

        // ---- Themes
        I("Themes", "Fluent, Office, Material, Bootstrap, Windows, Modern; light and dark", "All grid colors come from theme tokens", 1, "M", "grid-theming", "Theming", A11y + "; UnitTests/ThemingTests"),
        I("Themes", "Live theme switching", "Palette cache invalidated and repaint on ThemeChanged", 1, "S", "grid-theming", "Theming", Ui),

        // ---- Enterprise and tooling
        I("Enterprise", "Fluent configuration API", "GridBuilder<T>", 2, "S", "grid-enterprise", "Enterprise", Ui),
        I("Enterprise", "Code generation from live state", "C#, Designer, Fluent, JSON, XML and a complete exportable example (compiled in CI-style validation)", 2, "M", "grid-enterprise", "Enterprise", Ui),
    };

    public static IEnumerable<string> Areas => All.Select(a => a.Area).Distinct();

    public static IEnumerable<GridFeature> ForArea(string area) => All.Where(f => string.Equals(f.Area, area, StringComparison.OrdinalIgnoreCase));

    public static int Count(GridFeatureStatus status) => All.Count(f => f.Status == status);

    /// <summary>Share of in-scope features that are fully implemented (partial counts as half).</summary>
    public static double Coverage =>
        All.Where(f => f.Status != GridFeatureStatus.OutOfScope).Select(f => f.Status switch { GridFeatureStatus.Implemented => 1.0, GridFeatureStatus.Partial => 0.5, _ => 0.0 }).Average();

    public static string ToMarkdown(Func<GridFeature, bool>? filter = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Area | Feature  | EnterpriseUI DataGrid | Status | Priority | Complexity | Notes |");
        sb.AppendLine("|---|---|---|---|---:|---|---|");
        foreach (var f in filter == null ? All : All.Where(filter))
        {
            sb.AppendLine($"| {f.Area} | {f.Name} | {f.EnterpriseUIImplementation} | {f.Status} | P{f.Priority} | {f.Complexity} | {f.Notes} |");
        }
        return sb.ToString();
    }
}
