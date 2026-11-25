using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Input;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms.Design;

namespace EnterpriseUI.WinForms;

/// <summary>How the image or glyph of a button is arranged relative to its text.</summary>
public enum ButtonContentLayout { ImageBeforeText, ImageAfterText, ImageAboveText, TextAboveImage, Overlay }

/// <summary>Access keys: "&amp;Save" underlines S while Alt is held; "&amp;&amp;" is a literal ampersand.</summary>
internal static class Mnemonic
{
    public static (string Text, int Index) Parse(string? raw)
    {
        if (raw is null || raw.Length == 0) return (string.Empty, -1);
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

    public static void Draw(Graphics g, string? raw, Font font, Rectangle bounds, Color color, ContentAlignment alignment, bool showUnderline, bool wrap = false, bool rightToLeft = false)
    {
        var (text, index) = Parse(raw);
        DrawParsed(g, text, index, font, bounds, color, alignment, showUnderline, wrap, rightToLeft);
    }

    /// <summary>Draws text that <see cref="Parse"/> already split into the plain text and the index of the access key.</summary>
    public static void DrawParsed(Graphics g, string text, int index, Font font, Rectangle bounds, Color color, ContentAlignment alignment, bool showUnderline, bool wrap = false, bool rightToLeft = false)
    {
        if (text.Length == 0) return;
        var flags = Chrome.TextFlags(alignment, wrap);
        if (rightToLeft) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, text, font, bounds, color, flags);
        if (index < 0 || !showUnderline || wrap || rightToLeft) return;

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

/// <summary>Shared behavior of the button family: variants, glyph, image or SVG with text, badge, access keys, commands, tooltips, animated hover and press states and the keyboard focus ring.</summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
[Designer(typeof(EnterpriseControlDesigner<ButtonActions>))]
public abstract class EnterpriseButtonBase : EnterpriseControlBase
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int BaseImageSize = 18;
    private static ImageAttributes? s_disabledImage;

    private readonly ValueAnimator _hover = new();
    private readonly ValueAnimator _press = new();
    private readonly Func<int, Size> _measureText;
    private readonly EventHandler _canExecuteChanged;
    private EnterpriseButtonVariant _variant = EnterpriseButtonVariant.Primary;
    private string? _glyph;
    private Image? _image;
    private EnterpriseSvgImage? _svg;
    private ImageList? _imageList;
    private Image? _listImage;
    private int _imageIndex = -1;
    private string _imageKey = string.Empty;
    private Size _imageSize;
    private ButtonContentLayout _layout;
    private ButtonDisplayStyle _displayStyle;
    private ContentAlignment _textAlignment = ContentAlignment.MiddleCenter;
    private ContentAlignment _imageAlignment = ContentAlignment.MiddleCenter;
    private ButtonClickMode _clickMode;
    private AutoSizeMode _autoSizeMode = AutoSizeMode.GrowOnly;
    private string? _badgeText;
    private int _badgeCount;
    private int _cornerRadius = Chrome.CornerRadius;
    private Color _fillColor;
    private Color _borderColor;
    private Color _textColor;
    private string? _toolTipText;
    private ICommand? _command;
    private object? _commandParameter;
    private bool _enabledBeforeCommand = true;
    private bool _textWrap;
    private bool _useMnemonic = true;
    private bool _showFocusRing = true;
    private bool _animateStates = true;
    private bool _showToolTips = true;
    private bool _pressed;
    private bool _keyboardFocus;
    private bool _autoRepeat;
    private bool _adjusting;
    private ButtonVisualState _lastState;
    private string? _parsedRaw;
    private bool _parsedMnemonic = true;
    private string _parsedText = string.Empty;
    private int _parsedIndex = -1;
    private int _parsedWidth = -1;
    private Font? _parsedFont;
    private string? _glyphSizeKey;
    private Size _glyphSize;
    private ThemedToolTip? _tip;
    private System.Windows.Forms.Timer? _tipTimer;

    protected EnterpriseButtonBase()
    {
        SetStyle(ControlStyles.StandardClick, false);
        Size = new Size(150, 34);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        _measureText = MeasureText;
        _canExecuteChanged = OnCommandCanExecuteChanged;
        _hover.Updated += (_, _) => Invalidate();
        _press.Updated += (_, _) => Invalidate();
    }

    // ---- appearance ----

    /// <summary>The color style of the button: a solid color, or an outline.</summary>
    [Category("Appearance"), Description("The color style of the button.")]
    [DefaultValue(EnterpriseButtonVariant.Primary)]
    public EnterpriseButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; Invalidate(); }
    }

    /// <summary>Symbol drawn before the text, for example "★" or "⚙"; rendered with a symbol font.</summary>
    [Category("Appearance"), Description("A symbol character drawn with the text, for example a star or a gear.")]
    [DefaultValue(null)]
    public string? Glyph
    {
        get => _glyph;
        set { _glyph = string.IsNullOrEmpty(value) ? null : value; _glyphSizeKey = null; ContentChanged(); }
    }

    /// <summary>A bitmap shown with the text; it is scaled to <see cref="ImageSize"/> keeping its proportions. Used after <see cref="SvgImage"/> and <see cref="Glyph"/>.</summary>
    [Category("Appearance"), Description("A bitmap shown with the text.")]
    [DefaultValue(null), Localizable(true)]
    public Image? Image
    {
        get => _image;
        set { _image = value; ContentChanged(); }
    }

    /// <summary>A vector image painted in the text color of the current state, so it follows the theme, stays sharp at every DPI and is dimmed when disabled. It wins over <see cref="Glyph"/> and <see cref="Image"/>.</summary>
    [Category("Appearance"), Description("A vector image painted in the text color of the button.")]
    [DefaultValue(null)]
    public EnterpriseSvgImage? SvgImage
    {
        get => _svg;
        set { _svg = value; ContentChanged(); }
    }

    /// <summary>The image list that <see cref="ImageIndex"/> and <see cref="ImageKey"/> refer to.</summary>
    [Category("Appearance"), Description("The image list that ImageIndex and ImageKey refer to.")]
    [DefaultValue(null), RefreshProperties(RefreshProperties.Repaint)]
    public ImageList? ImageList
    {
        get => _imageList;
        set
        {
            if (_imageList == value) return;
            if (_imageList is not null) _imageList.RecreateHandle -= OnImageListChanged;
            _imageList = value;
            if (value is not null) value.RecreateHandle += OnImageListChanged;
            ReleaseListImage();
            ContentChanged();
        }
    }

    /// <summary>Index of the image in <see cref="ImageList"/>; -1 for none.</summary>
    [Category("Appearance"), Description("Index of the image in the image list.")]
    [DefaultValue(-1), TypeConverter(typeof(ImageIndexConverter)), RefreshProperties(RefreshProperties.Repaint)]
    public int ImageIndex
    {
        get => _imageIndex;
        set
        {
            if (value < -1) throw new ArgumentOutOfRangeException(nameof(value), value, "The index must be -1 or greater.");
            _imageIndex = value;
            if (value >= 0) _imageKey = string.Empty;
            ReleaseListImage();
            ContentChanged();
        }
    }

    /// <summary>Key of the image in <see cref="ImageList"/>; wins over <see cref="ImageIndex"/>.</summary>
    [Category("Appearance"), Description("Key of the image in the image list.")]
    [DefaultValue(""), TypeConverter(typeof(ImageKeyConverter)), RefreshProperties(RefreshProperties.Repaint)]
    public string ImageKey
    {
        get => _imageKey;
        set
        {
            _imageKey = value ?? string.Empty;
            if (_imageKey.Length > 0) _imageIndex = -1;
            ReleaseListImage();
            ContentChanged();
        }
    }

    /// <summary>The box the image, SVG or glyph is fitted into, in logical pixels; empty means 18 x 18.</summary>
    [Category("Appearance"), Description("The size of the image in logical pixels; empty means 18 x 18.")]
    public Size ImageSize
    {
        get => _imageSize;
        set { _imageSize = new Size(Math.Max(0, value.Width), Math.Max(0, value.Height)); ContentChanged(); }
    }

    private bool ShouldSerializeImageSize() => !_imageSize.IsEmpty;

    private void ResetImageSize() => ImageSize = Size.Empty;

    /// <summary>Which parts of the content are shown.</summary>
    [Category("Appearance"), Description("Which of the image and the text are shown.")]
    [DefaultValue(ButtonDisplayStyle.ImageAndText)]
    public ButtonDisplayStyle DisplayStyle
    {
        get => _displayStyle;
        set { _displayStyle = value; ContentChanged(); }
    }

    /// <summary>How the image is arranged relative to the text.</summary>
    [Category("Appearance"), Description("How the image is arranged relative to the text.")]
    [DefaultValue(ButtonContentLayout.ImageBeforeText)]
    public ButtonContentLayout ContentLayout
    {
        get => _layout;
        set { _layout = value; ContentChanged(); }
    }

    /// <summary>The standard WinForms name of <see cref="ContentLayout"/>.</summary>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Advanced), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextImageRelation TextImageRelation
    {
        get => _layout switch
        {
            ButtonContentLayout.ImageAfterText => TextImageRelation.TextBeforeImage,
            ButtonContentLayout.ImageAboveText => TextImageRelation.ImageAboveText,
            ButtonContentLayout.TextAboveImage => TextImageRelation.TextAboveImage,
            ButtonContentLayout.Overlay => TextImageRelation.Overlay,
            _ => TextImageRelation.ImageBeforeText
        };
        set => ContentLayout = value switch
        {
            TextImageRelation.TextBeforeImage => ButtonContentLayout.ImageAfterText,
            TextImageRelation.ImageAboveText => ButtonContentLayout.ImageAboveText,
            TextImageRelation.TextAboveImage => ButtonContentLayout.TextAboveImage,
            TextImageRelation.Overlay => ButtonContentLayout.Overlay,
            _ => ButtonContentLayout.ImageBeforeText
        };
    }

    /// <summary>Where the image and text block sits on the face; with <see cref="ButtonContentLayout.Overlay"/> where the text sits.</summary>
    [Category("Appearance"), Description("Where the content sits on the face.")]
    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment TextAlignment
    {
        get => _textAlignment;
        set { _textAlignment = value; ContentChanged(); }
    }

    /// <summary>Where the image sits on the face; used only with <see cref="ButtonContentLayout.Overlay"/>.</summary>
    [Category("Appearance"), Description("Where the image sits on the face when it overlays the text.")]
    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment ImageAlignment
    {
        get => _imageAlignment;
        set { _imageAlignment = value; ContentChanged(); }
    }

    /// <summary>Breaks long text into several lines instead of trimming it with an ellipsis. The access key underline is not drawn for wrapped text.</summary>
    [Category("Appearance"), Description("Breaks long text into several lines.")]
    [DefaultValue(false)]
    public bool TextWrap
    {
        get => _textWrap;
        set { _textWrap = value; ContentChanged(); }
    }

    /// <summary>Free text shown in the badge; wins over <see cref="BadgeCount"/>.</summary>
    [Category("Appearance"), Description("Text shown in a badge on the button.")]
    [DefaultValue(null)]
    public string? BadgeText
    {
        get => _badgeText;
        set { _badgeText = string.IsNullOrEmpty(value) ? null : value; BadgeChanged(); }
    }

    /// <summary>Counter badge; hidden at zero and shown as 99+ above 99.</summary>
    [Category("Appearance"), Description("A counter shown in a badge; hidden at zero.")]
    [DefaultValue(0)]
    public int BadgeCount
    {
        get => _badgeCount;
        set { _badgeCount = Math.Max(0, value); BadgeChanged(); }
    }

    /// <summary>Radius of the rounded corners in logical pixels.</summary>
    [Category("Appearance"), Description("Radius of the rounded corners in logical pixels.")]
    [DefaultValue(Chrome.CornerRadius)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    /// <summary>Replaces the variant color of the face; the accent of the outline variant. Empty follows the theme. Ignored in Windows high contrast mode.</summary>
    [Category("Appearance"), Description("Replaces the variant color; empty follows the theme.")]
    [DefaultValue(typeof(Color), "")]
    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    /// <summary>Replaces the border color. Empty follows the theme. Ignored in Windows high contrast mode.</summary>
    [Category("Appearance"), Description("Replaces the border color; empty follows the theme.")]
    [DefaultValue(typeof(Color), "")]
    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    /// <summary>Replaces the text and image color. Empty follows the theme. Ignored in Windows high contrast mode.</summary>
    [Category("Appearance"), Description("Replaces the text color; empty follows the theme.")]
    [DefaultValue(typeof(Color), "")]
    public Color TextColor
    {
        get => _textColor;
        set { _textColor = value; Invalidate(); }
    }

    /// <summary>Shows the access key (the letter after &amp; in <see cref="Control.Text"/>) and lets Alt+letter click the button.</summary>
    [Category("Appearance"), Description("Treats & in the text as the access key marker.")]
    [DefaultValue(true)]
    public bool UseMnemonic
    {
        get => _useMnemonic;
        set { _useMnemonic = value; ContentChanged(); }
    }

    /// <summary>Draws the focus ring when the button has keyboard focus.</summary>
    [Category("Appearance"), Description("Draws the focus ring for keyboard focus.")]
    [DefaultValue(true)]
    public bool ShowFocusRing
    {
        get => _showFocusRing;
        set { _showFocusRing = value; Invalidate(); }
    }

    /// <summary>Animates hover and pressed transitions. Animations are always skipped when Windows has animation effects turned off.</summary>
    [Category("Appearance"), Description("Animates hover and pressed transitions.")]
    [DefaultValue(true)]
    public bool AnimateStates
    {
        get => _animateStates;
        set => _animateStates = value;
    }

    /// <summary>Sizes the button to its content, see <see cref="AutoSizeMode"/>.</summary>
    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set
        {
            if (base.AutoSize == value) return;
            base.AutoSize = value;
            AdjustAutoSize();
        }
    }

    /// <summary>With GrowAndShrink the button always has the size of its content; with GrowOnly it never gets smaller than its current size.</summary>
    [Category("Layout"), Description("Whether an auto-sized button can shrink.")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    public AutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set { _autoSizeMode = value; AdjustAutoSize(); }
    }

    // ---- behavior ----

    /// <summary>When the button raises its click.</summary>
    [Category("Behavior"), Description("When the button raises its click: on release, on press or on hover.")]
    [DefaultValue(ButtonClickMode.Release)]
    public ButtonClickMode ClickMode
    {
        get => _clickMode;
        set => _clickMode = value;
    }

    /// <summary>Command executed after <see cref="Control.Click"/>. While a command is set, the button is enabled only while <see cref="ICommand.CanExecute"/> allows it; <see cref="Control.Enabled"/> is restored when the command is removed.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ICommand? Command
    {
        get => _command;
        set
        {
            if (_command == value) return;
            if (_command is null) _enabledBeforeCommand = Enabled;
            else _command.CanExecuteChanged -= _canExecuteChanged;
            _command = value;
            if (value is null)
            {
                Enabled = _enabledBeforeCommand;
                return;
            }

            value.CanExecuteChanged += _canExecuteChanged;
            SyncCommandState();
        }
    }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? CommandParameter
    {
        get => _commandParameter;
        set
        {
            _commandParameter = value;
            SyncCommandState();
        }
    }

    /// <summary>Text of the tooltip; image-only buttons fall back to their text.</summary>
    [Category("Behavior"), Description("Text of the tooltip.")]
    [DefaultValue(null), Localizable(true)]
    public string? ToolTipText
    {
        get => _toolTipText;
        set => _toolTipText = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Shows tooltips; turn off to suppress both <see cref="ToolTipText"/> and <see cref="ToolTipTextNeeded"/>.</summary>
    [Category("Behavior"), Description("Shows tooltips.")]
    [DefaultValue(true)]
    public bool ShowToolTips
    {
        get => _showToolTips;
        set
        {
            _showToolTips = value;
            if (!value) HideToolTip();
        }
    }

    // ---- state ----

    /// <summary>The state the button is painted in.</summary>
    [Browsable(false)]
    public ButtonVisualState VisualState => !Enabled ? ButtonVisualState.Disabled : _pressed ? ButtonVisualState.Pressed : IsHovered ? ButtonVisualState.Hot : ButtonVisualState.Normal;

    /// <summary>True while the left mouse button is held down on the button.</summary>
    [Browsable(false)]
    public bool IsPressed => _pressed;

    // ---- events ----

    /// <summary>Raised before the face (fill and border) is painted; set <see cref="EnterpriseButtonPaintEventArgs.Handled"/> to draw it yourself.</summary>
    [Category("Appearance"), Description("Raised before the face is painted.")]
    public event EventHandler<EnterpriseButtonPaintEventArgs>? FacePainting;

    /// <summary>Raised before the image and text are painted; set <see cref="EnterpriseButtonPaintEventArgs.Handled"/> to draw them yourself.</summary>
    [Category("Appearance"), Description("Raised before the content is painted.")]
    public event EventHandler<EnterpriseButtonPaintEventArgs>? ContentPainting;

    /// <summary>Raised when the tooltip is about to show; the text set by the handler wins over <see cref="ToolTipText"/>.</summary>
    [Category("Behavior"), Description("Raised when the tooltip is about to show.")]
    public event EventHandler<ButtonToolTipTextNeededEventArgs>? ToolTipTextNeeded;

    /// <summary>Raised when <see cref="VisualState"/> changes.</summary>
    [Category("Behavior"), Description("Raised when the visual state changes.")]
    public event EventHandler? VisualStateChanged;

    // ---- protected state used by derived painters ----

    protected string? EffectiveBadge => _badgeText ?? (_badgeCount > 0 ? NotificationCenter.BadgeText(_badgeCount) : null);
    protected double HoverAmount => _hover.Value;
    protected double PressAmount => _press.Value;
    protected bool ShowFocusVisual => _showFocusRing && Focused && _keyboardFocus && Enabled;
    protected Rectangle FaceBounds => Rectangle.Inflate(new Rectangle(0, 0, Width, Height), -S(2), -S(2));

    /// <summary>Corner radius in device pixels.</summary>
    protected int ScaledRadius => S(_cornerRadius);

    /// <summary>The accent of the outline variant and of unchecked toggles: <see cref="FillColor"/> or the theme primary color.</summary>
    protected Color AccentColor => CustomColor(_fillColor) ?? Colors.Primary;

    /// <summary>Text drawn on the face; differs from <see cref="Control.Text"/> while a button shows transient feedback.</summary>
    protected virtual string DisplayText => Text;

    /// <summary>The name of the default accessibility action.</summary>
    protected virtual string DefaultActionName => "Press";

    protected virtual AccessibleStates StateFlags() => _pressed ? AccessibleStates.Pressed : AccessibleStates.None;

    /// <summary>Whether releasing the mouse at this point counts as a click.</summary>
    protected virtual bool ShouldClick(Point location) => true;

    private Color? CustomColor(Color color) => color.IsEmpty || Colors.HighContrast ? null : color;

    // ---- actions ----

    /// <summary>Clicks the button as if the user had; does nothing when it is disabled or hidden.</summary>
    public virtual void PerformClick()
    {
        if (Enabled && Visible) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_command is { } command && command.CanExecute(_commandParameter)) command.Execute(_commandParameter);
    }

    // ---- input ----

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override bool ProcessMnemonic(char charCode)
    {
        if (!_useMnemonic || !CanSelect || !Enabled || !IsMnemonic(charCode, Text)) return false;
        Focus();
        PerformClick();
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg is WmKeyDown or WmSysKeyDown)
        {
            _autoRepeat = ((long)m.LParam & 0x40000000L) != 0;
            try { base.WndProc(ref m); }
            finally { _autoRepeat = false; }
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Animate(_hover, 1, 120);
        UpdateVisualState();
        ScheduleToolTip();
        if (_clickMode == ButtonClickMode.Hover && Enabled) OnClick(EventArgs.Empty);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Animate(_hover, 0, 180);
        HideToolTip();
        if (_pressed)
        {
            _pressed = false;
            Animate(_press, 0, 120);
        }

        UpdateVisualState();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        HideToolTip();
        if (e.Button != MouseButtons.Left || !Enabled) return;
        _keyboardFocus = false;
        _pressed = true;
        Focus();
        Animate(_press, 1, 70);
        UpdateVisualState();
        if (_clickMode == ButtonClickMode.Press && ShouldClick(e.Location)) OnClick(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_pressed) return;
        _pressed = false;
        Animate(_press, 0, 160);
        UpdateVisualState();
        if (_clickMode == ButtonClickMode.Release && e.Button == MouseButtons.Left && Enabled && ClientRectangle.Contains(e.Location) && ShouldClick(e.Location)) OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            Invalidate();
        }
        if (e.KeyCode is not (Keys.Enter or Keys.Space) || e.Alt || !Enabled) return;
        e.Handled = true;
        if (_autoRepeat) return;
        _press.Jump(1);
        Animate(_press, 0, 200);
        PerformClick();
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
        if (!Enabled) HideToolTip();
        UpdateVisualState();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ContentChanged();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _glyphSizeKey = null;
        AdjustAutoSize();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        ContentChanged();
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _glyphSizeKey = null;
        ContentChanged();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_command is not null) SyncCommandState();
    }

    // ---- sizing ----

    /// <summary>The size that fits the content, the badge and the padding; text is wrapped to the proposed width when <see cref="TextWrap"/> is on.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var insetX = 2 * (S(2) + S(8)) + Padding.Horizontal;
        var insetY = 2 * (S(2) + S(5)) + Padding.Vertical;
        var width = _textWrap && proposedSize.Width > 0 ? Math.Max(1, proposedSize.Width - insetX - BadgeReserve) : 8192;
        var content = LayoutContent(new Rectangle(0, 0, width, 8192)).Content;
        return new Size(Math.Max(S(30), content.Width + insetX + BadgeReserve), Math.Max(S(28), content.Height + insetY));
    }

    private void ContentChanged()
    {
        Invalidate();
        AdjustAutoSize();
    }

    private void AdjustAutoSize()
    {
        if (!AutoSize || _adjusting) return;
        var preferred = GetPreferredSize(Size.Empty);
        var target = _autoSizeMode == AutoSizeMode.GrowAndShrink ? preferred : new Size(Math.Max(Width, preferred.Width), Math.Max(Height, preferred.Height));
        if (target == Size) return;
        _adjusting = true;
        try { Size = target; }
        finally { _adjusting = false; }
    }

    // ---- painting ----

    protected override void OnPaint(PaintEventArgs e) => PaintStandard(e.Graphics);

    /// <summary>Face, content, badge and focus ring exactly as a plain button paints them.</summary>
    protected void PaintStandard(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Colors.Surface);
        var outline = Variant == EnterpriseButtonVariant.Outline;
        var fill = FillFor(Variant);
        var face = FaceBounds;
        PaintFace(g, face, fill, outline);
        PaintFaceDecoration(g, face, fill, outline);
        PaintContent(g, ContentArea(face, BadgeReserve), ForeFor(fill, outline));
        PaintBadge(g, face, fill);
        PaintFocus(g);
    }

    /// <summary>Called after the face and before the content, for markers such as the default button outline.</summary>
    protected virtual void PaintFaceDecoration(Graphics g, Rectangle face, Color fill, bool outline)
    {
    }

    /// <summary>Where the content may be drawn: the face without the horizontal margin, the <see cref="Control.Padding"/> and <paramref name="reserveRight"/>.</summary>
    protected Rectangle ContentArea(Rectangle face, int reserveRight = 0) =>
        new(face.X + S(8) + Padding.Left, face.Y + Padding.Top, Math.Max(0, face.Width - S(16) - Padding.Horizontal - reserveRight), Math.Max(0, face.Height - Padding.Vertical));

    /// <summary>Rounded face with hover and pressed tints blended by the animators.</summary>
    protected void PaintFace(Graphics g, Rectangle face, Color fill, bool outline, Color? outlineBorder = null)
    {
        if (FacePainting is { } handler)
        {
            var args = new EnterpriseButtonPaintEventArgs(g, face, VisualState, fill, ForeFor(fill, outline));
            handler(this, args);
            if (args.Handled) return;
        }

        var c = Colors;
        var accent = AccentColor;
        Color back;
        if (!Enabled) back = Chrome.Blend(c.Surface, c.Border, 0.35);
        else if (outline)
        {
            back = Chrome.Blend(c.Surface, accent, 0.12 * HoverAmount + 0.14 * PressAmount);
        }
        else
        {
            back = Chrome.Blend(fill, Color.White, 0.14 * HoverAmount);
            back = Chrome.Blend(back, Color.Black, 0.18 * PressAmount);
        }

        var radius = ScaledRadius;
        Chrome.FillRounded(g, face, back, radius);
        var border = !Enabled ? c.Border : CustomColor(_borderColor) ?? (outline ? (outlineBorder ?? accent) : back);
        var width = (outline || c.HighContrast ? (c.HighContrast ? 2f : 1.5f) : 1f) * Math.Max(1f, DpiScale);
        Chrome.DrawRoundedBorder(g, face, border, radius, width);
    }

    protected void PaintFocus(Graphics g)
    {
        if (!ShowFocusVisual) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Chrome.DrawFocusRing(g, new Rectangle(0, 0, Width, Height), Colors, ScaledRadius + 2);
    }

    protected Color FillFor(EnterpriseButtonVariant variant) => CustomColor(_fillColor) ?? FormChrome.VariantColor(Colors, variant);

    protected Color ForeFor(Color fill, bool outline)
    {
        if (!Enabled) return Colors.Disabled;
        if (CustomColor(_textColor) is { } custom) return custom;
        if (outline) return AccentColor;
        return Variant == EnterpriseButtonVariant.Primary && fill == Colors.Primary ? Colors.PrimaryText : FormChrome.TextOn(fill);
    }

    /// <summary>The text color: <paramref name="themed"/> unless the button is disabled or <see cref="TextColor"/> is set.</summary>
    protected Color Fore(Color themed) => !Enabled ? Colors.Disabled : CustomColor(_textColor) ?? themed;

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

    /// <summary>Draws the SVG, glyph or image plus the mnemonic text arranged in <paramref name="area"/>.</summary>
    protected void PaintContent(Graphics g, Rectangle area, Color fore)
    {
        var layout = LayoutContent(area);
        if (layout.Content.IsEmpty) return;
        if (ContentPainting is { } handler)
        {
            var args = new EnterpriseButtonPaintEventArgs(g, area, VisualState, FillFor(Variant), fore);
            handler(this, args);
            if (args.Handled) return;
        }

        if (!layout.Icon.IsEmpty)
        {
            if (_svg is not null) _svg.Draw(g, layout.Icon, fore);
            else if (_glyph is not null) TextRenderer.DrawText(g, _glyph, GlyphFont, layout.Icon, fore, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            else if (ResolveImage() is { } image) DrawImage(g, image, layout.Icon);
        }

        if (layout.Text.IsEmpty) return;
        var rtl = RightToLeft == RightToLeft.Yes;
        Mnemonic.DrawParsed(g, _parsedText, _parsedIndex, BoldFont, layout.Text, fore, ButtonLayoutEngine.HorizontalOnly(_textAlignment, rtl), ShowKeyboardCues && _useMnemonic, _textWrap, rtl);
    }

    private ButtonLayoutEngine.Result LayoutContent(Rectangle area)
    {
        var showText = _displayStyle is ButtonDisplayStyle.ImageAndText or ButtonDisplayStyle.Text;
        var hasText = false;
        if (showText)
        {
            ParseText();
            hasText = _parsedText.Length > 0;
        }

        return ButtonLayoutEngine.Arrange(area, IconSize(), hasText ? _measureText : null, S(6), _layout, _textAlignment, _imageAlignment, RightToLeft == RightToLeft.Yes);
    }

    private void ParseText()
    {
        var raw = DisplayText;
        if (_parsedMnemonic != _useMnemonic || !string.Equals(raw, _parsedRaw, StringComparison.Ordinal))
        {
            _parsedRaw = raw;
            _parsedMnemonic = _useMnemonic;
            (_parsedText, _parsedIndex) = _useMnemonic ? Mnemonic.Parse(raw) : (raw ?? string.Empty, -1);
            _parsedWidth = -1;
        }
    }

    private Size MeasureText(int maxWidth)
    {
        var font = BoldFont;
        if (_textWrap)
        {
            return TextRenderer.MeasureText(_parsedText, font, new Size(Math.Max(1, maxWidth), int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        }

        if (_parsedWidth < 0 || !ReferenceEquals(_parsedFont, font))
        {
            _parsedFont = font;
            _parsedWidth = Chrome.MeasureText(_parsedText, font);
        }

        return new Size(_parsedWidth, font.Height);
    }

    private Font GlyphFont => GlyphFonts.Get(Font.SizeInPoints * 1.25f);

    private Size IconSize()
    {
        if (_displayStyle is ButtonDisplayStyle.Text or ButtonDisplayStyle.None) return Size.Empty;
        var box = _imageSize.IsEmpty ? new Size(S(BaseImageSize), S(BaseImageSize)) : new Size(S(_imageSize.Width), S(_imageSize.Height));
        if (_svg is not null) return Fit(Size.Round(_svg.ViewBox.Size), box);
        if (_glyph is not null)
        {
            var key = _glyph + "|" + Font.SizeInPoints.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (_glyphSizeKey != key)
            {
                _glyphSizeKey = key;
                _glyphSize = TextRenderer.MeasureText(_glyph, GlyphFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            return _glyphSize;
        }

        return ResolveImage() is { } image ? Fit(image.Size, box) : Size.Empty;
    }

    private static Size Fit(Size source, Size box)
    {
        if (source.Width <= 0 || source.Height <= 0) return box;
        var scale = Math.Min(box.Width / (double)source.Width, box.Height / (double)source.Height);
        return new Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    private Image? ResolveImage()
    {
        if (_image is not null) return _image;
        if (_imageList is null) return null;
        if (_listImage is not null) return _listImage;
        if (_imageKey.Length > 0 && _imageList.Images.ContainsKey(_imageKey)) _listImage = _imageList.Images[_imageKey];
        else if (_imageIndex >= 0 && _imageIndex < _imageList.Images.Count) _listImage = _imageList.Images[_imageIndex];
        return _listImage;
    }

    private void DrawImage(Graphics g, Image image, Rectangle target)
    {
        var state = g.Save();
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (Enabled) g.DrawImage(image, target);
        else g.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, s_disabledImage ??= CreateDisabledAttributes());
        g.Restore(state);
    }

    private static ImageAttributes CreateDisabledAttributes()
    {
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[] { new[] { 0.3f, 0.3f, 0.3f, 0, 0 }, new[] { 0.59f, 0.59f, 0.59f, 0, 0 }, new[] { 0.11f, 0.11f, 0.11f, 0, 0 }, new[] { 0, 0, 0, 0.45f, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
        return attributes;
    }

    private void OnImageListChanged(object? sender, EventArgs e)
    {
        ReleaseListImage();
        ContentChanged();
    }

    private void ReleaseListImage()
    {
        _listImage?.Dispose();
        _listImage = null;
    }

    private void Animate(ValueAnimator animator, double target, int milliseconds)
    {
        if (_animateStates) animator.AnimateTo(target, milliseconds);
        else animator.Jump(target);
    }

    private void UpdateVisualState()
    {
        var state = VisualState;
        if (state == _lastState) return;
        _lastState = state;
        VisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BadgeChanged()
    {
        ContentChanged();
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    // ---- commands ----

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!InvokeRequired)
        {
            SyncCommandState();
            return;
        }

        if (!IsHandleCreated) return;
        try { BeginInvoke(new Action(SyncCommandState)); }
        catch (InvalidOperationException) { }
    }

    private void SyncCommandState()
    {
        if (_command is null || IsDisposed) return;
        var allowed = _command.CanExecute(_commandParameter);
        if (Enabled != allowed) Enabled = allowed;
    }

    // ---- tooltips ----

    private void ScheduleToolTip()
    {
        if (!_showToolTips || !Enabled) return;
        if (_tipTimer is null)
        {
            _tipTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _tipTimer.Tick += (_, _) =>
            {
                _tipTimer.Stop();
                ShowToolTipNow();
            };
        }

        _tipTimer.Stop();
        _tipTimer.Start();
    }

    private void ShowToolTipNow()
    {
        if (!_showToolTips || !IsHovered || !Enabled || !IsHandleCreated) return;
        var text = ResolveToolTipText();
        if (text is null || text.Length == 0) return;
        _tip ??= new ThemedToolTip(() => Colors, () => Font);
        _tip.Show(this, text, new Point(0, Height + S(2)));
    }

    private void HideToolTip()
    {
        _tipTimer?.Stop();
        _tip?.Hide();
    }

    /// <summary>The tooltip text that would show now: the handler of <see cref="ToolTipTextNeeded"/> wins over <see cref="ToolTipText"/>, and an image-only button falls back to its text.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public string? ResolveToolTipText()
    {
        var text = _toolTipText;
        if (ToolTipTextNeeded is { } handler)
        {
            var args = new ButtonToolTipTextNeededEventArgs { ToolTipText = text };
            handler(this, args);
            text = args.ToolTipText;
        }

        if (string.IsNullOrEmpty(text) && _displayStyle == ButtonDisplayStyle.Image) text = AccessibleLabel;
        return text;
    }

    // ---- accessibility ----

    internal string? AccessibleLabel
    {
        get
        {
            var (text, _) = _useMnemonic ? Mnemonic.Parse(Text) : (Text, -1);
            return text.Length > 0 ? text : _toolTipText;
        }
    }

    internal string? AccessibleBadge => EffectiveBadge;

    internal string AccessibleDefaultAction => DefaultActionName;

    internal string? AccessibleShortcut
    {
        get
        {
            if (!_useMnemonic) return null;
            var (text, index) = Mnemonic.Parse(Text);
            return index >= 0 && index < text.Length ? "Alt+" + char.ToUpperInvariant(text[index]) : null;
        }
    }

    internal AccessibleStates AccessibleStateFlags() => StateFlags();

    protected override AccessibleObject CreateAccessibilityInstance() => new ButtonAccessibleObject(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_command is not null) _command.CanExecuteChanged -= _canExecuteChanged;
            if (_imageList is not null) _imageList.RecreateHandle -= OnImageListChanged;
            ReleaseListImage();
            _tipTimer?.Stop();
            _tipTimer?.Dispose();
            _tip?.Dispose();
            _hover.Dispose();
            _press.Dispose();
        }

        base.Dispose(disposing);
    }
}
