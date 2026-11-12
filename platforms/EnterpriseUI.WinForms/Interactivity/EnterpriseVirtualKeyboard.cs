using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum VirtualKeyboardKind { Qwerty, Numeric, Symbols, Custom }

public enum KeyboardInjectionMode { EventsOnly, TargetControl, SystemInput }

public sealed class VirtualKeyEventArgs : EventArgs
{
    public VirtualKeyEventArgs(KeyDef key, KeyPressResult result)
    {
        Key = key;
        Result = result;
    }

    public KeyDef Key { get; }
    public KeyPressResult Result { get; }

    /// <summary>Set to stop the keyboard from delivering the key to the target.</summary>
    public bool Handled { get; set; }
}

/// <summary>On-screen keyboard with QWERTY, numeric and symbol layouts or your own layers, shift and caps lock, key repeat, press animation and delivery to a text box or to the focused window.</summary>
public class EnterpriseVirtualKeyboardControl : EnterpriseControlBase
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int Dx, Dy; public uint Data, Flags, Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyInput Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Union; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    private const uint InputKeyboard = 1, KeyUp = 2, KeyUnicode = 4;

    private readonly List<(KeyDef Key, Rectangle Bounds)> _keys = new();
    private readonly System.Windows.Forms.Timer _repeat = new();
    private readonly ValueAnimator _flash = new(0);
    private VirtualKeyboardState _state = new(KeyboardLayout.Qwerty());
    private VirtualKeyboardKind _kind = VirtualKeyboardKind.Qwerty;
    private KeyDef? _hot;
    private KeyDef? _down;
    private KeyDef? _flashKey;
    private KeyDef? _focusKey;
    private bool _keyNav;
    private bool _firstRepeat;
    private Font? _keyFont;
    private Font? _smallFont;

    public EnterpriseVirtualKeyboardControl()
    {
        Size = new Size(760, 280);
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = "On-screen keyboard";
        _state.Changed += (_, _) => { RebuildKeys(); Invalidate(); };
        _repeat.Tick += (_, _) => OnRepeat();
        _flash.Updated += (_, _) => Invalidate();
    }

    [DefaultValue(VirtualKeyboardKind.Qwerty)]
    public VirtualKeyboardKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            if (value == VirtualKeyboardKind.Custom) return;
            Layout = value switch { VirtualKeyboardKind.Numeric => KeyboardLayout.Numeric(), VirtualKeyboardKind.Symbols => KeyboardLayout.Symbols(), _ => KeyboardLayout.Qwerty() };
            _kind = value;
        }
    }

    [Browsable(false)]
    public KeyboardLayout Layout
    {
        get => _state.Layout;
        set
        {
            _kind = VirtualKeyboardKind.Custom;
            _state.Layout = value ?? throw new ArgumentNullException(nameof(value));
            _focusKey = null;
        }
    }

    [Browsable(false)]
    public string ActiveLayer
    {
        get => _state.ActiveLayerName;
        set => _state.ActiveLayerName = value;
    }

    [Browsable(false)]
    public bool ShiftActive => _state.Shift;

    [Browsable(false)]
    public bool CapsLockActive => _state.CapsLock;

    /// <summary>Text box (or any control with a Text property) that receives the keys.</summary>
    [DefaultValue(null)]
    public Control? Target { get; set; }

    [DefaultValue(KeyboardInjectionMode.TargetControl)]
    public KeyboardInjectionMode InjectionMode { get; set; } = KeyboardInjectionMode.TargetControl;

    [DefaultValue(true)]
    public bool KeyRepeat { get; set; } = true;

    [DefaultValue(false)]
    public bool KeyClickSound { get; set; }

    /// <summary>Hides the keyboard when the Hide key is pressed.</summary>
    [DefaultValue(false)]
    public bool HideOnClose { get; set; }

    /// <summary>Lets the keyboard take focus so keys can be chosen with arrows and Enter; leave off when it should never steal focus from the target.</summary>
    [DefaultValue(false)]
    public bool AllowKeyboardNavigation
    {
        get => _keyNav;
        set
        {
            _keyNav = value;
            SetStyle(ControlStyles.Selectable, value);
            TabStop = value;
        }
    }

    public event EventHandler<VirtualKeyEventArgs>? KeyPressed;
    public event EventHandler<string>? TextEntered;
    public event EventHandler<string>? CommandInvoked;
    public event EventHandler? CloseRequested;

    public void PressKey(KeyDef key) => Press(key);

    private void RebuildKeys()
    {
        _keys.Clear();
        if (Width < 20 || Height < 20) return;
        var layer = _state.ActiveLayer;
        var gap = S(6);
        var pad = S(8);
        var rows = layer.Rows;
        if (rows.Count == 0) return;
        var keyHeight = (Height - pad * 2 - gap * (rows.Count - 1)) / (double)rows.Count;
        var maxTotal = rows.Max(r => r.TotalWidth);
        var cell = (Width - pad * 2 + gap) / maxTotal;
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var x = pad + (Width - pad * 2 + gap - row.TotalWidth * cell) / 2;
            var y = pad + r * (keyHeight + gap);
            foreach (var key in row.Keys)
            {
                var w = key.Width * cell - gap;
                _keys.Add((key, Rectangle.Round(new RectangleF((float)x, (float)y, (float)w, (float)keyHeight))));
                x += key.Width * cell;
            }
        }

        _keyFont?.Dispose();
        _smallFont?.Dispose();
        var px = Math.Max(10f, (float)Math.Min(keyHeight * 0.36, 26 * DpiScale));
        _keyFont = new Font(Font.FontFamily, px, FontStyle.Regular, GraphicsUnit.Pixel);
        _smallFont = new Font(Font.FontFamily, Math.Max(9f, px * 0.62f), FontStyle.Regular, GraphicsUnit.Pixel);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildKeys();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RebuildKeys();
    }

    private KeyDef? KeyAt(Point p)
    {
        foreach (var (key, bounds) in _keys) if (Rectangle.Inflate(bounds, S(3), S(3)).Contains(p) && bounds.Contains(p)) return key;
        foreach (var (key, bounds) in _keys) if (Rectangle.Inflate(bounds, S(3), S(3)).Contains(p)) return key;
        return null;
    }

    // ---- input ----

    private void Press(KeyDef key)
    {
        var result = _state.Press(key);
        _flashKey = key;
        _flash.Jump(1);
        _flash.AnimateTo(0, 260, EasingKind.EaseOut);
        if (KeyClickSound && result.Action != KeyAction.None) SystemSounds.Asterisk.Play();

        var args = new VirtualKeyEventArgs(key, result);
        KeyPressed?.Invoke(this, args);
        if (!args.Handled) Deliver(result);

        switch (result.Action)
        {
            case KeyAction.Insert or KeyAction.Enter or KeyAction.Tab when result.Text.Length > 0: TextEntered?.Invoke(this, result.Text); break;
            case KeyAction.Command when result.Command != null: CommandInvoked?.Invoke(this, result.Command); break;
            case KeyAction.Close:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                if (HideOnClose) Visible = false;
                break;
        }
    }

    private void Deliver(KeyPressResult result)
    {
        if (result.Action is KeyAction.None or KeyAction.Command or KeyAction.Close) return;
        switch (InjectionMode)
        {
            case KeyboardInjectionMode.TargetControl: DeliverToTarget(result); break;
            case KeyboardInjectionMode.SystemInput: DeliverToSystem(result); break;
        }
    }

    private void DeliverToTarget(KeyPressResult result)
    {
        if (Target is not { IsDisposed: false } target) return;
        if (target is TextBoxBase { ReadOnly: true }) return;
        if (!target.Focused && target.CanFocus) target.Focus();
        if (target is TextBoxBase box)
        {
            switch (result.Action)
            {
                case KeyAction.Insert: box.SelectedText = result.Text; break;
                case KeyAction.Backspace:
                    if (box.SelectionLength > 0) box.SelectedText = string.Empty;
                    else if (box.SelectionStart > 0)
                    {
                        var at = box.SelectionStart;
                        box.Select(at - 1, 1);
                        box.SelectedText = string.Empty;
                    }

                    break;
                case KeyAction.Enter:
                    if (box.Multiline) box.SelectedText = Environment.NewLine;
                    else if (target.FindForm()?.AcceptButton is { } accept) accept.PerformClick();
                    break;
                case KeyAction.Tab: target.Parent?.SelectNextControl(target, true, true, true, true); break;
                case KeyAction.Clear: box.Clear(); break;
            }

            return;
        }

        switch (result.Action)
        {
            case KeyAction.Insert: target.Text += result.Text; break;
            case KeyAction.Backspace when target.Text.Length > 0: target.Text = target.Text[..^1]; break;
            case KeyAction.Clear: target.Text = string.Empty; break;
        }
    }

    private static void DeliverToSystem(KeyPressResult result)
    {
        var inputs = new List<Input>();
        void Key(ushort vk, ushort scan, uint flags) => inputs.Add(new Input { Type = InputKeyboard, Union = new InputUnion { Key = new KeyInput { Vk = vk, Scan = scan, Flags = flags } } });
        void Tap(ushort vk) { Key(vk, 0, 0); Key(vk, 0, KeyUp); }

        switch (result.Action)
        {
            case KeyAction.Insert:
                foreach (var ch in result.Text) { Key(0, ch, KeyUnicode); Key(0, ch, KeyUnicode | KeyUp); }
                break;
            case KeyAction.Backspace: Tap(0x08); break;
            case KeyAction.Enter: Tap(0x0D); break;
            case KeyAction.Tab: Tap(0x09); break;
            case KeyAction.Clear:
                Key(0x11, 0, 0); Tap(0x41); Key(0x11, 0, KeyUp); Tap(0x08);
                break;
        }

        if (inputs.Count > 0) _ = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
    }

    private static bool Repeatable(KeyDef key) => key.Kind is KeyKind.Character or KeyKind.Backspace or KeyKind.Space;

    private void OnRepeat()
    {
        if (_down == null) { _repeat.Stop(); return; }
        if (_firstRepeat)
        {
            _firstRepeat = false;
            _repeat.Interval = 55;
        }

        Press(_down);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        if (_keyNav) Focus();
        var key = KeyAt(e.Location);
        if (key == null) return;
        _down = key;
        _focusKey = key;
        Press(key);
        if (!KeyRepeat || !Repeatable(key)) return;
        _firstRepeat = true;
        _repeat.Interval = 450;
        _repeat.Start();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _repeat.Stop();
        _down = null;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var key = KeyAt(e.Location);
        if (key == _hot) return;
        _hot = key;
        if (_down != null && key != _down) _repeat.Stop();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = null;
        _repeat.Stop();
        _down = null;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => _keyNav && (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_keyNav || _keys.Count == 0) return;
        _focusKey ??= _keys[0].Key;
        var index = _keys.FindIndex(k => k.Key == _focusKey);
        if (index < 0) index = 0;
        var current = _keys[index].Bounds;
        switch (e.KeyCode)
        {
            case Keys.Left: _focusKey = _keys[Math.Max(0, index - 1)].Key; break;
            case Keys.Right: _focusKey = _keys[Math.Min(_keys.Count - 1, index + 1)].Key; break;
            case Keys.Up or Keys.Down:
            {
                var dir = e.KeyCode == Keys.Up ? -1 : 1;
                var candidates = _keys.Where(k => dir < 0 ? k.Bounds.Bottom <= current.Top + 2 : k.Bounds.Top >= current.Bottom - 2).ToList();
                if (candidates.Count == 0) break;
                var best = candidates.OrderBy(k => Math.Abs(k.Bounds.Y - current.Y)).ThenBy(k => Math.Abs(k.Bounds.X + k.Bounds.Width / 2 - (current.X + current.Width / 2))).First();
                var row = candidates.Where(k => k.Bounds.Y == best.Bounds.Y).OrderBy(k => Math.Abs(k.Bounds.X + k.Bounds.Width / 2 - (current.X + current.Width / 2))).First();
                _focusKey = row.Key;
                break;
            }
            case Keys.Enter or Keys.Space: Press(_focusKey); break;
            default: return;
        }

        e.Handled = true;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _repeat.Stop();
    }

    // ---- painting ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Chrome.Blend(Colors.Surface, Colors.Header, 0.55));
        if (_keys.Count == 0) RebuildKeys();
        var c = Colors;
        var radius = S(6);
        foreach (var (key, bounds) in _keys)
        {
            var pressed = _down == key;
            var hot = _hot == key && Enabled;
            var active = key.Kind switch { KeyKind.Shift => _state.Shift, KeyKind.CapsLock => _state.CapsLock, _ => false };
            var special = key.Kind != KeyKind.Character && key.Kind != KeyKind.Space;
            var flash = _flashKey == key ? _flash.Value : 0;

            var back = active ? c.Primary : special ? Chrome.Blend(c.Surface, c.Muted, 0.28) : c.Surface;
            if (hot && !active) back = Chrome.Blend(back, c.Primary, 0.12);
            if (key.Kind == KeyKind.Enter && !active) back = Chrome.Blend(c.Surface, c.Primary, 0.18);
            if (pressed) back = Chrome.Blend(back, c.Primary, 0.45);
            else if (flash > 0) back = Chrome.Blend(back, c.Primary, 0.4 * flash);
            if (!Enabled) back = Chrome.Blend(c.Surface, c.Border, 0.4);

            var rect = bounds;
            if (pressed) rect = new Rectangle(bounds.X, bounds.Y + S(2), bounds.Width, bounds.Height - S(2));
            else
            {
                using var shadow = new SolidBrush(Color.FromArgb(c.HighContrast ? 0 : 40, 0, 0, 0));
                using var path = Chrome.RoundedRect(new Rectangle(bounds.X, bounds.Y + S(2), bounds.Width, bounds.Height), radius);
                g.FillPath(shadow, path);
            }

            Chrome.FillRounded(g, rect, back, radius);
            Chrome.DrawRoundedBorder(g, rect, active ? c.Primary : c.Border, radius, c.HighContrast ? 2f : 1f);
            var foreColor = !Enabled ? c.Disabled : active || pressed ? c.PrimaryText : c.Text;
            var label = _state.LabelOf(key);
            var font = special && label.Length > 1 ? _smallFont! : _keyFont!;
            TextRenderer.DrawText(g, label, font, rect, foreColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (key.Kind == KeyKind.CapsLock && _state.CapsLock)
            {
                using var dot = new SolidBrush(c.PrimaryText);
                g.FillEllipse(dot, rect.X + S(7), rect.Y + S(7), S(5), S(5));
            }

            if (_keyNav && Focused && _focusKey == key)
            {
                using var ring = new Pen(c.FocusRing, 2f);
                using var path = Chrome.RoundedRect(Rectangle.Inflate(rect, 1, 1), radius);
                g.DrawPath(ring, path);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repeat.Stop();
            _repeat.Dispose();
            _flash.Dispose();
            _keyFont?.Dispose();
            _smallFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
