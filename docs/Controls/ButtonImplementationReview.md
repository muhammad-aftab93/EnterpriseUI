# Button implementation review

Review of the EnterpriseUI button family after the feature work described in [ButtonFeatureComparison.md](ButtonFeatureComparison.md). Scope: `EnterpriseButtonBase`, `EnterpriseButtonControl`, `EnterpriseToggleButtonControl`, `EnterpriseSplitButtonControl`, `EnterpriseDropDownButtonControl`, `EnterpriseSvgImage`, `ButtonLayoutEngine`, `ButtonAccessibleObject`, `ButtonActions`, and their use in the WinForms demo and in TestDataGrid.

## How this was verified

| Check | Result |
|---|---|
| Build of `EnterpriseUI.WinForms` for `net8.0-windows` and `net48` | Succeeds, 0 errors. `net48` was compiled only; no test run exists for it |
| 85 new xUnit tests (`ButtonFeatureTests.cs`: layout engine, SVG parser and markup, access keys, states, click modes, commands, tooltips, accessibility, toggles, designer metadata, painting, themes) | All pass |
| Whole UI automation suite (208 tests) | All pass when run serially (`-- xUnit.ParallelizeTestCollections=false`). In parallel runs a few tests that depend on real focus or the real cursor fail at random (seen: split button, label, rendering, focus ring); they pass on their own. This is a property of the existing test setup (many windows competing for the foreground), not of one test |
| Demo built-in verification (`--verify-demo`) | The new page `interactive-button-showcase` passes |
| TestDataGrid `--verify-school` | 126 checks pass, including new checks that every button has an accessible name, is reachable with Tab, keeps its DPI-scaled height, that auto-sized buttons fit their text and that a button clicks and exposes its access key |
| Not verified | Screen reader behavior, real Windows high contrast, a monitor change between different DPI values, right-to-left visual quality, the Visual Studio designer surface (only the attribute and smart tag logic is unit tested), Visual Studio toolbox appearance |

## Findings

### Code quality

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| C1 | `EnterpriseButtonBase` has about a thousand lines and several reasons to change: input, painting, sizing, commands, tooltips, accessibility hooks (single responsibility). The layout was already extracted into `ButtonLayoutEngine` | Medium | Extract a tooltip helper and a command binding helper; keep painting in the base |
| C2 | `EnterpriseRepeatButtonControl` does not derive from `EnterpriseButtonBase`. It does not get variants, commands, SVG, tooltips or the accessible object, and it rewrites its own `Text` ("Hold to Repeat (n)") | High | Derive from the base; make the caption the caller's business; add `Delay` and `Interval` |
| C3 | `FormChrome.PaintFace` is used only by the repeat button and duplicates `EnterpriseButtonBase.PaintFace` | Medium | Delete after C2 |
| C4 | `EnterpriseToggleButtonControl.OnPaint` and `EnterpriseSplitButtonControl.OnPaint` repeat the clear, face and focus steps of `PaintStandard`. The default and push buttons now use the shared path | Low | Express the toggle and split differences as `PaintFaceDecoration` and a content area hook |
| C5 | `EnterpriseControlBase` tracks hover (`_hovered`) and the button keeps its own animators; two sources of truth for "hot" | Low | Let the button read `IsHovered` only (already used for `VisualState`) |
| C6 | `ThemeOverride` is declared on `EnterpriseControlBase` and on about 20 other controls but nothing in the WinForms code reads it; only the Blazor components do | Medium | Honor it in `EnterpriseControlBase.Colors`, or remove the property |
| C7 | Dead or inconsistent: `IsSplitMode` setter does not repaint; `Cursor` is forced to hand for every button | Low | Invalidate in the setter; let the application own `Cursor` |
| C8 | Naming: `ContentLayout` and the alias `TextImageRelation` are two names for one thing (kept for compatibility and migration); `CheckState` instead of the reference suite's `ToggleState`; text is always bold | Low | Document, keep. Consider an obsolete notice on one of the two names in a major version |
| C9 | The icon pack data of `FluentIconPack.Add` was a stroke path (`M12 4v16m-8-8h16`) that fills to nothing. Found by the new "every registered icon renders" test | Fixed | Replaced by the filled Material "add" path |

### UI quality

| # | Finding | Recommendation |
|---|---|---|
| U1 | The focus ring width (`Chrome.FocusWidth = 2f`) is shared by all controls and not DPI scaled; the button border and corner radius now are | Scale `FocusWidth` in `Chrome` for every control |
| U2 | The access key underline is not drawn for wrapped text and in right-to-left mode | Draw it per line in `Mnemonic.DrawParsed` |
| U3 | In right-to-left mode the badge and the split arrow stay on the right | Mirror both |
| U4 | The badge ignores `Padding` and `ContentAlignment`; with `ImageAboveText` it moves to the top right corner, which can overlap long text | Reserve space in the layout engine instead of after it |
| U5 | The icon box is 18 logical pixels for images and SVG but follows the font for glyphs, so a glyph button and an SVG button have different icon sizes by default | Default the glyph box to the same logical size or document the difference |
| U6 | Custom colors are ignored in high contrast mode (on purpose) but the property grid gives no hint | Mention it in the property descriptions (done in the docs) and show a note in the smart tag |
| U7 | The hover tint blends with white; on dark themes that lightens, on light themes it can wash out the Outline variant | Blend toward the theme's `HoverBack` |
| U8 | The tooltip is placed under the control without checking the screen edge and uses a fixed 500 ms delay | Use `SystemInformation.MouseHoverTime` and flip above near the bottom edge |

### Performance

| # | Finding | Status |
|---|---|---|
| P1 | Parsing the access key and measuring the text happened on every paint | Fixed: cached per text and font; glyph size cached |
| P2 | The disabled-image color matrix and `ImageAttributes` were created on every paint | Fixed: one static instance (never disposed, by design) |
| P3 | `FacePainting` and `ContentPainting` arguments are only allocated when someone subscribed | By design |
| P4 | The tooltip timer and tooltip are created on first hover only | By design |
| P5 | Every button owns two animation timers (three for split buttons). Hundreds of buttons mean hundreds of timers | Open: share one animation clock |
| P6 | `SvgImage` strokes allocate a `Pen` per stroked shape per paint; fills do not allocate paths | Open: cache pens per color for stroked icons |
| P7 | `BadgeSize` measures text on each paint | Open: cache next to the text measurement |
| P8 | Event flooding: holding Enter or Space used to raise a `Click` for every keyboard repeat | Fixed: auto-repeat is ignored (read from the key message) |
| P9 | `ValueAnimator` invalidates on every 15 ms tick, repainting the whole button | Acceptable for one button; revisit together with P5 |

### Resource management

| # | Finding | Status |
|---|---|---|
| R1 | The button unsubscribes from `ThemeManager.ThemeChanged` (base class), from `ICommand.CanExecuteChanged` and from `ImageList.RecreateHandle` on dispose | Verified by code and a test for the command |
| R2 | Images obtained from an `ImageList` are copies; the cached copy is disposed when the list, index or key changes and on dispose | Verified |
| R3 | `GraphicsPath` objects inside `EnterpriseSvgImage` are not disposed explicitly (the type is immutable and shared) and rely on the finalizer | Accepted; make the type `IDisposable` only if icons are created in large numbers dynamically |
| R4 | The `ThemedToolTip` and its timer are disposed with the control | Verified by code |

### Accessibility

| # | Finding | Status |
|---|---|---|
| A1 | The accessible name was the raw text ("&Save"), there was no keyboard shortcut and no default action behavior | Fixed in `ButtonAccessibleObject` |
| A2 | Icon-only buttons without text or tooltip have no name | Mitigated: the tooltip is used as the name; a button with neither still has none. TestDataGrid now verifies that every button has a name |
| A3 | Only MSAA (through `ControlAccessibleObject`) is implemented. No UI Automation `Invoke` or `Toggle` pattern provider | Open |
| A4 | Badge changes raise a value change notification; state changes of toggle buttons raise a state change notification | Done |
| A5 | Keyboard: Enter, Space, Alt+letter work; Tab shows the focus ring; no ring after a mouse click | Done and tested |
| A6 | The tooltip text is not exposed as accessible description | Set `AccessibleDescription` where the tooltip adds information |
| A7 | High contrast uses system colors and thicker borders but was not exercised with a real high contrast theme | Open: manual test |

### Technical debt

1. Repeat button (C2, C3).
2. `ThemeOverride` (C6).
3. Shared animation clock (P5).
4. UI Automation providers (A3).
5. No custom toolbox bitmaps (Visual Studio shows the default control icon).
6. Parallel UI tests that depend on the foreground window.
7. `net48` is compiled but not tested.
8. The new properties are not yet listed in the demo's `Buttons` page text.

### Future improvements

- Multi-color SVG (per-shape fill colors mapped to theme tokens).
- Per-monitor DPI test on two monitors and a `DpiChanged` test with `DpiChangedAfterParent`.
- `Delay` and `Interval` for the repeat button, `IsLoading` state with a spinner, `Tag`-style `Icon` registry lookup by name (`IconName = "fluent:save"`).
- A designer editor for `SvgImage` that previews the icon.
- Toolbox bitmaps and a design-time theme preview.
- Optional `ToolTip` placement and delay properties.

## Decisions worth remembering

- Click on key down, not on key up, to stay compatible with existing tests and callers; auto-repeat is ignored.
- Color overrides lose to high contrast, because system colors are the accessibility contract.
- `Command` owns `Enabled` while attached, as the WPF command model does.
- The SVG renderer is monochrome on purpose: it keeps theme tinting and disabled dimming trivial and removes any need for a third-party SVG library.
