using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum EnterpriseButtonVariant
{
    Primary,
    Secondary,
    Success,
    Danger,
    Outline
}

internal static class FormChrome
{
    public static Color TextOn(Color back) => (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) > 160 ? Color.FromArgb(32, 32, 32) : Color.White;

    public static Color VariantColor(ControlColors c, EnterpriseButtonVariant variant) => variant switch
    {
        EnterpriseButtonVariant.Success => c.Success,
        EnterpriseButtonVariant.Danger => c.Danger,
        EnterpriseButtonVariant.Secondary => Chrome.Blend(c.Muted, c.Text, 0.25),
        EnterpriseButtonVariant.Outline => c.Surface,
        _ => c.Primary
    };

    /// <summary>Rounded button face with hover, pressed and focus states shared by all button-like controls.</summary>
    public static void PaintFace(Graphics g, Rectangle rect, ControlColors colors, Color fill, bool outline, bool hovered, bool pressed, bool focused, bool enabled)
    {
        var face = fill;
        if (!enabled) face = Chrome.Blend(colors.Surface, colors.Border, 0.35);
        else if (pressed) face = Chrome.Blend(fill, Color.Black, 0.18);
        else if (hovered) face = outline ? Chrome.Blend(colors.Surface, fill == colors.Surface ? colors.Primary : fill, 0.12) : Chrome.Blend(fill, Color.White, 0.14);

        Chrome.FillRounded(g, rect, face, Chrome.CornerRadius);
        var border = !enabled ? colors.Border : outline ? colors.Primary : face;
        Chrome.DrawRoundedBorder(g, rect, border, Chrome.CornerRadius, outline ? 1.5f : 1f);
        if (focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(rect, -3, -3), Math.Max(1, Chrome.CornerRadius - 1));
            using var pen = new Pen(outline ? colors.FocusRing : Color.FromArgb(200, TextOn(face)), 1.25f) { DashStyle = DashStyle.Dot };
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>Themed text box with a prefix glyph, a clear button, a focus ring and a validation border.</summary>
public class EnterpriseTextBoxControl : EnterpriseControlBase
{
    private readonly TextBox _inner = new() { BorderStyle = BorderStyle.None };
    private Font? _glyphFont;
    private bool _clearHot;
    private bool _hasError;

    public EnterpriseTextBoxControl()
    {
        Size = new Size(280, 34);
        TabStop = false;
        _inner.TextChanged += (_, e) =>
        {
            Invalidate();
            OnTextChanged(e);
            ValueChanged?.Invoke(this, e);
        };
        _inner.GotFocus += (_, _) => Invalidate();
        _inner.LostFocus += (_, _) => Invalidate();
        Controls.Add(_inner);
        OnThemeApplied();
        LayoutInner();
    }

    public string PrefixGlyph { get; set; } = "✎";
    public bool ShowClearButton { get; set; } = true;

    public bool HasValidationError
    {
        get => _hasError;
        set { _hasError = value; Invalidate(); }
    }

    public string PlaceholderText
    {
        get => _inner.PlaceholderText;
        set => _inner.PlaceholderText = value;
    }

    [AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set
        {
            _inner.Text = value ?? string.Empty;
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _inner.BackColor = Colors.Surface;
        _inner.ForeColor = Colors.Text;
        _inner.Font = Font;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _inner.Font = Font;
        _glyphFont?.Dispose();
        _glyphFont = null;
        LayoutInner();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _inner.Focus();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
    }

    private bool ClearVisible => ShowClearButton && _inner.Text.Length > 0 && Enabled;

    private void LayoutInner()
    {
        var left = PrefixGlyph.Length > 0 ? S(32) : S(10);
        var right = S(30);
        var h = _inner.PreferredHeight;
        _inner.SetBounds(left, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - left - right), h);
    }

    private Rectangle ClearRect => new(Width - S(30), 0, S(28), Height);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = ClearVisible && ClearRect.Contains(e.Location);
        if (hot != _clearHot)
        {
            _clearHot = hot;
            Cursor = hot ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (ClearVisible && ClearRect.Contains(e.Location)) _inner.Clear();
        _inner.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var rect = new Rectangle(0, 0, Width, Height);
        Chrome.FillRounded(g, rect, Enabled ? Colors.Surface : Chrome.Blend(Colors.Surface, Colors.Header, 0.5), Chrome.CornerRadius);
        var focused = _inner.Focused;
        var border = _hasError ? Colors.Danger : focused ? Colors.FocusRing : IsHovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.55) : Colors.Border;
        Chrome.DrawRoundedBorder(g, rect, border, Chrome.CornerRadius, focused || _hasError ? Chrome.FocusWidth : Chrome.BorderWidth);

        if (PrefixGlyph.Length > 0)
        {
            _glyphFont ??= new Font("Segoe UI Symbol", Font.Size);
            TextRenderer.DrawText(g, PrefixGlyph, _glyphFont, new Rectangle(S(6), 0, S(24), Height), focused ? Colors.Primary : Colors.Muted, Chrome.TextFlags(ContentAlignment.MiddleCenter));
        }

        if (ClearVisible)
        {
            var c = ClearRect;
            var size = S(5);
            using var pen = new Pen(_clearHot ? Colors.Text : Colors.Muted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = c.X + c.Width / 2;
            var cy = c.Y + c.Height / 2;
            g.DrawLine(pen, cx - size, cy - size, cx + size, cy + size);
            g.DrawLine(pen, cx - size, cy + size, cx + size, cy - size);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _glyphFont?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Text box with a command button on the left and on the right; the right button can open a file dialog.</summary>
public class EnterpriseButtonTextBoxControl : EnterpriseControlBase
{
    private readonly TextBox _inner = new() { BorderStyle = BorderStyle.None, PlaceholderText = "Select a path..." };
    private string _leftText = "🔍";
    private string _rightText = "Browse";
    private int _hotButton;
    private int _downButton;

    public EnterpriseButtonTextBoxControl()
    {
        Size = new Size(320, 34);
        TabStop = false;
        _inner.TextChanged += (_, _) => Invalidate();
        _inner.GotFocus += (_, _) => Invalidate();
        _inner.LostFocus += (_, _) => Invalidate();
        Controls.Add(_inner);
        OnThemeApplied();
        LayoutInner();
    }

    public bool EnableFileBrowseOnRightClick { get; set; }

    [AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value ?? string.Empty;
    }

    public string LeftButtonText
    {
        get => _leftText;
        set { _leftText = value ?? string.Empty; LayoutInner(); Invalidate(); }
    }

    public string RightButtonText
    {
        get => _rightText;
        set { _rightText = value ?? string.Empty; LayoutInner(); Invalidate(); }
    }

    public event EventHandler? LeftButtonClick;
    public event EventHandler? RightButtonClick;

    private int LeftWidth => _leftText.Length == 0 ? 0 : S(36);
    private int RightWidth => _rightText.Length == 0 ? 0 : Math.Max(S(40), Chrome.MeasureText(_rightText, Font) + S(24));

    private Rectangle LeftRect => new(1, 1, LeftWidth, Height - 2);
    private Rectangle RightRect => new(Width - 1 - RightWidth, 1, RightWidth, Height - 2);

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _inner.BackColor = Colors.Surface;
        _inner.ForeColor = Colors.Text;
        _inner.Font = Font;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _inner.Focus();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
    }

    private void LayoutInner()
    {
        var left = LeftWidth + S(10);
        var right = RightWidth + S(10);
        var h = _inner.PreferredHeight;
        _inner.SetBounds(left, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - left - right), h);
    }

    private int ButtonAt(Point p) => LeftWidth > 0 && LeftRect.Contains(p) ? 1 : RightWidth > 0 && RightRect.Contains(p) ? 2 : 0;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = ButtonAt(e.Location);
        if (hot != _hotButton)
        {
            _hotButton = hot;
            Cursor = hot > 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotButton = 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _downButton = e.Button == MouseButtons.Left ? ButtonAt(e.Location) : 0;
        if (_downButton == 0) _inner.Focus();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var released = ButtonAt(e.Location);
        var pressed = _downButton;
        _downButton = 0;
        Invalidate();
        if (released == 0 || released != pressed) return;
        if (released == 1)
        {
            LeftButtonClick?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (EnableFileBrowseOnRightClick)
        {
            using var dialog = new OpenFileDialog { Title = "Select file", Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) Text = dialog.FileName;
        }

        RightButtonClick?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var rect = new Rectangle(0, 0, Width, Height);
        Chrome.FillRounded(g, rect, Enabled ? Colors.Surface : Chrome.Blend(Colors.Surface, Colors.Header, 0.5), Chrome.CornerRadius);

        var state = g.Save();
        using (var clip = Chrome.RoundedRect(Rectangle.Inflate(rect, -1, -1), Chrome.CornerRadius - 1)) g.SetClip(clip);
        if (LeftWidth > 0)
        {
            using var back = new SolidBrush(Chrome.Blend(Colors.Surface, Colors.Primary, _hotButton == 1 || _downButton == 1 ? 0.22 : 0.1));
            g.FillRectangle(back, LeftRect);
            TextRenderer.DrawText(g, _leftText, Font, LeftRect, Colors.Primary, Chrome.TextFlags(ContentAlignment.MiddleCenter));
        }

        if (RightWidth > 0)
        {
            var fill = _downButton == 2 ? Chrome.Blend(Colors.Primary, Color.Black, 0.18) : _hotButton == 2 ? Chrome.Blend(Colors.Primary, Color.White, 0.14) : Colors.Primary;
            using var back = new SolidBrush(fill);
            g.FillRectangle(back, RightRect);
            TextRenderer.DrawText(g, _rightText, Font, RightRect, Colors.PrimaryText, Chrome.TextFlags(ContentAlignment.MiddleCenter));
        }

        g.Restore(state);
        var focused = _inner.Focused;
        Chrome.DrawRoundedBorder(g, rect, focused ? Colors.FocusRing : IsHovered ? Chrome.Blend(Colors.Border, Colors.Primary, 0.55) : Colors.Border, Chrome.CornerRadius, focused ? Chrome.FocusWidth : Chrome.BorderWidth);
    }
}

/// <summary>Button with variants, a notification badge, keyboard activation and the shared hover, pressed and focus states.</summary>
public class EnterpriseButtonControl : EnterpriseControlBase
{
    private bool _pressed;

    public EnterpriseButtonControl()
    {
        Size = new Size(150, 34);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        TabStop = true;
    }

    public EnterpriseButtonVariant Variant { get; set; } = EnterpriseButtonVariant.Primary;
    public string? BadgeText { get; set; }

    public void PerformClick()
    {
        if (Enabled) OnClick(EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = false;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        PerformClick();
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FormChrome.VariantColor(Colors, Variant);
        var rect = new Rectangle(0, 0, Width, Height);
        FormChrome.PaintFace(g, rect, Colors, fill, outline, IsHovered, _pressed, Focused, Enabled);

        var textColor = !Enabled ? Colors.Disabled : outline ? Colors.Primary : Variant == EnterpriseButtonVariant.Primary ? Colors.PrimaryText : FormChrome.TextOn(fill);
        var textBounds = new Rectangle(S(8), 0, Math.Max(0, Width - S(16) - (string.IsNullOrEmpty(BadgeText) ? 0 : S(18))), Height);
        Chrome.DrawText(g, Text, BoldFont, textBounds, textColor, ContentAlignment.MiddleCenter);

        if (!string.IsNullOrEmpty(BadgeText))
        {
            var badge = new Rectangle(Width - S(30), S(4), S(22), S(16));
            Chrome.FillRounded(g, badge, Colors.Danger == fill ? Colors.Warning : Colors.Danger, badge.Height / 2);
            Chrome.DrawText(g, BadgeText, Font, badge, Color.White, ContentAlignment.MiddleCenter);
        }
    }
}

/// <summary>Primary button with a drop-down part: a click on the left part runs the default action, the right part opens the menu.</summary>
public class EnterpriseSplitButtonControl : EnterpriseControlBase
{
    private readonly ContextMenuStrip _menu = new();
    private bool _pressed;

    public EnterpriseSplitButtonControl()
    {
        Size = new Size(175, 34);
        Cursor = Cursors.Hand;
        Text = "Export Report";
        AccessibleRole = AccessibleRole.SplitButton;
        AddOption("Export as PDF Document (.pdf)");
        AddOption("Export as Excel Workbook (.xlsx)");
        AddOption("Export as Word Document (.docx)");
        AddOption("Export as Compressed Archive (.zip)");
    }

    public bool IsSplitMode { get; set; } = true;
    public event EventHandler? PrimaryActionClicked;
    public event Action<string>? MenuItemSelected;

    public void AddOption(string itemText) => _menu.Items.Add(itemText, null, (_, _) => MenuItemSelected?.Invoke(itemText));

    public void ClearOptions() => _menu.Items.Clear();

    private int SplitX => Width - S(32);

    public void ShowMenu() => _menu.Show(this, new Point(0, Height));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        if (!IsSplitMode || e.X >= SplitX) ShowMenu();
        else
        {
            _pressed = true;
            Invalidate();
            PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Down && e.Alt) ShowMenu();
        else if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            if (IsSplitMode) PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
            else ShowMenu();
        }
        else return;
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        FormChrome.PaintFace(g, new Rectangle(0, 0, Width, Height), Colors, Colors.Primary, false, IsHovered, _pressed, Focused, Enabled);

        var textWidth = IsSplitMode ? SplitX : Width - S(32);
        if (IsSplitMode)
        {
            using var pen = new Pen(Color.FromArgb(140, Colors.PrimaryText));
            g.DrawLine(pen, SplitX, S(7), SplitX, Height - S(8));
        }

        Chrome.DrawText(g, Text, BoldFont, new Rectangle(S(8), 0, Math.Max(0, textWidth - S(12)), Height), Enabled ? Colors.PrimaryText : Colors.Disabled, ContentAlignment.MiddleCenter);
        Chrome.DrawChevron(g, new Rectangle(Width - S(32), 0, S(30), Height), ChevronDirection.Down, Enabled ? Colors.PrimaryText : Colors.Disabled, 1.8f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Button that fires repeatedly while it is held down.</summary>
public class EnterpriseRepeatButtonControl : EnterpriseControlBase
{
    private readonly System.Windows.Forms.Timer _repeatTimer;
    private bool _pressed;

    public EnterpriseRepeatButtonControl()
    {
        Size = new Size(170, 34);
        Cursor = Cursors.Hand;
        Text = "⏩ Hold to Repeat (0)";
        AccessibleRole = AccessibleRole.PushButton;
        _repeatTimer = new System.Windows.Forms.Timer { Interval = 90 };
        _repeatTimer.Tick += (_, _) => FireTick();
    }

    public int RepeatCount { get; private set; }

    public int RepeatInterval
    {
        get => _repeatTimer.Interval;
        set => _repeatTimer.Interval = Math.Max(10, value);
    }

    public event Action<int>? RepeatedClick;

    private void FireTick()
    {
        RepeatCount++;
        Text = $"⏩ Hold to Repeat ({RepeatCount})";
        RepeatedClick?.Invoke(RepeatCount);
    }

    private void Release()
    {
        _repeatTimer.Stop();
        if (!_pressed) return;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        _pressed = true;
        FireTick();
        _repeatTimer.Start();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Release();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Release();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var tint = Chrome.Blend(Colors.Surface, Colors.Primary, _pressed ? 0.3 : IsHovered ? 0.2 : 0.12);
        FormChrome.PaintFace(g, new Rectangle(0, 0, Width, Height), Colors, tint, true, false, false, Focused, Enabled);
        Chrome.DrawText(g, Text, BoldFont, new Rectangle(S(8), 0, Math.Max(0, Width - S(16)), Height), Enabled ? Colors.Primary : Colors.Disabled, ContentAlignment.MiddleCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repeatTimer.Stop();
            _repeatTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>Pill-shaped on/off switch with labels.</summary>
public class EnterpriseToggleSwitchControl : EnterpriseControlBase
{
    private bool _isOn = true;

    public EnterpriseToggleSwitchControl()
    {
        Size = new Size(76, 30);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public string OnText { get; set; } = "ON";
    public string OffText { get; set; } = "OFF";

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            Invalidate();
            Toggled?.Invoke(_isOn);
        }
    }

    public event Action<bool>? Toggled;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        IsOn = !_isOn;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Space || !Enabled) return;
        IsOn = !_isOn;
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var track = new Rectangle(1, 1, Width - 2, Height - 2);
        var fill = !Enabled ? Chrome.Blend(Colors.Surface, Colors.Border, 0.5) : _isOn ? Colors.Primary : Chrome.Blend(Colors.Surface, Colors.Muted, 0.55);
        if (IsHovered && Enabled) fill = Chrome.Blend(fill, Color.White, 0.08);
        Chrome.FillRounded(g, track, fill, track.Height / 2);
        if (Focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(track, 1, 1), track.Height / 2);
            using var pen = new Pen(Colors.FocusRing, 1.5f);
            g.DrawPath(pen, path);
        }

        var thumb = track.Height - S(8);
        var thumbX = _isOn ? track.Right - thumb - S(4) : track.X + S(4);
        using (var brush = new SolidBrush(_isOn ? Colors.PrimaryText : Colors.Surface)) g.FillEllipse(brush, thumbX, track.Y + S(4), thumb, thumb);

        var labelRect = _isOn
            ? new Rectangle(track.X + S(8), track.Y, Math.Max(0, track.Width - thumb - S(20)), track.Height)
            : new Rectangle(track.X + thumb + S(12), track.Y, Math.Max(0, track.Width - thumb - S(20)), track.Height);
        Chrome.DrawText(g, _isOn ? OnText : OffText, BoldFont, labelRect, Colors.PrimaryText, ContentAlignment.MiddleCenter);
    }
}

/// <summary>Progress bar with a rounded track and the percentage written over it.</summary>
public class EnterpriseProgressBarControl : EnterpriseControlBase
{
    private int _value = 68;

    public EnterpriseProgressBarControl()
    {
        Size = new Size(280, 26);
        TabStop = false;
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var track = new Rectangle(0, 0, Width, Height);
        var radius = Math.Min(Chrome.CornerRadius + 2, Height / 2);
        Chrome.FillRounded(g, track, Chrome.Blend(Colors.Surface, Colors.Header, 0.9), radius);

        var pct = Maximum > Minimum ? (double)(_value - Minimum) / (Maximum - Minimum) : 0.0;
        var fillWidth = (int)Math.Round(track.Width * pct);
        var text = $"{pct * 100:F0}% Complete";
        Chrome.DrawText(g, text, BoldFont, track, Colors.Text, ContentAlignment.MiddleCenter);

        if (fillWidth > 0)
        {
            using var path = Chrome.RoundedRect(track, radius);
            var state = g.Save();
            g.SetClip(path);
            g.IntersectClip(new Rectangle(0, 0, fillWidth, Height));
            using var brush = new SolidBrush(Colors.Primary);
            g.FillRectangle(brush, track);
            Chrome.DrawText(g, text, BoldFont, track, Colors.PrimaryText, ContentAlignment.MiddleCenter);
            g.Restore(state);
        }

        Chrome.DrawRoundedBorder(g, track, Colors.Border, radius);
    }
}

/// <summary>Drop-down list that shows every font name in its own typeface.</summary>
public class EnterpriseFontDropDownControl : ComboBox, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);

    public EnterpriseFontDropDownControl()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 24;
        Width = 240;
        foreach (var font in new[] { "Segoe UI", "Consolas", "Arial", "Calibri", "Cambria", "Georgia", "Tahoma", "Trebuchet MS", "Verdana", "Courier New" }) Items.Add(font);
        SelectedIndex = 0;
        ApplyColors();
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }

    public void OnThemeChanged(ITheme newTheme)
    {
        _colors = ControlColors.FromTheme(newTheme);
        if (IsHandleCreated && InvokeRequired) BeginInvoke(new Action(ApplyColors));
        else ApplyColors();
    }

    private void ApplyColors()
    {
        BackColor = _colors.Surface;
        ForeColor = _colors.Text;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        var back = selected ? _colors.SelectionBack : _colors.Surface;
        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, e.Bounds);
        if (e.Index < 0 || e.Index >= Items.Count) return;

        var name = Items[e.Index]?.ToString() ?? "Segoe UI";
        Font preview;
        try
        {
            preview = new Font(name, 9.5f);
        }
        catch (ArgumentException)
        {
            preview = (Font)Font.Clone();
        }

        using (preview)
        {
            TextRenderer.DrawText(g, $"{name} (AaBbCc)", preview, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), selected ? _colors.SelectionText : _colors.Text, Chrome.TextFlags());
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}

/// <summary>Container with a clickable header that collapses and expands its content.</summary>
public class EnterpriseCollapsiblePanelControl : Panel, IThemeableControl
{
    private sealed class Header : Control
    {
        private readonly EnterpriseCollapsiblePanelControl _owner;
        private bool _hot;

        public Header(EnterpriseCollapsiblePanelControl owner)
        {
            _owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hot = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left) _owner.IsExpanded = !_owner.IsExpanded;
        }

        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
            _owner.IsExpanded = !_owner.IsExpanded;
            e.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var colors = _owner._colors;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_hot ? Chrome.Blend(colors.Header, colors.Primary, 0.1) : colors.Header);
            if (_owner.IsExpanded)
            {
                using var pen = new Pen(colors.Border);
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            var scale = DeviceDpi / 96f;
            Chrome.DrawChevron(g, new Rectangle((int)(6 * scale), 0, (int)(22 * scale), Height), _owner.IsExpanded ? ChevronDirection.Down : ChevronDirection.Right, colors.Muted);
            using var bold = new Font(Font, FontStyle.Bold);
            Chrome.DrawText(g, Text, bold, new Rectangle((int)(32 * scale), 0, Math.Max(0, Width - (int)(40 * scale)), Height), colors.Text);
            if (Focused)
            {
                using var focus = new Pen(colors.FocusRing) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(focus, 2, 2, Width - 5, Height - 5);
            }
        }
    }

    private const int HeaderHeight = 34;
    private readonly Header _header;
    private readonly Panel _contentHost;
    private readonly EventHandler<ITheme> _themeHandler;
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
    private bool _isExpanded = true;
    private int _expandedHeight = 140;

    public EnterpriseCollapsiblePanelControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(460, 140);
        Padding = new Padding(1);
        Font = new Font("Segoe UI", 9f);
        _header = new Header(this) { Dock = DockStyle.Top, Height = HeaderHeight, Text = "Details" };
        _contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        Controls.Add(_contentHost);
        Controls.Add(_header);
        ApplyColors();
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }
    public Panel ContentPanel => _contentHost;
    public event Action<bool>? ExpandedChanged;

    public string HeaderTitle
    {
        get => _header.Text;
        set => _header.Text = value;
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            if (_isExpanded)
            {
                Height = _expandedHeight;
                _contentHost.Visible = true;
            }
            else
            {
                _expandedHeight = Math.Max(Height, 100);
                _contentHost.Visible = false;
                Height = _header.Height + Padding.Vertical;
            }

            _header.Invalidate();
            ExpandedChanged?.Invoke(_isExpanded);
        }
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _colors = ControlColors.FromTheme(newTheme);
        if (IsHandleCreated && InvokeRequired) BeginInvoke(new Action(ApplyColors));
        else ApplyColors();
    }

    private void ApplyColors()
    {
        BackColor = _colors.Border;
        _contentHost.BackColor = _colors.Surface;
        _contentHost.ForeColor = _colors.Text;
        ForeColor = _colors.Text;
        _header.Invalidate();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}
