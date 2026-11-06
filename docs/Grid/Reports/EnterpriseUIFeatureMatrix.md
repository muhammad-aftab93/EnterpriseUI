# 2. EnterpriseUI DataGrid feature matrix

Generated 2026-11-06 from `GridFeatureCatalog`. Statuses are literal: Implemented means built, documented, demonstrated and tested; Partial lists what is missing.

Summary: 64 implemented, 10 partial, 10 planned, 1 out of scope. Weighted coverage of in-scope features (partial counts half): 82%.

| Area | Implemented | Partial | Planned | Out of scope |
|---|---:|---:|---:|---:|
| Basics | 4 | 3 | 1 | 1 |
| Paging | 5 | 0 | 0 | 0 |
| Sorting | 3 | 0 | 0 | 0 |
| Filtering | 4 | 2 | 0 | 0 |
| Searching | 3 | 0 | 0 | 0 |
| Grouping | 4 | 0 | 0 | 0 |
| Hierarchy | 4 | 2 | 0 | 0 |
| Columns | 5 | 0 | 2 | 0 |
| Rows | 3 | 1 | 2 | 0 |
| Editing | 5 | 1 | 2 | 0 |
| Summaries | 3 | 0 | 1 | 0 |
| Exporting | 3 | 1 | 1 | 0 |
| Printing | 3 | 0 | 1 | 0 |
| Selection | 3 | 0 | 0 | 0 |
| Performance | 4 | 0 | 0 | 0 |
| Accessibility | 4 | 0 | 0 | 0 |
| Themes | 2 | 0 | 0 | 0 |
| Enterprise | 2 | 0 | 0 | 0 |

## Full matrix

| Area | Feature  | EnterpriseUI DataGrid | Status | Priority | Complexity | Notes |
|---|---|---|---|---:|---|---|
| Basics | Typed columns bound to properties | GridColumn.Field with dotted paths, format strings, alignment, tooltips | Implemented | P1 | S |  |
| Basics | Data sources: lists, collections, async sources | IEnumerable<T>, IList<T>, IDataSource<T>, RemoteDataSource<T>, OData request builder | Implemented | P1 | M |  |
| Basics | Alternate rows, row height, status bar | EnableAlternateRows, ItemHeight, status bar with counts | Implemented | P2 | S |  |
| Basics | Conditional formatting | Highlight, data bar and color scale rules plus RowStyleNeeded | Implemented | P2 | M |  |
| Basics | Icon-set conditional formatting | Rule type exists; not rendered | Partial | P3 | M | FormatRuleType.IconSet is defined but the control does not draw icons yet. |
| Basics | DataTable / DataView binding | Not supported directly; wrap rows in typed objects | Partial | P2 | M | Property access is reflection on typed items; DataRow indexers are not resolved. |
| Basics | Localization of built-in texts | Texts are English strings in the control | Partial | P2 | M | A localization provider hook for control strings is planned; number and date formatting follow CurrentCulture. |
| Basics | Right-to-left layout | Not available | Planned | P2 | L | The control does not mirror layout for RightToLeft yet. |
| Basics | Touch gestures and inertial scrolling | Not available | OutOfScope | P3 | L | Mouse and keyboard only; WinForms touch maps to mouse messages. |
| Paging | Client paging | PagingMode.Client with pager bar, page size menu, first/prev/next/last | Implemented | P1 | M |  |
| Paging | Server paging | PagingMode.Server sends skip/take to the data source and reads the total | Implemented | P1 | M |  |
| Paging | Virtual scrolling over paged sources | PagingMode.Virtual with page-aligned window fetches | Implemented | P1 | M |  |
| Paging | Infinite scroll / load more | PagingMode.Infinite with Load more button and scroll-triggered fetch | Implemented | P2 | M |  |
| Paging | Keyboard paging | Ctrl+PageUp / Ctrl+PageDown and announcements | Implemented | P2 | S |  |
| Sorting | Single and multi-column sorting | Tri-state header click, Ctrl+click for more levels, programmatic SortBy | Implemented | P1 | S |  |
| Sorting | Custom comparer | GridColumn.CustomComparer | Implemented | P2 | S |  |
| Sorting | Sort indicators and indexes | Arrow and level number in the header | Implemented | P2 | S |  |
| Filtering | Filter row | Filter row with Contains, per column | Implemented | P1 | S |  |
| Filtering | Excel-style value list | Distinct values with search, select all/none | Implemented | P1 | M |  |
| Filtering | Operators and between | 18 operators including between, in, empty and null | Implemented | P1 | M |  |
| Filtering | Filter presets | Save, apply, export and import presets as JSON | Implemented | P2 | S |  |
| Filtering | Composite filter dialog / expression builder | Descriptors and FilterBuilder exist; no dialog UI | Partial | P2 | L | Composite and expression filters can be built in code but there is no visual builder. |
| Filtering | Date and numeric range filter menus | Between operator exists; no dedicated menus | Partial | P2 | M | Use FilterBy with IsBetween or the value list. |
| Searching | Search panel with highlight and match navigation | Ctrl+F panel, filter or highlight mode, count, next/previous, F3 | Implemented | P1 | M |  |
| Searching | Incremental type-ahead | Typing jumps to the first matching row in the focused column | Implemented | P2 | S |  |
| Searching | Search in hierarchy and expand to reveal | Searches child tables and expands to matches | Implemented | P2 | M |  |
| Grouping | Multi-level grouping and group panel | Group panel with chips, drag header to group, header menu | Implemented | P1 | M |  |
| Grouping | Group summaries and footers | Header aggregates and optional group footer rows | Implemented | P1 | M |  |
| Grouping | Expand/collapse, default state | Expand all, collapse all, keyboard Alt+Right/Left | Implemented | P1 | S |  |
| Grouping | Custom group key | GroupBy(field, keySelector) | Implemented | P2 | S |  |
| Hierarchy | Multi-level master/detail | Nested relations without a depth limit | Implemented | P1 | L |  |
| Hierarchy | Lazy child loading | Relation.LoadChildrenAsync with loading row | Implemented | P1 | M |  |
| Hierarchy | Self-reference tree (TreeGrid) | AddSelfReference with cycle detection and filter ancestors | Implemented | P1 | L |  |
| Hierarchy | Property summary detail row | Parent property summary when no relation exists | Implemented | P3 | S |  |
| Hierarchy | Tabbed child views | Several relations render as sequential blocks | Partial | P3 | M | HierarchyMode.TabbedDetail is modelled but not drawn as tabs. |
| Hierarchy | Child tables with full grid features | Child tables are rows of the parent grid | Partial | P3 | L | Child rows are read-only; they are not independent grids with their own sorting or editing. |
| Columns | Reorder, resize, pin, hide | Header drag, edge drag, pin left/right menu, chooser submenu | Implemented | P1 | M |  |
| Columns | Best fit and auto-size modes | BestFitColumn / BestFitAllColumns, AutoSizeMode Fill or BestFit | Implemented | P2 | S |  |
| Columns | Stacked header bands | Nested bands with unlimited depth | Implemented | P2 | M |  |
| Columns | Layout persistence | JSON layouts with sorts, groups, filters; named layout store with file persistence | Implemented | P1 | M |  |
| Columns | Column types | Boolean, ComboBox, Date, Progress, Hyperlink and Command rendering | Implemented | P1 | M |  |
| Columns | Merged cells | Not available | Planned | P3 | L | No cell merging. |
| Columns | HTML and table view definitions | Not available | Planned | P3 | L | Only the single-line table view exists. |
| Rows | Add, delete rows with undo | AddRow, RemoveRows, Delete key, undo/redo | Implemented | P1 | S |  |
| Rows | Row drag and drop (reorder, cross-grid) | AllowRowDrag/AllowRowDrop with MoveRows and TransferRows | Implemented | P2 | L |  |
| Rows | Row styling | RowStyleNeeded event and format rules | Implemented | P2 | S |  |
| Rows | New-row template | AddRow API and toolbar; no inline template row | Partial | P2 | M | No inline new-row UI. |
| Rows | Auto row height / word wrap | Not available | Planned | P2 | L | Rows have a fixed height. |
| Rows | Pinned (frozen) rows | Not available | Planned | P3 | M | Only columns can be pinned. |
| Editing | In-cell editing with typed editors | Text, combo, date and checkbox editors; F2, Enter, double-click, Tab | Implemented | P1 | M |  |
| Editing | Popup / form editing | Row edit dialog with atomic commit and one undo step | Implemented | P2 | M |  |
| Editing | Batch editing | Pending changes, commit and rollback, dirty markers | Implemented | P1 | M |  |
| Editing | Validation rules and messages | Required, range, length, regex, delegate and async rules; row validators; error marker, tooltip and text | Implemented | P1 | M |  |
| Editing | Undo and redo | Undo stack with transactions | Implemented | P2 | M |  |
| Editing | Inline row editing | Behaves like in-cell editing with row validators | Partial | P3 | M | No all-cells-at-once row editor. |
| Editing | Spin, mask, color and browse editors | Not available | Planned | P3 | M | Only text, combo, date and checkbox editors. |
| Editing | Paste from clipboard | Not available | Planned | P2 | M | Copy is supported; paste is not. |
| Summaries | Column footer summaries | Footer rows with 11 aggregate functions and formats | Implemented | P1 | M |  |
| Summaries | Group summaries | Per-group aggregates in header and footer | Implemented | P1 | M |  |
| Summaries | Running totals and calculated columns | Expression columns and running totals | Implemented | P2 | M |  |
| Summaries | Top summary rows | Not available | Planned | P3 | S | Summaries render at the bottom only. |
| Exporting | Excel workbook (xlsx), CSV, PDF, HTML, JSON, XML | Streaming providers; atomic file writes; formulas sanitized for CSV | Implemented | P1 | L |  |
| Exporting | Groups, hierarchy, summaries, formatting, bands | Outline levels, child rows or sheets, footers, conditional colors, stacked headers | Implemented | P1 | M |  |
| Exporting | Progress and cancellation | IProgress<ExportProgress> and CancellationToken; dialog with cancel | Implemented | P2 | S |  |
| Exporting | PDF typography | Standard Latin text, no embedded fonts | Partial | P2 | L | Characters outside WinAnsi are not rendered faithfully. |
| Exporting | Word (docx) and image export | Not available | Planned | P3 | L | Not built. |
| Printing | Print preview and page setup | PrintPreviewDialog and PageSetupDialog over paginated geometry | Implemented | P1 | L |  |
| Printing | Scaling, repeated headers, column splitting, page order | Fit to width/page, percent, header repeat, down-then-over or over-then-down | Implemented | P1 | M |  |
| Printing | Header/footer tokens and templates | {title} {date} {page} {pages} tokens and named templates | Implemented | P2 | S |  |
| Printing | Print selected rows only | Not available | Planned | P3 | S | Always prints the current query result. |
| Selection | Row, checkbox, cell, range, column selection | Seven modes with mouse, keyboard and drag gestures | Implemented | P1 | M |  |
| Selection | Keyboard navigation and focus cell | Arrows, Home/End, PageUp/Down, Ctrl variants, Tab in editing | Implemented | P1 | M |  |
| Selection | Clipboard copy | Ctrl+C for rows, ranges and columns; copy modes | Implemented | P2 | S |  |
| Performance | Windowed rendering over very large data | Only the viewport rows are materialized; fixed row height | Implemented | P1 | L |  |
| Performance | Remote data with stale-request protection | Versioned loads cancel stale responses; error state and retry | Implemented | P1 | M |  |
| Performance | Real-time updates | INotifyPropertyChanged tracking for the visible window with debounced re-query | Implemented | P2 | M |  |
| Performance | Benchmarks | benchmarks/EnterpriseUI.Benchmarks measures memory, render, scroll, filter, sort, grouping, export and print | Implemented | P2 | S |  |
| Accessibility | UI Automation tree | Table > Row > Cell objects with names, values, states and default actions | Implemented | P1 | L |  |
| Accessibility | Announcements | Sort, filter, page, selection moves and errors via automation notifications | Implemented | P1 | M |  |
| Accessibility | Contrast-corrected palette and high contrast | Theme-derived colors audited for WCAG; system colors in Windows high contrast | Implemented | P1 | M |  |
| Accessibility | Keyboard-only operation | Every action has a key path; documented key map | Implemented | P1 | M |  |
| Themes | Fluent, Office, Material, Bootstrap, Windows, Modern; light and dark | All grid colors come from theme tokens | Implemented | P1 | M |  |
| Themes | Live theme switching | Palette cache invalidated and repaint on ThemeChanged | Implemented | P1 | S |  |
| Enterprise | Fluent configuration API | GridBuilder<T> | Implemented | P2 | S |  |
| Enterprise | Code generation from live state | C#, Designer, Fluent, JSON, XML and a complete exportable example (compiled in CI-style validation) | Implemented | P2 | M |  |
