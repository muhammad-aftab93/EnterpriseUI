# Button architecture

Internals of the button family in `EnterpriseUI.WinForms`. API: [Button.md](Button.md).

## Class structure

```mermaid
classDiagram
    class EnterpriseControlBase {
        +Colors
        +DpiScale
        +S(logical)
        +BoldFont
        #OnThemeApplied()
    }
    class EnterpriseButtonBase {
        +Variant, Glyph, Image, SvgImage, ImageList
        +ContentLayout, TextAlignment, DisplayStyle
        +ClickMode, Command, ToolTipText
        +VisualState, AutoSize
        +PerformClick()
        +GetPreferredSize()
        #PaintStandard()
        #PaintFace()
        #PaintContent()
    }
    class EnterpriseButtonControl
    class EnterpriseToggleButtonControl
    class EnterpriseSplitButtonControl
    class EnterpriseDropDownButtonControl
    class ButtonLayoutEngine {
        <<internal static>>
        +Arrange()
    }
    class ButtonAccessibleObject
    class EnterpriseSvgImage
    EnterpriseControlBase <|-- EnterpriseButtonBase
    EnterpriseButtonBase <|-- EnterpriseButtonControl
    EnterpriseButtonBase <|-- EnterpriseToggleButtonControl
    EnterpriseButtonBase <|-- EnterpriseSplitButtonControl
    EnterpriseSplitButtonControl <|-- EnterpriseDropDownButtonControl
    EnterpriseButtonBase ..> ButtonLayoutEngine : uses
    EnterpriseButtonBase ..> ButtonAccessibleObject : creates
    EnterpriseButtonBase ..> EnterpriseSvgImage : paints
```

| Piece | File | Responsibility |
|---|---|---|
| `EnterpriseButtonBase` | `Interactivity/EnterpriseButtonBase.cs` | State, input, painting steps, command, tooltip, sizing, accessibility hooks |
| `ButtonLayoutEngine` | `Interactivity/EnterpriseButtonSupport.cs` | Pure arrangement of image and text for a rectangle; used by painting and by `GetPreferredSize` |
| `ButtonAccessibleObject` | same | Name, value, shortcut, state, default action |
| `EnterpriseSvgImage`, `SvgPathParser` | `Interactivity/EnterpriseSvgImage.cs` | SVG path and markup to `GraphicsPath`, drawing in one color |
| `ButtonActions` | `Design/EnterpriseDesigners.cs` | Smart tag |
| Derived controls | `Interactivity/EnterpriseButtons.cs` | Dialog result, toggle state, split and drop-down menu |

## Rendering pipeline

`OnPaint` calls `PaintStandard` (toggle and split controls have their own `OnPaint` that calls the same steps):

1. Clear with the parent's back color.
2. `PaintFace`: raises `FacePainting`; if not handled fills the rounded face (variant color blended with white for hover and black for pressed, using the animator values) and draws the border (width scaled by DPI, radius scaled by DPI).
3. `PaintFaceDecoration`: default-button marker of `EnterpriseButtonControl`.
4. `PaintContent`: asks `ButtonLayoutEngine.Arrange` for the image and text rectangles, raises `ContentPainting`; if not handled draws the SVG, glyph or image, then the text with the access key underline.
5. `PaintBadge`, then `PaintFocus` (ring only for keyboard focus).

```mermaid
sequenceDiagram
    participant W as Windows (WM_PAINT)
    participant B as EnterpriseButtonBase
    participant E as ButtonLayoutEngine
    participant App as Application handlers
    W->>B: OnPaint
    B->>B: PaintFace
    B->>App: FacePainting (optional)
    App-->>B: Handled?
    B->>B: fill and border unless handled
    B->>B: PaintFaceDecoration
    B->>B: PaintContent
    B->>E: Arrange(area, icon size, text measure, relation, alignment, rtl)
    E-->>B: icon rectangle, text rectangle, content size
    B->>App: ContentPainting (optional)
    App-->>B: Handled?
    B->>B: draw SVG / glyph / image and text unless handled
    B->>B: PaintBadge, PaintFocus
```

Text is parsed (access key) and measured once per text or font change and cached; the glyph size is cached per glyph and font size; the disabled-image color matrix is created once.

## Sizing

```mermaid
sequenceDiagram
    participant L as Layout engine or AdjustAutoSize
    participant B as EnterpriseButtonBase
    participant E as ButtonLayoutEngine
    L->>B: GetPreferredSize(proposed)
    B->>E: Arrange(very large area or proposed width when TextWrap)
    E-->>B: content size
    B-->>L: content + face inset + margins + Padding + badge (+ arrow for split)
    Note over B: AutoSize with GrowOnly keeps the larger of current and preferred size
```

All numbers are logical values passed through `S()`, which multiplies by `DeviceDpi / 96`.

## Click processing (mouse)

```mermaid
sequenceDiagram
    actor U as User
    participant B as EnterpriseButtonBase
    participant D as Derived class
    participant App as Click handlers
    participant C as ICommand
    U->>B: MouseEnter
    B->>B: animate hover, VisualStateChanged(Hot), schedule tooltip
    U->>B: MouseDown (left)
    B->>B: pressed = true, Focus, animate press, VisualStateChanged(Pressed)
    alt ClickMode = Press
        B->>D: OnClick
    end
    U->>B: MouseUp
    B->>B: pressed = false, animate release
    alt ClickMode = Release and pointer inside and ShouldClick
        B->>D: OnClick
    end
    D->>D: set DialogResult / toggle state / raise PrimaryActionClicked
    D->>B: base.OnClick
    B->>App: Click event
    B->>C: CanExecute(parameter)
    alt allowed
        B->>C: Execute(parameter)
    end
```

`ClickMode.Hover` raises `OnClick` from `OnMouseEnter`. `ShouldClick` lets the split button ignore releases over its arrow.

## Keyboard interaction

```mermaid
sequenceDiagram
    actor U as User
    participant W as WndProc
    participant B as EnterpriseButtonBase
    U->>W: WM_KEYDOWN (Space, first)
    W->>W: autoRepeat = lParam bit 30 = 0
    W->>B: OnKeyDown
    B->>B: keyboard cue on, press tint flash
    B->>B: PerformClick (Click, then Command)
    U->>W: WM_KEYDOWN (Space, held)
    W->>W: autoRepeat = 1
    W->>B: OnKeyDown
    B-->>W: Handled, no click
    U->>B: Alt+S (ProcessMnemonic)
    B->>B: Focus, PerformClick
```

Enter and Space are declared input keys so dialogs do not steal them. The auto-repeat flag is read in `WndProc` because `KeyEventArgs` does not carry it.

## State transitions

```mermaid
stateDiagram-v2
    [*] --> Normal
    Normal --> Hot: MouseEnter
    Hot --> Normal: MouseLeave
    Hot --> Pressed: MouseDown (left)
    Pressed --> Hot: MouseUp
    Pressed --> Normal: MouseLeave
    Normal --> Disabled: Enabled = false
    Hot --> Disabled: Enabled = false
    Pressed --> Disabled: Enabled = false
    Disabled --> Normal: Enabled = true
```

`VisualState` is derived: `Disabled` if not enabled, else `Pressed`, else `Hot` (pointer inside), else `Normal`. `VisualStateChanged` fires after each input handler when the derived value changed. Keyboard focus is independent: it only decides whether the focus ring is drawn.

## Theme application

```mermaid
sequenceDiagram
    participant T as ThemeManager
    participant B as EnterpriseControlBase
    participant D as Button
    participant UI as UI thread
    T->>B: ThemeChanged(theme)
    B->>UI: marshal with BeginInvoke when on another thread
    B->>B: drop cached ControlColors
    B->>D: BackColor, ForeColor from the new palette
    B->>D: OnThemeApplied (split: restyle the menu)
    B->>D: Invalidate
    D->>D: next OnPaint reads Colors (variant, border, disabled, focus ring)
```

In Windows high contrast mode `ControlColors` returns system colors and the button ignores `FillColor`, `BorderColor` and `TextColor`.

## Command binding

```mermaid
sequenceDiagram
    participant App
    participant B as EnterpriseButtonBase
    participant C as ICommand
    App->>B: Command = command
    B->>B: remember Enabled, subscribe CanExecuteChanged
    B->>C: CanExecute(parameter)
    B->>B: Enabled = result
    C-->>B: CanExecuteChanged (any thread)
    B->>B: marshal to the UI thread when needed
    B->>C: CanExecute(parameter)
    B->>B: Enabled = result
    App->>B: Command = null
    B->>B: unsubscribe, restore remembered Enabled
```

## Tooltip

```mermaid
sequenceDiagram
    participant B as EnterpriseButtonBase
    participant App
    participant T as ThemedToolTip
    B->>B: MouseEnter starts a 500 ms timer (created on first use)
    B->>B: timer tick: still hot and enabled?
    B->>App: ToolTipTextNeeded(args.ToolTipText = ToolTipText)
    App-->>B: text
    B->>B: image-only and empty text: use the button text
    B->>T: Show(below the button)
    B->>T: Hide on leave, press, disable, dispose
```

## SVG drawing

```mermaid
sequenceDiagram
    participant B as Button
    participant S as EnterpriseSvgImage
    B->>S: Draw(graphics, icon rectangle, text color of the state)
    S->>S: scale = min(width / viewBoxWidth, height / viewBoxHeight)
    S->>S: translate and scale the graphics transform
    loop each shape
        S->>S: FillPath with the color (nonzero or even-odd)
        S->>S: DrawPath with the color when stroked
    end
```

The `GraphicsPath` objects are built once when the image is created; drawing only changes the graphics transform, so painting does not allocate paths.

## Event model

| Event | Raised from | Notes |
|---|---|---|
| `Click` | `OnClick` | Mouse, `Press` or `Hover` mode, Enter, Space, access key, `PerformClick`, accessibility default action |
| `VisualStateChanged` | Mouse and enabled handlers | After the state changed |
| `FacePainting`, `ContentPainting` | `OnPaint` | Only allocated and raised when subscribed |
| `ToolTipTextNeeded` | Tooltip timer | And from `ResolveToolTipText()` |
| `CheckStateChanging` | Toggle `CheckState` setter | Cancelable; not raised for group members that are unchecked by another member |
| `CheckStateChanged`, `CheckedChanged` | After the change | |

## Design-time model

`EnterpriseButtonBase` carries `[ToolboxItem]`, `[DefaultEvent(Click)]`, `[DefaultProperty(Text)]` and `[Designer(EnterpriseControlDesigner<ButtonActions>)]`. `ButtonActions` reads and writes through `TypeDescriptor`, so smart tag edits are undoable. Properties have `[Category]`, `[Description]` and `[DefaultValue]`; `Command` and `CommandParameter` are hidden from the designer; `SvgImage` serializes through `EnterpriseSvgImageConverter` (`InstanceDescriptor`) and can be typed as path data or markup in the property grid.
