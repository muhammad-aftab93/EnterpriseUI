# 11. Test coverage report

Results of the last full run (`dotnet test` for each project, solution builds with 0 errors).

| Project | Tests | Result | Scope |
|---|---:|---|---|
| UnitTests | 234 | all pass | query engine, grid engine (paging, rows, grouping, hierarchy, tree, selection, editing, validation, summaries, search), exporting providers and `GridExporter`, printing paginator, palette and accessibility text, layout persistence, feature catalog consistency |
| IntegrationTests | 12 | all pass | three-level hierarchy to xlsx, search in details, tree export levels, server paging requests, stale-request protection, failure and retry, every export provider well-formed, cancelled export leaves no file, grouped print plan, streamed export memory, layout round trip |
| UIAutomationTests (UI) | 23 | all pass | the WinForms control driven like a user: painting and theme switch, keyboard navigation, shift/ctrl selection, checkbox gutter, drag range selection, header sort, copy, type-ahead, pager click, group expand/collapse, F2 editing, invalid input keeps editor open, boolean toggle, batch commit/rollback, search panel, column resize, fluent builder, print plan and export from the control |
| AccessibilityTests | 17 | all pass | UI Automation tree (roles, names, states, default actions, bounds, hit test, focus), error text in accessible names, announcements, keyboard-only flow, theme-derived selection colors, high contrast border |
| PerformanceTests | 8 | all pass | million-row bind, window fetch, sort/filter/group budgets, repeated queries, export speed, frame time, scroll, memory |

Total: 294 tests.

## Coverage by requested test type

| Requested | Where |
|---|---|
| Unit | UnitTests |
| Integration | IntegrationTests |
| UI | UIAutomationTests |
| Accessibility | AccessibilityTests, `GridAccessibilityTests` (contrast audit for every built-in theme) |
| Performance | PerformanceTests, benchmarks project |
| Export | `ExportingTests`, `ExportPrintIntegrationTests` |
| Printing | `PrintingTests`, `ExportPrintIntegrationTests`, UI test for plan and export from the control |
| Hierarchy | `DataGridEngineTests` (relations, nested, tree, lazy), `HierarchyIntegrationTests` |

## Not covered by automated tests

- Real printing to a device and the Windows print preview dialog (the plan and geometry are tested, the dialog is not).
- Drag and drop between controls through the OS drag loop (`MoveRows` and `TransferRows` are tested in the engine).
- Real screen reader behavior (Narrator, NVDA); the UI Automation tree is tested, spoken output is not.
- Right-to-left layout (not implemented).
- Visual regression beyond pixel checks of selection, border and surface colors.
