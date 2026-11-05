# 12. Grid modernization roadmap

Generated 2026-11-05 from the open items of the feature catalog, ordered by priority (P1 first) then complexity (S, M, L).

## Priority 2

- **Basics: DataTable / DataView binding** (Partial, M) - Property access is reflection on typed items; DataRow indexers are not resolved.
- **Basics: Localization of built-in texts** (Partial, M) - A localization provider hook for control strings is planned; number and date formatting follow CurrentCulture.
- **Filtering: Date and numeric range filter menus** (Partial, M) - Use FilterBy with IsBetween or the value list.
- **Rows: New-row template** (Partial, M) - No inline new-row UI.
- **Editing: Paste from clipboard** (Planned, M) - Copy is supported; paste is not.
- **Basics: Right-to-left layout** (Planned, L) - The control does not mirror layout for RightToLeft yet.
- **Filtering: Composite filter dialog / expression builder** (Partial, L) - Composite and expression filters can be built in code but there is no visual builder.
- **Rows: Auto row height / word wrap** (Planned, L) - Rows have a fixed height.
- **Exporting: PDF typography** (Partial, L) - Characters outside WinAnsi are not rendered faithfully.

## Priority 3

- **Summaries: Top summary rows** (Planned, S) - Summaries render at the bottom only.
- **Printing: Print selected rows only** (Planned, S) - Always prints the current query result.
- **Basics: Icon-set conditional formatting** (Partial, M) - FormatRuleType.IconSet is defined but the control does not draw icons yet.
- **Hierarchy: Tabbed child views** (Partial, M) - HierarchyMode.TabbedDetail is modelled but not drawn as tabs.
- **Rows: Pinned (frozen) rows** (Planned, M) - Only columns can be pinned.
- **Editing: Inline row editing** (Partial, M) - No all-cells-at-once row editor.
- **Editing: Spin, mask, color and browse editors** (Planned, M) - Only text, combo, date and checkbox editors.
- **Hierarchy: Child tables with full grid features** (Partial, L) - Child rows are read-only; they are not independent grids with their own sorting or editing.
- **Columns: Merged cells** (Planned, L) - No cell merging.
- **Columns: HTML and table view definitions** (Planned, L) - Only the single-line table view exists.
- **Exporting: Word (docx) and image export** (Planned, L) - Not built.

## Cross-cutting

- Localization provider for the control's built-in texts and a right-to-left layout pass (they belong together).
- Variable row heights (word wrap, auto height) which also unlocks merged cells.
- Visual composite filter builder on top of the existing filter descriptors.
- Embedded fonts in PDF export and a real-device pass with Narrator and NVDA for the UI Automation tree.
