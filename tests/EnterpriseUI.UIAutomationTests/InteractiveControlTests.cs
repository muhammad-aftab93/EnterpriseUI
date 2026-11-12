using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Drives the interactive controls like a user and checks state, events, keyboard handling and that something is painted.</summary>
public class InteractiveControlTests
{
    private static Form NewHost(int width = 800, int height = 500)
    {
        var form = new Form { ClientSize = new Size(width, height), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        return form;
    }

    private static T Show<T>(Form host, T control, int x = 10, int y = 10) where T : Control
    {
        control.Location = new Point(x, y);
        host.Controls.Add(control);
        if (!host.Visible)
        {
            host.Show();
            Pump(40);
        }

        return control;
    }

    private static int Scale(Control c, int value) => (int)Math.Round(value * c.DeviceDpi / 96.0);

    private static void AssertPaints(Control control, string name)
    {
        using var bitmap = Ui.Render(control);
        Assert.True(Ui.DistinctColors(bitmap) >= 3, name + " rendered a blank surface");
        var shots = Environment.GetEnvironmentVariable("EUI_SHOTS");
        if (!string.IsNullOrEmpty(shots))
        {
            Directory.CreateDirectory(shots);
            bitmap.Save(Path.Combine(shots, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    // ---- buttons ----

    [Fact]
    public void ButtonsClickToggleAndGroup()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "&Save", Glyph = "✔", BadgeCount = 120 });
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            Ui.Click(button, 20, 10);
            Ui.Key(button, Keys.Space);
            Assert.Equal(2, clicks);
            Invoke(button, "ProcessMnemonic", 's');
            Assert.Equal(3, clicks);
            button.Enabled = false;
            Ui.Click(button, 20, 10);
            Assert.Equal(3, clicks);
            Assert.Equal("99+", button.AccessibilityObject.Value);

            var a = Show(host, new EnterpriseToggleButtonControl { Text = "A", GroupName = "g" }, 10, 60);
            var b = Show(host, new EnterpriseToggleButtonControl { Text = "B", GroupName = "g" }, 170, 60);
            var free = Show(host, new EnterpriseToggleButtonControl { Text = "Free", ThreeState = true }, 330, 60);
            Ui.Click(a, 20, 10);
            Assert.True(a.Checked);
            Ui.Click(b, 20, 10);
            Assert.True(b.Checked);
            Assert.False(a.Checked);
            Ui.Click(b, 20, 10);
            Assert.True(b.Checked);
            Ui.Click(free, 20, 10);
            Assert.Equal(CheckState.Checked, free.CheckState);
            Ui.Click(free, 20, 10);
            Assert.Equal(CheckState.Indeterminate, free.CheckState);
            Ui.Click(free, 20, 10);
            Assert.Equal(CheckState.Unchecked, free.CheckState);
            Assert.True(free.AccessibilityObject.State.HasFlag(AccessibleStates.Focusable));

            foreach (Control c in new Control[] { button, a, b, free }) AssertPaints(c, "button-" + c.GetType().Name + c.Text.Length);
            host.Dispose();
        });
    }

    [Fact]
    public void SplitButtonPrimaryAndMenu()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var split = Show(host, new EnterpriseSplitButtonControl { Text = "Export" });
            var picked = new List<string>();
            var primary = 0;
            split.PrimaryActionClicked += (_, _) => primary++;
            split.MenuItemSelected += item => picked.Add(item);
            split.AddOption("PDF");
            split.AddOption("Excel");

            Ui.Click(split, 20, 10);
            Assert.Equal(1, primary);
            Ui.Key(split, Keys.Enter);
            Assert.Equal(2, primary);

            Ui.Click(split, split.Width - 8, 10);
            Pump(60);
            Assert.True(split.IsDropDownOpen);
            Assert.Equal(2, primary);
            split.MenuItems[0].PerformClick();
            Pump(60);
            Assert.Equal(new[] { "PDF" }, picked);

            var drop = Show(host, new EnterpriseDropDownButtonControl { Text = "Actions" }, 10, 70);
            drop.AddOption("One");
            Ui.Key(drop, Keys.Down);
            Pump(60);
            Assert.True(drop.IsDropDownOpen);
            AssertPaints(split, "split");
            AssertPaints(drop, "dropdown");
            host.Dispose();
        });
    }

    // ---- progress, track bar, rating ----

    [Fact]
    public void ProgressBarClampsRaisesCompletedAndReportsValue()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var bar = Show(host, new EnterpriseProgressBarControl { Fill = EnterpriseProgressFill.Striped, AnimationSpeed = 0 });
            var completed = 0;
            bar.Completed += (_, _) => completed++;
            bar.Value = 40;
            Assert.Equal("40%", bar.AccessibilityObject.Value);
            bar.Value = 500;
            Assert.Equal(100, bar.Value);
            bar.Value = 100;
            Assert.Equal(1, completed);
            bar.Maximum = 50;
            Assert.Equal(50, bar.Value);
            bar.Style = EnterpriseProgressStyle.Indeterminate;
            Assert.Equal("Busy", bar.AccessibilityObject.Value);
            foreach (var fill in Enum.GetValues<EnterpriseProgressFill>())
            {
                bar.Fill = fill;
                bar.Style = EnterpriseProgressStyle.Determinate;
                bar.Value = 25;
                AssertPaints(bar, "progress-" + fill);
            }

            host.Dispose();
        });
    }

    [Fact]
    public void TrackBarKeyboardMouseAndRange()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var track = Show(host, new EnterpriseTrackBarControl { Width = 300, ShowTooltip = false });
            var committed = 0;
            track.ValueCommitted += (_, _) => committed++;
            Ui.Key(track, Keys.Right);
            Assert.Equal(1, track.Value);
            Ui.Key(track, Keys.PageUp);
            Assert.Equal(11, track.Value);
            Ui.Key(track, Keys.End);
            Assert.Equal(100, track.Value);
            Ui.Key(track, Keys.Home);
            Assert.Equal(0, track.Value);
            Assert.True(committed >= 4);
            Ui.Click(track, track.Width / 2, track.Height / 2);
            Assert.InRange(track.Value, 45, 55);

            track.Step = 25;
            track.Value = 62;
            Assert.Equal(50, track.Value);
            Ui.Key(track, Keys.Right);
            Assert.Equal(75, track.Value);

            var range = Show(host, new EnterpriseTrackBarControl { Width = 300, Mode = TrackSelectionMode.Range, ValueLow = 20, ValueHigh = 80, MinimumGap = 10, ShowTooltip = false }, 10, 80);
            Assert.Equal("20 to 80", range.AccessibilityObject.Value);
            range.ValueLow = 90;
            Assert.Equal(70, range.ValueLow);
            Ui.Key(range, Keys.Tab);
            Ui.Key(range, Keys.Right);
            Assert.Equal(81, range.ValueHigh);
            Ui.Key(range, Keys.Tab | Keys.Shift);
            Ui.Key(range, Keys.Left);
            Assert.Equal(69, range.ValueLow);
            range.AccessibilityObject.Value = "55";
            Assert.Equal(55, range.ValueLow);

            var vertical = Show(host, new EnterpriseTrackBarControl { Orientation = Orientation.Vertical, Value = 30, TickStyle = TrackTickStyle.Both, ShowTickLabels = true, ShowTooltip = false }, 400, 10);
            AssertPaints(track, "trackbar");
            AssertPaints(range, "trackbar-range");
            AssertPaints(vertical, "trackbar-vertical");
            host.Dispose();
        });
    }

    [Fact]
    public void RatingClickHalfPrecisionClearAndReadOnly()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var rating = Show(host, new EnterpriseStarRatingControl { Descriptions = new[] { "Poor", "Fair", "Good", "Great", "Excellent" } });
            var changes = new List<double>();
            rating.ValueChanged += (_, _) => changes.Add(rating.Value);
            var cell = Scale(rating, 30);
            var left = Scale(rating, 4);
            Ui.Click(rating, left + cell * 2 + 5, rating.Height / 2);
            Assert.Equal(3, rating.Value);
            Ui.Click(rating, left + cell * 2 + 5, rating.Height / 2);
            Assert.Equal(0, rating.Value);
            Assert.Equal("1 — Poor", rating.DescribeValue(1));

            rating.Precision = RatingPrecision.Half;
            Ui.Click(rating, left + cell * 3 + 3, rating.Height / 2);
            Assert.Equal(3.5, rating.Value);
            Ui.Key(rating, Keys.Right);
            Assert.Equal(4, rating.Value);
            Ui.Key(rating, Keys.D2);
            Assert.Equal(2, rating.Value);
            Ui.Key(rating, Keys.Delete);
            Assert.Equal(0, rating.Value);

            rating.ReadOnly = true;
            Ui.Click(rating, left + cell * 4, rating.Height / 2);
            Assert.Equal(0, rating.Value);
            rating.ReadOnly = false;
            rating.Value = 99;
            Assert.Equal(5, rating.Value);
            Assert.Equal("5 out of 5", rating.AccessibilityObject.Value);

            foreach (var shape in Enum.GetValues<RatingSymbolShape>())
            {
                rating.SymbolShape = shape;
                rating.Value = 3.5;
                AssertPaints(rating, "rating-" + shape);
            }

            host.Dispose();
        });
    }

    // ---- steps, waiting, label ----

    [Fact]
    public void StepProgressValidatesAndNavigates()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var steps = Show(host, new EnterpriseStepProgressBarControl { Width = 400 });
            foreach (var t in new[] { "One", "Two", "Three", "Four" }) steps.AddStep(t, "Description of " + t);
            var allow = false;
            steps.StepChanging += (_, e) =>
            {
                if (e.To > e.From && !allow)
                {
                    e.Cancel = true;
                    e.Message = "Not valid";
                }
            };

            Ui.Click(steps, 150, 20);
            Assert.Equal(0, steps.CurrentStep);
            Assert.Equal(StepStatus.Error, steps.Flow.Steps[0].Status);
            allow = true;
            Ui.Click(steps, 150, 20);
            Assert.Equal(1, steps.CurrentStep);
            Ui.Click(steps, 350, 20);
            Assert.Equal(1, steps.CurrentStep);
            steps.SetStatus(2, StepStatus.Warning, "Check this");
            AssertPaints(steps, "steps");
            Ui.Key(steps, Keys.Left);
            Ui.Key(steps, Keys.Enter);
            Assert.Equal(2, steps.CurrentStep);
            Assert.StartsWith("Step 3 of 4", steps.AccessibilityObject.Value);
            Assert.True(steps.Finish());
            Assert.True(steps.Flow.IsFinished);

            steps.Orientation = Orientation.Vertical;
            steps.Size = new Size(260, 240);
            AssertPaints(steps, "steps-vertical");
            host.Dispose();
        });
    }

    [Fact]
    public void WaitingBarStartsStopsAndPaintsEveryAnimation()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var bar = Show(host, new EnterpriseWaitingBarControl { Text = "Working…" });
            Assert.True(bar.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
            bar.StopWaiting();
            Assert.False(bar.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
            bar.StartWaiting();
            foreach (var animation in Enum.GetValues<WaitingBarAnimation>())
            {
                bar.Animation = animation;
                Pump(60);
                AssertPaints(bar, "waiting-" + animation);
            }

            bar.TextPosition = WaitingTextPosition.Below;
            bar.Size = new Size(160, 100);
            AssertPaints(bar, "waiting-below");
            host.Dispose();
        });
    }

    [Fact]
    public void LabelMarkupMnemonicEllipsisAndLinks()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var box = Show(host, new TextBox { Width = 100 }, 10, 120);
            var label = Show(host, new EnterpriseLabelControl { Text = "&Name:", LabelFor = box, Width = 80 });
            Invoke(label, "ProcessMnemonic", 'n');
            Pump(30);
            Assert.True(box.Focused);

            var rich = Show(host, new EnterpriseLabelControl { MarkupMode = LabelMarkupMode.Markdown, Text = "**Bold** and [link](docs)", Width = 220, Height = 30 }, 10, 50);
            Assert.Equal("Bold and link", rich.PlainText);
            string? clicked = null;
            rich.LinkClicked += (_, e) => clicked = e.LinkText;
            AssertPaints(rich, "label-markdown");
            for (var x = 5; x < rich.Width && clicked == null; x += 4) Ui.Click(rich, x, 8);
            Assert.Equal("docs", clicked);

            var narrow = Show(host, new EnterpriseLabelControl { Text = "A very long text that cannot fit in this label", Width = 100, Height = 24, WordWrap = false }, 10, 90);
            AssertPaints(narrow, "label-ellipsis");
            Assert.True(narrow.IsTruncated);

            narrow.AutoSize = true;
            narrow.WordWrap = false;
            narrow.AutoEllipsis = false;
            Assert.True(narrow.Width > 100);

            var rotated = Show(host, new EnterpriseLabelControl { Text = "Rotated", TextRotation = LabelTextRotation.Rotate90, Width = 30, Height = 100, TextAlign = ContentAlignment.MiddleCenter }, 300, 10);
            AssertPaints(rotated, "label-rotated");
            host.Dispose();
        });
    }

    // ---- carousel and rotator ----

    [Fact]
    public void CarouselNavigatesLoopsAndPaintsAllLayouts()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var carousel = Show(host, new EnterpriseCarouselControl { AnimationDuration = 0 });
            for (var i = 0; i < 5; i++) carousel.Items.Add(new CarouselItem("Item " + i, "Subtitle"));
            carousel.Items.Add("Plain text item");
            var painted = 0;
            carousel.PaintItem += (_, e) => painted++;
            var changes = 0;
            carousel.SelectedIndexChanged += (_, _) => changes++;

            carousel.MoveNext();
            Assert.Equal(1, carousel.SelectedIndex);
            carousel.MovePrevious();
            carousel.MovePrevious();
            Assert.Equal(5, carousel.SelectedIndex);
            Ui.Key(carousel, Keys.Right);
            Assert.Equal(0, carousel.SelectedIndex);
            Ui.Key(carousel, Keys.End);
            Assert.Equal(5, carousel.SelectedIndex);
            Ui.Key(carousel, Keys.Home);
            Assert.Equal(0, carousel.SelectedIndex);
            Assert.Equal(6, changes);

            carousel.InfiniteLoop = false;
            carousel.MovePrevious();
            Assert.Equal(0, carousel.SelectedIndex);
            var activated = -1;
            carousel.ItemActivated += (_, i) => activated = i;
            Ui.Key(carousel, Keys.Enter);
            Assert.Equal(0, activated);

            carousel.InfiniteLoop = true;
            foreach (var layout in Enum.GetValues<CarouselLayout>())
            {
                carousel.Layout = layout;
                AssertPaints(carousel, "carousel-" + layout);
            }

            Assert.True(painted >= 6);
            Assert.StartsWith("Item 1 of 6", carousel.AccessibilityObject.Value);
            host.Dispose();
        });
    }

    [Fact]
    public void RotatorChangesPanelsAndCanBeCancelled()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var rotator = Show(host, new EnterpriseRotatorControl { AutoRotate = false, TransitionDuration = 0 });
            rotator.AddPanel("<b>First</b> panel with a <a href=\"one\">link</a>", "One");
            rotator.AddPanel("Second *panel*", "Two", LabelMarkupMode.Markdown);
            rotator.AddPanel("Third", "Three");
            var changed = 0;
            rotator.PanelChanged += (_, _) => changed++;

            rotator.Next();
            Assert.Equal(1, rotator.SelectedIndex);
            rotator.Next();
            rotator.Next();
            Assert.Equal(0, rotator.SelectedIndex);
            rotator.Loop = false;
            rotator.Previous();
            Assert.Equal(0, rotator.SelectedIndex);
            rotator.Loop = true;

            rotator.PanelChanging += (_, e) => e.Cancel = e.To == 2;
            rotator.GoTo(2);
            Assert.Equal(0, rotator.SelectedIndex);
            Ui.Key(rotator, Keys.Right);
            Assert.Equal(1, rotator.SelectedIndex);
            Ui.Key(rotator, Keys.Space);
            Assert.True(rotator.IsPaused);
            Assert.Equal(4, changed);
            Assert.StartsWith("Panel 2 of 3", rotator.AccessibilityObject.Value);

            foreach (var effect in Enum.GetValues<RotatorEffect>())
            {
                rotator.Effect = effect;
                rotator.TransitionDuration = 400;
                rotator.GoTo(rotator.SelectedIndex == 0 ? 1 : 0);
                Pump(120);
                AssertPaints(rotator, "rotator-" + effect);
                rotator.TransitionDuration = 0;
            }

            host.Dispose();
        });
    }

    // ---- popups ----

    [Fact]
    public void CalloutOpensPlacesAndCloses()
    {
        RunSta(() =>
        {
            var host = NewHost(600, 400);
            var target = Show(host, new EnterpriseButtonControl { Text = "Target" }, 200, 150);
            using var callout = new EnterpriseCalloutControl { Title = "Tip", Message = "Press **F5** to refresh", MarkupMode = LabelMarkupMode.Markdown, AutoCloseInterval = 0 };
            callout.Actions.Add(new NotificationAction("ok", "Got it", true));
            CalloutCloseReason? reason = null;
            callout.Closed += (_, e) => reason = e.Reason;
            var opened = 0;
            callout.Opened += (_, _) => opened++;

            callout.Show(target);
            Assert.True(callout.IsOpen);
            Assert.Equal(1, opened);
            Assert.NotEqual(CalloutPlacement.Auto, callout.ActualPlacement);
            Pump(250);
            callout.Close();
            PumpUntil(() => !callout.IsOpen, 3000);
            Assert.Equal(CalloutCloseReason.Programmatic, reason);

            callout.Placement = CalloutPlacement.Right;
            callout.Show(target);
            Assert.Equal(CalloutPlacement.Right, callout.ActualPlacement);
            target.Dispose();
            PumpUntil(() => !callout.IsOpen, 3000);
            Assert.Equal(CalloutCloseReason.TargetLost, reason);
            host.Dispose();
        });
    }

    [Fact]
    public void NotifyIconCountsUnreadShowsToastsAndRunsActions()
    {
        RunSta(() =>
        {
            using var tray = new EnterpriseNotifyIcon { Text = "Test", Visible = false, ToastMode = NotifyToastMode.None };
            string? action = null;
            tray.ActionInvoked += (_, e) => action = e.ActionId;
            var first = tray.Notify("Build", "finished", NotificationSeverity.Success, new NotificationAction("open", "Open", true));
            tray.Notify("Disk", "low", NotificationSeverity.Warning);
            Assert.Equal(2, tray.UnreadCount);
            tray.Center.InvokeAction(first.Id, "open");
            Assert.Equal("open", action);
            Assert.Equal(1, tray.UnreadCount);
            tray.MarkAllRead();
            Assert.Equal(0, tray.UnreadCount);

            tray.ToastMode = NotifyToastMode.Custom;
            var closed = 0;
            tray.ToastClosed += (_, _) => closed++;
            for (var i = 0; i < 5; i++) tray.Notify("Toast " + i, "Message " + i);
            Pump(300);
            Assert.True(tray.UnreadCount == 5);
            Assert.True(tray.VisibleToastCount >= 1);
            tray.CloseToasts();
            PumpUntil(() => closed >= 5, 5000);
            Assert.Equal(0, tray.VisibleToastCount);

            var from = Task.Run(() => tray.Notify("Background", "from another thread"));
            PumpUntil(() => from.IsCompleted, 3000);
            Assert.Equal(6, tray.UnreadCount);
            tray.CloseToasts();
            Pump(300);
        });
    }

    // ---- chat, AI prompt, inline AI ----

    [Fact]
    public void ChatSendsGroupsTypingAndPaints()
    {
        RunSta(() =>
        {
            var host = NewHost(520, 620);
            var chat = Show(host, new EnterpriseChatControl { Size = new Size(480, 560) });
            var bot = new ChatAuthor("bot", "Assistant");
            var conversation = chat.Conversation;
            conversation.AddSystem("Started");
            conversation.Receive(bot, "Hello **there**, how can I help?", new[] { "Orders", "Invoices" });
            conversation.Receive(bot, "Second line from the assistant that is long enough to wrap onto more than one line inside of the bubble.");

            var submitted = new List<string>();
            chat.MessageSubmitted += (_, e) => submitted.Add(e.Message.Text);
            Assert.True(chat.SendText("Hi bot"));
            Assert.Equal(new[] { "Hi bot" }, submitted);
            Assert.Equal(ChatMessageStatus.Sending, conversation.Messages[^1].Status);
            conversation.SetStatus(conversation.Messages[^1], ChatMessageStatus.Read);

            chat.MessageSubmitting += (_, e) => e.Cancel = e.Text == "blocked";
            Assert.False(chat.SendText("blocked"));
            Assert.False(chat.SendText("   "));

            conversation.SetTyping(bot, true);
            Pump(120);
            conversation.Send("A message with files", new[] { new ChatAttachment { FileName = "report.pdf", SizeBytes = 5000 } });
            for (var i = 0; i < 40; i++) conversation.Receive(i % 2 == 0 ? bot : conversation.LocalAuthor, "Message number " + i);
            Pump(120);
            AssertPaints(chat, "chat");
            chat.ScrollToBottom();
            Pump(60);
            AssertPaints(chat, "chat-bottom");
            Assert.Contains("messages", chat.AccessibilityObject.Value);
            host.Dispose();
        });
    }

    [Fact]
    public void AIPromptRunsStreamsFailsAndKeepsHistory()
    {
        RunSta(() =>
        {
            var host = NewHost(640, 520);
            var prompt = Show(host, new EnterpriseAIPromptControl { Size = new Size(560, 480), Provider = new SampleAIPromptProvider { Latency = TimeSpan.Zero, StreamWordsPerStep = 0 } });
            prompt.PromptText = "One sentence. Two sentences. Three sentences here.";
            AssertPaints(prompt, "aiprompt-prompt");

            AIPromptHistoryEntry? received = null;
            prompt.ResponseReceived += (_, e) => received = e;
            var task = prompt.SubmitAsync(prompt.PromptText, SampleAIPromptProvider.Summarize);
            Wait(task);
            Assert.NotNull(received);
            Assert.Equal(AIPromptView.Output, prompt.ActiveView);
            Assert.Equal(AIPromptState.Completed, prompt.Session.State);
            AssertPaints(prompt, "aiprompt-output");

            prompt.RegisterCommand(new AIPromptCommand("shout", "Shout", "Shout: {input}", "Make it loud", "!", "Custom"));
            prompt.ActiveView = AIPromptView.Commands;
            AssertPaints(prompt, "aiprompt-commands");
            prompt.ActiveView = AIPromptView.History;
            Assert.Single(prompt.Session.History);
            AssertPaints(prompt, "aiprompt-history");

            prompt.Provider = new DemoFailing();
            Wait(prompt.SubmitAsync("fail please"));
            Assert.Equal(AIPromptState.Faulted, prompt.Session.State);
            prompt.ActiveView = AIPromptView.Output;
            AssertPaints(prompt, "aiprompt-error");

            var cancelled = false;
            prompt.Submitting += (_, e) => { e.Cancel = true; cancelled = true; };
            Assert.Null(prompt.SubmitAsync("blocked").GetAwaiter().GetResult());
            Assert.True(cancelled);
            host.Dispose();
        });
    }

    private sealed class DemoFailing : IAIPromptProvider
    {
        public Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken) => throw new InvalidOperationException("offline");
    }

    [Fact]
    public void InlineAssistantTransformsSelectionAfterAccept()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var box = Show(host, new TextBox { Multiline = true, Width = 500, Height = 120, Text = "i think we should utilize a lot of caching. it is fast." });
            using var assistant = new EnterpriseInlineAIAssistant { Target = box, Provider = new SampleAIPromptProvider { Latency = TimeSpan.Zero, StreamWordsPerStep = 0 } };
            var length = box.Text.IndexOf('.') + 1;
            box.Select(0, length);
            var tail = box.Text[length..];
            assistant.Show();
            Assert.Equal(InlineAIState.Menu, assistant.State);

            var task = assistant.RunAsync(SampleAIPromptProvider.Rewrite);
            Wait(task);
            var result = task.Result;
            Assert.NotNull(result);
            Assert.Equal(InlineAIState.Result, assistant.State);
            Assert.Contains("use many caching", result);

            Assert.True(assistant.Apply(result!));
            Assert.StartsWith(result, box.Text);
            Assert.EndsWith(tail, box.Text);
            assistant.Close();
            PumpUntil(() => assistant.State == InlineAIState.Closed, 3000);
            host.Dispose();
        });
    }

    // ---- smart paste, speech, keyboard ----

    [Fact]
    public void SmartPasteFillsBoundFieldsAndUndoes()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var name = Show(host, new TextBox { Name = "Name", Text = "old" }, 10, 50);
            var email = Show(host, new TextBox { Name = "Email" }, 10, 80);
            var zip = Show(host, new TextBox { Name = "Zip" }, 10, 110);
            var amount = Show(host, new NumericUpDown { Name = "Amount", Maximum = 100000 }, 10, 140);
            var paste = Show(host, new EnterpriseSmartPasteButton());
            paste.Bind(name, SmartPasteFieldKind.Name);
            paste.Bind(email, SmartPasteFieldKind.Email);
            paste.Bind(zip, SmartPasteFieldKind.PostalCode);
            paste.Bind(amount, SmartPasteFieldKind.Currency);

            var cancel = true;
            paste.Pasting += (_, e) => e.Cancel = cancel;
            Wait(paste.PasteAsync("Jane Public\njane@example.org\nWashington, DC 20500\nTotal due: $1,250.00"));
            Assert.Equal("old", name.Text);
            cancel = false;

            SmartPasteResult? result = null;
            var task = paste.PasteAsync("Jane Public\njane@example.org\nWashington, DC 20500\nTotal due: $1,250.00");
            Wait(task);
            result = task.Result;
            Assert.NotNull(result);
            Assert.Equal("Jane Public", name.Text);
            Assert.Equal("jane@example.org", email.Text);
            Assert.Equal("20500", zip.Text);
            Assert.Equal(1250m, amount.Value);
            Assert.True(paste.CanUndo);

            paste.Undo();
            Assert.Equal("old", name.Text);
            Assert.Equal(string.Empty, email.Text);
            Assert.Equal(0m, amount.Value);
            AssertPaints(paste, "smartpaste");

            var ignored = paste.PasteAsync(" ");
            Wait(ignored);
            Assert.Null(ignored.Result);
            host.Dispose();
        });
    }

    [Fact]
    public void SpeechButtonRecordsTranscribesAndReportsFaults()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var target = Show(host, new TextBox { Multiline = true, Width = 300, Height = 60, Text = "Start:" }, 10, 60);
            var mic = Show(host, new EnterpriseSpeechToTextButton
            {
                Target = target,
                Provider = new ScriptedSpeechProvider("send the report today") { BytesPerWord = 3200 },
                AudioInputFactory = () => new SyntheticAudioInput(),
                MaxDuration = 0
            });
            var partials = new List<string>();
            mic.PartialTranscript += (_, t) => partials.Add(t);
            string? final = null;
            mic.FinalTranscript += (_, t) => final = t;

            Wait(mic.StartListeningAsync());
            target.SelectionStart = target.TextLength;
            Assert.True(mic.IsListening);
            Pump(700);
            AssertPaints(mic, "speech-listening");
            Wait(mic.StopListeningAsync());
            Pump(150);
            Assert.Equal(SpeechSessionState.Idle, mic.State);
            Assert.NotEmpty(partials);
            Assert.NotNull(final);
            Assert.StartsWith("Start: send", target.Text);

            var bare = Show(host, new EnterpriseSpeechToTextButton(), 300, 10);
            Wait(bare.StartListeningAsync());
            Assert.Equal(SpeechSessionState.Faulted, bare.State);
            AssertPaints(bare, "speech-fault");
            host.Dispose();
        });
    }

    [Fact]
    public void VirtualKeyboardTypesIntoTargetWithShiftLayersAndCommands()
    {
        RunSta(() =>
        {
            var host = NewHost(800, 400);
            var box = Show(host, new TextBox { Width = 400 });
            var keyboard = Show(host, new EnterpriseVirtualKeyboardControl { Target = box, Size = new Size(700, 260), KeyRepeat = false }, 10, 50);
            KeyDef Find(string label) => keyboard.Layout.Layers.Values.SelectMany(l => l.Rows).SelectMany(r => r.Keys).First(k => k.Label == label);

            keyboard.PressKey(Find("h"));
            keyboard.PressKey(Find("Shift"));
            keyboard.PressKey(Find("i"));
            Assert.Equal("hI", box.Text);
            Assert.False(keyboard.ShiftActive);
            keyboard.PressKey(Find("Space"));
            keyboard.PressKey(Find("Caps"));
            keyboard.PressKey(Find("o"));
            keyboard.PressKey(Find("k"));
            Assert.Equal("hI OK", box.Text);
            Assert.True(keyboard.CapsLockActive);
            keyboard.PressKey(Find("⌫"));
            Assert.Equal("hI O", box.Text);

            keyboard.PressKey(Find("?123"));
            Assert.Equal("symbols", keyboard.ActiveLayer);
            AssertPaints(keyboard, "keyboard-symbols");
            keyboard.PressKey(Find("ABC"));
            Assert.Equal("letters", keyboard.ActiveLayer);

            var scale = keyboard.DeviceDpi / 96.0;
            box.Clear();
            keyboard.PressKey(Find("Caps"));
            Assert.False(keyboard.CapsLockActive);
            Ui.Click(keyboard, (int)(8 * scale) + 6, (int)(8 * scale) + 6);
            Assert.Equal("`", box.Text);

            string? command = null;
            keyboard.CommandInvoked += (_, c) => command = c;
            keyboard.Layout = new KeyboardLayout("custom", new[] { new KeyboardLayer("main", new[] { new KeyRow(new[] { KeyDef.Char("1"), KeyDef.CommandKey("Add", "add", 2) }) }) });
            keyboard.PressKey(keyboard.Layout.Layers["main"].Rows[0].Keys[1]);
            Assert.Equal("add", command);

            keyboard.Kind = VirtualKeyboardKind.Numeric;
            AssertPaints(keyboard, "keyboard-numeric");
            keyboard.Kind = VirtualKeyboardKind.Symbols;
            AssertPaints(keyboard, "keyboard-symbols-layout");
            keyboard.Kind = VirtualKeyboardKind.Qwerty;
            AssertPaints(keyboard, "keyboard-qwerty");
            host.Dispose();
        });
    }
}
