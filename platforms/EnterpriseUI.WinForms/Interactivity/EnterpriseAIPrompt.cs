using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum AIPromptView { Prompt, Output, Commands, History }

public sealed class AIPromptSubmittingEventArgs : CancelEventArgs
{
    public AIPromptSubmittingEventArgs(string text, string? commandId)
    {
        Text = text;
        CommandId = commandId;
    }

    public string Text { get; }
    public string? CommandId { get; }
}

/// <summary>AI assistant panel: prompt entry with command suggestions, a streaming output view with copy, insert, retry and rating, a command catalog and a history of previous requests with token usage.</summary>
public class EnterpriseAIPromptControl : EnterpriseControlBase
{
    private enum HitKind { Tab, Chip, ClearCommand, Generate, Stop, Copy, Insert, Retry, RateUp, RateDown, CommandItem, HistoryItem, ClearHistory, Suggestion }

    private sealed record Hit(Rectangle Bounds, HitKind Kind, int Index = -1, object? Payload = null);

    private readonly TextBox _input = new() { Multiline = true, BorderStyle = BorderStyle.None, WordWrap = true, ScrollBars = ScrollBars.None, AcceptsReturn = true };
    private readonly Label _placeholder = new() { AutoSize = false, Cursor = Cursors.IBeam };
    private readonly EnterpriseScrollBar _scroll = new(Orientation.Vertical) { Visible = false };
    private readonly FrameClock _clock = new() { Interval = 30 };
    private readonly List<Hit> _hits = new();
    private readonly Dictionary<AIPromptView, int> _scrollOffsets = new();
    private AIPromptSession _session;
    private AIPromptView _view = AIPromptView.Prompt;
    private AIPromptCommand? _command;
    private MarkupLayout? _outputLayout;
    private string? _layoutText;
    private int _layoutWidth;
    private int _contentHeight;
    private int _focusTab;
    private int _hotHit = -1;
    private bool _updatingScroll;
    private int _suggestionIndex;
    private string _promptHint = "Ask anything, or type / to pick a command";

    public EnterpriseAIPromptControl()
    {
        AccessibleRole = AccessibleRole.Pane;
        _session = new AIPromptSession(new SampleAIPromptProvider());
        foreach (var command in SampleAIPromptProvider.DefaultCommands) _session.Commands.Register(command);
        Attach(_session);
        Size = new Size(520, 460);

        _input.AccessibleName = "Prompt";
        _input.TextChanged += (_, _) => { _suggestionIndex = 0; _placeholder.Visible = _input.Text.Length == 0; Invalidate(); };
        _input.KeyDown += OnInputKeyDown;
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
        _placeholder.Text = _promptHint;
        _placeholder.Click += (_, _) => _input.Focus();
        _scroll.ValueChanged += (_, _) =>
        {
            if (!_updatingScroll)
            {
                _scrollOffsets[_view] = _scroll.Value;
                Invalidate();
            }
        };
        _clock.Frame += (_, _) => Invalidate();
        Controls.Add(_scroll);
        Controls.Add(_input);
        Controls.Add(_placeholder);
        OnThemeApplied();
        LayoutChildren();
    }

    /// <summary>History, commands, loading state and provider; replace it to share one session between controls.</summary>
    [Browsable(false)]
    public AIPromptSession Session
    {
        get => _session;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Detach(_session);
            _session = value;
            Attach(value);
            Invalidate();
        }
    }

    [Browsable(false)]
    public IAIPromptProvider? Provider
    {
        get => _session.Provider;
        set => _session.Provider = value;
    }

    [DefaultValue(AIPromptView.Prompt)]
    public AIPromptView ActiveView
    {
        get => _view;
        set
        {
            if (_view == value) return;
            _view = value;
            _focusTab = (int)value;
            LayoutChildren();
            Invalidate();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue("Ask anything, or type / to pick a command")]
    public string PromptHint
    {
        get => _promptHint;
        set { _promptHint = value; _placeholder.Text = value; }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PromptText { get => _input.Text; set => _input.Text = value; }

    /// <summary>Selected command that the next submit uses.</summary>
    [Browsable(false)]
    public AIPromptCommand? SelectedCommand
    {
        get => _command;
        set { _command = value; Invalidate(); }
    }

    public event EventHandler? ViewChanged;
    public event EventHandler<AIPromptSubmittingEventArgs>? Submitting;
    public event EventHandler<AIPromptHistoryEntry>? ResponseReceived;
    public event EventHandler<string>? InsertRequested;
    public event EventHandler<string>? OutputCopied;

    public void RegisterCommand(AIPromptCommand command) => _session.Commands.Register(command);

    private void Attach(AIPromptSession s)
    {
        s.StateChanged += OnSessionChanged;
        s.OutputChanged += OnSessionChanged;
        s.HistoryChanged += OnSessionChanged;
        s.Commands.Changed += OnSessionChanged;
    }

    private void Detach(AIPromptSession s)
    {
        s.StateChanged -= OnSessionChanged;
        s.OutputChanged -= OnSessionChanged;
        s.HistoryChanged -= OnSessionChanged;
        s.Commands.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnSessionChanged(sender, e))); return; }
        _clock.Enabled = IsHandleCreated && _session.State == AIPromptState.Loading;
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        Invalidate();
    }

    /// <summary>Runs a prompt and switches to the output view; returns null when a handler cancelled it.</summary>
    public async Task<AIPromptHistoryEntry?> SubmitAsync(string text, string? commandId = null)
    {
        if (string.IsNullOrWhiteSpace(text) || _session.State == AIPromptState.Loading) return null;
        var args = new AIPromptSubmittingEventArgs(text, commandId);
        Submitting?.Invoke(this, args);
        if (args.Cancel) return null;
        ActiveView = AIPromptView.Output;
        _scrollOffsets[AIPromptView.Output] = 0;
        var entry = await _session.SubmitAsync(text, commandId).ConfigureAwait(true);
        if (entry.State == AIPromptState.Completed) ResponseReceived?.Invoke(this, entry);
        Invalidate();
        return entry;
    }

    private void Generate()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        var command = _command?.Id;
        _ = SubmitAsync(text, command);
    }

    // ---- composer ----

    private IReadOnlyList<AIPromptCommand> Suggestions => _input.Text.StartsWith('/') && !_input.Text.Contains(' ', StringComparison.Ordinal) ? _session.Commands.Search(_input.Text[1..]).Take(6).ToArray() : Array.Empty<AIPromptCommand>();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var suggestions = Suggestions;
        if (suggestions.Count > 0)
        {
            if (e.KeyCode == Keys.Down) { _suggestionIndex = (_suggestionIndex + 1) % suggestions.Count; e.SuppressKeyPress = true; Invalidate(); return; }
            if (e.KeyCode == Keys.Up) { _suggestionIndex = (_suggestionIndex - 1 + suggestions.Count) % suggestions.Count; e.SuppressKeyPress = true; Invalidate(); return; }
            if (e.KeyCode is Keys.Enter or Keys.Tab) { PickSuggestion(suggestions[_suggestionIndex]); e.SuppressKeyPress = true; return; }
        }

        if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; Generate(); }
        else if (e.KeyCode == Keys.Escape && _command != null) { _command = null; e.SuppressKeyPress = true; Invalidate(); }
    }

    private void PickSuggestion(AIPromptCommand command)
    {
        _command = command;
        _input.Clear();
        Invalidate();
    }

    private Rectangle Header => new(1, 1, Width - 2, S(42));
    private Rectangle Body => new(1, Header.Bottom, Width - 2, Math.Max(0, Height - Header.Bottom - 1));
    private int Pad => S(16);

    private Rectangle InputFrame
    {
        get
        {
            var body = Body;
            var top = body.Y + Pad + S(22) + ChipsHeight + (_command != null ? S(36) : 0);
            var height = Math.Max(S(90), Math.Min(S(180), body.Bottom - top - S(64)));
            return new Rectangle(body.X + Pad, top, body.Width - Pad * 2, height);
        }
    }

    private int ChipsHeight
    {
        get
        {
            var rows = ChipRows(out _);
            return rows == 0 ? 0 : rows * (S(28) + S(6)) + S(6);
        }
    }

    private int ChipRows(out List<(Rectangle Rect, AIPromptCommand Command)> rects)
    {
        rects = new List<(Rectangle, AIPromptCommand)>();
        var commands = _session.Commands.All.Take(8).ToArray();
        if (commands.Length == 0) return 0;
        var x = 0;
        var row = 0;
        var width = Body.Width - Pad * 2;
        foreach (var command in commands)
        {
            var w = Chrome.MeasureText((command.Glyph != null ? command.Glyph + " " : string.Empty) + command.Title, Font) + S(22);
            if (x + w > width && x > 0) { x = 0; row++; }
            rects.Add((new Rectangle(x, row * (S(28) + S(6)), w, S(28)), command));
            x += w + S(8);
        }

        return row + 1;
    }

    private void LayoutChildren()
    {
        if (_input == null) return;
        var showInput = _view == AIPromptView.Prompt;
        var frame = InputFrame;
        _input.Visible = showInput;
        _placeholder.Visible = showInput && _input.Text.Length == 0;
        _input.SetBounds(frame.X + S(10), frame.Y + S(8), Math.Max(10, frame.Width - S(20)), Math.Max(10, frame.Height - S(16)));
        _placeholder.SetBounds(_input.Left, _input.Top, _input.Width, Math.Min(_input.Height, Font.Height + S(2)));
        UpdateScroll();
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _input.BackColor = Colors.Surface;
        _input.ForeColor = Colors.Text;
        _input.Font = Font;
        _placeholder.BackColor = Colors.Surface;
        _placeholder.ForeColor = Colors.Muted;
        _placeholder.Font = Font;
        _scroll.ApplyColors(Colors);
        DropOutputLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_input == null) return;
        _input.Font = Font;
        _placeholder.Font = Font;
        DropOutputLayout();
        LayoutChildren();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        DropOutputLayout();
        LayoutChildren();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _clock.Enabled = _session.State == AIPromptState.Loading;
        LayoutChildren();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _clock.Enabled = false;
        base.OnHandleDestroyed(e);
    }

    private void DropOutputLayout()
    {
        _outputLayout?.Dispose();
        _outputLayout = null;
        _layoutText = null;
    }

    // ---- scrolling ----

    private Rectangle ScrollArea => _view switch
    {
        AIPromptView.Output => new Rectangle(Body.X, Body.Y + S(84) + (_session.State == AIPromptState.Loading ? S(34) : 0), Body.Width, Math.Max(0, Body.Height - S(84) - S(36) - (_session.State == AIPromptState.Loading ? S(34) : 0))),
        AIPromptView.Commands or AIPromptView.History => new Rectangle(Body.X, Body.Y + (_view == AIPromptView.History ? S(40) : 0), Body.Width, Math.Max(0, Body.Height - (_view == AIPromptView.History ? S(40) : 0))),
        _ => Rectangle.Empty
    };

    private void UpdateScroll()
    {
        if (_scroll == null) return;
        var area = ScrollArea;
        _updatingScroll = true;
        try
        {
            var needed = !area.IsEmpty && _contentHeight > area.Height;
            _scroll.Visible = needed;
            if (!needed) { _scroll.Value = 0; return; }
            _scroll.SetBounds(area.Right - S(12), area.Y, S(12), area.Height);
            _scroll.Minimum = 0;
            _scroll.Maximum = _contentHeight - 1;
            _scroll.LargeChange = Math.Max(1, area.Height);
            _scroll.SmallChange = S(32);
            _scroll.Value = _scrollOffsets.GetValueOrDefault(_view);
        }
        finally
        {
            _updatingScroll = false;
        }
    }

    private int ScrollY => _scroll.Visible ? _scroll.Value : 0;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scroll.Visible) _scroll.Value -= e.Delta / 120 * S(48);
    }

    // ---- painting ----

    private void Add(Rectangle bounds, HitKind kind, int index = -1, object? payload = null) => _hits.Add(new Hit(bounds, kind, index, payload));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Colors.Surface);
        _hits.Clear();
        var c = Colors;
        PaintHeader(g, c);
        switch (_view)
        {
            case AIPromptView.Prompt: PaintPrompt(g, c); break;
            case AIPromptView.Output: PaintOutput(g, c); break;
            case AIPromptView.Commands: PaintCommands(g, c); break;
            default: PaintHistory(g, c); break;
        }

        Chrome.DrawRoundedBorder(g, new Rectangle(0, 0, Width, Height), Focused ? c.FocusRing : c.Border, Chrome.CornerRadius);
    }

    private void PaintHeader(Graphics g, ControlColors c)
    {
        var h = Header;
        using (var brush = new SolidBrush(Chrome.Blend(c.Surface, c.Header, 0.7))) g.FillRectangle(brush, h);
        using (var pen = new Pen(c.Border)) g.DrawLine(pen, h.X, h.Bottom - 1, h.Right, h.Bottom - 1);
        var x = h.X + S(8);
        var tabs = new[] { "Prompt", "Output", "Commands", "History" };
        for (var i = 0; i < tabs.Length; i++)
        {
            var label = tabs[i];
            if (i == 3 && _session.History.Count > 0) label += $" ({_session.History.Count})";
            var w = Chrome.MeasureText(label, BoldFont) + S(26);
            var r = new Rectangle(x, h.Y + S(6), w, h.Height - S(6));
            var active = (int)_view == i;
            if (active)
            {
                using var brush = new SolidBrush(c.Surface);
                g.FillRectangle(brush, r);
                using var bar = new SolidBrush(c.Primary);
                g.FillRectangle(bar, r.X, r.Y, r.Width, S(3));
            }
            else if (_hotHit == i) Chrome.FillRounded(g, r, c.HoverBack, 4);

            Chrome.DrawText(g, label, active ? BoldFont : Font, r, active ? c.Primary : c.Text, ContentAlignment.MiddleCenter);
            if (Focused && _focusTab == i && !_input.Focused)
            {
                using var ring = new Pen(c.FocusRing, 2f) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(ring, r.X + 2, r.Y + 4, r.Width - 4, r.Height - 6);
            }

            Add(r, HitKind.Tab, i);
            x += w + S(2);
        }

        var usage = _session.TotalUsage;
        if (usage.TotalTokens > 0) Chrome.DrawText(g, $"{usage.TotalTokens.ToString("N0", CultureInfo.CurrentCulture)} tokens used", Font, new Rectangle(x, h.Y, h.Right - x - S(12), h.Height), c.Muted, ContentAlignment.MiddleRight);
    }

    private void PaintButton(Graphics g, ControlColors c, Rectangle r, string text, bool primary, HitKind kind, int index = -1, bool enabled = true)
    {
        var fill = primary ? (enabled ? c.Primary : Chrome.Blend(c.Surface, c.Border, 0.6)) : c.Surface;
        Chrome.FillRounded(g, r, fill, 4);
        if (!primary) Chrome.DrawRoundedBorder(g, r, enabled ? c.Border : Chrome.Blend(c.Border, c.Surface, 0.5), 4);
        Chrome.DrawText(g, text, BoldFont, r, !enabled ? c.Disabled : primary ? c.PrimaryText : c.Text, ContentAlignment.MiddleCenter);
        if (enabled) Add(r, kind, index);
    }

    private void PaintPrompt(Graphics g, ControlColors c)
    {
        var body = Body;
        Chrome.DrawText(g, "What would you like to do?", BoldFont, new Rectangle(body.X + Pad, body.Y + Pad - S(2), body.Width - Pad * 2, S(20)), c.Text);
        var chipsTop = body.Y + Pad + S(22);
        ChipRows(out var rects);
        foreach (var (rect, command) in rects)
        {
            var r = new Rectangle(body.X + Pad + rect.X, chipsTop + rect.Y, rect.Width, rect.Height);
            var selected = _command?.Id == command.Id;
            Chrome.FillRounded(g, r, selected ? c.Primary : c.Surface, r.Height / 2);
            Chrome.DrawRoundedBorder(g, r, selected ? c.Primary : c.Border, r.Height / 2);
            Chrome.DrawText(g, (command.Glyph != null ? command.Glyph + " " : string.Empty) + command.Title, Font, r, selected ? c.PrimaryText : c.Text, ContentAlignment.MiddleCenter);
            Add(r, HitKind.Chip, -1, command);
        }

        var y = chipsTop + ChipsHeight;
        if (_command != null)
        {
            var label = "Command: " + _command.Title + (string.IsNullOrEmpty(_command.Description) ? string.Empty : " — " + _command.Description);
            var r = new Rectangle(body.X + Pad, y + S(4), body.Width - Pad * 2, S(26));
            Chrome.FillRounded(g, r, Chrome.Blend(c.Surface, c.Primary, 0.1), 4);
            Chrome.DrawText(g, label, Font, new Rectangle(r.X + S(8), r.Y, r.Width - S(34), r.Height), c.Text);
            var x = new Rectangle(r.Right - S(26), r.Y, S(24), r.Height);
            Chrome.DrawText(g, "✕", Font, x, c.Muted, ContentAlignment.MiddleCenter);
            Add(x, HitKind.ClearCommand);
        }

        var frame = InputFrame;
        Chrome.DrawFrame(g, frame, c, _input.Focused, false, 8, true);

        var loading = _session.State == AIPromptState.Loading;
        var buttonY = frame.Bottom + S(10);
        Chrome.DrawText(g, "Ctrl+Enter to generate", Font, new Rectangle(frame.X, buttonY, frame.Width - S(140), S(32)), c.Muted, ContentAlignment.MiddleLeft);
        if (loading) PaintButton(g, c, new Rectangle(frame.Right - S(120), buttonY, S(120), S(32)), "■ Stop", false, HitKind.Stop);
        else PaintButton(g, c, new Rectangle(frame.Right - S(120), buttonY, S(120), S(32)), "Generate ▶", true, HitKind.Generate, -1, _input.Text.Trim().Length > 0);

        var suggestions = Suggestions;
        if (suggestions.Count == 0) return;
        var listH = suggestions.Count * S(34) + S(8);
        var list = new Rectangle(frame.X, frame.Y - listH - S(4), frame.Width, listH);
        if (list.Y < body.Y) list.Y = frame.Bottom + S(4);
        Shapes.DrawSoftShadow(g, Chrome.RoundedRect(list, 6), S(5), Color.FromArgb(60, 0, 0, 0), S(2));
        Chrome.FillRounded(g, list, c.Surface, 6);
        Chrome.DrawRoundedBorder(g, list, c.Border, 6);
        for (var i = 0; i < suggestions.Count; i++)
        {
            var r = new Rectangle(list.X + S(4), list.Y + S(4) + i * S(34), list.Width - S(8), S(34));
            if (i == _suggestionIndex) Chrome.FillRounded(g, r, c.SelectionBack, 4);
            Chrome.DrawText(g, $"/{suggestions[i].Id}", BoldFont, new Rectangle(r.X + S(8), r.Y, S(110), r.Height), c.Primary);
            Chrome.DrawText(g, suggestions[i].Description ?? suggestions[i].Title, Font, new Rectangle(r.X + S(124), r.Y, r.Width - S(132), r.Height), c.Muted);
            Add(r, HitKind.Suggestion, i, suggestions[i]);
        }
    }

    private AIPromptHistoryEntry? Shown => _session.Current ?? (_session.History.Count > 0 ? _session.History[^1] : null);

    private void EnsureOutputLayout(string text, int width)
    {
        if (_outputLayout != null && _layoutText == text && _layoutWidth == width) return;
        DropOutputLayout();
        _layoutText = text;
        _layoutWidth = width;
        _outputLayout = MarkupLayout.Create(LabelMarkup.Parse(text, LabelMarkupMode.Markdown), Font, Colors, width, true, HorizontalAlignment.Left, 0, true, Colors.Text);
    }

    private void PaintOutput(Graphics g, ControlColors c)
    {
        var body = Body;
        var entry = Shown;
        if (entry == null)
        {
            Chrome.DrawText(g, "Nothing generated yet. Write a prompt and press Generate.", Font, body, c.Muted, ContentAlignment.MiddleCenter);
            _contentHeight = 0;
            UpdateScroll();
            return;
        }

        var state = _session.State == AIPromptState.Idle ? entry.State : _session.State;
        var y = body.Y + S(10);
        var x = body.X + Pad;

        // prompt summary
        Chrome.DrawText(g, "You asked", Font, new Rectangle(x, y, body.Width - Pad * 2, S(16)), c.Muted);
        Chrome.DrawText(g, entry.Input.ReplaceLineEndings(" "), BoldFont, new Rectangle(x, y + S(16), body.Width - Pad * 2, S(20)), c.Text);

        // toolbar
        var done = state is AIPromptState.Completed;
        var bx = x;
        foreach (var (label, kind, enabled) in new[] { ("Copy", HitKind.Copy, done), ("Insert", HitKind.Insert, done), ("Retry ↻", HitKind.Retry, state != AIPromptState.Loading) })
        {
            var w = Chrome.MeasureText(label, BoldFont) + S(24);
            PaintButton(g, c, new Rectangle(bx, y + S(38) - S(2), w, S(28)), label, false, kind, -1, enabled);
            bx += w + S(8);
        }

        if (done)
        {
            var up = new Rectangle(body.Right - Pad - S(68), y + S(36), S(32), S(28));
            var down = new Rectangle(body.Right - Pad - S(32), y + S(36), S(32), S(28));
            foreach (var (r, glyph, kind, active) in new[] { (up, "👍", HitKind.RateUp, entry.Rating > 0), (down, "👎", HitKind.RateDown, entry.Rating < 0) })
            {
                if (active) Chrome.FillRounded(g, r, Chrome.Blend(c.Surface, c.Primary, 0.2), 4);
                TextRenderer.DrawText(g, glyph, GlyphFonts.Get(Font.SizeInPoints + 1), r, c.Text, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Add(r, kind);
            }
        }

        var area = ScrollArea;
        var top = area.Y;
        if (state == AIPromptState.Loading)
        {
            var bar = new Rectangle(x, body.Y + S(84) + S(6), body.Width - Pad * 2, S(24));
            PaintSpinner(g, c, new Rectangle(bar.X, bar.Y, S(22), S(22)));
            Chrome.DrawText(g, string.IsNullOrEmpty(_session.Output) ? "Generating response…" : "Writing…", Font, new Rectangle(bar.X + S(30), bar.Y, S(220), bar.Height), c.Muted);
            PaintButton(g, c, new Rectangle(bar.Right - S(80), bar.Y - S(2), S(80), S(26)), "■ Stop", false, HitKind.Stop);
        }

        var text = state == AIPromptState.Faulted ? "**The request failed.**\n" + entry.Error : state == AIPromptState.Cancelled && string.IsNullOrEmpty(entry.Response) ? "_The request was cancelled._" : state == AIPromptState.Loading ? _session.Output : entry.Response ?? string.Empty;
        var width = area.Width - Pad * 2 - (_scroll.Visible ? S(12) : 0);
        EnsureOutputLayout(text, Math.Max(S(60), width));
        var layout = _outputLayout!;
        var newHeight = layout.Size.Height + S(20);
        if (newHeight != _contentHeight)
        {
            _contentHeight = newHeight;
            if (state == AIPromptState.Loading) _scrollOffsets[AIPromptView.Output] = Math.Max(0, newHeight - area.Height);
            UpdateScroll();
        }

        var clip = g.Save();
        g.SetClip(area);
        if (state == AIPromptState.Faulted)
        {
            var box = new Rectangle(area.X + Pad - S(6), top + S(4) - ScrollY, Math.Max(10, width + S(12)), layout.Size.Height + S(12));
            Chrome.FillRounded(g, box, Chrome.Blend(c.Surface, c.Danger, 0.1), 6);
        }

        layout.Draw(g, new Point(area.X + Pad, top + S(10) - ScrollY));
        g.Restore(clip);

        var footer = new Rectangle(body.X + Pad, body.Bottom - S(30), body.Width - Pad * 2, S(26));
        using (var pen = new Pen(c.Border)) g.DrawLine(pen, body.X, footer.Y - S(2), body.Right, footer.Y - S(2));
        var u = entry.Usage;
        var meta = state == AIPromptState.Loading ? "Streaming…" : $"Prompt {u.PromptTokens} · Completion {u.CompletionTokens} · Total {u.TotalTokens} tokens · {entry.Duration.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} s";
        Chrome.DrawText(g, meta, Font, footer, c.Muted, ContentAlignment.MiddleLeft);
    }

    private void PaintSpinner(Graphics g, ControlColors c, Rectangle r)
    {
        var angle = (float)(_clock.Seconds * 360 % 360);
        using var back = new Pen(Chrome.Blend(c.Surface, c.Border, 0.8), S(3));
        g.DrawEllipse(back, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        using var arc = new Pen(c.Primary, S(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(arc, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4, angle, 100);
    }

    private void PaintCommands(Graphics g, ControlColors c)
    {
        var area = ScrollArea;
        var commands = _session.Commands.All;
        var rowH = S(54);
        var groups = commands.GroupBy(cmd => cmd.Category ?? "Commands").ToList();
        var y = S(8);
        var rows = new List<(int Y, string? Header, AIPromptCommand? Command)>();
        foreach (var group in groups)
        {
            rows.Add((y, group.Key, null));
            y += S(28);
            foreach (var command in group)
            {
                rows.Add((y, null, command));
                y += rowH;
            }
        }

        var total = y + S(8);
        if (total != _contentHeight) { _contentHeight = total; UpdateScroll(); }
        var clip = g.Save();
        g.SetClip(area);
        if (commands.Count == 0) Chrome.DrawText(g, "No commands registered. Use Session.Commands.Register to add some.", Font, area, c.Muted, ContentAlignment.MiddleCenter);
        var innerW = area.Width - (_scroll.Visible ? S(12) : 0);
        foreach (var (ry, header, command) in rows)
        {
            var top = area.Y + ry - ScrollY;
            if (top > area.Bottom || top + rowH < area.Y) continue;
            if (header != null)
            {
                Chrome.DrawText(g, header.ToUpperInvariant(), BoldFont, new Rectangle(area.X + Pad, top + S(6), innerW - Pad * 2, S(18)), c.Muted);
                continue;
            }

            var r = new Rectangle(area.X + S(8), top, innerW - S(16), rowH - S(4));
            var hot = _hotHit == 100 + _session.Commands.All.ToList().IndexOf(command!);
            Chrome.FillRounded(g, r, hot ? c.HoverBack : c.Surface, 6);
            Chrome.DrawRoundedBorder(g, r, c.Border, 6);
            var glyph = new Rectangle(r.X + S(8), r.Y + S(10), S(30), S(30));
            using (var brush = new SolidBrush(Chrome.Blend(c.Surface, c.Primary, 0.15))) g.FillEllipse(brush, glyph);
            TextRenderer.DrawText(g, command!.Glyph ?? "✦", GlyphFonts.Get(Font.SizeInPoints + 1), glyph, c.Primary, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Chrome.DrawText(g, command.Title, BoldFont, new Rectangle(r.X + S(46), r.Y + S(6), r.Width - S(54), S(20)), c.Text);
            Chrome.DrawText(g, command.Description ?? command.Template.ReplaceLineEndings(" "), Font, new Rectangle(r.X + S(46), r.Y + S(26), r.Width - S(54), S(18)), c.Muted);
            Add(r, HitKind.CommandItem, -1, command);
        }

        g.Restore(clip);
    }

    private void PaintHistory(Graphics g, ControlColors c)
    {
        var body = Body;
        var history = _session.History;
        var bar = new Rectangle(body.X + Pad, body.Y + S(6), body.Width - Pad * 2, S(28));
        Chrome.DrawText(g, history.Count == 0 ? "No requests yet" : $"{history.Count} request{(history.Count == 1 ? string.Empty : "s")}", Font, bar, c.Muted, ContentAlignment.MiddleLeft);
        if (history.Count > 0) PaintButton(g, c, new Rectangle(bar.Right - S(96), bar.Y, S(96), bar.Height), "Clear all", false, HitKind.ClearHistory);

        var area = ScrollArea;
        var rowH = S(62);
        var total = history.Count * rowH + S(8);
        if (total != _contentHeight) { _contentHeight = total; UpdateScroll(); }
        var clip = g.Save();
        g.SetClip(area);
        var innerW = area.Width - (_scroll.Visible ? S(12) : 0);
        for (var i = 0; i < history.Count; i++)
        {
            var entry = history[history.Count - 1 - i];
            var top = area.Y + S(4) + i * rowH - ScrollY;
            if (top > area.Bottom || top + rowH < area.Y) continue;
            var r = new Rectangle(area.X + S(8), top, innerW - S(16), rowH - S(6));
            var hot = _hotHit == 1000 + i;
            Chrome.FillRounded(g, r, hot ? c.HoverBack : c.Surface, 6);
            Chrome.DrawRoundedBorder(g, r, c.Border, 6);
            var dot = entry.State switch { AIPromptState.Completed => c.Success, AIPromptState.Faulted => c.Danger, AIPromptState.Loading => c.Primary, _ => c.Muted };
            using (var brush = new SolidBrush(dot)) g.FillEllipse(brush, r.X + S(10), r.Y + S(12), S(10), S(10));
            Chrome.DrawText(g, entry.Input.ReplaceLineEndings(" "), BoldFont, new Rectangle(r.X + S(28), r.Y + S(6), r.Width - S(36), S(20)), c.Text);
            var meta = $"{entry.Timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {entry.State} · {entry.Usage.TotalTokens} tokens" + (entry.Rating > 0 ? " · 👍" : entry.Rating < 0 ? " · 👎" : string.Empty);
            Chrome.DrawText(g, meta, Font, new Rectangle(r.X + S(28), r.Y + S(28), r.Width - S(36), S(18)), c.Muted);
            Add(r, HitKind.HistoryItem, history.Count - 1 - i, entry);
        }

        g.Restore(clip);
    }

    // ---- interaction ----

    private Hit? HitAt(Point p) => _hits.LastOrDefault(h => h.Bounds.Contains(p));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitAt(e.Location);
        var hot = -1;
        if (hit?.Kind == HitKind.Tab) hot = hit.Index;
        else if (hit?.Kind == HitKind.CommandItem) hot = 100 + _session.Commands.All.ToList().IndexOf((AIPromptCommand)hit.Payload!);
        else if (hit?.Kind == HitKind.HistoryItem) hot = 1000 + (_session.History.Count - 1 - hit.Index);
        Cursor = hit != null ? Cursors.Hand : Cursors.Default;
        if (hot == _hotHit) return;
        _hotHit = hot;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotHit < 0) return;
        _hotHit = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        if (HitAt(e.Location) is not { } hit) return;
        var entry = Shown;
        switch (hit.Kind)
        {
            case HitKind.Tab: ActiveView = (AIPromptView)hit.Index; break;
            case HitKind.Chip:
                var chip = (AIPromptCommand)hit.Payload!;
                _command = _command?.Id == chip.Id ? null : chip;
                _input.Focus();
                Invalidate();
                break;
            case HitKind.Suggestion: PickSuggestion((AIPromptCommand)hit.Payload!); _input.Focus(); break;
            case HitKind.ClearCommand: _command = null; Invalidate(); break;
            case HitKind.Generate: Generate(); break;
            case HitKind.Stop: _session.Cancel(); break;
            case HitKind.Copy when entry?.Response != null: CopyOutput(entry.Response); break;
            case HitKind.Insert when entry?.Response != null: InsertRequested?.Invoke(this, entry.Response); break;
            case HitKind.Retry: if (_session.History.Count > 0) _ = RetryAsync(); break;
            case HitKind.RateUp when entry != null: _session.Rate(entry.Id, entry.Rating > 0 ? 0 : 1); break;
            case HitKind.RateDown when entry != null: _session.Rate(entry.Id, entry.Rating < 0 ? 0 : -1); break;
            case HitKind.CommandItem:
                _command = (AIPromptCommand)hit.Payload!;
                ActiveView = AIPromptView.Prompt;
                _input.Focus();
                break;
            case HitKind.HistoryItem:
                var chosen = (AIPromptHistoryEntry)hit.Payload!;
                _input.Text = chosen.Input;
                _command = _session.Commands.Find(chosen.CommandId);
                ActiveView = AIPromptView.Output;
                break;
            case HitKind.ClearHistory: _session.ClearHistory(); break;
        }
    }

    private async Task RetryAsync()
    {
        if (_session.State == AIPromptState.Loading) return;
        ActiveView = AIPromptView.Output;
        await _session.RetryAsync().ConfigureAwait(true);
        Invalidate();
    }

    private void CopyOutput(string text)
    {
        try
        {
            Clipboard.SetText(text);
            OutputCopied?.Invoke(this, text);
        }
        catch (ExternalException)
        {
            // The clipboard can be locked by another process; the user can press Copy again.
        }
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: _focusTab = Math.Max(0, _focusTab - 1); ActiveView = (AIPromptView)_focusTab; break;
            case Keys.Right: _focusTab = Math.Min(3, _focusTab + 1); ActiveView = (AIPromptView)_focusTab; break;
            case Keys.Escape when _session.State == AIPromptState.Loading: _session.Cancel(); break;
            case Keys.PageDown when _scroll.Visible: _scroll.Value += _scroll.LargeChange; break;
            case Keys.PageUp when _scroll.Visible: _scroll.Value -= _scroll.LargeChange; break;
            default: return;
        }

        e.Handled = true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () => _session.State switch
    {
        AIPromptState.Loading => "Generating response",
        AIPromptState.Completed => "Response ready: " + _session.Output,
        AIPromptState.Faulted => "Request failed",
        AIPromptState.Cancelled => "Request cancelled",
        _ => "Ready"
    }, () => _session.State == AIPromptState.Loading ? AccessibleStates.Busy : AccessibleStates.None);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Detach(_session);
            _session.Cancel();
            _clock.Dispose();
            DropOutputLayout();
        }

        base.Dispose(disposing);
    }
}
