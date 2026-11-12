using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public sealed class SmartPasteBinding
{
    internal SmartPasteBinding(Control control, SmartPasteField field)
    {
        Control = control;
        Field = field;
    }

    public Control Control { get; }
    public SmartPasteField Field { get; }
}

public sealed class SmartPastingEventArgs : CancelEventArgs
{
    public SmartPastingEventArgs(string text, SmartPasteResult result)
    {
        Text = text;
        Result = result;
    }

    public string Text { get; }

    /// <summary>What will be written to the fields; handlers may add, change or remove assignments.</summary>
    public SmartPasteResult Result { get; }
}

public sealed class SmartPastedEventArgs : EventArgs
{
    public SmartPastedEventArgs(SmartPasteResult result, IReadOnlyList<string> filled)
    {
        Result = result;
        FilledFields = filled;
    }

    public SmartPasteResult Result { get; }
    public IReadOnlyList<string> FilledFields { get; }
}

/// <summary>Reads the clipboard, works out what the text is (address, labeled lines, JSON, a table row, code) and fills the bound form controls.</summary>
public class EnterpriseSmartPasteButton : EnterpriseButtonBase
{
    private readonly List<SmartPasteBinding> _bindings = new();
    private readonly List<Action> _undo = new();
    private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 2400 };
    private readonly FrameClock _spinner = new();
    private string? _feedback;
    private bool _busy;

    public EnterpriseSmartPasteButton()
    {
        Size = new Size(160, 34);
        Text = "Smart Paste";
        Glyph = "📋";
        Variant = EnterpriseButtonVariant.Outline;
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); _feedback = null; Invalidate(); };
        _spinner.Frame += (_, _) => Invalidate();
    }

    [Browsable(false)]
    public IReadOnlyList<SmartPasteBinding> Bindings => _bindings;

    /// <summary>Optional model-backed mapper for fields the built-in recognizers could not place.</summary>
    [Browsable(false)]
    public ISmartPasteProvider? Provider { get; set; }

    [Browsable(false)]
    public bool CanUndo => _undo.Count > 0;

    public event EventHandler<SmartPastingEventArgs>? Pasting;
    public event EventHandler<SmartPastedEventArgs>? Pasted;
    public event EventHandler<Exception>? Failed;

    /// <summary>Maps clipboard text to <paramref name="control"/>; the field name defaults to the control name.</summary>
    public SmartPasteBinding Bind(Control control, SmartPasteFieldKind kind = SmartPasteFieldKind.Text, string? fieldName = null, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(control);
        var name = fieldName ?? (string.IsNullOrEmpty(control.Name) ? $"Field{_bindings.Count + 1}" : control.Name);
        var binding = new SmartPasteBinding(control, new SmartPasteField(name, kind, aliases));
        _bindings.RemoveAll(b => b.Control == control);
        _bindings.Add(binding);
        return binding;
    }

    public void Unbind(Control control) => _bindings.RemoveAll(b => b.Control == control);

    protected override string DisplayText => _busy ? "Reading…" : _feedback ?? Text;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _ = PasteAsync();
    }

    private static string ReadClipboard()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty; }
            catch (ExternalException) { Thread.Sleep(20); }
        }

        return string.Empty;
    }

    private void Feedback(string text)
    {
        _feedback = text;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
        Invalidate();
    }

    /// <summary>Pastes <paramref name="text"/> (or the clipboard) into the bound fields; returns null when nothing was applied.</summary>
    public async Task<SmartPasteResult?> PasteAsync(string? text = null)
    {
        if (_busy || _bindings.Count == 0) return null;
        var raw = text ?? ReadClipboard();
        if (string.IsNullOrWhiteSpace(raw))
        {
            Feedback("Clipboard is empty");
            return null;
        }

        var fields = _bindings.Select(b => b.Field).ToList();
        var result = SmartPasteMapper.Map(raw, fields);
        if (Provider != null && result.UnmatchedFields.Count > 0)
        {
            _busy = true;
            _spinner.Enabled = true;
            Invalidate();
            try
            {
                var open = fields.Where(f => result.UnmatchedFields.Contains(f.Name)).ToList();
                var extra = await Provider.MapAsync(raw, open, CancellationToken.None).ConfigureAwait(true);
                foreach (var (name, value) in extra)
                {
                    if (string.IsNullOrWhiteSpace(value) || result.Value(name) != null || fields.All(f => !string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                    result.Assignments.Add(new SmartPasteAssignment(fields.First(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)).Name, value, 0.6, "provider"));
                    result.UnmatchedFields.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Failed?.Invoke(this, ex);
            }
            finally
            {
                _busy = false;
                _spinner.Enabled = false;
            }
        }

        var args = new SmartPastingEventArgs(raw, result);
        Pasting?.Invoke(this, args);
        if (args.Cancel) return null;

        _undo.Clear();
        var filled = new List<string>();
        foreach (var assignment in result.Assignments)
        {
            var binding = _bindings.Find(b => string.Equals(b.Field.Name, assignment.FieldName, StringComparison.OrdinalIgnoreCase));
            if (binding == null || !ApplyValue(binding.Control, assignment.Value)) continue;
            filled.Add(assignment.FieldName);
        }

        Feedback(filled.Count == 0 ? "Nothing recognized" : $"✓ {filled.Count} field{(filled.Count == 1 ? string.Empty : "s")} filled");
        Pasted?.Invoke(this, new SmartPastedEventArgs(result, filled));
        return filled.Count == 0 ? null : result;
    }

    /// <summary>Restores the values the last paste replaced.</summary>
    public void Undo()
    {
        for (var i = _undo.Count - 1; i >= 0; i--) _undo[i]();
        _undo.Clear();
        Feedback("Paste undone");
    }

    private bool ApplyValue(Control control, string value)
    {
        switch (control)
        {
            case ComboBox combo:
            {
                var before = combo.SelectedIndex;
                var beforeText = combo.Text;
                var index = combo.FindStringExact(value);
                if (index >= 0) combo.SelectedIndex = index;
                else if (combo.DropDownStyle != ComboBoxStyle.DropDownList) combo.Text = value;
                else return false;
                _undo.Add(() => { if (before >= 0) combo.SelectedIndex = before; else combo.Text = beforeText; });
                return true;
            }
            case CheckBox check:
            {
                var before = check.Checked;
                check.Checked = value.Trim().ToLowerInvariant() is "true" or "yes" or "y" or "1" or "on" or "checked";
                _undo.Add(() => check.Checked = before);
                return true;
            }
            case NumericUpDown number:
            {
                var clean = new string(value.Where(ch => char.IsDigit(ch) || ch is '.' or '-' or ',').ToArray());
                if (!decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && !decimal.TryParse(clean, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed)) return false;
                var before = number.Value;
                number.Value = Math.Clamp(parsed, number.Minimum, number.Maximum);
                _undo.Add(() => number.Value = before);
                return true;
            }
            case DateTimePicker date:
            {
                if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed) && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return false;
                var before = date.Value;
                date.Value = parsed < date.MinDate ? date.MinDate : parsed > date.MaxDate ? date.MaxDate : parsed;
                _undo.Add(() => date.Value = before);
                return true;
            }
            default:
            {
                if (control is TextBoxBase { ReadOnly: true }) return false;
                var before = control.Text;
                control.Text = value;
                _undo.Add(() => control.Text = before);
                return true;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintStandard(e.Graphics);
        if (!_busy) return;
        var g = e.Graphics;
        var r = new Rectangle(FaceBounds.X + S(10), FaceBounds.Y + (FaceBounds.Height - S(16)) / 2, S(16), S(16));
        using var back = new SolidBrush(Colors.Surface);
        g.FillRectangle(back, Rectangle.Inflate(r, 3, 3));
        using var arc = new Pen(Colors.Primary, S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(arc, r, (float)(_spinner.Seconds * 360 % 360), 100);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _feedbackTimer.Dispose();
            _spinner.Dispose();
        }

        base.Dispose(disposing);
    }
}

public enum SpeechInsertMode { AtCaret, Append, Replace }

/// <summary>Microphone toggle that streams audio to a recognition provider, shows a live waveform and partial text, and inserts the final transcript into a text box.</summary>
public class EnterpriseSpeechToTextButton : EnterpriseButtonBase
{
    private readonly float[] _levels = new float[96];
    private readonly FrameClock _clock = new() { Interval = 33 };
    private readonly System.Windows.Forms.Timer _limit = new();
    private SpeechDictationSession? _session;
    private IAudioInput? _input;
    private SpeechSessionState _state;
    private string _partial = string.Empty;
    private string? _error;
    private float _level;
    private bool _discard;
    private int _head;

    public EnterpriseSpeechToTextButton()
    {
        Size = new Size(200, 40);
        Text = "Dictate";
        AccessibleRole = AccessibleRole.PushButton;
        _clock.Frame += (_, _) => Tick();
        _limit.Tick += (_, _) => _ = StopListeningAsync();
    }

    /// <summary>Text box that receives the transcript.</summary>
    [DefaultValue(null)]
    public Control? Target { get; set; }

    [DefaultValue(SpeechInsertMode.AtCaret)]
    public SpeechInsertMode InsertMode { get; set; }

    [DefaultValue("en-US")]
    public string Language { get; set; } = "en-US";

    [DefaultValue(true)]
    public bool ShowWaveform { get; set; } = true;

    [DefaultValue(true)]
    public bool ShowPartialText { get; set; } = true;

    /// <summary>Longest recording in seconds before it stops by itself; 0 means unlimited.</summary>
    [DefaultValue(60)]
    public int MaxDuration { get; set; } = 60;

    [Browsable(false)]
    public ISpeechRecognitionProvider? Provider { get; set; }

    /// <summary>Creates the audio source for each recording; the default is the Windows microphone.</summary>
    [Browsable(false)]
    public Func<IAudioInput>? AudioInputFactory { get; set; }

    [Browsable(false)]
    public SpeechSessionState State => _state;

    [Browsable(false)]
    public bool IsListening => _state == SpeechSessionState.Listening;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? PartialTranscript;
    public event EventHandler<string>? FinalTranscript;
    public event EventHandler<Exception>? Error;

    private void SetState(SpeechSessionState state)
    {
        if (_state == state) return;
        _state = state;
        _clock.Enabled = IsHandleCreated && state is SpeechSessionState.Listening or SpeechSessionState.Starting or SpeechSessionState.Processing;
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        StateChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override string DisplayText => _state switch
    {
        SpeechSessionState.Starting => "Starting…",
        SpeechSessionState.Listening => ShowPartialText && _partial.Length > 0 ? _partial : "Listening…",
        SpeechSessionState.Processing => "Transcribing…",
        SpeechSessionState.Faulted => "Microphone error",
        _ => Text
    };

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_state == SpeechSessionState.Listening) _ = StopListeningAsync();
        else if (_state is SpeechSessionState.Idle or SpeechSessionState.Faulted) _ = StartListeningAsync();
    }

    public async Task StartListeningAsync()
    {
        if (_state is not (SpeechSessionState.Idle or SpeechSessionState.Faulted)) return;
        _error = null;
        _partial = string.Empty;
        _discard = false;
        Array.Clear(_levels);
        if (Provider == null)
        {
            Fail(new InvalidOperationException("No speech recognition provider is configured."));
            return;
        }

        SetState(SpeechSessionState.Starting);
        try
        {
            _input = AudioInputFactory?.Invoke() ?? new WaveInAudioInput();
            _session = new SpeechDictationSession(_input, Provider, new SpeechRecognitionOptions(Language));
            _session.LevelChanged += (_, level) => { _level = (float)level; _head = (_head + 1) % _levels.Length; _levels[_head] = _level; };
            _session.PartialTextChanged += (_, text) => { _partial = text; PartialTranscript?.Invoke(this, text); Invalidate(); };
            _session.FinalTextAvailable += (_, text) =>
            {
                if (_discard) return;
                Insert(text);
                FinalTranscript?.Invoke(this, text);
            };
            _session.StateChanged += (_, _) => OnSessionState();
            await _session.StartAsync().ConfigureAwait(true);
            if (_session?.State == SpeechSessionState.Listening && MaxDuration > 0)
            {
                _limit.Interval = MaxDuration * 1000;
                _limit.Start();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(ex);
        }
    }

    private void OnSessionState()
    {
        if (_session == null) return;
        switch (_session.State)
        {
            case SpeechSessionState.Listening: SetState(SpeechSessionState.Listening); break;
            case SpeechSessionState.Faulted: Fail(_session.Error ?? new InvalidOperationException("Speech recognition failed.")); break;
        }
    }

    private void Fail(Exception ex)
    {
        _limit.Stop();
        _error = ex.Message;
        CleanUp();
        SetState(SpeechSessionState.Faulted);
        Error?.Invoke(this, ex);
    }

    /// <summary>Stops recording and inserts what was recognized (use <see cref="Cancel"/> to drop it).</summary>
    public async Task StopListeningAsync()
    {
        if (_session == null || _state is not (SpeechSessionState.Listening or SpeechSessionState.Starting)) return;
        _limit.Stop();
        SetState(SpeechSessionState.Processing);
        var session = _session;
        try
        {
            await session.StopAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(ex);
            return;
        }

        CleanUp();
        _partial = string.Empty;
        SetState(SpeechSessionState.Idle);
    }

    /// <summary>Stops recording and throws the transcript away.</summary>
    public async Task Cancel()
    {
        _discard = true;
        await StopListeningAsync().ConfigureAwait(true);
    }

    private void CleanUp()
    {
        var session = _session;
        var input = _input;
        _session = null;
        _input = null;
        if (session != null) _ = session.DisposeAsync().AsTask();
        input?.Dispose();
    }

    private void Insert(string text)
    {
        if (Target == null || text.Length == 0) return;
        if (Target is TextBoxBase { ReadOnly: true }) return;
        if (Target is TextBoxBase box)
        {
            var spaced = InsertMode == SpeechInsertMode.Replace ? text : NeedsSpace(box.Text, InsertMode == SpeechInsertMode.Append ? box.TextLength : box.SelectionStart) ? " " + text : text;
            switch (InsertMode)
            {
                case SpeechInsertMode.Append: box.AppendText(spaced); break;
                case SpeechInsertMode.Replace: box.Text = text; box.SelectionStart = box.TextLength; break;
                default: box.SelectedText = spaced; break;
            }
        }
        else Target.Text = InsertMode == SpeechInsertMode.Replace ? text : (Target.Text + (NeedsSpace(Target.Text, Target.Text.Length) ? " " : string.Empty) + text);
    }

    private static bool NeedsSpace(string text, int position) => position > 0 && position <= text.Length && !char.IsWhiteSpace(text[position - 1]);

    private void Tick()
    {
        _level *= 0.82f;
        if (_state == SpeechSessionState.Listening && _level < 0.01f)
        {
            _head = (_head + 1) % _levels.Length;
            _levels[_head] = 0;
        }

        Invalidate();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _clock.Enabled = false;
        base.OnHandleDestroyed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && IsListening)
        {
            _ = Cancel();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override AccessibleStates StateFlags() => (IsListening ? AccessibleStates.Pressed : AccessibleStates.None) | (_state is SpeechSessionState.Starting or SpeechSessionState.Processing ? AccessibleStates.Busy : AccessibleStates.None);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var c = Colors;
        var face = FaceBounds;
        var listening = _state == SpeechSessionState.Listening;
        var failed = _state == SpeechSessionState.Faulted;
        var accent = listening || failed ? c.Danger : c.Primary;
        PaintFace(g, face, c.Surface, true, listening || failed ? c.Danger : c.Border);
        if (listening)
        {
            using var tint = new SolidBrush(Color.FromArgb(22, c.Danger));
            using var path = Chrome.RoundedRect(face, CornerRadius);
            g.FillPath(tint, path);
        }

        var icon = new Rectangle(face.X + S(6), face.Y + (face.Height - S(28)) / 2, S(28), S(28));
        var pulse = listening ? 0.5 + 0.5 * Math.Sin(_clock.Seconds * 5) : 0;
        if (listening)
        {
            using var halo = new SolidBrush(Color.FromArgb((int)(50 + 50 * Math.Max(_level, pulse * 0.4)), c.Danger));
            var grow = (int)(S(3) + S(5) * Math.Max(_level, pulse * 0.4));
            g.FillEllipse(halo, Rectangle.Inflate(icon, grow, grow));
        }

        using (var brush = new SolidBrush(listening ? c.Danger : failed ? c.Danger : Chrome.Blend(c.Surface, c.Primary, 0.14))) g.FillEllipse(brush, icon);
        var iconColor = listening || failed ? Color.White : accent;
        if (_state is SpeechSessionState.Starting or SpeechSessionState.Processing)
        {
            using var arc = new Pen(accent, S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, Rectangle.Inflate(icon, -S(6), -S(6)), (float)(_clock.Seconds * 360 % 360), 110);
        }
        else if (failed) Chrome.DrawText(g, "!", BoldFont, icon, iconColor, ContentAlignment.MiddleCenter);
        else DrawMicrophone(g, icon, iconColor);

        var contentLeft = icon.Right + S(8);
        var contentRight = face.Right - S(10);
        var textColor = !Enabled ? c.Disabled : failed ? c.Danger : c.Text;
        if (listening && ShowWaveform)
        {
            var wave = new Rectangle(contentLeft, face.Y + S(6), Math.Max(S(20), (contentRight - contentLeft) / 2), face.Height - S(12));
            PaintWave(g, wave, c.Danger);
            Chrome.DrawText(g, DisplayText, Font, new Rectangle(wave.Right + S(6), face.Y, Math.Max(0, contentRight - wave.Right - S(6)), face.Height), textColor);
        }
        else Chrome.DrawText(g, DisplayText, BoldFont, new Rectangle(contentLeft, face.Y, Math.Max(0, contentRight - contentLeft), face.Height), textColor);
        PaintFocus(g);
    }

    private void PaintWave(Graphics g, Rectangle area, Color color)
    {
        var barWidth = Math.Max(2, S(3));
        var gap = Math.Max(1, S(2));
        var count = Math.Max(1, area.Width / (barWidth + gap));
        using var brush = new SolidBrush(color);
        for (var i = 0; i < count; i++)
        {
            var level = _levels[((_head - (count - 1 - i)) % _levels.Length + _levels.Length) % _levels.Length];
            var h = Math.Max(S(2), (int)(area.Height * Math.Min(1, level * 1.4)));
            var r = new Rectangle(area.Right - (count - i) * (barWidth + gap), area.Y + (area.Height - h) / 2, barWidth, h);
            Chrome.FillRounded(g, r, Color.FromArgb(110 + (int)(145.0 * i / count), color), Math.Max(1, barWidth / 2));
        }
    }

    private static void DrawMicrophone(Graphics g, Rectangle r, Color color)
    {
        var w = r.Width * 0.3f;
        var cx = r.X + r.Width / 2f;
        var top = r.Y + r.Height * 0.2f;
        var bodyH = r.Height * 0.34f;
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, Math.Max(1.5f, r.Width / 16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using (var capsule = Chrome.RoundedRect(new Rectangle((int)(cx - w / 2), (int)top, (int)w, (int)bodyH), (int)(w / 2))) g.FillPath(brush, capsule);
        var cup = new RectangleF(cx - w * 0.9f, top + bodyH * 0.25f, w * 1.8f, bodyH * 1.2f);
        g.DrawArc(pen, cup, 0, 180);
        g.DrawLine(pen, cx, cup.Bottom, cx, cup.Bottom + r.Height * 0.12f);
        g.DrawLine(pen, cx - w * 0.5f, cup.Bottom + r.Height * 0.12f, cx + w * 0.5f, cup.Bottom + r.Height * 0.12f);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () => _error ?? _state.ToString(), StateFlags, null, "Toggle dictation");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _limit.Dispose();
            _clock.Dispose();
            CleanUp();
        }

        base.Dispose(disposing);
    }
}
