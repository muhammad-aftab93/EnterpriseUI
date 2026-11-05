# 3. Missing features report

Everything that is not fully implemented, with the reason. Nothing here is hidden: this is the gap list that the roadmap works through.

| Area | Feature | Telerik RadGridView | EnterpriseUI DataGrid | Status | Priority | Complexity | Notes |
|---|---|---|---|---|---:|---|---|
| Basics | Icon-set conditional formatting | Icon sets | Rule type exists; not rendered | Partial | P3 | M | FormatRuleType.IconSet is defined but the control does not draw icons yet. |
| Basics | DataTable / DataView binding | DataTable, BindingSource | Not supported directly; wrap rows in typed objects | Partial | P2 | M | Property access is reflection on typed items; DataRow indexers are not resolved. |
| Basics | Localization of built-in texts | Localization provider | Texts are English strings in the control | Partial | P2 | M | A localization provider hook for control strings is planned; number and date formatting follow CurrentCulture. |
| Basics | Right-to-left layout | RightToLeft support | Not available | Planned | P2 | L | The control does not mirror layout for RightToLeft yet. |
| Basics | Touch gestures and inertial scrolling | Touch panning | Not available | OutOfScope | P3 | L | Mouse and keyboard only; WinForms touch maps to mouse messages. |
| Filtering | Composite filter dialog / expression builder | Composite filter dialog | Descriptors and FilterBuilder exist; no dialog UI | Partial | P2 | L | Composite and expression filters can be built in code but there is no visual builder. |
| Filtering | Date and numeric range filter menus | Date/number filter menus | Between operator exists; no dedicated menus | Partial | P2 | M | Use FilterBy with IsBetween or the value list. |
| Hierarchy | Tabbed child views | Multiple child templates as tabs | Several relations render as sequential blocks | Partial | P3 | M | HierarchyMode.TabbedDetail is modelled but not drawn as tabs. |
| Hierarchy | Child tables with full grid features | Child grids with own features | Child tables are rows of the parent grid | Partial | P3 | L | Child rows are read-only; they are not independent grids with their own sorting or editing. |
| Columns | Merged cells | Cell spanning | Not available | Planned | P3 | L | No cell merging. |
| Columns | HTML and table view definitions | HtmlView, TableView | Not available | Planned | P3 | L | Only the single-line table view exists. |
| Rows | New-row template | Add-new row at top/bottom | AddRow API and toolbar; no inline template row | Partial | P2 | M | No inline new-row UI. |
| Rows | Auto row height / word wrap | Auto-size rows | Not available | Planned | P2 | L | Rows have a fixed height. |
| Rows | Pinned (frozen) rows | Pinned rows | Not available | Planned | P3 | M | Only columns can be pinned. |
| Editing | Inline row editing | Row edit mode | Behaves like in-cell editing with row validators | Partial | P3 | M | No all-cells-at-once row editor. |
| Editing | Spin, mask, color and browse editors | Specialized editors | Not available | Planned | P3 | M | Only text, combo, date and checkbox editors. |
| Editing | Paste from clipboard | Paste support | Not available | Planned | P2 | M | Copy is supported; paste is not. |
| Summaries | Top summary rows | Summary rows at top | Not available | Planned | P3 | S | Summaries render at the bottom only. |
| Exporting | PDF typography | PDF with embedded fonts | Standard Latin text, no embedded fonts | Partial | P2 | L | Characters outside WinAnsi are not rendered faithfully. |
| Exporting | Word (docx) and image export | Export to Word / image | Not available | Planned | P3 | L | Not built. |
| Printing | Print selected rows only | Print selection | Not available | Planned | P3 | S | Always prints the current query result. |
