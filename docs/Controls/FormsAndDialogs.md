# Forms, Dialogs and Shell Controls

Windows Forms windows, dialogs and shell integration controls in `EnterpriseUI.WinForms`. They share the `Chrome`/`ControlColors` visual language, follow the active theme (light, dark and Windows high contrast), scale with DPI and honor the system "animation effects" setting. The task dialog and desktop alert are bound to the platform-neutral models `EnterpriseTaskDialog` (`EnterpriseUI.Layout`) and `EnterpriseDesktopAlert` (`EnterpriseUI.Notifications`); the ribbon is bound to `EnterpriseRibbonBar` (`EnterpriseUI.Navigation`).

| Type | Kind | Summary |
|---|---|---|
| `EnterpriseForm` | Form | Own caption bar, sizing frame, Aero snap, maximize that respects the taskbar, rounded corners, theme sync |
| `EnterpriseTitleBarControl` | Control | Icon, title, content area, command buttons, minimize / maximize / close, drags the window |
| `EnterpriseRibbonForm` / `EnterpriseRibbonBarControl` | Form / Control | Quick access buttons and tab headers (with contextual tabs) in the title bar, collapsible ribbon body |
| `EnterpriseTabbedForm` | Form | Browser-style tabs: new tab, close, pin, reorder, drag out into a new window |
| `EnterpriseToolbarForm` / `EnterpriseToolbarStrip` | Form / Control | Menus, buttons and search box in the title bar, overflow menu |
| `EnterpriseShapedForm` | Form | Rounded, ellipse, polygon, vector path or image-mask outline, drag by the body |
| `EnterpriseStatusStrip` | Control | Text, progress and link panels, spring panels, size grip |
| `EnterpriseAboutBox` | Dialog | Assembly information, link, copyable system information |
| `EnterpriseMarkupDialog` | Dialog | Markdown or HTML-style body with links, standard buttons and icons |
| `EnterpriseTaskDialogForm` | Dialog | Command links, radio buttons, verification, expandable details, progress, footer, timer |
| `EnterpriseDesktopAlertWindow` | Popup | Screen-corner alert that never takes focus, pin and options buttons, actions, countdown |
| `EnterpriseToastNotification` | Component | Stacking themed toasts with actions and history, optional `ToastService` link |
| `EnterpriseSplashScreen` | Popup | Logo, title, status, progress; thread-safe updates, minimum duration, fade-out |
| `EnterpriseOverlay` | Component | Dims and blocks a host with a busy indicator and caption |
| `EnterpriseFlyout` | Form | Slide-in panel from any edge, modal or non-modal, light dismiss |
| `EnterpriseTaskbarButton` | Component | Taskbar progress, overlay badge and thumbnail buttons |

Already available and unchanged: `EnterpriseMessageBox`, `EnterpriseColorDialog`, `EnterpriseFileDialog`, `EnterpriseFormConverter`, `EnterpriseScrollBar`.

## EnterpriseForm and the caption bar

```csharp
var form = new EnterpriseForm { Text = "Orders", CornerRadius = 8 };
form.TitleBar.AddButton("settings", "⚙", "Settings");
form.TitleBar.ButtonClick += (_, e) => Open(e.Button.Id);
form.Controls.Add(new Panel { Dock = DockStyle.Fill });
```

- The client area starts below `TitleBar`; fill controls added later are laid out under it.
- `AllowResize = false` gives a fixed window (dialogs); `MinimizeBox`, `MaximizeBox` and `ControlBox` hide the matching system buttons (call `RefreshTitleBar()` after changing them at run time).
- `TitleBar.ContentControl` hosts any control between the title and the buttons. Derive from `EnterpriseTitleBarContentControl` and override `IsCaptionArea` so the empty parts of your control still move the window.
- `FrameHitTest(screenPoint)` reports `Caption`, an edge or corner, or `Client`; use it to test custom caption content.
- Events: `ThemeApplied`, `CaptionDoubleClick`, `WindowStateChanged`.

## Ribbon form

```csharp
var form = new EnterpriseRibbonForm();
form.Ribbon.Tabs.Add(new RibbonTab { Title = "Home", Groups = { clipboardGroup } });
form.Ribbon.Tabs.Add(new RibbonTab { Title = "Picture Tools", ContextualGroup = "Picture", ContextualColor = "#C0504D" });
form.QuickAccessItems.Add(new RibbonItem { Text = "Save", Icon = "💾", Command = save });
form.Ribbon.RefreshRibbon();
form.ShowContextualTab("Picture");
```

`RibbonTab.ContextualGroup` hides a tab until its group is shown. Double-clicking a tab header collapses the ribbon; choosing a tab expands it again. `EnterpriseRibbonBarControl` also works on its own (`ShowTabHeaders = true`).

## Tabbed form

```csharp
var form = new EnterpriseTabbedForm();
form.AddTab("Orders", new OrdersView(), pinned: true);
form.TabAdding += (_, e) => e.Tab = new EnterpriseTabbedFormTab("New", new OrdersView());
form.TabClosing += (_, e) => e.Cancel = HasUnsavedChanges(e.Tab);
```

Pinned tabs stay first and show only their icon. Dragging a tab far above or below the title bar (`AllowTearOff`) or calling `DetachTab` opens it in a new window; override `CreateDetachedForm` to use your own form type.

## Dialogs

```csharp
EnterpriseAboutBox.ShowAbout(owner);
EnterpriseMarkupDialog.ShowMarkup("**Update** ready. See the [notes](https://example.org).", "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

var model = new EnterpriseTaskDialog { MainInstruction = "Pick a ring", VerificationText = "Do not ask again" };
model.CommandLinks.Add(new EnterpriseTaskDialog.CommandLinkOption("canary", "Canary", "5% of traffic"));
using var dialog = new EnterpriseTaskDialogForm(model) { CommonButtons = EnterpriseTaskDialogButtons.Cancel };
dialog.Execute(owner);                       // model.SelectedCommandId and IsVerificationChecked are written back
```

Links in the About box and markup dialog open http and https addresses only; handle `LinkClicked` to override. A task dialog command can keep the dialog open by setting `KeepOpen` in `CommandLinkClicked`.

## Alerts, toasts, splash and overlays

- `EnterpriseDesktopAlertWindow` stacks in the chosen corner and pauses its countdown while hovered or pinned.
- `EnterpriseToastNotification` can show a toast for every `ToastService.Instance.Show(...)` call when `ListenToToastService` is true.
- `EnterpriseSplashScreen.ShowSplash()` draws without a message loop, so it appears while the application is still starting; `ReportProgress` can be called from any thread, and `Close()` waits for `MinimumDuration`.
- `EnterpriseOverlay.Show(host)` disables the host (`BlockInput`) and covers it with a dimmer that follows the host when it moves or resizes.
- `EnterpriseFlyout.ShowFlyout(host)` slides in from `Edge`; a modal flyout also dims and blocks the host. `FlyoutClosing` can cancel, Escape closes.
- `EnterpriseTaskbarButton` wraps the Windows taskbar interfaces (progress, overlay icon, up to seven thumbnail buttons) and does nothing when they are unavailable. Jump lists are not included.

## Platform notes

- The window frame uses `WM_NCCALCSIZE` and the window manager for shadow and rounded corners; on systems that lack the rounded-corner setting the corners stay square.
- Popups use layered windows with per-pixel alpha and never take focus.
- `net8.0-windows` is the target of the WinForms project; the models are platform neutral.

## Accessibility

Controls expose accessible roles (title bar, status bar, toolbar), every button is operable from the keyboard (command links: Enter / Space), dialogs have default and cancel buttons, and Windows high contrast colors replace the theme palette.
