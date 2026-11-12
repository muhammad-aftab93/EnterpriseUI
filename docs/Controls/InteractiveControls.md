# Interactive Controls

Seventeen themed Windows Forms controls and components for input, feedback, AI assistance and notifications. They are built on the shared `Chrome`/`EnterpriseControlBase` visual language, follow the active theme (light, dark and Windows high contrast), scale with DPI and honor the system "animation effects" setting.

Headless logic (state machines, parsers, providers) lives in `EnterpriseUI.Editors.Interactivity` and has no UI dependency; the controls are in `EnterpriseUI.WinForms`.

| Control | Summary |
|---|---|
| `EnterpriseAIPromptControl` | Prompt, streamed output, command suggestions, history, token usage, copy / insert / retry / rating |
| `EnterpriseButtonControl`, `EnterpriseToggleButtonControl`, `EnterpriseSplitButtonControl`, `EnterpriseDropDownButtonControl` | Variants, glyph or image, badge counter, access keys, animated hover / pressed / focus states |
| `EnterpriseCarouselControl` | Flat, cover flow and ring layouts, looping, auto-play, custom item painting |
| `EnterpriseChatControl` | Grouped bubbles, avatars, timestamps, delivery state, attachments, quick replies, typing indicator |
| `EnterpriseInlineAIAssistant` (component) | Summarize, rewrite, expand and tone actions on the selection of a text box, with preview |
| `EnterpriseLabelControl` | Auto-ellipsis, access keys, 90/180/270 rotation, Markdown and HTML subset, image alignment, links |
| `EnterpriseProgressBarControl` | Determinate / indeterminate, solid, gradient, striped and segmented fills, text overlay, speed |
| `EnterpriseStarRatingControl` | Full, half and free precision, hover preview and tooltip, read-only, custom symbols |
| `EnterpriseRotatorControl` | Timed or manual panels with fade, slide and flip transitions |
| `EnterpriseSmartPasteButton` | Maps clipboard text (address, key/value, JSON, delimited, code) onto bound controls |
| `EnterpriseSpeechToTextButton` | Microphone toggle, live waveform, partial and final transcript into a target |
| `EnterpriseStepProgressBarControl` | Completed / active / upcoming / error steps, validation, click and keyboard navigation |
| `EnterpriseTrackBarControl` | Single or range thumbs, discrete steps, ticks, value tooltip, horizontal or vertical |
| `EnterpriseWaitingBarControl` | Spinning dots, ring, wave bars, bouncing blocks and marquee busy indicators |
| `EnterpriseVirtualKeyboardControl` | QWERTY, numeric and symbol layers, shift / caps, key-press animation |
| `EnterpriseCalloutControl` (component) | Anchored speech-bubble popup with arrow placement, shadow and close triggers |
| `EnterpriseNotifyIcon` (component) | Tray icon with unread badge, stacked toasts, action buttons and history |

Every control has a runnable page in the demo application (search for "interactive").

## Theming

Colors come from the active `ITheme` through `ControlColors`; there are no hard-coded palettes. Override per control with the color properties where offered (`FillStartColor`, `IndicatorColor`, `FillColor`, ...); an empty color follows the theme. Switching `ThemeManager.Instance.CurrentTheme` repaints all controls and open popups.

## Accessibility

- Controls expose an accessible role, name and value (for example `ProgressBar` reports "42%", the slider reports "20 to 80", the rating reports "3.5 out of 5").
- Everything is keyboard operable: buttons (Enter / Space, access keys), toggle groups, track bar (arrows, Page Up/Down, Home/End, Tab between range thumbs), rating (arrows, digits, Delete), steps (arrows, Enter), carousel and rotator (arrows), keyboard (focusable keys), callout and toasts (Esc closes).
- A 2 px focus ring is shown for keyboard focus only.
- In Windows high contrast mode the controls use system colors and thicker borders.
- Animations stop or jump to the end when the system disables animation effects.

## Examples

### AIPrompt

```csharp
var prompt = new EnterpriseAIPromptControl { Provider = new MyProvider() };
prompt.RegisterCommand(new AIPromptCommand("translate", "Translate", "Translate to French:\n\n{input}"));
prompt.InsertRequested += (_, text) => editor.SelectedText = text;
```

`MyProvider` implements `IAIPromptProvider`. `SampleAIPromptProvider` is a deterministic offline provider for demos and tests.

### Buttons

```csharp
var save = new EnterpriseButtonControl { Text = "&Save", Glyph = "S", BadgeCount = 3, DialogResult = DialogResult.OK };
var split = new EnterpriseSplitButtonControl { Text = "Export" };
split.AddOption("PDF", () => Export("pdf"));
```

`EnterpriseToggleButtonControl` supports `ThreeState` and `GroupName` (exclusive group). `EnterpriseDropDownButtonControl` opens its menu from the whole surface.

### Carousel

```csharp
var carousel = new EnterpriseCarouselControl { Layout = CarouselLayout.Coverflow, AutoPlay = true };
carousel.Items.Add(new CarouselItem("Dashboard", "KPIs"));
carousel.PaintItem += (_, e) => { /* draw e.Item into e.Bounds; set e.Handled */ };
```

### Chat

```csharp
var chat = new EnterpriseChatControl();
chat.MessageSubmitted += (_, e) => { chat.Conversation.SetStatus(e.Message, ChatMessageStatus.Sent); chat.Conversation.SetTyping(bot, true); };
chat.Conversation.Receive(bot, "Hello!", new[] { "Hi", "Help" });
```

### InlineAIAssistant

```csharp
var assistant = new EnterpriseInlineAIAssistant { Target = notesBox, Provider = new MyProvider() };
assistant.Applying += (_, e) => e.Result = e.Result.Trim();
```

### Label

```csharp
var label = new EnterpriseLabelControl { MarkupMode = LabelMarkupMode.Markdown, Text = "**Total:** [open report](report)" };
label.LinkClicked += (_, e) => Open(e.LinkText);
```

Markdown supports `**bold**`, `*italic*`, `~~strike~~`, `` `code` `` and `[text](target)`. HTML supports `b i u s code br a span font p div`.

### ProgressBar

```csharp
var bar = new EnterpriseProgressBarControl { Fill = EnterpriseProgressFill.Striped, TextFormat = "{0:0}% uploaded" };
bar.Value = 42;
```

### Rating

```csharp
var rating = new EnterpriseStarRatingControl { Maximum = 5, Precision = RatingPrecision.Half, Descriptions = new[] { "Poor", "Fair", "Good", "Great", "Excellent" } };
rating.ValueChanged += (_, _) => Save(rating.Value);
```

Set `SymbolPathFactory` or handle `PaintSymbol` for custom symbols.

### Rotator

```csharp
var rotator = new EnterpriseRotatorControl { Effect = RotatorEffect.SlideLeft, Interval = 5000 };
rotator.AddPanel("<b>Welcome</b><br/>Read the <a href='news'>news</a>", "Hello");
```

### SmartPasteButton

```csharp
var paste = new EnterpriseSmartPasteButton();
paste.Bind(emailBox, SmartPasteFieldKind.Email);
paste.Bind(cityBox, SmartPasteFieldKind.City);
paste.Pasting += (_, e) => e.Result.Assignments.RemoveAll(a => a.Confidence < 0.6);
```

The button inspects the clipboard text with `SmartPasteMapper`, which recognizes JSON, `label: value` blocks, delimited rows, postal addresses and code. Assign `Provider` (`ISmartPasteProvider`) to let a model fill fields the built-in recognizers leave empty.

### SpeechToTextButton

```csharp
var mic = new EnterpriseSpeechToTextButton { Target = notesBox, Provider = new MyRecognizer() };
mic.FinalTranscript += (_, text) => Log(text);
```

Audio is captured with the Windows wave-in API (`AudioInputFactory` replaces the source) and streamed to an `ISpeechRecognitionProvider`. The waveform follows the live input level.

### StepProgressBar

```csharp
var steps = new EnterpriseStepProgressBarControl();
steps.AddStep("Account"); steps.AddStep("Details"); steps.AddStep("Review");
steps.StepChanging += (_, e) => { if (!Valid(e.From)) { e.Cancel = true; e.Message = "Fix the errors"; } };
```

### TrackBar

```csharp
var range = new EnterpriseTrackBarControl { Mode = TrackSelectionMode.Range, Minimum = 0, Maximum = 1000, ValueLow = 200, ValueHigh = 800, ValueFormat = "{0:C0}" };
range.ValueCommitted += (_, _) => ApplyFilter(range.ValueLow, range.ValueHigh);
```

### WaitingBar

```csharp
var busy = new EnterpriseWaitingBarControl { Animation = WaitingBarAnimation.Ring, Text = "Loading…" };
busy.StartWaiting(); await LoadAsync(); busy.StopWaiting();
```

### VirtualKeyboard

```csharp
var keyboard = new EnterpriseVirtualKeyboardControl { Target = amountBox, Kind = VirtualKeyboardKind.Numeric };
keyboard.KeyPressed += (_, e) => Log(e.Key.Label);
```

### Callout

```csharp
var callout = new EnterpriseCalloutControl { Title = "Tip", Message = "Press **F5** to refresh.", MarkupMode = LabelMarkupMode.Markdown };
callout.Actions.Add(new NotificationAction("ok", "Got it", true));
callout.Show(refreshButton);
```

### NotifyIcon

```csharp
var tray = new EnterpriseNotifyIcon { Icon = AppIcon, Text = "My app", Visible = true };
tray.ActionInvoked += (_, e) => Open(e.Notification);
tray.Notify("Build finished", "All tests passed.", NotificationSeverity.Success, new NotificationAction("open", "Open", true));
```

`Notify` may be called from any thread. `ToastMode` selects custom toasts, native balloon tips or none; the unread count is drawn on the tray icon.
