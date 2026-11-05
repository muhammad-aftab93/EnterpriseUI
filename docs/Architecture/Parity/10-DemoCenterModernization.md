# Demo Center Modernization Plan

Assessment: 2026-11-05. Evidence-based baseline, not a parity certification.

## Existing owning application

Extend EnterpriseUI.WinFormsDemo, not the separate TestDataGrid sample. Keep dense work-focused navigation and inspectable controls.

## Explorer views

Control, Feature, Theme, API, Property, Event, Accessibility, Performance, Documentation, Architecture, Source and Learning explorers must share catalog IDs and evidence references.

## Live configuration

One typed runtime configuration drives the demo, C# source, Designer source, fluent API, JSON and theme configuration. Changes must update all views; compile and instantiate generated output in an isolated validation harness. Theme playground must edit tokens/colors/fonts/spacing/density and support import/export and source output.

## Layout gates

1920x1080 at 100%, 125%, 150%, 200% DPI; additional 1366x768 and 2560x1440 cases. Avoid unnecessary page scrolling; use overflow for toolbars and deliberate scrolling only for inherently scrollable data. Check text bounds, focus order and images.
