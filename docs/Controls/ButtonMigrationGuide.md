# Migrating from Telerik RadButton to EnterpriseUI

Mapping of the `RadButton` API (as known from the Telerik API; see the limits in [ButtonFeatureComparison.md](ButtonFeatureComparison.md)) to `EnterpriseButtonControl`. Namespace: `EnterpriseUI.WinForms`.

## Controls

| Telerik | EnterpriseUI |
|---|---|
| `RadButton` | `EnterpriseButtonControl` |
| `RadToggleButton` | `EnterpriseToggleButtonControl` |
| `RadDropDownButton` | `EnterpriseDropDownButtonControl` |
| `RadSplitButton` | `EnterpriseSplitButtonControl` |
| `RadRepeatButton` | `EnterpriseRepeatButtonControl` (does not share the new members yet) |
| `RadButtonElement` | not needed; the control is its own element |

## Properties

| RadButton | EnterpriseUI | Note |
|---|---|---|
| `Text` | `Text` | `&` marks the access key; `UseMnemonic = false` turns it off |
| `Image` | `Image` | Fitted into `ImageSize` keeping proportions |
| `ImageList`, `ImageIndex`, `ImageKey` | same | |
| `SvgImage` | `SvgImage` | `EnterpriseSvgImage.FromMarkup(...)`, `.FromIcon(...)`; one color |
| `TextImageRelation` | `TextImageRelation` or `ContentLayout` | Same five values; `ImageAfterText` equals `TextBeforeImage` |
| `ImageAlignment` | `ImageAlignment` | Only used with `ContentLayout.Overlay` |
| `TextAlignment` | `TextAlignment` | Positions the whole block (the text with `Overlay`) |
| `DisplayStyle` (`Text`, `Image`, `ImageAndText`, `None`) | `DisplayStyle` (`ButtonDisplayStyle`) | |
| `TextWrap` | `TextWrap` | |
| `AutoSize`, `AutoSizeMode` | same | |
| `ClickMode` (`Release`, `Press`, `Hover`) | `ClickMode` (`ButtonClickMode`) | |
| `Command`, `CommandParameter` | same | `System.Windows.Input.ICommand` |
| `DialogResult` | `DialogResult` | `EnterpriseButtonControl` only |
| `UseMnemonic` | `UseMnemonic` | |
| `ShowItemToolTips` | `ShowToolTips` | |
| `ButtonElement.ToolTipText` | `ToolTipText` | |
| `ToolTipTextNeeded` event | `ToolTipTextNeeded` event | `e.ToolTipText` |
| `ButtonElement.IsPressed` | `IsPressed` | |
| `Enabled` | `Enabled` | |
| `Padding` | `Padding` | |
| `ThemeName` | none | Use `ThemeManager.Instance.ApplyTheme(name)`; global |
| `ButtonElement.TextElement.ForeColor` | `TextColor` | Ignored in high contrast |
| `ButtonElement.ButtonFillElement.BackColor` | `FillColor` | |
| `ButtonElement.BorderElement.*Color` | `BorderColor` | One color for all four sides |
| `ButtonElement.BorderElement.BoxStyle` | none | |
| `ButtonElement.Shape` (rounded) | `CornerRadius` | |
| `ToggleState` (`Off`, `On`, `Indeterminate`) | `CheckState` (`Unchecked`, `Checked`, `Indeterminate`), `Checked` | |
| `IsThreeState` | `ThreeState` | |
| `ToggleStateChanging` / `ToggleStateChanged` | `CheckStateChanging` / `CheckStateChanged`, `CheckedChanged` | |

New in EnterpriseUI with no RadButton counterpart: `Variant`, `Glyph`, `BadgeText`, `BadgeCount`, `FacePainting`, `ContentPainting`, `VisualState`, `ShowFocusRing`, `AnimateStates`, `ImageSize`.

## Methods and events

| RadButton | EnterpriseUI |
|---|---|
| `PerformClick()` | `PerformClick()` |
| `Click` | `Click` (also raised by keyboard, access key and `PerformClick`) |
| `ThemeNameChanged` | `ThemeManager.Instance.ThemeChanged` |
| `NotifyDefault` | `NotifyDefault` (`IButtonControl`) |

## Before and after

Telerik:

```csharp
var button = new RadButton { Text = "Save", Image = saveImage, TextImageRelation = TextImageRelation.ImageBeforeText, DisplayStyle = DisplayStyle.ImageAndText };
button.ButtonElement.ToolTipText = "Save the document";
button.ButtonElement.TextElement.ForeColor = Color.Red;
button.ButtonElement.ButtonFillElement.BackColor = Color.Aqua;
button.ToolTipTextNeeded += (s, e) => e.ToolTipText = "Click me";
button.ThemeName = "Fluent";
```

EnterpriseUI:

```csharp
var button = new EnterpriseButtonControl
{
    Text = "&Save",
    SvgImage = EnterpriseSvgImage.FromIcon(FluentIconPack.Save),     // or Image = saveImage
    ContentLayout = ButtonContentLayout.ImageBeforeText,
    DisplayStyle = ButtonDisplayStyle.ImageAndText,
    ToolTipText = "Save the document",
    TextColor = Color.Red,
    FillColor = Color.Aqua
};
button.ToolTipTextNeeded += (s, e) => e.ToolTipText = "Click me";
ThemeManager.Instance.ApplyTheme("Fluent-Light");                    // global instead of per control
```

## Behavioral differences to check after migrating

1. **Themes are global.** Code that sets `ThemeName` on single buttons must switch to the global theme or to color overrides.
2. **Element customization.** Code that reaches into `ButtonElement` must move to the color properties or to `FacePainting` / `ContentPainting`.
3. **Fill and border overrides** are ignored in Windows high contrast mode.
4. **SVG is monochrome.** Multi-color SVG assets must be split or provided as `Image`.
5. **Access keys.** `&` always marks an access key unless `UseMnemonic` is false. Use `&&` for a literal ampersand.
6. **Enabled and commands.** While `Command` is set the button's `Enabled` is controlled by `CanExecute`.
7. **Key clicks.** Holding Enter or Space produces one click.
8. **Text is bold** and image-only buttons need `ToolTipText` for an accessible name.

## Migration steps

1. Replace the control types and add `using EnterpriseUI.WinForms;`.
2. Run the compiler and rename members using the table above.
3. Replace `ThemeName` and element code (steps 1 and 2 above).
4. Convert images to `SvgImage` where an SVG source exists.
5. Add `ToolTipText` to icon-only buttons.
6. Check the screens in a dark theme and with `AutoSize` where widths were measured by hand.
