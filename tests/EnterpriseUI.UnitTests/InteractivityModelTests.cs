using EnterpriseUI.Editors.Interactivity;
using Xunit;

namespace EnterpriseUI.UnitTests;

public class InteractivityModelTests
{
    private sealed class FailingProvider : IAIPromptProvider
    {
        public Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class BlockingProvider : IAIPromptProvider
    {
        public async Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new AIPromptResponse("never");
        }
    }

    [Fact]
    public async Task PromptSessionTracksHistoryUsageAndRetry()
    {
        var session = new AIPromptSession(new SampleAIPromptProvider { Latency = TimeSpan.Zero, StreamWordsPerStep = 0 });
        foreach (var c in SampleAIPromptProvider.DefaultCommands) session.Commands.Register(c);

        var entry = await session.SubmitAsync("The cat sat. The cat slept. A dog barked loudly at the cat.", SampleAIPromptProvider.Summarize);

        Assert.Equal(AIPromptState.Completed, session.State);
        Assert.Equal(entry.Response, session.Output);
        Assert.Contains("Summarize", entry.Prompt);
        Assert.True(session.TotalUsage.TotalTokens > 0);

        await session.RetryAsync();
        Assert.Equal(2, session.History.Count);
        session.Rate(session.History[1].Id, 1);
        Assert.Equal(1, session.History[1].Rating);
    }

    [Fact]
    public async Task PromptSessionReportsFaultsAndCancellation()
    {
        var faulted = new AIPromptSession(new FailingProvider());
        var entry = await faulted.SubmitAsync("hello");
        Assert.Equal(AIPromptState.Faulted, faulted.State);
        Assert.Equal("boom", entry.Error);

        var session = new AIPromptSession(new BlockingProvider());
        var task = session.SubmitAsync("hello");
        Assert.Equal(AIPromptState.Loading, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitAsync("again"));
        session.Cancel();
        await task;
        Assert.Equal(AIPromptState.Cancelled, session.State);
    }

    [Fact]
    public void PromptCommandsRegisterReplaceAndSearch()
    {
        var registry = new AIPromptCommandRegistry();
        registry.Register(new AIPromptCommand("fix", "Fix grammar", "Fix: {input}"));
        registry.Register(new AIPromptCommand("fix", "Fix grammar v2", "Fix: {input}"));
        registry.Register(new AIPromptCommand("tr", "Translate", "Translate {input} using {context}"));

        Assert.Equal(2, registry.All.Count);
        Assert.Equal("Fix grammar v2", registry.Find("FIX")!.Title);
        Assert.Single(registry.Search("trans"));
        Assert.Equal("Translate hi using French", registry.Find("tr")!.BuildPrompt("hi", "French"));
        Assert.True(registry.Unregister("tr"));
    }

    [Fact]
    public void SampleProviderTransformsText()
    {
        var text = "i think we should utilize a lot of caching. it's really very important.";
        Assert.StartsWith("I think we should use many caching.", SampleAIPromptProvider.Transform(SampleAIPromptProvider.Rewrite, text));
        Assert.DoesNotContain("I think", SampleAIPromptProvider.Transform(SampleAIPromptProvider.Confident, text));
        Assert.Contains("it is", SampleAIPromptProvider.Transform(SampleAIPromptProvider.Formal, text));
        Assert.Contains("Expanded outline", SampleAIPromptProvider.Transform(SampleAIPromptProvider.Expand, text));
    }

    [Theory]
    [InlineData(RatingPrecision.Full, 33, 2.0)]
    [InlineData(RatingPrecision.Half, 28, 1.5)]
    [InlineData(RatingPrecision.Half, 100, 5.0)]
    [InlineData(RatingPrecision.Full, 0, 0.0)]
    public void RatingValueFollowsPointer(RatingPrecision precision, double position, double expected)
    {
        Assert.Equal(expected, RatingMath.ValueAt(position, 100, 5, precision));
    }

    [Fact]
    public void RatingFillAndKeyboardSteps()
    {
        Assert.Equal(1, RatingMath.FillOf(0, 3.5));
        Assert.Equal(0.5, RatingMath.FillOf(3, 3.5));
        Assert.Equal(0, RatingMath.FillOf(4, 3.5));
        Assert.Equal(4.0, RatingMath.Step(3.5, 1, 5, RatingPrecision.Half));
        Assert.Equal(5.0, RatingMath.Step(5, 1, 5, RatingPrecision.Full));
    }

    [Fact]
    public void TrackScaleSnapsAndMapsPositions()
    {
        var scale = new TrackScale { Minimum = 0, Maximum = 100, Step = 5 };
        Assert.Equal(45, scale.Snap(47.4));
        Assert.Equal(100, scale.Snap(250));
        Assert.Equal(50, scale.FromPosition(110, 10, 200));
        Assert.Equal(110, scale.ToPosition(50, 10, 200));
        Assert.Equal(new double[] { 0, 25, 50, 75, 100 }, scale.Ticks(25));
        Assert.Equal(new double[] { 0, 40, 80, 100 }, scale.Ticks(40));
    }

    [Fact]
    public void StepFlowValidatesForwardAndAllowsBack()
    {
        var flow = new StepFlow();
        flow.Add("Account");
        flow.Add("Details");
        flow.Add("Review");
        var allow = false;
        flow.Changing += (_, e) =>
        {
            if (!allow && e.To > e.From) { e.Cancel = true; e.Message = "Required"; }
        };

        Assert.False(flow.Next());
        Assert.Equal(StepStatus.Error, flow.Steps[0].Status);
        Assert.Equal("Required", flow.Steps[0].Message);

        allow = true;
        Assert.True(flow.Next());
        Assert.Equal(1, flow.Current);
        Assert.Equal(StepStatus.Completed, flow.Steps[0].Status);
        Assert.False(flow.CanNavigateTo(2) && flow.Steps[1].Status != StepStatus.Active);
        Assert.True(flow.Previous());
        Assert.Equal(0, flow.Current);
        Assert.True(flow.CanNavigateTo(1));
        Assert.True(flow.Next());
        Assert.True(flow.Next());
        Assert.True(flow.Next());
        Assert.True(flow.IsFinished);
        Assert.All(flow.Steps, s => Assert.Equal(StepStatus.Completed, s.Status));
    }

    [Fact]
    public void StepFlowBlocksJumpingOverIncompleteSteps()
    {
        var flow = new StepFlow();
        flow.Add("A");
        flow.Add("B");
        flow.Add("C", optional: false);
        Assert.False(flow.CanNavigateTo(2));
        Assert.False(flow.TryNavigateTo(2));
        flow.AllowJumpAhead = true;
        Assert.True(flow.TryNavigateTo(2));
        Assert.Equal(StepStatus.Skipped, flow.Steps[1].Status);
    }

    [Fact]
    public void ChatGroupsMessagesAndSeparatesDays()
    {
        var me = new ChatAuthor("me", "Ada Lovelace", true);
        var bot = new ChatAuthor("bot", "Assistant");
        var start = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var chat = new ChatConversation(me);
        chat.Add(new ChatMessage { Author = me, Text = "a", Timestamp = start });
        chat.Add(new ChatMessage { Author = me, Text = "b", Timestamp = start.AddMinutes(1) });
        chat.Add(new ChatMessage { Author = bot, Text = "c", Timestamp = start.AddMinutes(2) });
        chat.Add(new ChatMessage { Author = bot, Text = "d", Timestamp = start.AddDays(1) });

        var info = Enumerable.Range(0, 4).Select(i => ChatGrouping.Describe(chat.Messages, i)).ToArray();
        Assert.True(info[0].ShowDateSeparator);
        Assert.True(info[0].StartsGroup);
        Assert.False(info[0].EndsGroup);
        Assert.False(info[1].StartsGroup);
        Assert.True(info[1].EndsGroup);
        Assert.True(info[3].ShowDateSeparator);
        Assert.Equal("AL", me.Initials);
        Assert.Equal("Today", ChatGrouping.DateLabel(start, start));
        Assert.Equal("Yesterday", ChatGrouping.DateLabel(start, start.AddDays(1)));
    }

    [Fact]
    public void ChatTypingAndAttachments()
    {
        var chat = new ChatConversation(new ChatAuthor("me", "Me", true));
        var bot = new ChatAuthor("bot", "Bot");
        var changes = 0;
        chat.TypingChanged += (_, _) => changes++;
        chat.SetTyping(bot, true);
        chat.SetTyping(bot, true);
        Assert.Single(chat.TypingAuthors);
        chat.Receive(bot, "hi", new[] { "Yes", "No" });
        Assert.Empty(chat.TypingAuthors);
        Assert.Equal(2, changes);
        var sent = chat.Send("file", new[] { new ChatAttachment { FileName = "a.png", SizeBytes = 1536 } });
        Assert.Equal(ChatMessageStatus.Sending, sent.Status);
        Assert.Equal("1.5 KB", sent.Attachments[0].SizeText);
        Assert.True(sent.Attachments[0].IsImage);
    }

    [Fact]
    public void KeyboardShiftCapsAndLayers()
    {
        var state = new VirtualKeyboardState(KeyboardLayout.Qwerty());
        KeyDef Find(string label) => state.ActiveLayer.Rows.SelectMany(r => r.Keys).First(k => k.Label == label);

        Assert.Equal("a", state.Press(Find("a")).Text);
        state.Press(Find("Shift"));
        Assert.Equal("A", state.LabelOf(Find("a")));
        Assert.Equal("A", state.Press(Find("a")).Text);
        Assert.False(state.Shift);
        state.Press(Find("Caps"));
        Assert.Equal("!", state.Press(Find("1")).Text);
        state.Press(Find("Caps"));
        Assert.Equal("1", state.Press(Find("1")).Text);

        state.Press(Find("?123"));
        Assert.Equal("symbols", state.ActiveLayerName);
        Assert.Equal(KeyAction.Backspace, state.Press(Find("⌫")).Action);
        Assert.Equal(KeyAction.Close, state.Press(Find("Hide")).Action);
        state.Press(Find("ABC"));
        Assert.Equal("letters", state.ActiveLayerName);
        Assert.Equal(" ", state.Press(Find("Space")).Text);
    }

    [Fact]
    public void NumericAndSymbolLayoutsAreComplete()
    {
        var pad = KeyboardLayout.Numeric().Layers["pad"];
        Assert.Equal(4, pad.Rows.Count);
        Assert.All(pad.Rows, r => Assert.Equal(4, r.Keys.Count));
        Assert.Equal(2, KeyboardLayout.Symbols().Layers.Count);
        var custom = KeyDef.CommandKey("Paste", "paste");
        Assert.Equal("paste", new VirtualKeyboardState(KeyboardLayout.Numeric()).Press(custom).Command);
    }

    [Fact]
    public void NotificationCenterCountsGroupsAndActions()
    {
        var center = new NotificationCenter();
        string? invoked = null;
        center.ActionInvoked += (_, e) => invoked = e.ActionId;
        var a = center.Notify("Build", "started", NotificationSeverity.Info, "build");
        center.Notify("Mail", "new message", NotificationSeverity.Info, null, new NotificationAction("open", "Open", true));
        center.Notify("Build", "finished", NotificationSeverity.Success, "build");

        Assert.Equal(2, center.Items.Count);
        Assert.Equal(2, center.UnreadCount);
        var mail = center.Items.First(n => n.Title == "Mail");
        center.InvokeAction(mail.Id, "open");
        center.InvokeAction(mail.Id, "missing");
        Assert.Equal("open", invoked);
        Assert.Equal(1, center.UnreadCount);
        Assert.False(center.Dismiss(a.Id));
        center.MarkAllRead();
        Assert.Equal(0, center.UnreadCount);
        Assert.Equal("99+", NotificationCenter.BadgeText(150));
        Assert.Equal(string.Empty, NotificationCenter.BadgeText(0));
        center.Clear();
        Assert.Empty(center.Items);
    }

    [Fact]
    public void MarkdownParsesInlineFormatting()
    {
        var runs = LabelMarkup.Parse("Hello **bold *both*** `code` ~~gone~~ [site](http://x.io)\nnext", LabelMarkupMode.Markdown);
        Assert.Contains(runs, r => r.Text == "Hello " && !r.Bold);
        Assert.Contains(runs, r => r.Text == "bold " && r.Bold && !r.Italic);
        Assert.Contains(runs, r => r.Text == "both" && r.Bold && r.Italic);
        Assert.Contains(runs, r => r.Text == "code" && r.Code);
        Assert.Contains(runs, r => r.Text == "gone" && r.Strike);
        Assert.Contains(runs, r => r.Text == "site" && r.Href == "http://x.io" && r.Underline);
        Assert.Contains(runs, r => r.LineBreak);
        Assert.Equal("Hello bold both code gone site\nnext", LabelMarkup.ToPlainText(runs));
    }

    [Fact]
    public void HtmlParsesTagsEntitiesAndColors()
    {
        var runs = LabelMarkup.Parse("<b>Total</b> &amp; <span style=\"color:#ff0000\">tax</span><br/><a href=\"u\">go</a> <i>x</i>", LabelMarkupMode.Html);
        Assert.Contains(runs, r => r.Text == "Total" && r.Bold);
        Assert.Contains(runs, r => r.Text == " & " && !r.Bold);
        Assert.Contains(runs, r => r.Text == "tax" && r.Color == "#ff0000");
        Assert.Contains(runs, r => r.LineBreak);
        Assert.Contains(runs, r => r.Text == "go" && r.Href == "u");
        Assert.Contains(runs, r => r.Text == "x" && r.Italic);
        Assert.Empty(LabelMarkup.Parse(null, LabelMarkupMode.Html));
        Assert.Single(LabelMarkup.Parse("a <b>b", LabelMarkupMode.Plain));
    }

    private static readonly SmartPasteField[] ContactFields =
    {
        new("Name", SmartPasteFieldKind.Name), new("Email", SmartPasteFieldKind.Email), new("Phone", SmartPasteFieldKind.Phone),
        new("Street", SmartPasteFieldKind.Street), new("City", SmartPasteFieldKind.City), new("State", SmartPasteFieldKind.Region), new("Zip", SmartPasteFieldKind.PostalCode)
    };

    [Fact]
    public void SmartPasteMapsAddressBlock()
    {
        var text = "Jane Q Public\n1600 Pennsylvania Ave NW\nWashington, DC 20500\njane@example.org\n(202) 456-1111";
        var result = SmartPasteMapper.Map(text, ContactFields);
        Assert.Equal(SmartPasteContentKind.Address, result.ContentKind);
        Assert.Equal("Jane Q Public", result.Value("Name"));
        Assert.Equal("1600 Pennsylvania Ave NW", result.Value("Street"));
        Assert.Equal("Washington", result.Value("City"));
        Assert.Equal("DC", result.Value("State"));
        Assert.Equal("20500", result.Value("Zip"));
        Assert.Equal("jane@example.org", result.Value("Email"));
        Assert.Equal("(202) 456-1111", result.Value("Phone"));
        Assert.Empty(result.UnmatchedFields);
    }

    [Fact]
    public void SmartPasteMapsLabeledJsonAndTabularText()
    {
        var labeled = SmartPasteMapper.Map("Full name: Ada Lovelace\nE-mail = ada@x.io\nTelephone: +44 20 7946 0958", ContactFields);
        Assert.Equal(SmartPasteContentKind.KeyValue, labeled.ContentKind);
        Assert.Equal("Ada Lovelace", labeled.Value("Name"));
        Assert.Equal("ada@x.io", labeled.Value("Email"));
        Assert.Equal("+44 20 7946 0958", labeled.Value("Phone"));

        var json = SmartPasteMapper.Map("{\"name\":\"Bob\",\"contact\":{\"email\":\"b@x.io\"},\"zip\":\"10001\"}", ContactFields);
        Assert.Equal(SmartPasteContentKind.Json, json.ContentKind);
        Assert.Equal("b@x.io", json.Value("Email"));
        Assert.Equal("10001", json.Value("Zip"));

        var table = SmartPasteMapper.Map("Name\tEmail\tCity\nCy\tcy@x.io\tOslo", ContactFields);
        Assert.Equal(SmartPasteContentKind.Delimited, table.ContentKind);
        Assert.Equal("Oslo", table.Value("City"));
        Assert.Equal("cy@x.io", table.Value("Email"));
    }

    [Fact]
    public void SmartPasteRoutesCodeAndEmptyText()
    {
        var fields = new[] { new SmartPasteField("Title"), new SmartPasteField("Snippet", SmartPasteFieldKind.Code) };
        var code = SmartPasteMapper.Map("public class A\n{\n    int x = 1;\n}", fields);
        Assert.Equal(SmartPasteContentKind.Code, code.ContentKind);
        Assert.StartsWith("public class A", code.Value("Snippet"));
        Assert.Contains("Title", code.UnmatchedFields);

        var empty = SmartPasteMapper.Map("   ", fields);
        Assert.Equal(SmartPasteContentKind.Empty, empty.ContentKind);
        Assert.Equal(2, empty.UnmatchedFields.Count);
    }

    [Fact]
    public async Task DictationSessionStreamsTranscriptAndLevels()
    {
        var input = new SyntheticAudioInput();
        var provider = new ScriptedSpeechProvider("send the report today") { BytesPerWord = 3200 };
        await using var session = new SpeechDictationSession(input, provider);
        var levels = 0;
        var partials = new List<string>();
        session.LevelChanged += (_, _) => Interlocked.Increment(ref levels);
        session.PartialTextChanged += (_, t) => partials.Add(t);

        await session.StartAsync();
        Assert.Equal(SpeechSessionState.Listening, session.State);
        await Task.Delay(600);
        var text = await session.StopAsync();

        Assert.Equal(SpeechSessionState.Idle, session.State);
        Assert.True(levels > 0);
        Assert.NotEmpty(partials);
        Assert.StartsWith("send", text);
    }

    [Fact]
    public void AudioLevelMeasuresLoudness()
    {
        var silent = new byte[200];
        var loud = new byte[200];
        for (var i = 0; i < loud.Length; i += 2) { loud[i] = 0xFF; loud[i + 1] = 0x3F; }
        Assert.Equal(0, AudioLevel.Rms16(silent));
        Assert.True(AudioLevel.Rms16(loud) > 0.5);
        Assert.Equal(0, AudioLevel.Rms16(ReadOnlySpan<byte>.Empty));
    }
}
