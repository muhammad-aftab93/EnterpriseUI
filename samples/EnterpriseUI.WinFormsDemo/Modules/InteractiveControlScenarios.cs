using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Modules;

/// <summary>Always fails; shows how the AI prompt control reports an error and offers a retry.</summary>
internal sealed class DemoFailingProvider : IAIPromptProvider
{
    public async Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
        throw new InvalidOperationException("The service returned 503 (temporarily unavailable).");
    }
}

/// <summary>Workshop pages for the interactive and AI controls, one control family per page.</summary>
public static class InteractiveControlScenarios
{
    private static readonly string[] Latencies = { "None", "Short", "Long" };

    public static IReadOnlyList<GridScenario> All { get; } = new List<GridScenario>
    {
        AIPrompt(), Buttons(), Carousel(), Chat(), InlineAI(), Label(), ProgressBar(), Rating(), Rotator(),
        SmartPaste(), SpeechToText(), StepProgress(), TrackBar(), WaitingBar(), VirtualKeyboard(), Callout(), NotifyIcon()
    };

    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), WrapContents = true };

    private static T Put<T>(FlowLayoutPanel panel, T control, bool breakAfter = false) where T : Control
    {
        control.Margin = new Padding(0, 0, 14, 12);
        panel.Controls.Add(control);
        if (breakAfter) panel.SetFlowBreak(control, true);
        return control;
    }

    private static Label Caption(FlowLayoutPanel panel, string text)
    {
        var label = new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(0, 6, 0, 6) };
        panel.Controls.Add(label);
        panel.SetFlowBreak(label, true);
        return label;
    }

    // ---- 1. AIPrompt --------------------------------------------------------------------

    private static GridScenario AIPrompt() => new()
    {
        Id = "interactive-aiprompt",
        Title = "AIPrompt - Prompt, Output, Commands and History",
        Description = "An assistant panel with four views: write a prompt or pick a command, watch the answer stream in, copy, insert, retry or rate it, browse the command catalog and the history with token usage.",
        Features = new[] { "Prompt, Output, Commands, History views", "Streaming output and loading state", "Custom command registration", "Retry, cancel and rating", "Token usage and history" },
        Documentation = "EnterpriseAIPromptControl is driven by an AIPromptSession. The session owns the history, the command registry and the state machine (Idle, Loading, Completed, Faulted, Cancelled); the control only paints it. Any IAIPromptProvider can be plugged in; the sample provider works offline.",
        Usage = "var prompt = new EnterpriseAIPromptControl { Provider = new MyProvider() };\nprompt.RegisterCommand(new AIPromptCommand(\"translate\", \"Translate\", \"Translate to French:\\n\\n{input}\"));\nprompt.InsertRequested += (_, text) => editor.SelectedText = text;",
        Api = "EnterpriseAIPromptControl -> Provider, Session, RegisterCommand, ActiveView, PromptText, SelectedCommand, SubmitAsync, InsertRequested, OutputCopied, ResponseReceived",
        Accessibility = "The loading state is exposed as AccessibleStates.Busy; Ctrl+Enter generates, Escape cancels, Left and Right switch views, and type / in the prompt to list commands.",
        Build = host =>
        {
            var sample = new SampleAIPromptProvider { Latency = TimeSpan.FromMilliseconds(500) };
            var prompt = new EnterpriseAIPromptControl { Provider = sample };
            prompt.PromptText = "Our new dashboard loads faster. It also has better filters. We think users will like it. Please test it and tell us what you think.";
            prompt.InsertRequested += (_, text) => host.Log("AIPrompt.Insert", text);
            prompt.OutputCopied += (_, _) => host.Log("AIPrompt.Copied", "Output copied to the clipboard");
            prompt.ResponseReceived += (_, entry) => host.Metric($"Last answer: {entry.Usage.TotalTokens} tokens in {entry.Duration.TotalSeconds:0.0} s | history: {prompt.Session.History.Count}");
            host.Combo("Latency:", Latencies, "Short", v => sample.Latency = TimeSpan.FromMilliseconds(v == "None" ? 0 : v == "Short" ? 500 : 2500));
            host.Button("Add /translate command", () => prompt.RegisterCommand(new AIPromptCommand("translate", "Translate", "Translate to French:\n\n{input}", "Custom command registered at run time", "A", "Custom")));
            host.Button("Simulate failure", () => prompt.Provider = new DemoFailingProvider());
            host.Button("Restore provider", () => prompt.Provider = sample);
            host.Button("Show commands", () => prompt.ActiveView = AIPromptView.Commands);
            host.Metric("Pick a command chip or type a prompt, then press Generate (Ctrl+Enter).");
            return prompt;
        }
    };

    // ---- 2. Buttons ---------------------------------------------------------------------

    private static GridScenario Buttons() => new()
    {
        Id = "interactive-buttons",
        Title = "Buttons - Standard, Toggle, Split and DropDown",
        Description = "Push buttons in five variants with glyphs, badge counters and access keys, latching toggle buttons (two-state, three-state and exclusive groups), split buttons and drop-down buttons with themed menus.",
        Features = new[] { "Variants, glyph and image", "Badge counter", "Toggle and exclusive groups", "Split and drop-down", "Animated hover, press and focus" },
        Documentation = "All buttons share EnterpriseButtonBase: animated hover and pressed tints, a focus ring that only appears for keyboard focus, access keys (&Save), DialogResult support and a badge. The split button raises PrimaryActionClicked for the main part and opens its menu from the arrow.",
        Usage = "var save = new EnterpriseButtonControl { Text = \"&Save\", Glyph = \"S\", BadgeCount = 3, DialogResult = DialogResult.OK };\nvar split = new EnterpriseSplitButtonControl { Text = \"Export\" };\nsplit.AddOption(\"PDF\", () => Export(\"pdf\"));",
        Api = "EnterpriseButtonControl, EnterpriseToggleButtonControl, EnterpriseSplitButtonControl, EnterpriseDropDownButtonControl -> Variant, Glyph, Image, ContentLayout, BadgeText, BadgeCount, Checked, GroupName, AddOption",
        Accessibility = "Buttons expose PushButton, CheckButton or SplitButton roles; toggle state is reported as Checked or Mixed and the split button as Expanded or Collapsed. Enter and Space activate; Alt+Down or F4 opens menus.",
        Build = host =>
        {
            var panel = Flow();
            var all = new List<Control>();
            Caption(panel, "Variants with glyphs and a badge counter");
            var count = 0;
            foreach (var variant in Enum.GetValues<EnterpriseButtonVariant>())
            {
                var b = Put(panel, new EnterpriseButtonControl { Text = variant.ToString(), Variant = variant, Glyph = variant == EnterpriseButtonVariant.Danger ? "✖" : "★", Size = new Size(140, 36) });
                b.Click += (_, _) => host.Log("Button.Click", variant.ToString());
                all.Add(b);
            }

            var badge = Put(panel, new EnterpriseButtonControl { Text = "Inbox", Glyph = "✉", BadgeCount = 3, Size = new Size(140, 36) }, true);
            badge.Click += (_, _) => { badge.BadgeCount = ++count + 3; host.Metric($"Badge shows {badge.BadgeCount}"); };
            all.Add(badge);

            Caption(panel, "Access keys: Alt+S saves, Alt+C cancels. Layout variants:");
            all.Add(Put(panel, new EnterpriseButtonControl { Text = "&Save", Glyph = "✔", Variant = EnterpriseButtonVariant.Success, Size = new Size(120, 36) }));
            all.Add(Put(panel, new EnterpriseButtonControl { Text = "&Cancel", Variant = EnterpriseButtonVariant.Outline, Size = new Size(120, 36) }));
            all.Add(Put(panel, new EnterpriseButtonControl { Text = "Above", Glyph = "☁", ContentLayout = ButtonContentLayout.ImageAboveText, Size = new Size(90, 64) }));
            all.Add(Put(panel, new EnterpriseButtonControl { Text = "Next", Glyph = "➤", ContentLayout = ButtonContentLayout.ImageAfterText, Size = new Size(120, 36) }, true));

            Caption(panel, "Toggle buttons: two-state, three-state and an exclusive group");
            var bold = Put(panel, new EnterpriseToggleButtonControl { Text = "Bold", Glyph = "B", Size = new Size(100, 36) });
            var tri = Put(panel, new EnterpriseToggleButtonControl { Text = "Tri-state", ThreeState = true, Size = new Size(110, 36) });
            bold.CheckedChanged += (_, _) => host.Log("ToggleButton.Checked", $"Bold = {bold.Checked}");
            tri.CheckStateChanged += (_, _) => host.Log("ToggleButton.CheckState", tri.CheckState.ToString());
            all.Add(bold);
            all.Add(tri);
            foreach (var name in new[] { "Left", "Center", "Right" })
            {
                var t = Put(panel, new EnterpriseToggleButtonControl { Text = name, GroupName = "align", Checked = name == "Left", Size = new Size(90, 36) });
                t.CheckedChanged += (_, _) => { if (t.Checked) host.Metric($"Alignment: {name}"); };
                all.Add(t);
            }

            panel.SetFlowBreak(panel.Controls[^1], true);
            Caption(panel, "Split button (main part runs the default action) and drop-down button");
            var split = Put(panel, new EnterpriseSplitButtonControl { Text = "Export", Glyph = "⇩", Size = new Size(160, 36) });
            split.AddOption("Export as PDF", () => host.Log("Split.Item", "PDF"), "▤");
            split.AddOption("Export as Excel", () => host.Log("Split.Item", "XLSX"), "▦");
            split.AddSeparator();
            split.AddOption("Settings…");
            split.PrimaryActionClicked += (_, _) => host.Log("Split.Primary", "Default export");
            var drop = Put(panel, new EnterpriseDropDownButtonControl { Text = "Actions", Variant = EnterpriseButtonVariant.Secondary, Size = new Size(140, 36) });
            drop.AddOption("Rename");
            drop.AddOption("Duplicate");
            drop.AddOption("Delete");
            drop.MenuItemSelected += item => host.Log("DropDown.Item", item);
            all.Add(split);
            all.Add(drop);

            host.Toggle("Enabled", true, v => all.ForEach(c => c.Enabled = v));
            host.Metric("Hover, press and Tab to the buttons; the focus ring shows for keyboard focus only.");
            return panel;
        }
    };

    // ---- 3. Carousel --------------------------------------------------------------------

    private static GridScenario Carousel() => new()
    {
        Id = "interactive-carousel",
        Title = "Carousel - Flat, Cover Flow and Ring",
        Description = "Cycle through items with smooth animation. Click a side item, use the arrows or keys, swipe with the mouse, switch layouts or let it play automatically.",
        Features = new[] { "Flat, cover-flow (perspective) and ring layouts", "Infinite loop", "Autoplay with pause on hover", "Swipe, wheel and keyboard", "Custom item templates" },
        Documentation = "EnterpriseCarouselControl renders each item once into a cached face and moves the cached faces; add CarouselItem objects, any other object is shown with its text, or draw your own faces in PaintItem.",
        Usage = "var carousel = new EnterpriseCarouselControl { Layout = CarouselLayout.Coverflow, AutoPlay = true };\ncarousel.Items.Add(new CarouselItem(\"Dashboard\", \"KPIs\"));\ncarousel.PaintItem += (_, e) => { /* draw e.Item into e.Bounds; set e.Handled */ };",
        Api = "EnterpriseCarouselControl -> Items, Layout, SelectedIndex, InfiniteLoop, AutoPlay, AutoPlayInterval, ItemSize, ShowIndicators, ShowReflection, PaintItem, SelectedIndexChanged, ItemActivated",
        Accessibility = "Left and Right change the item, Home and End jump to the ends, Enter activates; autoplay pauses while the pointer is over the control or it has focus. The selected item is exposed as the control value.",
        Build = host =>
        {
            var carousel = new EnterpriseCarouselControl();
            var accents = new[] { Color.SteelBlue, Color.SeaGreen, Color.Chocolate, Color.MediumPurple, Color.Teal, Color.IndianRed, Color.Goldenrod, Color.SlateGray };
            var titles = new[] { "Executive dashboard", "Order pipeline", "Inventory heat map", "Support queue", "Revenue forecast", "Risk register", "Hiring tracker", "Audit trail" };
            for (var i = 0; i < titles.Length; i++) carousel.Items.Add(new CarouselItem(titles[i], $"Report {i + 1} of {titles.Length}") { Accent = accents[i] });
            carousel.SelectedIndexChanged += (_, _) => host.Metric($"Selected {carousel.SelectedIndex + 1} of {carousel.Items.Count}: {carousel.SelectedItem}");
            carousel.ItemActivated += (_, i) => host.Log("Carousel.Activated", carousel.Items[i].ToString() ?? string.Empty);
            host.Combo("Layout:", Enum.GetNames<CarouselLayout>(), "Coverflow", v => carousel.Layout = Enum.Parse<CarouselLayout>(v));
            host.Toggle("Infinite loop", true, v => carousel.InfiniteLoop = v);
            host.Toggle("Autoplay", false, v => carousel.AutoPlay = v);
            host.Toggle("Reflection", true, v => carousel.ShowReflection = v);
            host.Toggle("Arrows", true, v => carousel.ShowNavigationButtons = v);
            host.Button("◀ Previous", carousel.MovePrevious);
            host.Button("Next ▶", carousel.MoveNext);
            host.Metric("Selected 1 of 8");
            return carousel;
        }
    };

    // ---- 4. Chat ------------------------------------------------------------------------

    private static GridScenario Chat() => new()
    {
        Id = "interactive-chat",
        Title = "Chat UI - Bubbles, Attachments, Quick Replies and Typing",
        Description = "A conversation view with grouped bubbles, avatars, timestamps and delivery state, formatted text, file attachments, quick-reply chips and a typing indicator. Send a message and the assistant answers.",
        Features = new[] { "Grouped message bubbles and date separators", "Avatars, timestamps, delivery status", "Attachments and quick replies", "Typing indicator", "Markdown text and links" },
        Documentation = "EnterpriseChatControl displays a ChatConversation. Your code adds messages and updates their status; the control lays out only what is visible, keeps the scroll position when you read history and shows a button for new messages.",
        Usage = "var chat = new EnterpriseChatControl();\nchat.MessageSubmitted += (_, e) => { chat.Conversation.SetStatus(e.Message, ChatMessageStatus.Sent); chat.Conversation.SetTyping(bot, true); };\nchat.Conversation.Receive(bot, \"Hello!\", new[] { \"Hi\", \"Help\" });",
        Api = "EnterpriseChatControl -> Conversation, SendText, MessageSubmitting, MessageSubmitted, QuickReplyClicked, AttachmentClicked, RetryRequested, ShowAvatars, ShowTimestamps, AllowAttachments, MessageMarkup",
        Accessibility = "New messages update the control value so assistive technology can announce them; the composer is a labelled text box, Enter sends and Shift+Enter inserts a line break.",
        Build = host =>
        {
            var me = new ChatAuthor("me", "You", true);
            var bot = new ChatAuthor("bot", "Assistant");
            var chat = new EnterpriseChatControl { Conversation = new ChatConversation(me), PlaceholderText = "Message the assistant" };
            var conversation = chat.Conversation;
            conversation.AddSystem("Conversation started");
            conversation.Receive(bot, "Hi! I can help with **orders**, invoices and reports. Try a quick reply or type below.", new[] { "Show open orders", "Summarize invoices", "Talk to a person" });

            async Task Reply(string text)
            {
                await Task.Delay(500);
                conversation.SetTyping(bot, true);
                await Task.Delay(1400);
                conversation.Receive(bot, text.Contains("order", StringComparison.OrdinalIgnoreCase)
                    ? "You have **12 open orders**; 3 are overdue. See the [order list](orders) for details."
                    : $"You said: *{text}*. This demo replies locally; connect a real service in MessageSubmitted.", new[] { "Thanks", "Tell me more" });
            }

            chat.MessageSubmitted += async (_, e) =>
            {
                conversation.SetStatus(e.Message, ChatMessageStatus.Sent);
                host.Metric($"{conversation.Messages.Count} messages");
                await Task.Delay(400);
                conversation.SetStatus(e.Message, ChatMessageStatus.Delivered);
                await Reply(e.Message.Text);
                conversation.SetStatus(e.Message, ChatMessageStatus.Read);
            };
            chat.LinkClicked += (_, e) => host.Log("Chat.Link", e.LinkText ?? string.Empty);
            chat.AttachmentClicked += (_, a) => host.Log("Chat.Attachment", a.FileName);
            host.Toggle("Avatars", true, v => chat.ShowAvatars = v);
            host.Toggle("Author names", true, v => chat.ShowAuthorNames = v);
            host.Toggle("Timestamps", true, v => chat.ShowTimestamps = v);
            host.Toggle("Attachments", true, v => chat.AllowAttachments = v);
            host.Button("Incoming message", () => conversation.Receive(bot, "A new **incoming** message arrived."));
            host.Button("Typing…", () => conversation.SetTyping(bot, !conversation.TypingAuthors.Contains(bot)));
            host.Button("Message with files", () =>
            {
                var message = conversation.Send("Here are the files you asked for.", new[] { new ChatAttachment { FileName = "Q4-report.pdf", SizeBytes = 482_113 }, new ChatAttachment { FileName = "chart.png", SizeBytes = 91_200 } });
                conversation.SetStatus(message, ChatMessageStatus.Delivered);
            });
            host.Metric($"{conversation.Messages.Count} messages");
            return chat;
        }
    };

    // ---- 5. InlineAIAssistant -----------------------------------------------------------

    private static GridScenario InlineAI() => new()
    {
        Id = "interactive-inlineai",
        Title = "InlineAIAssistant - Rewrite, Summarize, Expand and Tone",
        Description = "Select text in the box (a sparkle button appears) or press Ctrl+Space. Pick an action or type an instruction; the suggestion is previewed before it replaces the selection.",
        Features = new[] { "Summarize, rewrite, expand, tone", "Free-form instruction", "Preview with accept, retry and discard", "Works with any TextBox or RichTextBox", "Pluggable provider and commands" },
        Documentation = "EnterpriseInlineAIAssistant is a component: set Target to a text box and it adds the sparkle button, the Ctrl+Space shortcut and the action popup. The result replaces only the selection (or all text when nothing is selected) and only after you accept.",
        Usage = "var assistant = new EnterpriseInlineAIAssistant { Target = notesBox, Provider = new MyProvider() };\nassistant.Applying += (_, e) => e.Result = e.Result.Trim();",
        Api = "EnterpriseInlineAIAssistant -> Target, Provider, Commands, TriggerKey, ShowSelectionButton, Context, Show, Close, RunAsync, Apply, Applying, Applied",
        Accessibility = "The popup is a normal window with keyboard navigation: arrows choose an action, Enter runs or accepts, Escape closes or cancels.",
        Build = host =>
        {
            var box = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 11f),
                Text = "i think we should probably utilize a lot of caching in order to make the dashboard faster. it's really very important for our users, and they don't like waiting. maybe we can also add filters, perhaps in the header, so the data is easier to find."
            };
            var assistant = new EnterpriseInlineAIAssistant { Target = box, Context = "Product update note" };
            assistant.Applied += (_, _) => host.Metric("Suggestion applied to the text.");
            assistant.StateChanged += (_, _) => host.Metric($"Assistant state: {assistant.State}");
            box.Disposed += (_, _) => assistant.Dispose();
            host.Button("Open assistant (Ctrl+Space)", () => { box.Focus(); assistant.Show(); });
            host.Toggle("Selection button", true, v => assistant.ShowSelectionButton = v);
            host.Button("Select first sentence", () => { box.Focus(); box.Select(0, Math.Max(0, box.Text.IndexOf('.') + 1)); });
            host.Metric("Select text and click the sparkle button, or press Ctrl+Space.");
            return box;
        }
    };

    // ---- 6. Label -----------------------------------------------------------------------

    private static GridScenario Label() => new()
    {
        Id = "interactive-label",
        Title = "Label - Ellipsis, Access Keys, Rotation and Rich Text",
        Description = "A text container with auto-ellipsis and a tooltip for cut text, access keys that focus the next field, text rotation, Markdown and HTML formatting with clickable links, and image alignment.",
        Features = new[] { "Auto-ellipsis and max lines", "Access keys (LabelFor)", "Rotation by 90 degrees", "Markdown and HTML inline formatting", "Image and text alignment" },
        Documentation = "EnterpriseLabelControl paints through the same markup engine as the chat bubbles and the rotator. Plain text supports &-mnemonics; Markdown and HTML modes support bold, italic, strike-through, code, colors and links.",
        Usage = "var label = new EnterpriseLabelControl { MarkupMode = LabelMarkupMode.Markdown, Text = \"**Total:** [open report](report)\" };\nlabel.LinkClicked += (_, e) => Open(e.LinkText);",
        Api = "EnterpriseLabelControl -> Text, MarkupMode, AutoEllipsis, MaxLines, WordWrap, TextAlign, TextRotation, Image, ImageAlign, TextImageRelation, UseMnemonic, LabelFor, LinkClicked",
        Accessibility = "The accessible name is the plain text without markup; the access key moves focus to LabelFor. Links can be activated with the pointer.",
        Build = host =>
        {
            var panel = Flow();
            Caption(panel, "Access key: press Alt+N to focus the name box");
            var name = new TextBox { Width = 220 };
            Put(panel, new EnterpriseLabelControl { Text = "&Name:", LabelFor = name, Width = 70, Height = 24, TextAlign = ContentAlignment.MiddleLeft });
            Put(panel, name, true);

            Caption(panel, "Auto-ellipsis (hover for the full text) and a three-line limit");
            Put(panel, new EnterpriseLabelControl { Text = "A very long single line of text that does not fit in the label and is shortened with an ellipsis.", WordWrap = false, Width = 260, Height = 24 });
            Put(panel, new EnterpriseLabelControl { Text = "This paragraph wraps over several lines but stops after three lines, so the rest is cut with an ellipsis at the end of the third line which you can see here.", MaxLines = 3, Width = 260, Height = 60 }, true);

            Caption(panel, "Markdown and HTML");
            var md = Put(panel, new EnterpriseLabelControl { MarkupMode = LabelMarkupMode.Markdown, Text = "**Bold**, *italic*, ~~gone~~, `code` and a [link](docs)", Width = 340, Height = 44 });
            var html = Put(panel, new EnterpriseLabelControl { MarkupMode = LabelMarkupMode.Html, Text = "<b>Total</b> <span style=\"color:#107c10\">+18%</span><br/><u>Underlined</u> and <a href=\"report\">open the report</a>", Width = 300, Height = 44 }, true);
            md.LinkClicked += (_, e) => host.Log("Label.Link", e.LinkText ?? string.Empty);
            html.LinkClicked += (_, e) => host.Log("Label.Link", e.LinkText ?? string.Empty);

            Caption(panel, "Rotation and image alignment");
            Put(panel, new EnterpriseLabelControl { Text = "Rotated 90°", TextRotation = LabelTextRotation.Rotate90, TextAlign = ContentAlignment.MiddleCenter, Width = 40, Height = 120 });
            Put(panel, new EnterpriseLabelControl { Text = "Rotated 270°", TextRotation = LabelTextRotation.Rotate270, TextAlign = ContentAlignment.MiddleCenter, Width = 40, Height = 120 });
            var icon = SystemIcons.Information.ToBitmap();
            var withImage = Put(panel, new EnterpriseLabelControl { Text = "Image before the text", Image = icon, TextImageRelation = TextImageRelation.ImageBeforeText, ImageAlign = ContentAlignment.MiddleCenter, TextAlign = ContentAlignment.MiddleLeft, Width = 220, Height = 50 });
            Put(panel, new EnterpriseLabelControl { Text = "Image above the text", Image = icon, TextImageRelation = TextImageRelation.ImageAboveText, ImageAlign = ContentAlignment.TopCenter, TextAlign = ContentAlignment.TopCenter, Width = 160, Height = 80 }, true);

            host.Combo("Text align:", new[] { "TopLeft", "MiddleCenter", "BottomRight" }, "TopLeft", v => withImage.TextAlign = Enum.Parse<ContentAlignment>(v));
            host.Toggle("Auto-ellipsis", true, v => panel.Controls.OfType<EnterpriseLabelControl>().ToList().ForEach(l => l.AutoEllipsis = v));
            host.Metric("Hover the shortened labels for the full text; click a link to log it.");
            return panel;
        }
    };

    // ---- 7. ProgressBar -----------------------------------------------------------------

    private static GridScenario ProgressBar() => new()
    {
        Id = "interactive-progressbar",
        Title = "ProgressBar - Determinate, Indeterminate, Gradient and Segmented",
        Description = "Progress with solid, gradient, striped or segmented fill, text overlays, a marquee mode, horizontal and vertical orientation, paused and error states and adjustable animation speed.",
        Features = new[] { "Determinate and indeterminate", "Gradient, striped, segmented fill", "Text overlay and formats", "Paused and error states", "Animation speed" },
        Documentation = "EnterpriseProgressBarControl animates the displayed value toward Value, so jumps look smooth. AnimationSpeed scales every animation and 0 turns them off. The overlay text switches color where the fill passes beneath it.",
        Usage = "var bar = new EnterpriseProgressBarControl { Fill = EnterpriseProgressFill.Striped, TextFormat = \"{0:0}% uploaded\" };\nbar.Value = 42;",
        Api = "EnterpriseProgressBarControl -> Value, Minimum, Maximum, Style, Fill, State, FillStartColor, FillEndColor, TextFormat, ShowText, AnimationSpeed, Orientation, Completed",
        Accessibility = "Exposed as a progress bar with the percentage as its value, or Busy while indeterminate.",
        Build = host =>
        {
            var panel = Flow();
            var bars = new List<EnterpriseProgressBarControl>();
            EnterpriseProgressBarControl Bar(string caption, EnterpriseProgressFill fill, int width = 360)
            {
                Caption(panel, caption);
                var bar = Put(panel, new EnterpriseProgressBarControl { Fill = fill, Width = width, Height = 24, Value = 0 }, true);
                bars.Add(bar);
                return bar;
            }

            Bar("Gradient (default)", EnterpriseProgressFill.Gradient);
            Bar("Solid with a text format", EnterpriseProgressFill.Solid).TextFormat = "{0:0}% uploaded";
            Bar("Striped", EnterpriseProgressFill.Striped);
            Bar("Segmented", EnterpriseProgressFill.Segmented).Height = 22;
            Caption(panel, "Indeterminate (marquee) and a custom caption");
            var marquee = Put(panel, new EnterpriseProgressBarControl { Style = EnterpriseProgressStyle.Indeterminate, Text = "Connecting…", Width = 360, Height = 24 }, true);
            Caption(panel, "States and a vertical bar");
            var paused = Put(panel, new EnterpriseProgressBarControl { State = EnterpriseProgressState.Paused, Value = 55, Width = 170, Height = 22 });
            var error = Put(panel, new EnterpriseProgressBarControl { State = EnterpriseProgressState.Error, Value = 80, Width = 170, Height = 22, Text = "Failed" });
            var vertical = Put(panel, new EnterpriseProgressBarControl { Orientation = Orientation.Vertical, Width = 26, Height = 120, Fill = EnterpriseProgressFill.Gradient, ShowText = false });
            bars.Add(vertical);

            var timer = new System.Windows.Forms.Timer { Interval = 60 };
            var running = false;
            timer.Tick += (_, _) =>
            {
                foreach (var b in bars) b.Value = b.Value >= 100 ? 0 : b.Value + 1;
                host.Metric($"Value {bars[0].Value}%");
            };
            panel.Disposed += (_, _) => timer.Dispose();
            host.Button("Start / stop simulation", () => { running = !running; timer.Enabled = running; });
            host.Button("Reset", () => bars.ForEach(b => b.Value = 0));
            host.Combo("Speed:", new[] { "0", "0.5", "1", "2", "4" }, "1", v =>
            {
                var speed = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                foreach (var b in bars.Append(marquee).Append(paused).Append(error)) b.AnimationSpeed = speed;
            });
            host.Toggle("Show text", true, v => bars.ForEach(b => b.ShowText = v && b != vertical));
            host.Metric("Press Start to animate the bars.");
            return panel;
        }
    };

    // ---- 8. Rating ----------------------------------------------------------------------

    private static GridScenario Rating() => new()
    {
        Id = "interactive-rating",
        Title = "Rating - Stars, Half Stars and Custom Symbols",
        Description = "Interactive scoring with whole, half or free precision, hover preview with a tooltip, read-only display, clear-on-repeat and custom symbol shapes.",
        Features = new[] { "Full, half and free precision", "Hover preview and tooltip", "Read-only mode", "Star, heart, circle, diamond and custom paths", "Keyboard and RTL support" },
        Documentation = "EnterpriseStarRatingControl keeps the value as a double and paints each symbol with the filled fraction, so half and free precision need no extra art. Provide SymbolPathFactory (or handle PaintSymbol) to use your own symbol.",
        Usage = "var rating = new EnterpriseStarRatingControl { Maximum = 5, Precision = RatingPrecision.Half, Descriptions = new[] { \"Poor\", \"Fair\", \"Good\", \"Great\", \"Excellent\" } };\nrating.ValueChanged += (_, _) => Save(rating.Value);",
        Api = "EnterpriseStarRatingControl -> Value, Maximum, Precision, ReadOnly, AllowClear, SymbolShape, SymbolPathFactory, Descriptions, TooltipFormatter, PaintSymbol, ValueChanged, HoverValueChanged",
        Accessibility = "Exposed as a slider with the value text (for example 3.5 out of 5). Arrow keys step by the precision, digits set the value and Delete clears it.",
        Build = host =>
        {
            var panel = Flow();
            var ratings = new List<EnterpriseStarRatingControl>();
            EnterpriseStarRatingControl Make(string caption, Action<EnterpriseStarRatingControl> setup)
            {
                Caption(panel, caption);
                var r = new EnterpriseStarRatingControl();
                setup(r);
                r.ValueChanged += (_, _) => host.Metric($"{caption}: {r.Value:0.##} of {r.Maximum}");
                ratings.Add(r);
                return Put(panel, r, true);
            }

            Make("Whole stars with descriptions", r => { r.Value = 3; r.ShowValueText = true; r.Descriptions = new[] { "Poor", "Fair", "Good", "Great", "Excellent" }; });
            Make("Half-star precision", r => { r.Precision = RatingPrecision.Half; r.Value = 3.5; r.ShowValueText = true; });
            Make("Free precision, read-only average", r => { r.Precision = RatingPrecision.Exact; r.Value = 4.27; r.ReadOnly = true; r.ShowValueText = true; });
            Make("Hearts, ten symbols", r => { r.SymbolShape = RatingSymbolShape.Heart; r.Maximum = 10; r.SymbolSize = 22; r.FillColor = Color.Crimson; r.Value = 6; r.Width = 300; });
            Make("Custom path (diamond with a notch)", r =>
            {
                r.SymbolPathFactory = b =>
                {
                    var path = new GraphicsPath();
                    path.AddPolygon(new[] { new PointF(b.X + b.Width / 2, b.Y), new PointF(b.Right, b.Y + b.Height * 0.4f), new PointF(b.X + b.Width / 2, b.Bottom), new PointF(b.X, b.Y + b.Height * 0.4f) });
                    return path;
                };
                r.FillColor = Color.DodgerBlue;
                r.Value = 2;
            });
            host.Combo("Precision:", Enum.GetNames<RatingPrecision>(), "Full", v => ratings[0].Precision = Enum.Parse<RatingPrecision>(v));
            host.Toggle("Read-only", false, v => ratings[0].ReadOnly = v);
            host.Toggle("Right to left", false, v => ratings[0].RightToLeft = v ? RightToLeft.Yes : RightToLeft.No);
            host.Metric("Hover to preview, click to rate, click the same value again to clear.");
            return panel;
        }
    };

    // ---- 9. Rotator ---------------------------------------------------------------------

    private static GridScenario Rotator() => new()
    {
        Id = "interactive-rotator",
        Title = "Rotator - Fade, Slide and Flip Transitions",
        Description = "A content ticker that cycles through formatted panels on a timer or on demand, with fade, slide and flip effects, pause on hover, indicators, arrows and a countdown bar.",
        Features = new[] { "Fade, slide and flip effects", "HTML and Markdown panels", "Auto-rotate with pause on hover", "Indicators, arrows and countdown bar", "Cancelable panel changes" },
        Documentation = "EnterpriseRotatorControl renders every panel into a cached face and composes the transition from two faces, so it stays smooth with any content. Space pauses or resumes; rotation also pauses while the control is hovered or focused.",
        Usage = "var rotator = new EnterpriseRotatorControl { Effect = RotatorEffect.SlideLeft, Interval = 5000 };\nrotator.AddPanel(\"<b>Welcome</b><br/>Read the <a href='news'>news</a>\", \"Hello\");",
        Api = "EnterpriseRotatorControl -> Panels, AddPanel, Effect, Interval, TransitionDuration, AutoRotate, PauseOnHover, Loop, ShowIndicators, ShowTimerBar, Next, Previous, GoTo, PanelChanging, PanelChanged",
        Accessibility = "Rotation pauses on hover and focus and can be paused with Space; the current panel is exposed as the control value so the content is available without waiting for the timer.",
        Build = host =>
        {
            var rotator = new EnterpriseRotatorControl { Height = 190, ShowTimerBar = true, Interval = 3500 };
            rotator.AddPanel("Quarterly revenue is <b>up <span style=\"color:#107c10\">18.4%</span></b> against plan.<br/>Read the <a href=\"report\">full report</a>.", "Q4 results");
            rotator.AddPanel("The new <i>approval workflow</i> is live in all regions.<br/>Questions? Contact <a href=\"help\">the service desk</a>.", "Release notes").Accent = Color.SeaGreen;
            rotator.AddPanel("**Maintenance window** on Saturday, 22:00–23:00 UTC.\n\nExpect short interruptions to `reporting` and exports.", "Heads up", LabelMarkupMode.Markdown).Accent = Color.Chocolate;
            rotator.AddPanel("Welcome <b>Alex Morgan</b> to the finance team!<br/>Say hello at the <u>Friday coffee break</u>.", "New colleague").Accent = Color.MediumPurple;
            rotator.PanelChanged += (_, _) => host.Metric($"Panel {rotator.SelectedIndex + 1} of {rotator.Panels.Count}");
            rotator.LinkClicked += (_, e) => host.Log("Rotator.Link", e.LinkText ?? string.Empty);
            host.Combo("Effect:", Enum.GetNames<RotatorEffect>(), "Fade", v => rotator.Effect = Enum.Parse<RotatorEffect>(v));
            host.Combo("Interval:", new[] { "2000", "3500", "6000" }, "3500", v => rotator.Interval = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture));
            host.Toggle("Auto-rotate", true, v => rotator.AutoRotate = v);
            host.Toggle("Pause on hover", true, v => rotator.PauseOnHover = v);
            host.Button("◀ Previous", rotator.Previous);
            host.Button("Next ▶", rotator.Next);
            host.Button("Pause / resume", rotator.TogglePause);
            host.Metric("Panel 1 of 4");
            var host2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };
            rotator.Dock = DockStyle.Top;
            host2.Controls.Add(rotator);
            return host2;
        }
    };

    // ---- 10. SmartPasteButton -----------------------------------------------------------

    private static GridScenario SmartPaste() => new()
    {
        Id = "interactive-smartpaste",
        Title = "SmartPasteButton - Clipboard Text into Form Fields",
        Description = "Copy an address, labeled lines, JSON, a spreadsheet row or code, then press Smart Paste: the text is classified and mapped into the bound fields. Use the sample buttons if you do not want to touch the clipboard.",
        Features = new[] { "Addresses, labeled lines, JSON, table rows, code", "Field kinds and aliases", "Optional model-backed mapper", "Cancelable preview and undo", "Any bound control type" },
        Documentation = "EnterpriseSmartPasteButton binds controls to fields. SmartPasteMapper classifies the text and maps it by label, by recognizing emails, phones, URLs, dates, amounts and address parts, or by header position; an ISmartPasteProvider can fill what is left.",
        Usage = "var paste = new EnterpriseSmartPasteButton();\npaste.Bind(emailBox, SmartPasteFieldKind.Email);\npaste.Bind(cityBox, SmartPasteFieldKind.City);\npaste.Pasting += (_, e) => e.Result.Assignments.RemoveAll(a => a.Confidence < 0.6);",
        Api = "EnterpriseSmartPasteButton -> Bind, Unbind, Bindings, PasteAsync, Undo, Provider, Pasting, Pasted; SmartPasteMapper -> Classify, Map",
        Accessibility = "The button reports how many fields were filled in its label (and to assistive technology); every change can be undone with Undo.",
        Build = host =>
        {
            var samples = new Dictionary<string, string>
            {
                ["Address block"] = "Jane Q Public\n1600 Pennsylvania Ave NW\nWashington, DC 20500\njane@example.org\n(202) 456-1111",
                ["Labeled lines"] = "Full name: Ada Lovelace\nE-mail = ada@analytical.io\nTelephone: +44 20 7946 0958\nStreet: 10 Downing Street\nCity: London\nPostcode: SW1A 2AA",
                ["JSON"] = "{\"name\":\"Bob Stone\",\"contact\":{\"email\":\"bob@stone.dev\",\"phone\":\"415-555-0134\"},\"city\":\"Austin\",\"state\":\"TX\",\"zip\":\"73301\"}",
                ["Spreadsheet row"] = "Name\tEmail\tCity\tZip\nCy Young\tcy@young.org\tOslo\t0150",
                ["Code snippet"] = "public class Order\n{\n    public int Id { get; set; }\n}"
            };

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            var paste = new EnterpriseSmartPasteButton { Width = 200 };
            var fields = new (string Name, SmartPasteFieldKind Kind)[]
            {
                ("Name", SmartPasteFieldKind.Name), ("Email", SmartPasteFieldKind.Email), ("Phone", SmartPasteFieldKind.Phone), ("Street", SmartPasteFieldKind.Street),
                ("City", SmartPasteFieldKind.City), ("State", SmartPasteFieldKind.Region), ("Zip", SmartPasteFieldKind.PostalCode), ("Snippet", SmartPasteFieldKind.Code)
            };
            foreach (var (name, kind) in fields)
            {
                var box = new TextBox { Name = name, Width = 320, Multiline = kind == SmartPasteFieldKind.Code, Height = kind == SmartPasteFieldKind.Code ? 64 : 24 };
                table.Controls.Add(new Label { Text = name + ":", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
                table.Controls.Add(box);
                paste.Bind(box, kind);
            }

            table.Controls.Add(new Label());
            table.Controls.Add(paste);
            paste.Pasted += (_, e) => host.Metric($"{e.Result.ContentKind}: filled {e.FilledFields.Count} ({string.Join(", ", e.FilledFields)}); unmatched: {e.Result.UnmatchedFields.Count}");
            var sampleCombo = host.Combo("Sample:", samples.Keys, "Address block", _ => { });
            host.Button("Paste selected sample", () => _ = paste.PasteAsync(samples[sampleCombo.SelectedItem?.ToString() ?? "Address block"]));
            host.Button("Copy sample to clipboard", () =>
            {
                Clipboard.SetText(samples[sampleCombo.SelectedItem?.ToString() ?? "Address block"]);
                host.Metric("Sample copied: now press Smart Paste.");
            });
            host.Button("Undo", paste.Undo);
            host.Metric("Choose a sample, press 'Paste selected sample', then try Undo.");
            return table;
        }
    };

    // ---- 11. SpeechToTextButton ---------------------------------------------------------

    private static GridScenario SpeechToText() => new()
    {
        Id = "interactive-speechtotext",
        Title = "SpeechToTextButton - Dictation with a Live Waveform",
        Description = "Toggle the microphone: audio is streamed to a speech provider, a waveform follows the voice level, partial text shows on the button and the final transcript is inserted into the text box.",
        Features = new[] { "Microphone toggle with waveform", "Streaming provider interface", "Partial and final transcripts", "Insert at caret, append or replace", "Simulated source for demos and tests" },
        Documentation = "EnterpriseSpeechToTextButton connects an IAudioInput (the Windows microphone by default) to an ISpeechRecognitionProvider through a SpeechDictationSession. Choose Simulated to try the control without a microphone or a recognition service.",
        Usage = "var mic = new EnterpriseSpeechToTextButton { Target = notesBox, Provider = new MyRecognizer() };\nmic.FinalTranscript += (_, text) => Log(text);",
        Api = "EnterpriseSpeechToTextButton -> Provider, AudioInputFactory, Target, InsertMode, Language, MaxDuration, StartListeningAsync, StopListeningAsync, Cancel, PartialTranscript, FinalTranscript, Error",
        Accessibility = "Reported as a toggle (pressed while listening) with the state as its value. Escape cancels without inserting text.",
        Build = host =>
        {
            var panel = Flow();
            Caption(panel, "Dictate into the box below");
            var target = Put(panel, new TextBox { Multiline = true, Width = 460, Height = 110, Font = new Font("Segoe UI", 10.5f) }, true);
            var mic = Put(panel, new EnterpriseSpeechToTextButton { Target = target, Width = 260, Height = 44, Provider = new ScriptedSpeechProvider("please send the quarterly report to the finance team today") { BytesPerWord = 6400 }, AudioInputFactory = () => new SyntheticAudioInput() });
            mic.StateChanged += (_, _) => host.Metric($"State: {mic.State}");
            mic.PartialTranscript += (_, t) => host.Metric($"Hearing: {t}");
            mic.FinalTranscript += (_, t) => host.Log("SpeechToText.Final", t);
            mic.Error += (_, ex) => host.Metric("Error: " + ex.Message);
            host.Combo("Source:", new[] { "Simulated voice", "Microphone (needs a provider)" }, "Simulated voice", v =>
            {
                if (v.StartsWith("Sim", StringComparison.Ordinal))
                {
                    mic.AudioInputFactory = () => new SyntheticAudioInput();
                    mic.Provider = new ScriptedSpeechProvider("please send the quarterly report to the finance team today") { BytesPerWord = 6400 };
                }
                else
                {
                    mic.AudioInputFactory = () => new WaveInAudioInput();
                    mic.Provider = new ScriptedSpeechProvider("microphone captured, plug a real recognizer into Provider");
                }
            });
            host.Combo("Insert:", Enum.GetNames<SpeechInsertMode>(), "AtCaret", v => mic.InsertMode = Enum.Parse<SpeechInsertMode>(v));
            host.Toggle("Waveform", true, v => mic.ShowWaveform = v);
            host.Toggle("Partial text", true, v => mic.ShowPartialText = v);
            host.Button("Clear text", () => target.Clear());
            host.Metric("Press the button, speak (or wait for the simulated voice) and press it again.");
            return panel;
        }
    };

    // ---- 12. StepProgressBar ------------------------------------------------------------

    private static GridScenario StepProgress() => new()
    {
        Id = "interactive-stepprogress",
        Title = "StepProgressBar - Workflow Steps with Validation",
        Description = "Show where a user is in a multi-step process: completed, active and upcoming steps, warning and error markers, optional steps, click navigation and validation that blocks moving forward.",
        Features = new[] { "Completed, active, upcoming, error, warning, skipped", "Click and keyboard navigation", "Forward validation", "Horizontal and vertical", "Optional steps" },
        Documentation = "EnterpriseStepProgressBarControl wraps a StepFlow. StepChanging fires before moving forward; set Cancel to reject, and the step is marked with an error and a message. Going back is always allowed; jumping ahead needs every step in between to be complete (or AllowJumpAhead).",
        Usage = "var steps = new EnterpriseStepProgressBarControl();\nsteps.AddStep(\"Account\"); steps.AddStep(\"Details\"); steps.AddStep(\"Review\");\nsteps.StepChanging += (_, e) => { if (!Valid(e.From)) { e.Cancel = true; e.Message = \"Fix the errors\"; } };",
        Api = "EnterpriseStepProgressBarControl -> AddStep, Next, Previous, Finish, Reset, CurrentStep, SetStatus, Flow, AllowJumpAhead, ClickableSteps, Orientation, StepChanging, StepChanged",
        Accessibility = "The value reads 'Step 2 of 4: Details (Active)'. Arrow keys move between steps and Enter or Space opens the focused step.",
        Build = host =>
        {
            var panel = Flow();
            Caption(panel, "Type a name to unlock the next step (validation runs when moving forward)");
            var name = Put(panel, new TextBox { Width = 240, PlaceholderText = "Name (required)" }, true);
            var steps = Put(panel, new EnterpriseStepProgressBarControl { Width = 700, Height = 110 }, true);
            steps.AddStep("Account", "Create the login");
            steps.AddStep("Profile", "Required name");
            steps.AddStep("Preferences", "Optional", null, true);
            steps.AddStep("Review", "Check and confirm");
            steps.StepChanging += (_, e) =>
            {
                if (e.To > e.From && e.From == 1 && name.Text.Trim().Length == 0)
                {
                    e.Cancel = true;
                    e.Message = "Enter a name first";
                }
            };
            steps.StepChanged += (_, _) => host.Metric($"Current step: {steps.CurrentStep + 1} of {steps.StepCount}{(steps.Flow.IsFinished ? " (finished)" : string.Empty)}");
            steps.StepClicked += (_, i) => host.Log("StepProgress.Click", $"Step {i + 1}");
            Caption(panel, "Vertical variant with statuses set from code");
            var vertical = Put(panel, new EnterpriseStepProgressBarControl { Orientation = Orientation.Vertical, Width = 280, Height = 220 }, true);
            foreach (var t in new[] { "Order received", "Payment check", "Packing", "Shipped" }) vertical.AddStep(t);
            vertical.SetStatus(0, StepStatus.Completed);
            vertical.SetStatus(1, StepStatus.Warning, "Awaiting bank");
            vertical.SetStatus(2, StepStatus.Pending);

            host.Button("◀ Back", () => steps.Previous());
            host.Button("Next ▶", () => steps.Next());
            host.Button("Finish", () => steps.Finish());
            host.Button("Reset", steps.Reset);
            host.Toggle("Allow jump ahead", false, v => steps.AllowJumpAhead = v);
            host.Toggle("Clickable steps", true, v => steps.ClickableSteps = v);
            host.Metric("Current step: 1 of 4");
            return panel;
        }
    };

    // ---- 13. TrackBar -------------------------------------------------------------------

    private static GridScenario TrackBar() => new()
    {
        Id = "interactive-trackbar",
        Title = "TrackBar - Single, Range and Discrete Sliders",
        Description = "Continuous or stepped sliders with one thumb or a range of two, tick marks and labels, a value tooltip while dragging and full keyboard control.",
        Features = new[] { "Single and range (two thumbs)", "Discrete steps and ticks", "Value tooltip and custom formats", "Horizontal and vertical", "Keyboard step, page, home and end" },
        Documentation = "EnterpriseTrackBarControl separates the live ValueChanged event from ValueCommitted (raised when a drag or keyboard step ends), so expensive work can wait for the commit. In range mode MinimumGap keeps the thumbs apart.",
        Usage = "var range = new EnterpriseTrackBarControl { Mode = TrackSelectionMode.Range, Minimum = 0, Maximum = 1000, ValueLow = 200, ValueHigh = 800, ValueFormat = \"{0:C0}\" };\nrange.ValueCommitted += (_, _) => ApplyFilter(range.ValueLow, range.ValueHigh);",
        Api = "EnterpriseTrackBarControl -> Minimum, Maximum, Value, ValueLow, ValueHigh, Mode, Step, SmallChange, LargeChange, TickFrequency, TickStyle, ShowTickLabels, ValueFormat, MinimumGap, Orientation, ValueChanged, ValueCommitted",
        Accessibility = "Exposed as a slider whose value is the formatted number (or 'low to high' in range mode). Arrows step, PageUp and PageDown take large steps, Tab moves between the two thumbs.",
        Build = host =>
        {
            var panel = Flow();
            Caption(panel, "Single thumb with ticks and labels");
            var single = Put(panel, new EnterpriseTrackBarControl { Width = 420, Height = 60, Value = 40, TickStyle = TrackTickStyle.BottomRight, ShowTickLabels = true, TickFrequency = 20 }, true);
            Caption(panel, "Price range with two thumbs");
            var range = Put(panel, new EnterpriseTrackBarControl { Width = 420, Height = 50, Mode = TrackSelectionMode.Range, Minimum = 0, Maximum = 1000, ValueLow = 200, ValueHigh = 750, Step = 10, LargeChange = 100, MinimumGap = 50, ValueFormat = "{0:C0}" }, true);
            Caption(panel, "Discrete steps (five levels) and a vertical slider");
            var levels = Put(panel, new EnterpriseTrackBarControl { Width = 300, Height = 56, Minimum = 1, Maximum = 5, Step = 1, Value = 3, TickFrequency = 1, TickStyle = TrackTickStyle.Both, ShowTickLabels = true, ValueFormatter = v => new[] { "", "Low", "Medium-low", "Medium", "Medium-high", "High" }[(int)v] });
            var vertical = Put(panel, new EnterpriseTrackBarControl { Orientation = Orientation.Vertical, Width = 56, Height = 160, Value = 65, TickStyle = TrackTickStyle.BottomRight, TickFrequency = 25 });
            single.ValueChanged += (_, _) => host.Metric($"Single: {single.Value:0.##}");
            range.ValueChanged += (_, _) => host.Metric($"Range: {range.ValueLow:C0} to {range.ValueHigh:C0}");
            range.ValueCommitted += (_, _) => host.Log("TrackBar.Committed", $"{range.ValueLow:0}-{range.ValueHigh:0}");
            levels.ValueChanged += (_, _) => host.Metric($"Level: {levels.Format(levels.Value)}");
            vertical.ValueChanged += (_, _) => host.Metric($"Vertical: {vertical.Value:0}");
            host.Toggle("Tooltip", true, v => { single.ShowTooltip = v; range.ShowTooltip = v; levels.ShowTooltip = v; vertical.ShowTooltip = v; });
            host.Toggle("Enabled", true, v => { single.Enabled = v; range.Enabled = v; levels.Enabled = v; vertical.Enabled = v; });
            host.Button("Reset", () => { single.Value = 40; range.ValueLow = 200; range.ValueHigh = 750; levels.Value = 3; vertical.Value = 65; });
            host.Metric("Drag a thumb or use the arrow keys (Tab switches thumbs in the range slider).");
            return panel;
        }
    };

    // ---- 14. WaitingBar -----------------------------------------------------------------

    private static GridScenario WaitingBar() => new()
    {
        Id = "interactive-waitingbar",
        Title = "WaitingBar - Busy Indicators",
        Description = "Looping animations for background work: spinning dots, a ring, wave bars, bouncing blocks and a marquee bar, with optional captions, custom colors, speed and direction.",
        Features = new[] { "Five animation modes", "Caption right or below", "Speed, direction, colors, sizes", "Start and stop", "Stops when hidden" },
        Documentation = "EnterpriseWaitingBarControl only runs its animation clock while it is visible and waiting, so idle indicators cost nothing. Set Text for a caption, HideWhenIdle to hide the graphic when stopped.",
        Usage = "var busy = new EnterpriseWaitingBarControl { Animation = WaitingBarAnimation.Ring, Text = \"Loading…\" };\nbusy.StartWaiting(); await LoadAsync(); busy.StopWaiting();",
        Api = "EnterpriseWaitingBarControl -> Animation, IsWaiting, StartWaiting, StopWaiting, Text, TextPosition, AnimationSpeed, Reversed, IndicatorSize, ElementCount, IndicatorColor, HideWhenIdle",
        Accessibility = "Reported as a progress indicator in the Busy state with its caption as the value.",
        Build = host =>
        {
            var panel = Flow();
            var bars = new List<EnterpriseWaitingBarControl>();
            foreach (var animation in Enum.GetValues<WaitingBarAnimation>())
            {
                Caption(panel, animation.ToString());
                bars.Add(Put(panel, new EnterpriseWaitingBarControl { Animation = animation, Text = animation == WaitingBarAnimation.Marquee ? "Importing records…" : "Working…", Width = animation == WaitingBarAnimation.Marquee ? 360 : 220, Height = animation == WaitingBarAnimation.Marquee ? 40 : 52 }, true));
            }

            Caption(panel, "Caption below, custom color and size");
            bars.Add(Put(panel, new EnterpriseWaitingBarControl { Animation = WaitingBarAnimation.Ring, TextPosition = WaitingTextPosition.Below, Text = "Please wait", IndicatorColor = Color.SeaGreen, IndicatorSize = 40, Width = 160, Height = 100 }, true));
            host.Toggle("Waiting", true, v => bars.ForEach(b => b.IsWaiting = v));
            host.Toggle("Reversed", false, v => bars.ForEach(b => b.Reversed = v));
            host.Combo("Speed:", new[] { "0.5", "1", "2", "4" }, "1", v => bars.ForEach(b => b.AnimationSpeed = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
            host.Metric("All indicators animate while Waiting is on.");
            return panel;
        }
    };

    // ---- 15. VirtualKeyboard ------------------------------------------------------------

    private static GridScenario VirtualKeyboard() => new()
    {
        Id = "interactive-virtualkeyboard",
        Title = "VirtualKeyboard - QWERTY, Numeric and Symbol Layers",
        Description = "An on-screen keyboard for touch and kiosk use: QWERTY with shift, caps lock and a symbols layer, a numeric pad, symbol layouts and your own layers. It never takes focus from the text box it types into.",
        Features = new[] { "QWERTY, numeric, symbols and custom layouts", "Shift, caps lock and layers", "Key repeat and press animation", "Types into a control or the focused window", "Optional keyboard navigation" },
        Documentation = "EnterpriseVirtualKeyboardControl delivers keys to Target, raises KeyPressed for every key and can inject into the focused window with SystemInput mode. Build a KeyboardLayout from KeyboardLayer, KeyRow and KeyDef to create any layout, including command keys.",
        Usage = "var keyboard = new EnterpriseVirtualKeyboardControl { Target = amountBox, Kind = VirtualKeyboardKind.Numeric };\nkeyboard.KeyPressed += (_, e) => Log(e.Key.Label);",
        Api = "EnterpriseVirtualKeyboardControl -> Kind, Layout, ActiveLayer, Target, InjectionMode, KeyRepeat, HideOnClose, AllowKeyboardNavigation, KeyPressed, TextEntered, CommandInvoked, CloseRequested; KeyboardLayout, KeyDef, KeyRow",
        Accessibility = "The keyboard does not take focus unless AllowKeyboardNavigation is on; then arrows move between keys and Enter or Space presses the key. Shift and caps lock state are shown on the keys.",
        Build = host =>
        {
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            var box = new TextBox { Dock = DockStyle.Top, Multiline = true, Height = 70, Font = new Font("Segoe UI", 12f), Text = "Type with the keys below" };
            var keyboard = new EnterpriseVirtualKeyboardControl { Dock = DockStyle.Fill, Target = box };
            keyboard.TextEntered += (_, t) => host.Metric($"Typed: {(t == "\n" ? "Enter" : t)}   |   text length {box.TextLength}");
            keyboard.CommandInvoked += (_, c) => host.Log("VirtualKeyboard.Command", c);
            keyboard.CloseRequested += (_, _) => host.Log("VirtualKeyboard.Close", "Hide key pressed");
            root.Controls.Add(keyboard);
            root.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
            root.Controls.Add(box);

            KeyboardLayout Custom()
            {
                KeyRow Row(params KeyDef[] keys) => new(keys);
                return new KeyboardLayout("Order entry", new[]
                {
                    new KeyboardLayer("main", new[]
                    {
                        Row(KeyDef.Char("7"), KeyDef.Char("8"), KeyDef.Char("9"), KeyDef.CommandKey("Qty +", "qty-up")),
                        Row(KeyDef.Char("4"), KeyDef.Char("5"), KeyDef.Char("6"), KeyDef.CommandKey("Qty -", "qty-down")),
                        Row(KeyDef.Char("1"), KeyDef.Char("2"), KeyDef.Char("3"), KeyDef.Special("⌫", KeyKind.Backspace, 1)),
                        Row(KeyDef.Char("0", null), KeyDef.Char("."), KeyDef.CommandKey("Add item", "add", 2))
                    })
                });
            }

            host.Combo("Layout:", new[] { "Qwerty", "Numeric", "Symbols", "Custom" }, "Qwerty", v =>
            {
                if (v == "Custom") keyboard.Layout = Custom();
                else keyboard.Kind = Enum.Parse<VirtualKeyboardKind>(v);
            });
            host.Toggle("Key repeat", true, v => keyboard.KeyRepeat = v);
            host.Toggle("Click sound", false, v => keyboard.KeyClickSound = v);
            host.Toggle("Keyboard navigation", false, v => keyboard.AllowKeyboardNavigation = v);
            host.Button("Clear text", () => box.Clear());
            host.Metric("Click the keys (hold Backspace or a letter to repeat).");
            return root;
        }
    };

    // ---- 16. Callout --------------------------------------------------------------------

    private static GridScenario Callout() => new()
    {
        Id = "interactive-callout",
        Title = "Callout - Speech-Bubble Popups",
        Description = "A popup anchored to a control with an arrow that points at it: choose the side (or let it pick one that fits), severity, shadow, action buttons and how it closes (button, Escape, click outside, body click or timeout).",
        Features = new[] { "Arrow on any side with auto placement", "Severity, glyph and formatted text", "Soft shadow", "Action buttons", "Close triggers and auto-close" },
        Documentation = "EnterpriseCalloutControl is a component, not a window you place: call Show(target) and it creates a non-activating popup that follows the target when the form moves and closes when the target is hidden or disposed.",
        Usage = "var callout = new EnterpriseCalloutControl { Title = \"Tip\", Message = \"Press **F5** to refresh.\", MarkupMode = LabelMarkupMode.Markdown };\ncallout.Actions.Add(new NotificationAction(\"ok\", \"Got it\", true));\ncallout.Show(refreshButton);",
        Api = "EnterpriseCalloutControl -> Show, ShowAt, Close, Title, Message, MarkupMode, Severity, Placement, ArrowSize, ShowShadow, ShowCloseButton, CloseOnClickOutside, CloseOnEscape, AutoCloseInterval, Actions, ActionInvoked, Closed",
        Accessibility = "The popup never takes focus, so it does not interrupt typing; Escape closes it. Keep the message short and put essential information in the form itself as well.",
        Build = host =>
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var target = new EnterpriseButtonControl { Text = "Target button", Glyph = "◎", Location = new Point(300, 150), Size = new Size(160, 40) };
            var field = new TextBox { Location = new Point(40, 40), Width = 220, PlaceholderText = "Click here for a hint" };
            panel.Controls.Add(target);
            panel.Controls.Add(field);

            var callout = new EnterpriseCalloutControl
            {
                Title = "Smart filters",
                Message = "Pin a filter with **Ctrl+P** to keep it across sessions. See the [help page](help) for more tips.",
                MarkupMode = LabelMarkupMode.Markdown,
                AutoCloseInterval = 0
            };
            callout.Actions.Add(new NotificationAction("ok", "Got it", true));
            callout.Actions.Add(new NotificationAction("later", "Remind me later"));
            callout.ActionInvoked += (_, e) => host.Log("Callout.Action", e.ActionId);
            callout.Closed += (_, e) => host.Metric($"Callout closed: {e.Reason}");
            callout.LinkClicked += (_, e) => host.Log("Callout.Link", e.LinkText ?? string.Empty);
            panel.Disposed += (_, _) => callout.Dispose();

            var hint = new EnterpriseCalloutControl { Title = "Required field", Message = "Enter at least 3 characters.", Severity = NotificationSeverity.Warning, Placement = CalloutPlacement.Right, CloseOnClickOutside = true, ShowCloseButton = false, AutoCloseInterval = 4000 };
            panel.Disposed += (_, _) => hint.Dispose();
            field.Enter += (_, _) => { if (field.Text.Length < 3 && !hint.IsOpen) hint.Show(field); };
            target.Click += (_, _) => callout.Show(target);

            host.Combo("Placement:", Enum.GetNames<CalloutPlacement>(), "Auto", v => callout.Placement = Enum.Parse<CalloutPlacement>(v));
            host.Combo("Severity:", Enum.GetNames<NotificationSeverity>(), "Info", v => callout.Severity = Enum.Parse<NotificationSeverity>(v));
            host.Toggle("Shadow", true, v => callout.ShowShadow = v);
            host.Toggle("Close on click outside", true, v => callout.CloseOnClickOutside = v);
            host.Toggle("Body click closes", false, v => callout.CloseOnBodyClick = v);
            host.Button("Show at target", () => callout.Show(target));
            host.Button("Close", callout.Close);
            host.Metric("Click the target button; focus the text box for a validation hint.");
            return panel;
        }
    };

    // ---- 17. NotifyIcon -----------------------------------------------------------------

    private static GridScenario NotifyIcon() => new()
    {
        Id = "interactive-notifyicon",
        Title = "NotifyIcon - Tray Icon, Toasts and Unread Badge",
        Description = "A tray icon that shows themed toast alerts above the taskbar, keeps a notification history, counts unread notifications on the icon itself and runs action buttons.",
        Features = new[] { "Tray icon with unread badge", "Stacked themed toasts", "Action buttons and click events", "Notification history", "Thread-safe Notify" },
        Documentation = "EnterpriseNotifyIcon wraps the tray icon and a NotificationCenter. Notify adds an entry and shows a toast (Custom), a balloon (Native) or nothing; the unread count is drawn on the tray icon and in its tooltip until notifications are read through a click, an action or MarkAllRead.",
        Usage = "var tray = new EnterpriseNotifyIcon { Icon = AppIcon, Text = \"My app\", Visible = true };\ntray.ActionInvoked += (_, e) => Open(e.Notification);\ntray.Notify(\"Build finished\", \"All tests passed.\", NotificationSeverity.Success, new NotificationAction(\"open\", \"Open\", true));",
        Api = "EnterpriseNotifyIcon -> Notify, Center, Icon, Text, Visible, ShowBadge, ToastMode, ToastPlacement, MaxVisibleToasts, UnreadCount, MarkAllRead, NotificationClicked, ActionInvoked, ToastClosed",
        Accessibility = "The tooltip includes the unread count; toasts never take focus. Because toasts disappear, every notification also stays in the history list.",
        Build = host =>
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Segoe UI", 9.5f) };
            panel.Controls.Add(list);
            var tray = new EnterpriseNotifyIcon { Icon = SystemIcons.Information, Text = "EnterpriseUI demo", Visible = true };
            panel.Disposed += (_, _) => tray.Dispose();

            void Refresh()
            {
                list.BeginUpdate();
                list.Items.Clear();
                foreach (var n in tray.Center.Items.Reverse()) list.Items.Add($"{(n.IsRead ? "   " : "● ")}[{n.Severity}] {n.Title}: {n.Message}");
                list.EndUpdate();
                host.Metric($"Unread: {tray.UnreadCount} of {tray.Center.Items.Count}");
            }

            tray.Center.Changed += (_, _) => Refresh();
            tray.NotificationClicked += (_, e) => host.Log("NotifyIcon.Clicked", e.Notification.Title);
            tray.ActionInvoked += (_, e) => host.Log("NotifyIcon.Action", $"{e.Notification.Title}: {e.ActionId}");

            var counter = 0;
            host.Button("Info", () => tray.Notify("Sync complete", $"Update #{++counter} finished without errors."));
            host.Button("Success + action", () => tray.Notify("Build finished", "All 412 tests passed.", NotificationSeverity.Success, new NotificationAction("open", "Open report", true), new NotificationAction("dismiss", "Dismiss")));
            host.Button("Warning", () => tray.Notify("Disk space low", "Only 4% of the drive is free.", NotificationSeverity.Warning));
            host.Button("Error", () => tray.Notify("Upload failed", "The server rejected the file (HTTP 413).", NotificationSeverity.Error, new NotificationAction("retry", "Retry", true)));
            host.Button("Mark all read", tray.MarkAllRead);
            host.Button("Clear history", () => tray.Center.Clear());
            host.Combo("Placement:", Enum.GetNames<ToastPlacement>(), "BottomRight", v => tray.ToastPlacement = Enum.Parse<ToastPlacement>(v));
            host.Combo("Mode:", Enum.GetNames<NotifyToastMode>(), "Custom", v => tray.ToastMode = Enum.Parse<NotifyToastMode>(v));
            host.Toggle("Badge on icon", true, v => tray.ShowBadge = v);
            Refresh();
            return panel;
        }
    };
}
