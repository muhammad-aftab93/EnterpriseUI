# Button feature comparison: Telerik RadButton and EnterpriseUI

Scope: `RadButton` (Telerik UI for WinForms) against `EnterpriseButtonControl` and the shared `EnterpriseButtonBase` of EnterpriseUI.

## Sources and limits of this analysis

- Read online: the RadButton Overview, Getting Started, Structure, Design Time, Tooltips, Themes and Accessing and Customizing Elements pages.
- The Properties/Methods/Events, SVG, Commands and the other linked reference pages were not reachable (HTTP 404 from the documentation server). Those rows are marked **API knowledge** below: they come from the published RadButton API as known, not from a page that was re-read for this report. Verify them against the Telerik API reference before relying on a detail.
- Behavior of EnterpriseUI was verified by reading the code, by 85 new automated tests, by the demo verification run and by the TestDataGrid verification run. It was not verified with a screen reader, in real Windows high contrast mode, on a second monitor with a different scale, or on net48 at run time (net48 was compiled only).

## What RadButton documents (fetched pages)

| Topic | Telerik implementation |
|---|---|
| Overview | A themable replacement of the WinForms `Button`. Clicked with the mouse, Enter or Space while focused |
| Structure | Element tree: `FillPrimitive`, `ImageAndTextLayoutPanel` (`ImagePrimitive`, `TextPrimitive`), `BorderPrimitive` |
| Customizing elements | At design time with the Element Hierarchy Editor, at run time through `ButtonElement.TextElement.ForeColor`, `ButtonFillElement.BackColor`, `BorderElement.BoxStyle` and the four border colors |
| Themes | Styles live in themes edited with VisualStyleBuilder; applied through `ThemeName` or a `RadThemeManager` |
| Design time | Smart tag: New Theme Manager, Edit UI elements, Theme Name, Learning Center, Search |
| Tooltips | `ButtonElement.ToolTipText`, or the `ToolTipTextNeeded` event (higher priority); needs `ShowItemToolTips` (default true) |
| Getting started | Drop on the form, set `Text`, double-click for the `Click` handler; or create in code |

## Inventory and gap analysis

Complexity is the estimated effort to build the capability in EnterpriseUI (Low, Medium, High). Status is the state **before** this work, followed by the result.

| # | Capability | Telerik | EnterpriseUI equivalent | Business value | Complexity | Before | Now |
|---|---|---|---|---|---|---|---|
| 1 | Click by mouse, Enter, Space | Fetched | `OnClick`; key handling | Core | Low | Implemented (key auto-repeat fired repeated clicks) | Implemented, one click per key press |
| 2 | `PerformClick()` | API knowledge | `PerformClick()` | Automation, access keys | Low | Implemented | Implemented |
| 3 | `Text`, mnemonics | API knowledge | `Text`, `&` access key | Keyboard access | Low | Implemented (always on) | `UseMnemonic` switch added |
| 4 | `Image` | API knowledge | `Image` | Icons | Low | Implemented (stretched to a square) | Fitted keeping proportions, `ImageSize` |
| 5 | `ImageList`, `ImageIndex`, `ImageKey` | API knowledge | Same names | Migration from `Button` | Low | Missing | Implemented |
| 6 | SVG image (`RadSvgImage`) | API knowledge | `SvgImage` / `EnterpriseSvgImage` | Sharp icons at every DPI, theme tint | High | Missing | Implemented, monochrome only |
| 7 | `TextImageRelation` | API knowledge | `ContentLayout`, alias `TextImageRelation` | Layout control | Low | Partial (3 of 5 relations) | Implemented (5 of 5) |
| 8 | `ImageAlignment`, `TextAlignment` | API knowledge | Same names | Left aligned or corner buttons | Medium | Missing | Implemented |
| 9 | `DisplayStyle` | API knowledge | `DisplayStyle` | Icon-only buttons | Low | Missing | Implemented |
| 10 | `TextWrap` | API knowledge | `TextWrap` | Multi-line captions | Medium | Missing | Implemented |
| 11 | `AutoSize` | API knowledge | `AutoSize`, `AutoSizeMode`, `GetPreferredSize` | No hand-measured widths | Medium | Missing | Implemented |
| 12 | `ClickMode` | API knowledge | `ClickMode` | Press and hover actions | Low | Missing | Implemented |
| 13 | `Command`, `CommandParameter` | API knowledge | `ICommand` based | MVVM binding, enable state from model | Medium | Missing | Implemented |
| 14 | `DialogResult`, default button | API knowledge | `DialogResult`, `IButtonControl` | Dialogs | Low | Implemented | Implemented, `IsDefault` exposed |
| 15 | Tooltip text | Fetched | `ToolTipText`, `ShowToolTips` | Icon-only buttons | Low | Missing | Implemented |
| 16 | `ToolTipTextNeeded` | Fetched | `ToolTipTextNeeded` | Dynamic text | Low | Missing | Implemented |
| 17 | Pressed state (`IsPressed`) | API knowledge | `IsPressed` (public), `VisualState` | State aware code | Low | Protected | Public, with `VisualStateChanged` |
| 18 | Hover, pressed, disabled, focus visuals | Fetched (theme) | Animated tints, focus ring | Feedback | Medium | Implemented | Implemented, `AnimateStates`, `ShowFocusRing` |
| 19 | Element access (`TextElement`, `ButtonFillElement`, `BorderElement`) | Fetched | `FillColor`, `BorderColor`, `TextColor`, `CornerRadius` | Brand colors | Medium | Missing | Implemented as properties (intentional deviation, see below) |
| 20 | Custom painting | Via elements | `FacePainting`, `ContentPainting` | Special looks | Medium | Missing | Implemented |
| 21 | Themes (`ThemeName`, theme manager, VisualStyleBuilder) | Fetched | Global `ThemeManager`, `ControlColors` | Consistent look | High | Implemented globally | Implemented globally; per-control theme not supported (deviation) |
| 22 | Runtime theme switching | Fetched (`ThemeName`) | `ThemeManager.ApplyTheme` repaints all | Dark mode | Medium | Implemented | Implemented, tested over all themes |
| 23 | High contrast | API knowledge | System colors, thicker border | Accessibility | Medium | Implemented | Implemented, overrides ignored |
| 24 | High DPI | API knowledge | Logical to device conversion | Modern displays | Medium | Partial (border, radius, no preferred size) | Border and radius scale; preferred size scales |
| 25 | Right to left | API knowledge | `RightToLeft` mirrors layout | Localization | Medium | Missing | Implemented |
| 26 | Smart tag | Fetched | `ButtonActions` | Design time speed | Medium | Missing | Implemented (no theme manager entry, see below) |
| 27 | Element hierarchy editor | Fetched | None | Design time styling | High | Missing | Not applicable (no element tree) |
| 28 | Property categories and descriptions | API knowledge | Attributes on every member | Property grid | Low | Missing | Implemented |
| 29 | Toolbox integration | API knowledge | `[ToolboxItem]`, default event and property | Discoverability | Low | Partial | Implemented without a custom toolbox bitmap |
| 30 | Designer serialization | API knowledge | `[DefaultValue]`, converters, `InstanceDescriptor` for `SvgImage` | Round-trip | Medium | Partial | Implemented |
| 31 | Accessibility (MSAA/UIA) | API knowledge | `ButtonAccessibleObject` | Screen readers | Medium | Partial (raw `Text`, no shortcut, no default action) | Name, shortcut, value, state, default action |
| 32 | Localization | API knowledge | Localizable `Text`, `Image`, `ToolTipText` | Resx | Low | Partial | Implemented (no built-in strings exist) |
| 33 | Animation | Theme transitions | `ValueAnimator`, `AnimateStates` | Polish | Medium | Implemented | Implemented |
| 34 | Toggle with cancelable change (`ToggleStateChanging`) | API knowledge | `CheckStateChanging` | Validation | Low | Missing | Implemented |
| 35 | Repeat button | API knowledge | `EnterpriseRepeatButtonControl` | Spinners | Medium | Needs refactoring: separate base | Unchanged (see review) |
| 36 | Badge, variants | Not in RadButton | `BadgeText`, `BadgeCount`, `Variant` | Notifications | Low | Implemented | Implemented (EnterpriseUI extra) |

## Comparison matrix

| Capability | Telerik | EnterpriseUI | Status | Recommendation |
|---|---|---|---|---|
| Mouse, Enter, Space click | Yes | Yes | Implemented | Keep |
| `PerformClick` | Yes | Yes | Implemented | Keep |
| Image, image list | Yes | Yes | Implemented | Keep |
| SVG | Full SVG | Monochrome SVG subset | Partially Implemented | Add multi-color rendering only if demanded; see future improvements |
| Text and image relation, alignment | Yes | Yes | Implemented | Keep |
| `DisplayStyle`, `TextWrap`, `AutoSize` | Yes | Yes | Implemented | Keep |
| `ClickMode` | Yes | Yes | Implemented | Keep |
| `Command` | Yes | Yes | Implemented | Keep |
| Tooltips | Property and event | Property and event | Implemented | Keep |
| Per-control theme (`ThemeName`) | Yes | No (`ThemeOverride` declared but unused in WinForms) | Needs Refactoring | Honor `ThemeOverride` in `EnterpriseControlBase` or remove it |
| Element tree styling | Yes | Color properties and paint events | Not Applicable | Document as intentional |
| Theme manager component, VisualStyleBuilder | Yes | `ThemeManager` and the Theme Designer tool | Not Applicable | Keep |
| High DPI | Yes | Yes | Implemented | Verify on mixed-DPI monitors |
| High contrast | Yes | Yes | Implemented | Verify with a real high contrast theme |
| Right to left | Yes | Yes | Implemented | Underline and badge are not mirrored yet, see review |
| Smart tag | Yes | Yes | Implemented | Add a "Theme" entry when per-control themes exist |
| Accessibility | MSAA, UIA | MSAA through `ControlAccessibleObject` | Partially Implemented | Add UIA Invoke and Toggle providers |
| Toolbox glyph | Yes | No custom bitmap | Partially Implemented | Ship toolbox bitmaps with the package |
| Repeat button | Yes | Separate legacy class | Needs Refactoring | Derive from `EnterpriseButtonBase` |
| Toggle changing event | Yes | Yes | Implemented | Keep |

## Intentional deviations

1. **No element tree.** RadButton exposes `ButtonElement` with child elements. EnterpriseUI paints directly with a pure layout routine (see [ButtonArchitecture.md](ButtonArchitecture.md)). The customization points are `FillColor`, `BorderColor`, `TextColor`, `CornerRadius`, `ImageSize`, `FacePainting` and `ContentPainting`.
2. **Global themes.** Colors come from the active `ThemeManager` theme only. There is no `ThemeName` per control and no `RadThemeManager` component.
3. **Monochrome SVG.** `SvgImage` paints one color (the text color of the current state). Multi-color SVG documents lose their colors. This is what makes theme tinting and disabled dimming work.
4. **Bold text.** Button captions are drawn bold, as before.
5. **One alignment model.** For every `ContentLayout` except `Overlay`, `TextAlignment` positions the whole image and text block and `ImageAlignment` is ignored. This keeps the model simple; independent alignment of image and text is only available with `Overlay`.
6. **Command owns `Enabled`.** While a `Command` is set, `Enabled` follows `CanExecute`.
7. **Smart tag scope.** The smart tag edits common properties. It has no theme manager or element editor entries because those concepts do not exist.
