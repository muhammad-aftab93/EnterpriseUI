# Navigation, Menus, Containers and Docking

All controls below live in `EnterpriseUI.WinForms`, follow the active theme, scale with DPI, support the keyboard and expose accessible names, roles and states. The library builds for `net8.0-windows` and .NET Framework 4.8.

| Control | Purpose |
| :--- | :--- |
| `EnterpriseNavigationPane` | Side navigation with sections, nested groups, badges, pinned footer items, an icon rail (`DisplayMode` Expanded, Compact or Auto), fly-outs for groups in the rail, and command items (`IsCommand`) that never become the selection. |
| `EnterpriseCompactNavBar` | Labelled destination buttons that collapse to icons and an overflow menu, horizontal or vertical. |
| `EnterpriseBackstageMenu` | Full-window application menu: pages and commands on the left, the selected page on the right, Back and Escape close it. |
| `EnterpriseMenuBar`, `EnterpriseContextMenu`, `EnterpriseMenuRenderer` | Themed menus with path based creation (`AddItem("File/Export/PDF", ...)`). `EnterpriseMenuRenderer.Apply(toolStrip)` themes any tool strip or drop-down. |
| `EnterpriseCommandBarControl` | Stacked command rows with glyph icons, overflow and end-user show/hide of commands. Backed by the `EnterpriseCommandBar` model. |
| `EnterpriseBreadcrumbControl` | Clickable path with an overflow menu; `SetPath("School/Students")`. Backed by the `EnterpriseBreadcrumb` model. |
| `EnterpriseBindingNavigatorControl` | First, previous, position, next, last, add and delete over a `BindingSource`, with cancelable `ItemAdding` and `ItemDeleting`. |
| `EnterpriseDockHost`, `EnterpriseDockPanelControl` | Tool windows on four sides and a tabbed document area, draggable splitters, auto-hide strips, floating windows, a panel menu, `SaveLayout()` and `LoadLayout()` as JSON. |
| `EnterprisePageViewControl` | The same pages as a tab strip, stack, Outlook bar, accordion, backstage list or navigation pane (`ViewMode`). |
| `EnterprisePanoramaControl` | Horizontally scrolling live tiles in named groups; multi-frame tiles cycle with a slide transition. |
| `EnterpriseCustomizableLayout` | Grid of captioned controls; in customization mode items are dragged by their caption, resized from the corner, hidden and restored; layouts are saved as JSON. |
| `EnterpriseSplitPanel` | Splitter with grip, collapse buttons, double-click toggle and minimum sizes. |
| `EnterpriseSurfacePanel`, `EnterpriseGroupPanel`, `EnterpriseScrollPanel`, `EnterpriseDivider` | Flat, outlined, filled or raised surfaces; outlined, header or underlined group captions; scrolling container with light or dark scroll bars; horizontal or vertical rule with optional caption. |
| `EnterpriseSparklineControl` | Line, area, column and win/loss micro charts with high, low, first and last markers. |

## Style engine (`EnterpriseUI.Framework`)

`EnterpriseElement` is a retained tree (type name, id, classes, attributes, interaction state, children). `StyleSheet.Parse` reads CSS-like rules and `StyleResolver.Resolve(element)` returns the cascaded `ComputedStyle`:

- selectors: `Type`, `#id`, `.class`, `:hover`, `:focus`, `:pressed`, `:disabled`, `:selected`, `:checked`, `[attr]`, `[attr=value]`, descendant and `>` combinators;
- cascade order: `!important`, then specificity, then source order;
- inherited properties (`color`, `font-*`, `cursor`, `visibility`, `text-align`) and custom properties (`--name`), with `var(--name, fallback)`.

```csharp
var resolver = new StyleResolver();
resolver.AddSheet(StyleSheet.Parse("Window { --brand: #0a64ad; } Button { background: var(--brand); } Button:hover { background: #107c41; }"));
resolver.Watch(root);
var style = resolver.Resolve(button);
```

## Design time

Panels, the navigation pane, page view, divider, sparkline and customizable layout have smart tags (`EnterpriseUI.WinForms.Design`) for their main properties.

## Demonstrations

The Demo Center page **Navigation, Menus, Containers & Docking** shows each control once; `TestDataGrid` uses the navigation pane, breadcrumb, themed context menu and dashboard sparklines.
