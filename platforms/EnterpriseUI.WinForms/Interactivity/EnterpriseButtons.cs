using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public enum ButtonContentLayout { ImageBeforeText, ImageAfterText, ImageAboveText }

/// <summary>Access keys: "&amp;Save" underlines S while Alt is held; "&amp;&amp;" is a literal ampersand.</summary>
internal static class Mnemonic
{
    public static (string Text, int Index) Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return (string.Empty, -1);
        var text = new System.Text.StringBuilder();
        var index = -1;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '&' && i + 1 < raw.Length)
            {
                i++;
                if (raw[i] != '&' && index < 0) index = text.Length;
            }

            text.Append(raw[i]);
        }

        return (text.ToString(), index);
    }

    public static void Draw(Graphics g, string? raw, Font font, Rectangle bounds, Color color, ContentAlignment alignment, bool showUnderline, bool wrap = false)
    {
        var (text, index) = Parse(raw);
        if (text.Length == 0) return;
        Chrome.DrawText(g, text, font, bounds, color, alignment, wrap);
        if (index < 0 || !showUnderline || wrap) return;

        var width = Chrome.MeasureText(text, font);
        var left = alignment is ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter ? bounds.X + Math.Max(0, (bounds.Width - width) / 2)
            : alignment is ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight ? bounds.Right - Math.Min(width, bounds.Width) : bounds.X;
        var before = index == 0 ? 0 : Chrome.MeasureText(text[..index], font);
        var charWidth = Math.Max(4, Chrome.MeasureText(text.Substring(index, 1), font));
        if (before + charWidth > bounds.Width) return;
        var top = alignment is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight ? bounds.Y
            : alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight ? bounds.Bottom - font.Height : bounds.Y + (bounds.Height - font.Height) / 2;
        using var pen = new Pen(color);
        var y = top + font.Height - 2;
        g.DrawLine(pen, left + before, y, left + before + charWidth - 2, y);
    }
}

/// <summary>Shared behavior of the button family: variants, glyph or image with text, badge, access keys, animated hover and press states and keyboard focus ring.</summary>
public abstract class EnterpriseButtonBase : EnterpriseControlBase
{
    private readonly ValueAnimator _hover = new();
    private readonly ValueAnimator _press = new();
    private EnterpriseButtonVariant _variant = EnterpriseButtonVariant.Primary;
    private string? _glyph;
    private Image? _image;
    private ButtonContentLayout _layout;
    private string? _badgeText;
    private int _badgeCount;
    private int _cornerRadius = Chrome.CornerRadius;
    private bool _pressed;
    private bool _keyboardFocus;

    protected EnterpriseButtonBase()
    {
        SetStyle(ControlStyles.StandardClick, false);
        Size = new Size(150, 34);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        _hover.Updated += (_, _) => Invalidate();
        _press.Updated += (_, _) => Invalidate();
    }

    [DefaultValue(EnterpriseButtonVariant.Primary)]
    public EnterpriseButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; Invalidate(); }
    }

    /// <summary>Symbol drawn before the text, for example "★" or "⚙"; rendered with a symbol font.</summary>
    [DefaultValue(null)]
    public string? Glyph
    {
        get => _glyph;
        set { _glyph = string.IsNullOrEmpty(value) ? null : value; Invalidate(); }
    }

    [DefaultValue(null)]
    public Image? Image
    {
        get => _image;
        set { _image = value; Invalidate(); }
    }

    [DefaultValue(ButtonContentLayout.ImageBeforeText)]
    public ButtonContentLayout ContentLayout
    {
        get => _layout;
        set { _layout = value; Invalidate(); }
    }

    /// <summary>Free text shown in the badge; wins over <see cref="BadgeCount"/>.</summary>
    [DefaultValue(null)]
    public string? BadgeText
    {
        get => _badgeText;
        set { _badgeText = string.IsNullOrEmpty(value) ? null : value; Invalidate(); }
    }

    /// <summary>Counter badge; hidden at zero and shown as 99+ above 99.</summary>
    [DefaultValue(0)]
    public int BadgeCount
    {
        get => _badgeCount;
        set { _badgeCount = Math.Max(0, value); Invalidate(); }
    }

    [DefaultValue(Chrome.CornerRadius)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    protected string? EffectiveBadge => _badgeText ?? (_badgeCount > 0 ? NotificationCenter.BadgeText(_badgeCount) : null);
    protected bool IsPressed => _pressed;
    protected double HoverAmount => _hover.Value;
    protected double PressAmount => _press.Value;
    protected bool ShowFocusVisual => Focused && _keyboardFocus && Enabled;
    protected Rectangle FaceBounds => Rectangle.Inflate(new Rectangle(0, 0, Width, Height), -S(2), -S(2));

    public virtual void PerformClick()
    {
        if (Enabled && Visible) OnClick(EventArgs.Empty);
    }

    /// <summary>Whether releasing the mouse at this point counts as a click.</summary>
    protected virtual bool ShouldClick(Point location) => true;

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override bool ProcessMnemonic(char charCode)
    {
        if (!CanSelect || !Enabled || !IsMnemonic(charCode, Text)) return false;
        Focus();
        PerformClick();
        return true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover.AnimateTo(1, 120);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover.AnimateTo(0, 180);
        if (!_pressed) return;
        _pressed = false;
        _press.AnimateTo(0, 120);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        _keyboardFocus = false;
        _pressed = true;
        Focus();
        _press.AnimateTo(1, 70);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_pressed) return;
        _pressed = false;
        _press.AnimateTo(0, 160);
        if (e.Button == MouseButtons.Left && Enabled && ClientRectangle.Contains(e.Location) && ShouldClick(e.Location)) OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _keyboardFocus = true;
        if (e.KeyCode is not (Keys.Enter or Keys.Space) || e.Alt || !Enabled) return;
        _press.Jump(1);
        _press.AnimateTo(0, 200);
        PerformClick();
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        _keyboardFocus = !_pressed;
        base.OnGotFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () => EffectiveBadge, StateFlags, null, "Press");

    protected virtual AccessibleStates StateFlags() => _pressed ? AccessibleStates.Pressed : AccessibleStates.None;

    protected Color FillFor(EnterpriseButtonVariant variant) => FormChrome.VariantColor(Colors, variant);

    /// <summary>Text drawn on the face; differs from <see cref="Control.Text"/> while a button shows transient feedback.</summary>
    protected virtual string DisplayText => Text;

    /// <summary>Face, content, badge and focus ring exactly as a plain button paints them.</summary>
    protected void PaintStandard(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FillFor(Variant);
        var face = FaceBounds;
        PaintFace(g, face, fill, outline);
        PaintContent(g, new Rectangle(face.X + S(8), face.Y, Math.Max(0, face.Width - S(16) - BadgeReserve), face.Height), ForeFor(fill, outline));
        PaintBadge(g, face, fill);
        PaintFocus(g);
    }

    /// <summary>Rounded face with hover and pressed tints blended by the animators.</summary>
    protected void PaintFace(Graphics g, Rectangle face, Color fill, bool outline, Color? outlineBorder = null)
    {
        var c = Colors;
        Color back;
        if (!Enabled) back = Chrome.Blend(c.Surface, c.Border, 0.35);
        else if (outline)
        {
            back = Chrome.Blend(c.Surface, c.Primary, 0.12 * HoverAmount + 0.14 * PressAmount);
        }
        else
        {
            back = Chrome.Blend(fill, Color.White, 0.14 * HoverAmount);
            back = Chrome.Blend(back, Color.Black, 0.18 * PressAmount);
        }

        Chrome.FillRounded(g, face, back, _cornerRadius);
        var border = !Enabled ? c.Border : outline ? (outlineBorder ?? c.Primary) : back;
        Chrome.DrawRoundedBorder(g, face, border, _cornerRadius, outline || c.HighContrast ? (c.HighContrast ? 2f : 1.5f) : 1f);
    }

    protected void PaintFocus(Graphics g)
    {
        if (!ShowFocusVisual) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Chrome.DrawFocusRing(g, new Rectangle(0, 0, Width, Height), Colors, _cornerRadius + 2);
    }

    protected Color ForeFor(Color fill, bool outline)
    {
        if (!Enabled) return Colors.Disabled;
        if (outline) return Colors.Primary;
        return Variant == EnterpriseButtonVariant.Primary && fill == Colors.Primary ? Colors.PrimaryText : FormChrome.TextOn(fill);
    }

    protected int BadgeReserve => EffectiveBadge == null || _layout == ButtonContentLayout.ImageAboveText ? 0 : BadgeSize(EffectiveBadge).Width + S(6);

    private Size BadgeSize(string text) => new(Math.Max(S(18), Chrome.MeasureText(text, Font) + S(10)), S(16));

    protected void PaintBadge(Graphics g, Rectangle face, Color fill)
    {
        var text = EffectiveBadge;
        if (text == null) return;
        var size = BadgeSize(text);
        var rect = new Rectangle(face.Right - size.Width - S(6), face.Y + (_layout == ButtonContentLayout.ImageAboveText ? S(3) : (face.Height - size.Height) / 2), size.Width, size.Height);
        var back = Colors.Danger == fill ? Colors.Warning : Colors.Danger;
        if (!Enabled) back = Colors.Disabled;
        Chrome.FillRounded(g, rect, back, rect.Height / 2);
        Chrome.DrawText(g, text, Font, rect, ColorUtil.Readable(back), ContentAlignment.MiddleCenter);
    }

    /// <summary>Draws glyph or image plus the mnemonic text centered in <paramref name="area"/>.</summary>
    protected void PaintContent(Graphics g, Rectangle area, Color fore)
    {
        var (display, _) = Mnemonic.Parse(DisplayText);
        var hasText = display.Length > 0;
        var hasIcon = _glyph != null || _image != null;
        if (!hasText && !hasIcon) return;

        var gap = S(6);
        var glyphFont = _glyph != null ? GlyphFonts.Get(Font.SizeInPoints * 1.25f) : null;
        var iconSize = _glyph != null ? TextRenderer.MeasureText(_glyph, glyphFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix) : _image != null ? new Size(S(18), S(18)) : Size.Empty;
        var textSize = hasText ? new Size(Chrome.MeasureText(display, BoldFont), BoldFont.Height) : Size.Empty;
        var above = _layout == ButtonContentLayout.ImageAboveText && hasIcon && hasText;
        var spacing = hasIcon && hasText ? gap : 0;

        int groupW, groupH;
        if (above)
        {
            groupW = Math.Max(iconSize.Width, Math.Min(textSize.Width, area.Width));
            groupH = iconSize.Height + spacing + textSize.Height;
        }
        else
        {
            textSize.Width = Math.Min(textSize.Width, Math.Max(0, area.Width - iconSize.Width - spacing));
            groupW = iconSize.Width + spacing + textSize.Width;
            groupH = Math.Max(iconSize.Height, textSize.Height);
        }

        var x = area.X + Math.Max(0, (area.Width - groupW) / 2);
        var y = area.Y + Math.Max(0, (area.Height - groupH) / 2);
        Rectangle iconRect, textRect;
        if (above)
        {
            iconRect = new Rectangle(x + (groupW - iconSize.Width) / 2, y, iconSize.Width, iconSize.Height);
            textRect = new Rectangle(area.X, y + iconSize.Height + spacing, area.Width, textSize.Height);
        }
        else if (_layout == ButtonContentLayout.ImageAfterText)
        {
            textRect = new Rectangle(x, area.Y, textSize.Width, area.Height);
            iconRect = new Rectangle(x + textSize.Width + spacing, y + (groupH - iconSize.Height) / 2, iconSize.Width, iconSize.Height);
        }
        else
        {
            iconRect = new Rectangle(x, y + (groupH - iconSize.Height) / 2, iconSize.Width, iconSize.Height);
            textRect = new Rectangle(x + iconSize.Width + spacing, area.Y, textSize.Width, area.Height);
        }

        if (_glyph != null) TextRenderer.DrawText(g, _glyph, glyphFont, iconRect, fore, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        else if (_image != null) DrawImage(g, iconRect);
        if (hasText) Mnemonic.Draw(g, DisplayText, BoldFont, textRect, fore, above ? ContentAlignment.TopCenter : ContentAlignment.MiddleLeft, ShowKeyboardCues);
    }

    private void DrawImage(Graphics g, Rectangle target)
    {
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (Enabled) g.DrawImage(_image!, target);
        else
        {
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix(new[] { new[] { 0.3f, 0.3f, 0.3f, 0, 0 }, new[] { 0.59f, 0.59f, 0.59f, 0, 0 }, new[] { 0.11f, 0.11f, 0.11f, 0, 0 }, new[] { 0, 0, 0, 0.45f, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
            g.DrawImage(_image!, target, 0, 0, _image!.Width, _image.Height, GraphicsUnit.Pixel, attributes);
        }

        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
            _press.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>Standard push button; supports <see cref="DialogResult"/> and acts as a form's accept or cancel button.</summary>
public class EnterpriseButtonControl : EnterpriseButtonBase, IButtonControl
{
    private bool _isDefault;

    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value)
    {
        _isDefault = value;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        if (FindForm() is { } form && DialogResult != DialogResult.None) form.DialogResult = DialogResult;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FillFor(Variant);
        var face = FaceBounds;
        PaintFace(g, face, fill, outline);
        if (_isDefault && !outline && Enabled) Chrome.DrawRoundedBorder(g, Rectangle.Inflate(face, -2, -2), Color.FromArgb(150, FormChrome.TextOn(fill)), CornerRadius, 1f);
        PaintContent(g, new Rectangle(face.X + S(8), face.Y, Math.Max(0, face.Width - S(16) - BadgeReserve), face.Height), ForeFor(fill, outline));
        PaintBadge(g, face, fill);
        PaintFocus(g);
    }
}

/// <summary>Latching button: two-state (or three-state) with an optional exclusive group.</summary>
public class EnterpriseToggleButtonControl : EnterpriseButtonBase
{
    private CheckState _state;

    public EnterpriseToggleButtonControl() => AccessibleRole = AccessibleRole.CheckButton;

    [DefaultValue(false)]
    public bool ThreeState { get; set; }

    /// <summary>Toggle buttons sharing a group name under one parent behave like radio buttons.</summary>
    [DefaultValue(null)]
    public string? GroupName { get; set; }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _state == CheckState.Checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            if (value == CheckState.Checked && !string.IsNullOrEmpty(GroupName) && Parent != null)
            {
                foreach (var sibling in Parent.Controls.OfType<EnterpriseToggleButtonControl>().Where(s => s != this && s.GroupName == GroupName)) sibling.CheckState = CheckState.Unchecked;
            }

            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            CheckStateChanged?.Invoke(this, EventArgs.Empty);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;
    public event EventHandler? CheckStateChanged;

    public void Toggle()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            if (_state != CheckState.Checked) CheckState = CheckState.Checked;
            return;
        }

        CheckState = ThreeState ? _state switch { CheckState.Unchecked => CheckState.Checked, CheckState.Checked => CheckState.Indeterminate, _ => CheckState.Unchecked }
            : _state == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
    }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled) Toggle();
        base.OnClick(e);
    }

    protected override AccessibleStates StateFlags() => _state switch { CheckState.Checked => AccessibleStates.Checked, CheckState.Indeterminate => AccessibleStates.Mixed, _ => AccessibleStates.None };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var face = FaceBounds;
        Color fore;
        var fill = FillFor(Variant == EnterpriseButtonVariant.Outline ? EnterpriseButtonVariant.Primary : Variant);
        if (_state == CheckState.Checked)
        {
            PaintFace(g, face, fill, false);
            fore = ForeFor(fill, false);
        }
        else
        {
            PaintFace(g, face, Colors.Surface, true, _state == CheckState.Indeterminate ? Colors.Primary : Colors.Border);
            fore = !Enabled ? Colors.Disabled : _state == CheckState.Indeterminate ? Colors.Primary : Colors.Text;
        }

        PaintContent(g, new Rectangle(face.X + S(8), face.Y, Math.Max(0, face.Width - S(16) - BadgeReserve), face.Height), fore);
        if (_state == CheckState.Indeterminate)
        {
            using var pen = new Pen(Colors.Primary, S(2));
            g.DrawLine(pen, face.X + face.Width / 2 - S(8), face.Bottom - S(5), face.X + face.Width / 2 + S(8), face.Bottom - S(5));
        }

        PaintBadge(g, face, fill);
        PaintFocus(g);
    }
}

/// <summary>Button with a drop-down part: the main part runs the default action, the arrow part opens the menu.</summary>
public class EnterpriseSplitButtonControl : EnterpriseButtonBase
{
    private readonly ContextMenuStrip _menu = new();
    private readonly ValueAnimator _arrowHover = new();
    private int _hotPart;
    private int _pressedPart;
    private DateTime _menuClosedAt = DateTime.MinValue;
    private bool _menuOpen;

    public EnterpriseSplitButtonControl()
    {
        Size = new Size(175, 34);
        AccessibleRole = AccessibleRole.SplitButton;
        _menu.Opened += (_, _) => { _menuOpen = true; Invalidate(); };
        _menu.Closed += (_, _) => { _menuOpen = false; _menuClosedAt = DateTime.UtcNow; Invalidate(); DropDownClosed?.Invoke(this, EventArgs.Empty); };
        _menu.Opening += (_, _) => DropDownOpening?.Invoke(this, EventArgs.Empty);
        _arrowHover.Updated += (_, _) => Invalidate();
        ApplyMenuTheme();
    }

    /// <summary>True: the main part clicks and only the arrow opens the menu. False: the whole button opens the menu.</summary>
    [DefaultValue(true)]
    public bool IsSplitMode { get; set; } = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripItemCollection MenuItems => _menu.Items;

    [Browsable(false)]
    public bool IsDropDownOpen => _menuOpen;

    public event EventHandler? PrimaryActionClicked;
    public event Action<string>? MenuItemSelected;
    public event EventHandler? DropDownOpening;
    public event EventHandler? DropDownClosed;

    public ToolStripMenuItem AddOption(string itemText, Action? onClick = null, string? glyph = null)
    {
        var item = new ToolStripMenuItem(itemText);
        if (glyph != null) item.Text = glyph + "  " + itemText;
        item.Click += (_, _) =>
        {
            onClick?.Invoke();
            MenuItemSelected?.Invoke(itemText);
        };
        _menu.Items.Add(item);
        return item;
    }

    public void AddSeparator() => _menu.Items.Add(new ToolStripSeparator());

    public void ClearOptions() => _menu.Items.Clear();

    private int ArrowWidth => S(30);
    private Rectangle ArrowRect => new(FaceBounds.Right - ArrowWidth, FaceBounds.Y, ArrowWidth, FaceBounds.Height);

    private int PartAt(Point p) => !IsSplitMode || ArrowRect.Contains(p) || p.X >= ArrowRect.X ? 2 : 1;

    public void ShowMenu()
    {
        if (!Enabled || _menu.Items.Count == 0) return;
        _menu.MinimumSize = new Size(Width, 0);
        _menu.Show(this, new Point(0, Height));
    }

    private void ApplyMenuTheme()
    {
        _menu.Renderer = new ThemedMenuRenderer(Colors);
        _menu.BackColor = Colors.Surface;
        _menu.ForeColor = Colors.Text;
        _menu.Font = Font;
    }

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        ApplyMenuTheme();
    }

    protected override bool ShouldClick(Point location) => IsSplitMode && PartAt(location) == 1;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (IsSplitMode) PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
    }

    public override void PerformClick()
    {
        if (!Enabled) return;
        if (IsSplitMode) OnClick(EventArgs.Empty);
        else ShowMenu();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var part = PartAt(e.Location);
        if (part == _hotPart) return;
        _hotPart = part;
        _arrowHover.AnimateTo(part == 2 && IsSplitMode ? 1 : 0, 100);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotPart = 0;
        _arrowHover.AnimateTo(0, 120);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressedPart = PartAt(e.Location);
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled || _pressedPart != 2) return;
        if ((DateTime.UtcNow - _menuClosedAt).TotalMilliseconds > 250) ShowMenu();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down || e.KeyCode == Keys.F4 || (e.KeyCode == Keys.Space && !IsSplitMode) || (e.KeyCode == Keys.Enter && !IsSplitMode))
        {
            base.OnKeyDown(e);
            ShowMenu();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Down || base.IsInputKey(keyData);

    protected override AccessibleStates StateFlags() => (_menuOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed) | base.StateFlags();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FillFor(Variant);
        var face = FaceBounds;
        PaintFace(g, face, fill, outline);
        var fore = ForeFor(fill, outline);

        var arrow = ArrowRect;
        var contentRight = IsSplitMode ? arrow.X : face.Right - arrow.Width;
        if (IsSplitMode)
        {
            if (_arrowHover.Value > 0.01 && Enabled)
            {
                var state = g.Save();
                using var clip = Chrome.RoundedRect(face, CornerRadius);
                g.SetClip(clip);
                using var tint = new SolidBrush(Color.FromArgb((int)(40 * _arrowHover.Value), outline ? Colors.Primary : Color.White));
                g.FillRectangle(tint, arrow);
                g.Restore(state);
            }

            using var pen = new Pen(Color.FromArgb(Enabled ? 120 : 60, fore));
            g.DrawLine(pen, arrow.X, face.Y + S(7), arrow.X, face.Bottom - S(8));
        }

        PaintContent(g, new Rectangle(face.X + S(8), face.Y, Math.Max(0, contentRight - face.X - S(12)), face.Height), fore);
        Chrome.DrawChevron(g, arrow, _menuOpen ? ChevronDirection.Up : ChevronDirection.Down, fore, 1.8f);
        PaintFocus(g);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _arrowHover.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>Button whose whole surface opens a menu.</summary>
public class EnterpriseDropDownButtonControl : EnterpriseSplitButtonControl
{
    public EnterpriseDropDownButtonControl()
    {
        IsSplitMode = false;
        Size = new Size(165, 34);
    }
}
