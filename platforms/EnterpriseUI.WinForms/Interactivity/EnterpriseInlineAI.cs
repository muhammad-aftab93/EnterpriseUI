using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum InlineAIState { Closed, Menu, Working, Result, Error }

public sealed class InlineAIApplyingEventArgs : CancelEventArgs
{
    public InlineAIApplyingEventArgs(string original, string result)
    {
        Original = original;
        Result = result;
    }

    public string Original { get; }

    /// <summary>The text that replaces the selection; handlers may edit it.</summary>
    public string Result { get; set; }
}

/// <summary>Adds an AI action menu to a text box: summarize, rewrite, expand, change tone or a free instruction applied to the selection (or the whole text) with a preview before it replaces anything.</summary>
[DefaultProperty("Target")]
public class EnterpriseInlineAIAssistant : Component
{
    private readonly AIPromptSession _session = new(new SampleAIPromptProvider());
    private TextBoxBase? _target;
    private InlineAIWindow? _window;
    private SparkButton? _spark;
    private int _start, _length;
    private string _original = string.Empty;
    private InlineAIState _state;

    public EnterpriseInlineAIAssistant()
    {
        foreach (var command in SampleAIPromptProvider.DefaultCommands) _session.Commands.Register(command);
    }

    public EnterpriseInlineAIAssistant(IContainer container) : this() => container.Add(this);

    /// <summary>Text box the assistant works in.</summary>
    [DefaultValue(null)]
    public TextBoxBase? Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            if (_target != null) Unhook(_target);
            _target = value;
            if (value != null) Hook(value);
        }
    }

    [Browsable(false)]
    public IAIPromptProvider? Provider
    {
        get => _session.Provider;
        set => _session.Provider = value;
    }

    [Browsable(false)]
    public AIPromptCommandRegistry Commands => _session.Commands;

    /// <summary>Key combination that opens the menu inside the text box.</summary>
    [DefaultValue(Keys.Control | Keys.Space)]
    public Keys TriggerKey { get; set; } = Keys.Control | Keys.Space;

    /// <summary>Shows a small sparkle button next to a selection.</summary>
    [DefaultValue(true)]
    public bool ShowSelectionButton { get; set; } = true;

    /// <summary>Extra information (document title, audience, language) sent along with each request.</summary>
    [DefaultValue(null)]
    public string? Context
    {
        get => _session.Context;
        set => _session.Context = value;
    }

    [Browsable(false)]
    public InlineAIState State => _state;

    public event EventHandler<InlineAIApplyingEventArgs>? Applying;
    public event EventHandler? Applied;
    public event EventHandler? StateChanged;

    private void Hook(TextBoxBase box)
    {
        box.KeyDown += OnKeyDown;
        box.MouseUp += OnSelectionMaybeChanged;
        box.KeyUp += OnSelectionMaybeChanged;
        box.Leave += OnLeave;
        box.TextChanged += OnTextChanged;
        box.Disposed += OnTargetDisposed;
    }

    private void Unhook(TextBoxBase box)
    {
        box.KeyDown -= OnKeyDown;
        box.MouseUp -= OnSelectionMaybeChanged;
        box.KeyUp -= OnSelectionMaybeChanged;
        box.Leave -= OnLeave;
        box.TextChanged -= OnTextChanged;
        box.Disposed -= OnTargetDisposed;
        HideSpark();
        Close();
    }

    private void OnTargetDisposed(object? sender, EventArgs e)
    {
        HideSpark();
        Close();
        _target = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyData != TriggerKey || _target is not { ReadOnly: false }) return;
        e.SuppressKeyPress = true;
        e.Handled = true;
        Show();
    }

    private void OnSelectionMaybeChanged(object? sender, EventArgs e)
    {
        if (_target == null || _window != null) return;
        if (ShowSelectionButton && _target.SelectionLength > 0 && !_target.ReadOnly) ShowSpark();
        else HideSpark();
    }

    private void OnLeave(object? sender, EventArgs e)
    {
        if (_window == null) HideSpark();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_window == null) HideSpark();
    }

    private void ShowSpark()
    {
        if (_target == null || !_target.IsHandleCreated) return;
        var index = Math.Min(_target.TextLength, _target.SelectionStart + _target.SelectionLength);
        var p = _target.GetPositionFromCharIndex(index);
        p = new Point(Math.Clamp(p.X + 6, 0, Math.Max(0, _target.ClientSize.Width - 30)), Math.Clamp(p.Y - 4, 0, Math.Max(0, _target.ClientSize.Height - 30)));
        _spark ??= new SparkButton(this);
        _spark.Place(_target.PointToScreen(p));
    }

    private void HideSpark()
    {
        _spark?.Dispose();
        _spark = null;
    }

    /// <summary>Opens the menu for the current selection (or the whole text when nothing is selected).</summary>
    public void Show()
    {
        if (_target == null || _window != null || !_target.IsHandleCreated) return;
        HideSpark();
        _start = _target.SelectionLength > 0 ? _target.SelectionStart : 0;
        _length = _target.SelectionLength > 0 ? _target.SelectionLength : _target.TextLength;
        _original = _target.Text.Substring(_start, Math.Min(_length, _target.TextLength - _start));
        _target.HideSelection = false;
        _window = new InlineAIWindow(this, _target);
        SetState(InlineAIState.Menu);
        _window.Open();
    }

    public void Close()
    {
        _session.Cancel();
        _window?.CloseWindow();
    }

    internal void WindowClosed()
    {
        _window = null;
        if (_target is { IsDisposed: false }) _target.HideSelection = true;
        SetState(InlineAIState.Closed);
    }

    internal AIPromptSession Session => _session;
    internal string Original => _original;
    internal bool HasSelection => _length > 0 && _original.Length > 0;

    internal void SetState(InlineAIState state)
    {
        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs a command or a free instruction and returns the suggestion, or null when it failed or was cancelled.</summary>
    public async Task<string?> RunAsync(string? commandId, string? instruction = null)
    {
        if (_target == null || string.IsNullOrWhiteSpace(_original) || _session.State == AIPromptState.Loading) return null;
        SetState(InlineAIState.Working);
        if (commandId == null)
        {
            commandId = "custom";
            _session.Commands.Register(new AIPromptCommand(commandId, "Custom", (instruction ?? "Edit the text") + ":\n\n{input}"));
        }

        var entry = await _session.SubmitAsync(_original, commandId).ConfigureAwait(true);
        if (_window != null) SetState(entry.State == AIPromptState.Completed ? InlineAIState.Result : entry.State == AIPromptState.Faulted ? InlineAIState.Error : InlineAIState.Menu);
        return entry.State == AIPromptState.Completed ? entry.Response : null;
    }

    /// <summary>Replaces the original text with the result after raising <see cref="Applying"/>.</summary>
    public bool Apply(string result)
    {
        if (_target == null) return false;
        var args = new InlineAIApplyingEventArgs(_original, result);
        Applying?.Invoke(this, args);
        if (args.Cancel) return false;
        var length = Math.Min(_length, _target.TextLength - _start);
        _target.Select(_start, length);
        _target.SelectedText = args.Result;
        _target.Select(_start, args.Result.Length);
        _target.Focus();
        Applied?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_target != null) Unhook(_target);
            _target = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>Small sparkle button shown beside a selection.</summary>
    private sealed class SparkButton : LayeredPopupForm
    {
        private readonly EnterpriseInlineAIAssistant _owner;
        private readonly ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
        private bool _hot;
        private Point _location;

        public SparkButton(EnterpriseInlineAIAssistant owner) => _owner = owner;

        public void Place(Point screen)
        {
            _location = screen;
            Draw();
            if (!Visible) Show();
        }

        private void Draw()
        {
            var size = (int)Math.Round(26 * DeviceDpi / 96.0);
            var shadow = 6;
            Render(new Size(size + shadow * 2, size + shadow * 2), new Point(_location.X - shadow, _location.Y - shadow), g =>
            {
                var rect = new Rectangle(shadow, shadow, size, size);
                using var path = Shapes.Circle(rect);
                Shapes.DrawSoftShadow(g, path, 4, Color.FromArgb(70, 0, 0, 0), 2);
                using (var brush = new SolidBrush(_hot ? Chrome.Blend(_colors.Primary, Color.White, 0.15) : _colors.Primary)) g.FillEllipse(brush, rect);
                using var star = Shapes.Star(new RectangleF(rect.X + size * 0.22f, rect.Y + size * 0.22f, size * 0.56f, size * 0.56f), 4, 0.35);
                using var fill = new SolidBrush(_colors.PrimaryText);
                g.FillPath(fill, star);
            });
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Cursor = Cursors.Hand;
            Draw();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Draw();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) _owner.Show();
        }
    }
}

/// <summary>The popup that hosts the action list, progress and the result preview.</summary>
internal sealed class InlineAIWindow : Form
{
    private enum HitKind { Row, Accept, Retry, Discard, Cancel, Close }

    private sealed record Hit(Rectangle Bounds, HitKind Kind, int Index = -1);

    private readonly EnterpriseInlineAIAssistant _owner;
    private readonly TextBoxBase _target;
    private readonly ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
    private readonly TextBox _prompt = new() { BorderStyle = BorderStyle.None };
    private readonly FrameClock _clock = new() { Interval = 30 };
    private readonly List<Hit> _hits = new();
    private readonly float _scale;
    private List<(string Id, string Title, string? Description, string? Glyph, string? Category)> _rows = new();
    private int _selected;
    private string? _lastCommand;
    private string? _lastInstruction;
    private string _result = string.Empty;
    private bool _closing;
    private int _hot = -1;

    public InlineAIWindow(EnterpriseInlineAIAssistant owner, TextBoxBase target)
    {
        _owner = owner;
        _target = target;
        _scale = DeviceDpi / 96f;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9f);
        BackColor = _colors.Surface;
        _prompt.Font = Font;
        _prompt.BackColor = _colors.Surface;
        _prompt.ForeColor = _colors.Text;
        _prompt.PlaceholderText = "Ask AI to edit this text…";
        _prompt.AccessibleName = "AI instruction";
        _prompt.TextChanged += (_, _) => { RefreshRows(); _selected = 0; Relayout(); };
        _prompt.KeyDown += OnPromptKeyDown;
        Controls.Add(_prompt);
        _clock.Frame += (_, _) => { if (_owner.State == InlineAIState.Working) Invalidate(); };
        _owner.StateChanged += OnOwnerState;
        RefreshRows();
        Deactivate += (_, _) => { if (!_closing && _owner.State != InlineAIState.Working) CloseWindow(); };
    }

    private int S(double v) => (int)Math.Round(v * _scale);
    private int Pad => S(12);
    private int WidthPx => S(400);

    private void RefreshRows()
    {
        var text = _prompt.Text.Trim();
        _rows = _owner.Commands.Search(text).Take(9).Select(c => (c.Id, c.Title, c.Description, c.Glyph, c.Category)).ToList();
        if (text.Length > 0) _rows.Add(("custom", "Ask AI: " + text, "Apply your instruction to the text", "✎", null));
    }

    public void Open()
    {
        Relayout();
        Owner = _target.FindForm();
        Show();
        Activate();
        _prompt.Focus();
        _clock.Enabled = true;
    }

    private void Relayout()
    {
        if (_closing) return;
        var state = _owner.State;
        var height = state switch
        {
            InlineAIState.Menu => S(52) + _rows.Count * S(36) + S(14),
            InlineAIState.Working => S(110),
            InlineAIState.Error => S(130),
            _ => S(70) + ResultHeight() + S(56)
        };
        _prompt.Visible = state == InlineAIState.Menu;
        _prompt.SetBounds(Pad + S(26), S(14), WidthPx - Pad * 2 - S(26), _prompt.PreferredHeight);

        var anchor = CaretScreen();
        var screen = Screen.FromPoint(anchor).WorkingArea;
        var x = Math.Clamp(anchor.X, screen.Left + S(4), Math.Max(screen.Left + S(4), screen.Right - WidthPx - S(4)));
        var y = anchor.Y + S(22);
        if (y + height > screen.Bottom) y = Math.Max(screen.Top + S(4), anchor.Y - height - S(6));
        SetBounds(x, y, WidthPx, height);
        Invalidate();
    }

    private Point CaretScreen()
    {
        var index = Math.Min(_target.TextLength, _target.SelectionStart + Math.Max(0, _target.SelectionLength));
        var p = _target.GetPositionFromCharIndex(index);
        return _target.PointToScreen(new Point(Math.Clamp(p.X, 0, _target.ClientSize.Width), Math.Clamp(p.Y, 0, _target.ClientSize.Height)));
    }

    private int ResultHeight() => Math.Min(S(170), Math.Max(S(40), TextRenderer.MeasureText(_result, Font, new Size(WidthPx - Pad * 2, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + S(10)));

    private void OnOwnerState(object? sender, EventArgs e)
    {
        if (_closing) return;
        if (_owner.State == InlineAIState.Result) _result = _owner.Session.Output;
        Relayout();
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down) { _selected = Math.Min(_rows.Count - 1, _selected + 1); e.SuppressKeyPress = true; Invalidate(); }
        else if (e.KeyCode == Keys.Up) { _selected = Math.Max(0, _selected - 1); e.SuppressKeyPress = true; Invalidate(); }
        else if (e.KeyCode == Keys.Enter && _rows.Count > 0) { e.SuppressKeyPress = true; Run(_selected); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            if (_owner.State == InlineAIState.Working) _owner.Session.Cancel();
            else CloseWindow();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _owner.State == InlineAIState.Result) { Accept(); e.Handled = true; }
    }

    private async void Run(int row)
    {
        if (row < 0 || row >= _rows.Count) return;
        var item = _rows[row];
        _lastCommand = item.Id == "custom" ? null : item.Id;
        _lastInstruction = item.Id == "custom" ? _prompt.Text.Trim() : null;
        await RunLast().ConfigureAwait(true);
    }

    private async Task RunLast()
    {
        _owner.SetState(InlineAIState.Working);
        await _owner.RunAsync(_lastCommand, _lastInstruction).ConfigureAwait(true);
        if (_closing) return;
        if (_owner.State == InlineAIState.Working) _owner.SetState(InlineAIState.Menu);
        Relayout();
    }

    private void Accept()
    {
        if (_owner.Apply(_owner.Session.Output)) CloseWindow();
    }

    public void CloseWindow()
    {
        if (_closing) return;
        _closing = true;
        _clock.Enabled = false;
        _owner.StateChanged -= OnOwnerState;
        _owner.WindowClosed();
        Close();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var c = _colors;
        g.Clear(c.Surface);
        _hits.Clear();
        switch (_owner.State)
        {
            case InlineAIState.Menu: PaintMenu(g, c); break;
            case InlineAIState.Working: PaintWorking(g, c); break;
            case InlineAIState.Result: PaintResult(g, c); break;
            case InlineAIState.Error: PaintError(g, c); break;
        }

        using var pen = new Pen(c.Primary, 1.5f);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void PaintMenu(Graphics g, ControlColors c)
    {
        TextRenderer.DrawText(g, "✦", GlyphFonts.Get(Font.SizeInPoints + 2), new Rectangle(Pad, S(10), S(26), S(26)), c.Primary, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        using (var pen = new Pen(c.Border)) g.DrawLine(pen, 0, S(44), Width, S(44));
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var r = new Rectangle(S(6), S(50) + i * S(36), Width - S(12), S(34));
            if (i == _selected || i == _hot) Chrome.FillRounded(g, r, i == _selected ? c.SelectionBack : c.HoverBack, 4);
            TextRenderer.DrawText(g, row.Glyph ?? "✦", GlyphFonts.Get(Font.SizeInPoints + 1), new Rectangle(r.X + S(6), r.Y, S(26), r.Height), c.Primary, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            Chrome.DrawText(g, row.Title, Font, new Rectangle(r.X + S(38), r.Y, S(140), r.Height), c.Text);
            if (row.Description != null) Chrome.DrawText(g, row.Description, Font, new Rectangle(r.X + S(180), r.Y, r.Width - S(190), r.Height), c.Muted);
            _hits.Add(new Hit(r, HitKind.Row, i));
        }
    }

    private void PaintWorking(Graphics g, ControlColors c)
    {
        var angle = (float)(_clock.Seconds * 360 % 360);
        var spinner = new Rectangle(Pad, S(20), S(26), S(26));
        using (var back = new Pen(Chrome.Blend(c.Surface, c.Border, 0.8), S(3))) g.DrawEllipse(back, spinner);
        using (var arc = new Pen(c.Primary, S(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(arc, spinner, angle, 100);
        Chrome.DrawText(g, "Working on your text…", BoldFont, new Rectangle(Pad + S(38), S(18), Width - Pad * 2 - S(38), S(20)), c.Text);
        var partial = _owner.Session.Output;
        Chrome.DrawText(g, partial.Length == 0 ? "Waiting for the model" : partial.ReplaceLineEndings(" "), Font, new Rectangle(Pad + S(38), S(40), Width - Pad * 2 - S(38), S(20)), c.Muted);
        Button(g, c, new Rectangle(Width - Pad - S(80), Height - S(38), S(80), S(28)), "Cancel", false, HitKind.Cancel);
    }

    private Font BoldFont => _bold ??= new Font(Font, FontStyle.Bold);
    private Font? _bold;

    private void Button(Graphics g, ControlColors c, Rectangle r, string text, bool primary, HitKind kind)
    {
        Chrome.FillRounded(g, r, primary ? c.Primary : c.Surface, 4);
        if (!primary) Chrome.DrawRoundedBorder(g, r, c.Border, 4);
        Chrome.DrawText(g, text, BoldFont, r, primary ? c.PrimaryText : c.Text, ContentAlignment.MiddleCenter);
        _hits.Add(new Hit(r, kind));
    }

    private void PaintResult(Graphics g, ControlColors c)
    {
        Chrome.DrawText(g, "Suggestion", BoldFont, new Rectangle(Pad, S(10), S(200), S(20)), c.Primary);
        var before = WordCount(_owner.Original);
        var after = WordCount(_result);
        Chrome.DrawText(g, $"{before} → {after} words", Font, new Rectangle(Width - Pad - S(160), S(10), S(160), S(20)), c.Muted, ContentAlignment.MiddleRight);
        var box = new Rectangle(Pad, S(36), Width - Pad * 2, ResultHeight());
        Chrome.FillRounded(g, box, Chrome.Blend(c.Surface, c.Primary, 0.06), 6);
        Chrome.DrawRoundedBorder(g, box, c.Border, 6);
        Chrome.DrawText(g, _result, Font, Rectangle.Inflate(box, -S(8), -S(5)), c.Text, ContentAlignment.TopLeft, true);
        var y = box.Bottom + S(12);
        Button(g, c, new Rectangle(Width - Pad - S(84), y, S(84), S(30)), "Accept", true, HitKind.Accept);
        Button(g, c, new Rectangle(Width - Pad - S(84) - S(90), y, S(84), S(30)), "Retry ↻", false, HitKind.Retry);
        Button(g, c, new Rectangle(Width - Pad - S(84) - S(180), y, S(84), S(30)), "Discard", false, HitKind.Discard);
    }

    private void PaintError(Graphics g, ControlColors c)
    {
        Chrome.DrawText(g, "✖ The request failed", BoldFont, new Rectangle(Pad, S(12), Width - Pad * 2, S(20)), c.Danger);
        var entry = _owner.Session.Current;
        Chrome.DrawText(g, entry?.Error ?? "Unknown error", Font, new Rectangle(Pad, S(38), Width - Pad * 2, S(40)), c.Text, ContentAlignment.TopLeft, true);
        Button(g, c, new Rectangle(Width - Pad - S(84), Height - S(40), S(84), S(28)), "Retry ↻", true, HitKind.Retry);
        Button(g, c, new Rectangle(Width - Pad - S(84) - S(90), Height - S(40), S(84), S(28)), "Close", false, HitKind.Close);
    }

    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = _hits.LastOrDefault(h => h.Bounds.Contains(e.Location));
        Cursor = hit != null ? Cursors.Hand : Cursors.Default;
        var hot = hit?.Kind == HitKind.Row ? hit.Index : -1;
        if (hot == _hot) return;
        _hot = hot;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _hits.LastOrDefault(h => h.Bounds.Contains(e.Location)) is not { } hit) return;
        switch (hit.Kind)
        {
            case HitKind.Row: Run(hit.Index); break;
            case HitKind.Accept: Accept(); break;
            case HitKind.Retry: _ = RunLast(); break;
            case HitKind.Discard or HitKind.Close: CloseWindow(); break;
            case HitKind.Cancel: _owner.Session.Cancel(); break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _bold?.Dispose();
            Font.Dispose();
        }

        base.Dispose(disposing);
    }
}
