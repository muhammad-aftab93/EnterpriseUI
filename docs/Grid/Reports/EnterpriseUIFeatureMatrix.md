# 2. EnterpriseUI DataGrid feature matrix

Generated 2026-11-05 from `GridFeatureCatalog`. Statuses are literal: Implemented means built, documented, demonstrated and tested; Partial lists what is missing.

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

| Area | Feature | Telerik RadGridView | EnterpriseUI DataGrid | Status | Priority | Complexity | Notes |
|---|---|---|---|---|---:|---|---|
| Basics | Typed columns bound to properties | GridViewDataColumn per property | GridColumn.Field with dotted paths, format strings, alignment, tooltips | Implemented | P1 | S |  |
| Basics | Data sources: lists, collections, async sources | BindingList, DataTable, async binding | IEnumerable<T>, IList<T>, IDataSource<T>, RemoteDataSource<T>, OData request builder | Implemented | P1 | M |  |
| Basics | Alternate rows, row height, status bar | Alternating row style, row height | EnableAlternateRows, ItemHeight, status bar with counts | Implemented | P2 | S |  |
| Basics | Conditional formatting | FormatRules, ConditionalFormattingObject | Highlight, data bar and color scale rules plus RowStyleNeeded | Implemented | P2 | M |  |
| Basics | Icon-set conditional formatting | Icon sets | Rule type exists; not rendered | Partial | P3 | M | FormatRuleType.IconSet is defined but the control does not draw icons yet. |
| Basics | DataTable / DataView binding | DataTable, BindingSource | Not supported directly; wrap rows in typed objects | Partial | P2 | M | Property access is reflection on typed items; DataRow indexers are not resolved. |
| Basics | Localization of built-in texts | Localization provider | Texts are English strings in the control | Partial | P2 | M | A localization provider hook for control strings is planned; number and date formatting follow CurrentCulture. |
| Basics | Right-to-left layout | RightToLeft support | Not available | Planned | P2 | L | The control does not mirror layout for RightToLeft yet. |
| Basics | Touch gestures and inertial scrolling | Touch panning | Not available | OutOfScope | P3 | L | Mouse and keyboard only; WinForms touch maps to mouse messages. |
| Paging | Client paging | RadDataPager over bound data | PagingMode.Client with pager bar, page size menu, first/prev/next/last | Implemented | P1 | M |  |
| Paging | Server paging | Custom paging with virtual mode | PagingMode.Server sends skip/take to the data source and reads the total | Implemented | P1 | M |  |
| Paging | Virtual scrolling over paged sources | Virtual mode | PagingMode.Virtual with page-aligned window fetches | Implemented | P1 | M |  |
| Paging | Infinite scroll / load more | Not built in | PagingMode.Infinite with Load more button and scroll-triggered fetch | Implemented | P2 | M |  |
| Paging | Keyboard paging | Pager keyboard | Ctrl+PageUp / Ctrl+PageDown and announcements | Implemented | P2 | S |  |
| Sorting | Single and multi-column sorting | SortDescriptors, AllowSort | Tri-state header click, Ctrl+click for more levels, programmatic SortBy | Implemented | P1 | S |  |
| Sorting | Custom comparer | Custom sorting event | GridColumn.CustomComparer | Implemented | P2 | S |  |
| Sorting | Sort indicators and indexes | Sort glyphs | Arrow and level number in the header | Implemented | P2 | S |  |
| Filtering | Filter row | Filter row cells | Filter row with Contains, per column | Implemented | P1 | S |  |
| Filtering | Excel-style value list | Excel-like filter popup | Distinct values with search, select all/none | Implemented | P1 | M |  |
| Filtering | Operators and between | Filter operators | 18 operators including between, in, empty and null | Implemented | P1 | M |  |
| Filtering | Filter presets | Saved filters | Save, apply, export and import presets as JSON | Implemented | P2 | S |  |
| Filtering | Composite filter dialog / expression builder | Composite filter dialog | Descriptors and FilterBuilder exist; no dialog UI | Partial | P2 | L | Composite and expression filters can be built in code but there is no visual builder. |
| Filtering | Date and numeric range filter menus | Date/number filter menus | Between operator exists; no dedicated menus | Partial | P2 | M | Use FilterBy with IsBetween or the value list. |
| Searching | Search panel with highlight and match navigation | Search row | Ctrl+F panel, filter or highlight mode, count, next/previous, F3 | Implemented | P1 | M |  |
| Searching | Incremental type-ahead | Incremental search | Typing jumps to the first matching row in the focused column | Implemented | P2 | S |  |
| Searching | Search in hierarchy and expand to reveal | Search in child views | Searches child tables and expands to matches | Implemented | P2 | M |  |
| Grouping | Multi-level grouping and group panel | Group panel, GroupDescriptors | Group panel with chips, drag header to group, header menu | Implemented | P1 | M |  |
| Grouping | Group summaries and footers | GroupSummaryItems | Header aggregates and optional group footer rows | Implemented | P1 | M |  |
| Grouping | Expand/collapse, default state | ExpandAll / CollapseAll | Expand all, collapse all, keyboard Alt+Right/Left | Implemented | P1 | S |  |
| Grouping | Custom group key | Custom grouping | GroupBy(field, keySelector) | Implemented | P2 | S |  |
| Hierarchy | Multi-level master/detail | Hierarchical grid, child templates | Nested relations without a depth limit | Implemented | P1 | L |  |
| Hierarchy | Lazy child loading | Load on demand | Relation.LoadChildrenAsync with loading row | Implemented | P1 | M |  |
| Hierarchy | Self-reference tree (TreeGrid) | Self-referencing hierarchy | AddSelfReference with cycle detection and filter ancestors | Implemented | P1 | L |  |
| Hierarchy | Property summary detail row | Row details | Parent property summary when no relation exists | Implemented | P3 | S |  |
| Hierarchy | Tabbed child views | Multiple child templates as tabs | Several relations render as sequential blocks | Partial | P3 | M | HierarchyMode.TabbedDetail is modelled but not drawn as tabs. |
| Hierarchy | Child tables with full grid features | Child grids with own features | Child tables are rows of the parent grid | Partial | P3 | L | Child rows are read-only; they are not independent grids with their own sorting or editing. |
| Columns | Reorder, resize, pin, hide | Reordering, resizing, pinning, column chooser | Header drag, edge drag, pin left/right menu, chooser submenu | Implemented | P1 | M |  |
| Columns | Best fit and auto-size modes | BestFitColumns | BestFitColumn / BestFitAllColumns, AutoSizeMode Fill or BestFit | Implemented | P2 | S |  |
| Columns | Stacked header bands | Column groups view | Nested bands with unlimited depth | Implemented | P2 | M |  |
| Columns | Layout persistence | Save/load layout | JSON layouts with sorts, groups, filters; named layout store with file persistence | Implemented | P1 | M |  |
| Columns | Column types | Checkbox, combo, date, progress, link, command columns | Boolean, ComboBox, Date, Progress, Hyperlink and Command rendering | Implemented | P1 | M |  |
| Columns | Merged cells | Cell spanning | Not available | Planned | P3 | L | No cell merging. |
| Columns | HTML and table view definitions | HtmlView, TableView | Not available | Planned | P3 | L | Only the single-line table view exists. |
| Rows | Add, delete rows with undo | AddNewRow, row deletion | AddRow, RemoveRows, Delete key, undo/redo | Implemented | P1 | S |  |
| Rows | Row drag and drop (reorder, cross-grid) | Row drag and drop service | AllowRowDrag/AllowRowDrop with MoveRows and TransferRows | Implemented | P2 | L |  |
| Rows | Row styling | ViewCellFormatting | RowStyleNeeded event and format rules | Implemented | P2 | S |  |
| Rows | New-row template | Add-new row at top/bottom | AddRow API and toolbar; no inline template row | Partial | P2 | M | No inline new-row UI. |
| Rows | Auto row height / word wrap | Auto-size rows | Not available | Planned | P2 | L | Rows have a fixed height. |
| Rows | Pinned (frozen) rows | Pinned rows | Not available | Planned | P3 | M | Only columns can be pinned. |
| Editing | In-cell editing with typed editors | Editors per column type | Text, combo, date and checkbox editors; F2, Enter, double-click, Tab | Implemented | P1 | M |  |
| Editing | Popup / form editing | Edit form | Row edit dialog with atomic commit and one undo step | Implemented | P2 | M |  |
| Editing | Batch editing | Batch edit | Pending changes, commit and rollback, dirty markers | Implemented | P1 | M |  |
| Editing | Validation rules and messages | CellValidating, RowValidating | Required, range, length, regex, delegate and async rules; row validators; error marker, tooltip and text | Implemented | P1 | M |  |
| Editing | Undo and redo | Not built in | Undo stack with transactions | Implemented | P2 | M |  |
| Editing | Inline row editing | Row edit mode | Behaves like in-cell editing with row validators | Partial | P3 | M | No all-cells-at-once row editor. |
| Editing | Spin, mask, color and browse editors | Specialized editors | Not available | Planned | P3 | M | Only text, combo, date and checkbox editors. |
| Editing | Paste from clipboard | Paste support | Not available | Planned | P2 | M | Copy is supported; paste is not. |
| Summaries | Column footer summaries | SummaryRowItems (bottom) | Footer rows with 11 aggregate functions and formats | Implemented | P1 | M |  |
| Summaries | Group summaries | Group summary items | Per-group aggregates in header and footer | Implemented | P1 | M |  |
| Summaries | Running totals and calculated columns | Calculated/unbound columns | Expression columns and running totals | Implemented | P2 | M |  |
| Summaries | Top summary rows | Summary rows at top | Not available | Planned | P3 | S | Summaries render at the bottom only. |
| Exporting | Excel workbook (xlsx), CSV, PDF, HTML, JSON, XML | ExportToExcelML, Spreadsheet, CSV, PDF, HTML | Streaming providers; atomic file writes; formulas sanitized for CSV | Implemented | P1 | L |  |
| Exporting | Groups, hierarchy, summaries, formatting, bands | Export options | Outline levels, child rows or sheets, footers, conditional colors, stacked headers | Implemented | P1 | M |  |
| Exporting | Progress and cancellation | Async export events | IProgress<ExportProgress> and CancellationToken; dialog with cancel | Implemented | P2 | S |  |
| Exporting | PDF typography | PDF with embedded fonts | Standard Latin text, no embedded fonts | Partial | P2 | L | Characters outside WinAnsi are not rendered faithfully. |
| Exporting | Word (docx) and image export | Export to Word / image | Not available | Planned | P3 | L | Not built. |
| Printing | Print preview and page setup | Print preview | PrintPreviewDialog and PageSetupDialog over paginated geometry | Implemented | P1 | L |  |
| Printing | Scaling, repeated headers, column splitting, page order | Print settings | Fit to width/page, percent, header repeat, down-then-over or over-then-down | Implemented | P1 | M |  |
| Printing | Header/footer tokens and templates | Header and footer | {title} {date} {page} {pages} tokens and named templates | Implemented | P2 | S |  |
| Printing | Print selected rows only | Print selection | Not available | Planned | P3 | S | Always prints the current query result. |
| Selection | Row, checkbox, cell, range, column selection | SelectionMode, MultiSelect | Seven modes with mouse, keyboard and drag gestures | Implemented | P1 | M |  |
| Selection | Keyboard navigation and focus cell | Keyboard navigation | Arrows, Home/End, PageUp/Down, Ctrl variants, Tab in editing | Implemented | P1 | M |  |
| Selection | Clipboard copy | Copy with headers | Ctrl+C for rows, ranges and columns; copy modes | Implemented | P2 | S |  |
| Performance | Windowed rendering over very large data | Virtualization | Only the viewport rows are materialized; fixed row height | Implemented | P1 | L |  |
| Performance | Remote data with stale-request protection | Async data operations | Versioned loads cancel stale responses; error state and retry | Implemented | P1 | M |  |
| Performance | Real-time updates | Property change notifications | INotifyPropertyChanged tracking for the visible window with debounced re-query | Implemented | P2 | M |  |
| Performance | Benchmarks | n/a | benchmarks/EnterpriseUI.Benchmarks measures memory, render, scroll, filter, sort, grouping, export and print | Implemented | P2 | S |  |
| Accessibility | UI Automation tree | Accessibility support | Table > Row > Cell objects with names, values, states and default actions | Implemented | P1 | L |  |
| Accessibility | Announcements | Live regions | Sort, filter, page, selection moves and errors via automation notifications | Implemented | P1 | M |  |
| Accessibility | Contrast-corrected palette and high contrast | High contrast | Theme-derived colors audited for WCAG; system colors in Windows high contrast | Implemented | P1 | M |  |
| Accessibility | Keyboard-only operation | Keyboard | Every action has a key path; documented key map | Implemented | P1 | M |  |
| Themes | Fluent, Office, Material, Bootstrap, Windows, Modern; light and dark | Theme library | All grid colors come from theme tokens | Implemented | P1 | M |  |
| Themes | Live theme switching | Theme change | Palette cache invalidated and repaint on ThemeChanged | Implemented | P1 | S |  |
| Enterprise | Fluent configuration API | n/a | GridBuilder<T> | Implemented | P2 | S |  |
| Enterprise | Code generation from live state | n/a | C#, Designer, Fluent, JSON, XML and a complete exportable example (compiled in CI-style validation) | Implemented | P2 | M |  |
