# Runtime Theme Engine Architecture

Assessment: 2026-11-05. Evidence-based baseline, not a parity certification.

## Current flow

Validated document -> resolve snapshot -> register/apply -> ThemeChanged -> existing control hooks. Reload parses fully before replacing the active theme; invalid reload retains last-known-good appearance.

## Ownership

Applications must call apply/reload on their UI context. Controls must unsubscribe on disposal and resolve appearance through platform-specific typed adapters. The current manager is not a concurrent multi-UI-thread service.

## Remaining runtime work

Debounced file watching; ordered UI-thread notifications; OS accent/light/dark/high-contrast precedence; per-window and per-control scopes; safe font/GDI resource ownership; layout invalidation on typography/density changes; latency and leak tests.
