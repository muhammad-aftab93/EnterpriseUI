using System.Collections.Generic;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>
/// Reference text for every workshop page. A scenario's own text wins; these entries fill the remaining documentation sections
/// (architecture, performance, accessibility, troubleshooting, best practices) so every feature page is complete.
/// </summary>
internal static class GridScenarioDocs
{
    internal sealed record Entry(string Architecture, string Performance, string Accessibility, string Troubleshooting, string BestPractices);

    private static readonly Dictionary<string, Entry> Entries = new()
    {
        ["grid-basics"] = new(
            "EnterpriseDataGrid<T> is a thin GDI+ view over the headless DataGrid<T> engine. The engine owns data, query, selection and editing state; the control owns painting, input and UI Automation.",
            "Only the rows near the viewport are materialized. Row height is fixed, so the first visible row, the scroll range and hit testing are computed arithmetically.",
            "The control exposes a Table > Row > Cell UI Automation tree, a visible focus ring and a keyboard model (arrows, Home/End, Page keys, F2, Space, Ctrl+A/C/Z/Y/F).",
            "Columns that show nothing usually name a property that does not exist: Field is matched to a public property of T (case-insensitive, dotted paths allowed). Call RefreshDataAsync after replacing items outside the grid.",
            "Declare columns explicitly, format with FormatString, keep ItemHeight fixed, set AriaLabel and avoid mutating the bound list from background threads."),
        ["grid-paging"] = new(
            "Paging is applied after sorting, filtering and grouping. Client mode reads the whole result once; Server mode sends skip/take; Virtual and Infinite modes fetch page-aligned windows.",
            "Server and Virtual paging keep memory flat for sources that cannot be materialized. Client paging is the fastest to navigate but holds one query result.",
            "Page changes are announced through the live region. Ctrl+PageUp and Ctrl+PageDown change page; the pager buttons are drawn but are exposed through the grid's accessible tree and the keyboard shortcuts.",
            "If a page shows fewer rows than PageSize the filtered result is shorter; if the pager is missing check PagingMode (None and Virtual have no pager) and ShowPager.",
            "Use Server paging for remote data, keep PageSizeOptions short, and reset to the first page when the query changes (the grid does this automatically)."),
        ["grid-sorting"] = new(
            "Sorting is a list of SortDescriptor entries in the request. Columns with a CustomComparer sort in memory; others sort in the data source through the query engine.",
            "Sorting a million in-memory rows takes a fraction of a second on a development machine (see the performance report); repeated queries over unchanged data reuse the cached source.",
            "Sort state is part of the header's accessible name ('sorted ascending') and every change is announced.",
            "A header that does not sort has Sortable = false or the grid has EnableSorting = false. Custom order columns need a comparer that tolerates null values.",
            "Prefer data-source sorting for large or remote data, use CustomComparer only for business orders, and keep multi-column sorting to a few levels."),
        ["grid-filtering"] = new(
            "Filters are descriptors (FilterDescriptor, CompositeFilterDescriptor, CustomFilterDescriptor) evaluated by the query engine. Value-list, filter-row and search-panel filters are all expressed as descriptors, so they combine and serialize.",
            "In-memory filters compile to expression trees once per query. Distinct value lists are capped at 1,000 entries with search to keep the popup responsive.",
            "Filtered columns are announced as 'filtered', the status bar shows the matching count, and each filter change is announced ('2 filters applied').",
            "If a filter seems ignored check the column's FilterType and that the value type matches the property type (the engine converts strings but not arbitrary objects). Calculated columns filter through an in-memory predicate.",
            "Save common filters as presets, show the filter row for power users, and combine the search panel with column filters instead of building custom text boxes."),
        ["grid-grouping"] = new(
            "Groups are computed over the whole filtered result and flattened into a row list of group headers, data rows and optional footers. Expansion state is keyed by group path so it survives refreshes.",
            "Group the lowest-cardinality column first. Collapsed groups add one row each, so very large datasets stay responsive when groups start collapsed.",
            "Group rows announce level, expanded state and item count; Alt+Right and Alt+Left expand and collapse; the focus outline covers the whole group row.",
            "If groups never show totals add GroupSummaries entries; if a group shows (blank) the key value is null. Group footers need ShowGroupFooters.",
            "Limit grouping to two or three levels, set GroupsExpandedByDefault = false for big data, and use group summaries rather than computing totals in the UI."),
        ["grid-hierarchy"] = new(
            "Relations join child rows to parents on KeyField/ForeignField and nest through ChildRelations. Expanded relations contribute DetailHeader and DetailData rows to the same virtual row list, so selection, search and export see them.",
            "Children are indexed once per relation (a dictionary lookup per parent). Lazy relations load on first expansion and cache the result until the grid refreshes.",
            "Detail rows announce their level and relation title; the expander is reachable with Alt+Right and Alt+Left, and the header of each child table is a labelled row.",
            "A relation that shows no children usually has mismatched key types (int versus long) or the wrong field names; set ChildItemType so columns can be generated for empty tables.",
            "Provide ChildColumns for important child tables, use lazy loading for remote children, and keep nesting depth reasonable for readability."),
        ["grid-columns"] = new(
            "Column state lives on GridColumn and in ColumnLayoutProfile snapshots. The layout engine (GridColumnLayout) turns ordered visible columns into pixel slots in left, scrollable and right zones.",
            "Layout is recomputed per frame from the column list, which is cheap (linear in visible columns). Best fit samples at most a few hundred rows per column.",
            "Reordering, pinning and visibility changes keep the focus cell visible; header context menus are keyboard reachable (Menu key) and every column has an accessible header name.",
            "A profile restored on a grid with different columns returns warnings instead of failing; check them to see which saved columns no longer exist.",
            "Save one layout per user and grid id, store it as JSON, and apply it after columns are defined and before the first load."),
        ["grid-rows"] = new(
            "Structural edits (add, remove, move, transfer) operate on the in-memory list behind the grid and are recorded as undoable entries. MoveRows rebuilds the order in one step so a multi-row drag is one undo.",
            "Moving rows rewrites the list once; for very large lists prefer data-source specific operations.",
            "Every drag operation has a keyboard path: Delete removes, Ctrl+Z undoes, and the context menu offers the same commands. Moves are announced.",
            "Drag and drop needs AllowRowDrag on the source and AllowRowDrop on the target, a list source (IList<T>), and the same item type in both grids.",
            "Use RowStyleNeeded for state-based colors instead of custom painting, confirm destructive deletes in the UI, and keep a visible undo."),
        ["grid-editing"] = new(
            "Editing goes through DataGrid<T>.TryEditCell: convert, validate (column rules, then row rules), apply, record undo, raise events. The control only chooses an editor and shows the outcome.",
            "Validation runs per edited cell and per row; async rules run off the UI thread. Batch mode keeps a dictionary of pending changes instead of copying rows.",
            "Invalid input is announced assertively and exposed as 'error: message' in the cell's accessible name; the error marker is paired with text so it never relies on color alone.",
            "If an edit silently does nothing the column is ReadOnly, calculated, or EditMode is None. If an invalid value appears to be accepted, RejectInvalidEdits is false.",
            "Put rules on columns, use Batch mode for review-and-save flows, show validation text next to the grid, and keep undo available."),
        ["grid-selection"] = new(
            "GridSelectionModel tracks rows, cell ranges and columns by display position and implements the mouse and keyboard gestures. The engine maps display rows to items for SelectedItems.",
            "Selection stores row indexes and ranges, not item copies, so selecting a million rows is a range, not a million objects.",
            "Selection is exposed as selected state on rows and cells; the focus cell has its own ring; the checkbox column has an accessible header toggle for select all.",
            "Selection resets when the query or page changes because display positions change; keep identity with SelectedItems before changing the query.",
            "Use CheckboxRow for bulk actions, CellRange for spreadsheet-like use, and read SelectedItems instead of indexes in application code."),
        ["grid-exporting"] = new(
            "GridExporter builds an ExportDocument (sheets, columns, bands, rows, summaries) from the grid and ExportService streams it through an IExportProvider. Providers: csv, json, xml, html, xlsx, excelxml, pdf.",
            "Rows are streamed once; CSV and JSON use constant memory and xlsx writes worksheet XML directly. 300,000 rows export to CSV in under a second on a development machine.",
            "Progress and completion are announced by the demo; PDFs contain real text and the document title, and HTML output uses semantic table markup with header cells.",
            "If a file is missing the export failed or was cancelled (files are written atomically). For CSV opened in Excel keep CsvSanitizeFormulas on. Large PDFs paginate by page size.",
            "Export from the grid so the file matches what users see, set a title, cap rows for PDF, and report progress for long exports."),
        ["grid-printing"] = new(
            "PrintPaginator converts the export document into pages of geometry (points): scale, column slices, repeated headers, placed rows. The WinForms control renders those pages with GDI+ to PrintDocument.",
            "Pagination is linear in rows and happens before drawing, so preview of thousands of rows opens quickly. MaxRows caps very large documents.",
            "Printed output uses high-contrast black on white by default; the preview dialog is the standard Windows PrintPreviewDialog with its own accessibility support.",
            "If columns are cut off choose FitToWidth or let them continue on extra pages (ScaleMode None). If no printer is installed the page setup dialog reports it instead of failing.",
            "Use templates for consistent headers and footers, repeat header rows, prefer FitToWidth for wide grids, and test with the printer's real paper size."),
        ["grid-performance"] = new(
            "Rendering and data are decoupled: the engine returns windows, the control paints the visible rows. Queries run in the data layer (expression trees over IQueryable, cached per source).",
            "Measured on the development machine: bind 1,000,000 rows in tens of milliseconds, sort in about half a second, filter in tens of milliseconds, group in about a third of a second.",
            "Large grids stay keyboard operable because focus moves by display index and scrolls the focus cell into view without materializing other rows.",
            "If scrolling stutters check for expensive RowStyleNeeded handlers, very tall custom columns, or a data source that does work in property getters.",
            "Bind lists directly, avoid per-row work in events, keep row height fixed, and measure with the benchmark project before tuning."),
        ["grid-accessibility"] = new(
            "GridAccessibility builds screen reader text from engine state; the control's AccessibleObject tree maps rows and cells to it. GridPalette derives colors from theme tokens and corrects them to WCAG contrast limits.",
            "Accessible objects are created on demand for visible rows only, so the tree costs nothing for large grids.",
            "The grid supports keyboard-only use, announces sort/filter/page/validation changes, never uses color alone for state, and switches to Windows system colors in high contrast mode.",
            "If a screen reader reads only the grid name make sure the control has focus and the Windows UI Automation notification APIs are enabled; Narrator and NVDA both read rows and cells from the tree.",
            "Give every grid an AriaLabel, test with Narrator and keyboard only, run the contrast audit when adding themes, and keep error messages short and specific."),
        ["grid-theming"] = new(
            "Colors are tokens (#hex or rgba()) in the theme. GridPalette resolves them against the surface, then corrects text, indicators and selection to their contrast targets. The control converts the palette to Colors once per theme change.",
            "A theme change invalidates one cached palette object and repaints; no per-cell color lookups happen while painting.",
            "All built-in themes pass the grid contrast audit (text 4.5:1 minimum, non-text indicators 3:1). High contrast themes use the text color for borders and a solid primary for selection.",
            "If a custom theme looks wrong run GridAccessibility.Audit(theme); low-contrast tokens are reported with their ratio. Missing tokens fall back to neutral defaults.",
            "Define tokens, not colors, in custom themes; test light, dark and high contrast; do not hard-code colors in format rules unless the contrast is verified."),
        ["grid-realtime"] = new(
            "Items that implement INotifyPropertyChanged are observed while they are in the rendered window. Changes repaint or, when they can affect sorting, grouping, filtering or summaries, trigger a debounced re-query.",
            "Observers are limited to 2,000 rendered items, so streaming into huge sources does not create huge subscription sets. Repaints are coalesced by the message loop.",
            "Rapid updates do not spam screen readers: value changes are not announced individually, only query-level events (sort, filter, page) are.",
            "If values change but the grid does not repaint, the item type does not raise PropertyChanged (or ObserveItemChanges is false). For bulk external changes call NotifyDataChanged.",
            "Raise PropertyChanged on the UI thread or marshal first, batch bulk updates, and avoid sorting on a field that changes many times per second."),
        ["grid-large-data"] = new(
            "A custom IDataSource<T> receives a DataSourceRequest and returns a DataSourceResult. The engine versions requests so a slow older response never overwrites a newer query, and surfaces failures as LastError plus DataLoadFailed.",
            "Only the requested page is transferred. The grid asks for the visible window plus a small buffer and caches the last window.",
            "Loading and error states are real text ('Loading...', 'Data could not be loaded: ...') that screen readers can read, not only spinners or colors.",
            "If nothing loads check LastError; a source that throws leaves the grid in an error state until a refresh succeeds. Stale responses are dropped by design.",
            "Implement paging, sorting and filtering in the source, keep requests cancellable, and show errors with a retry."),
        ["grid-master-detail"] = new(
            "Master/detail is a relation whose children appear below the expanded parent as a labelled child table. Without a relation the parent's properties are shown in a summary row.",
            "Child lookups are dictionary based; lazy relations fetch only on first expansion and then cache.",
            "Expanded state is exposed on the parent row; the child table header is a labelled row; Alt+Right and Alt+Left expand and collapse.",
            "If expansion shows nothing the relation key types differ or the foreign field is misspelled; if it spins, a lazy loader is still running or failed (see LastError).",
            "Define child columns, load children lazily when they are remote, and expand only what users ask for."),
        ["grid-treegrid"] = new(
            "A self-reference relation turns flat rows into a tree. The engine builds the visible row list from parent keys, orders siblings by the active sort, keeps ancestors of filter matches, and detects cycles.",
            "The tree is built once per query in linear time; collapsed subtrees cost nothing to render.",
            "Tree rows announce their level and expanded state; Alt+Right and Alt+Left expand and collapse; the chevron is a click target but never the only way to expand.",
            "Rows with a missing parent appear at the root; cyclic parent chains are broken at the repeat instead of looping. Check ParentId values if nodes disappear.",
            "Use stable integer keys, index parent keys in your source, and expand lazily for large trees."),
        ["grid-enterprise"] = new(
            "The workbench combines engine features through public APIs only: query state, bands, summaries, validation, batch editing, layouts, export and printing all work together on one grid.",
            "Twenty thousand rows with paging, grouping, summaries and conditional formatting stay responsive because only the current page is rendered and queries run in the engine.",
            "Keyboard operable end to end (navigate, edit, save with Ctrl+S, search with Ctrl+F), announcements for query changes, errors in text, theme-derived colors.",
            "If Save changes reports errors, fix the cells marked with the error corner (hover for the message) or use Discard; validation blocks the commit by design.",
            "Persist layouts per user, validate on columns, review in batch mode, export from the grid and keep the generated Exportable Example as the starting point for your own screen.")
    };

    public static void Fill(GridScenario scenario, out string architecture, out string performance, out string accessibility, out string troubleshooting, out string bestPractices)
    {
        Entries.TryGetValue(scenario.Id, out var entry);
        architecture = Pick(scenario.Architecture, entry?.Architecture);
        performance = Pick(scenario.Performance, entry?.Performance);
        accessibility = Pick(scenario.Accessibility, entry?.Accessibility);
        troubleshooting = Pick(scenario.Troubleshooting, entry?.Troubleshooting);
        bestPractices = Pick(scenario.BestPractices, entry?.BestPractices);
    }

    private static string Pick(string own, string? fallback) => string.IsNullOrWhiteSpace(own) ? fallback ?? string.Empty : own;
}
