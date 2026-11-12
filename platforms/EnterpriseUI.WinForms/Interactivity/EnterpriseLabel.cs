using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum LabelTextRotation { None = 0, Rotate90 = 90, Rotate180 = 180, Rotate270 = 270 }

/// <summary>Label with auto-ellipsis, access keys, text rotation, Markdown or HTML inline formatting with clickable links, and image alignment.</summary>
public class EnterpriseLabelControl : Control, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private readonly ThemedToolTip _tip;
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
    private Color _fore = Color.Empty;
    private LabelMarkupMode _mode = LabelMarkupMode.Plain;
    private ContentAlignment _textAlign = ContentAlignment.TopLeft;
    private ContentAlignment _imageAlign = ContentAlignment.MiddleLeft;
    private TextImageRelation _relation = TextImageRelation.Overlay;
    private LabelTextRotation _rotation;
    private Image? _image;
    private bool _wordWrap = true;
    private bool _autoEllipsis = true;
    private bool _useMnemonic = true;
    private bool _tipShown;
    private int _maxLines;
    private int _linkFocus = -1;
    private Control? _labelFor;
    private MarkupLayout? _layout;
    private Rectangle _textArea;
    private bool _truncated;

    public EnterpriseLabelControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 9f);
        Size = new Size(200, 24);
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        _tip = new ThemedToolTip(() => _colors, () => Font);
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public string? ThemeOverride { get; set; }

    public void OnThemeChanged(ITheme newTheme)
    {
        _colors = ControlColors.FromTheme(newTheme);
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(InvalidateLayoutAndPaint)); } catch (InvalidOperationException) { }
            return;
        }

        InvalidateLayoutAndPaint();
    }

    private void InvalidateLayoutAndPaint()
    {
        DropLayout();
        Invalidate();
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    [DefaultValue("")]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value ?? string.Empty;
    }

    [DefaultValue(LabelMarkupMode.Plain)]
    public LabelMarkupMode MarkupMode
    {
        get => _mode;
        set { _mode = value; Refresh_(); }
    }

    [DefaultValue(ContentAlignment.TopLeft)]
    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set { _textAlign = value; Refresh_(); }
    }

    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment ImageAlign
    {
        get => _imageAlign;
        set { _imageAlign = value; Refresh_(); }
    }

    [DefaultValue(TextImageRelation.Overlay)]
    public TextImageRelation TextImageRelation
    {
        get => _relation;
        set { _relation = value; Refresh_(); }
    }

    [DefaultValue(null)]
    public Image? Image
    {
        get => _image;
        set { _image = value; Refresh_(); }
    }

    [DefaultValue(LabelTextRotation.None)]
    public LabelTextRotation TextRotation
    {
        get => _rotation;
        set { _rotation = value; Refresh_(); }
    }

    [DefaultValue(true)]
    public bool WordWrap
    {
        get => _wordWrap;
        set { _wordWrap = value; Refresh_(); }
    }

    /// <summary>Shortens text that does not fit with "…" and shows the full text as a tooltip.</summary>
    [DefaultValue(true)]
    public bool AutoEllipsis
    {
        get => _autoEllipsis;
        set { _autoEllipsis = value; Refresh_(); }
    }

    /// <summary>0 means as many lines as fit.</summary>
    [DefaultValue(0)]
    public int MaxLines
    {
        get => _maxLines;
        set { _maxLines = Math.Max(0, value); Refresh_(); }
    }

    /// <summary>"&amp;File" makes Alt+F focus <see cref="LabelFor"/>; only applies to plain text.</summary>
    [DefaultValue(true)]
    public bool UseMnemonic
    {
        get => _useMnemonic;
        set { _useMnemonic = value; Refresh_(); }
    }

    /// <summary>Control that receives focus when the access key is pressed.</summary>
    [DefaultValue(null)]
    public Control? LabelFor
    {
        get => _labelFor;
        set => _labelFor = value;
    }

    [DefaultValue(true)]
    public bool ShowTooltipWhenTruncated { get; set; } = true;

    [Browsable(true)]
    [DefaultValue(false)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set
        {
            base.AutoSize = value;
            if (value) AdjustSize();
        }
    }

    [Browsable(false)]
    public bool IsTruncated => _truncated;

    /// <summary>Text without markup, as read by assistive technology.</summary>
    [Browsable(false)]
    public string PlainText => LabelMarkup.ToPlainText(BuildRuns(false));

    public override Color ForeColor
    {
        get => _fore.IsEmpty ? _colors.Text : _fore;
        set { _fore = value; Invalidate(); }
    }

    private bool ShouldSerializeForeColor() => !_fore.IsEmpty;
    public override void ResetForeColor() => ForeColor = Color.Empty;

    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    private void Refresh_()
    {
        DropLayout();
        if (AutoSize) AdjustSize();
        Invalidate();
    }

    private void DropLayout()
    {
        _layout?.Dispose();
        _layout = null;
    }

    private void AdjustSize()
    {
        var preferred = GetPreferredSize(_wordWrap ? new Size(Width, 0) : Size.Empty);
        if (preferred != Size) Size = preferred;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Refresh_();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Refresh_();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        DropLayout();
    }

    private bool IsRotatedSideways => _rotation is LabelTextRotation.Rotate90 or LabelTextRotation.Rotate270;

    private IReadOnlyList<MarkupRun> BuildRuns(bool underlineMnemonic)
    {
        if (_mode != LabelMarkupMode.Plain) return LabelMarkup.Parse(Text, _mode);
        if (!_useMnemonic) return LabelMarkup.Parse(Text, LabelMarkupMode.Plain);

        var (display, index) = Mnemonic.Parse(Text);
        var runs = LabelMarkup.Parse(display, LabelMarkupMode.Plain);
        return index < 0 || !underlineMnemonic ? runs : Underline(runs, index);
    }

    private static IReadOnlyList<MarkupRun> Underline(IReadOnlyList<MarkupRun> runs, int index)
    {
        var result = new List<MarkupRun>();
        var offset = 0;
        foreach (var run in runs)
        {
            var length = run.LineBreak ? 1 : run.Text.Length;
            if (!run.LineBreak && index >= offset && index < offset + length)
            {
                var local = index - offset;
                if (local > 0) result.Add(run with { Text = run.Text[..local] });
                result.Add(run with { Text = run.Text.Substring(local, 1), Underline = true });
                if (local + 1 < run.Text.Length) result.Add(run with { Text = run.Text[(local + 1)..] });
            }
            else result.Add(run);

            offset += length;
        }

        return result;
    }

    private Rectangle ContentArea => new(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));

    /// <summary>Splits the content area into image and text rectangles according to <see cref="TextImageRelation"/>.</summary>
    private (Rectangle Text, Rectangle Image) Arrange(Rectangle area, Size imageSize)
    {
        if (_image == null) return (area, Rectangle.Empty);
        var gap = _relation == TextImageRelation.Overlay ? 0 : 6;
        Rectangle Place(Rectangle within) => Align(within, imageSize, _imageAlign);

        switch (_relation)
        {
            case TextImageRelation.ImageBeforeText:
            {
                var strip = new Rectangle(area.X, area.Y, Math.Min(area.Width, imageSize.Width), area.Height);
                return (new Rectangle(strip.Right + gap, area.Y, Math.Max(0, area.Width - imageSize.Width - gap), area.Height), Place(strip));
            }
            case TextImageRelation.TextBeforeImage:
            {
                var strip = new Rectangle(area.Right - Math.Min(area.Width, imageSize.Width), area.Y, Math.Min(area.Width, imageSize.Width), area.Height);
                return (new Rectangle(area.X, area.Y, Math.Max(0, area.Width - imageSize.Width - gap), area.Height), Place(strip));
            }
            case TextImageRelation.ImageAboveText:
            {
                var strip = new Rectangle(area.X, area.Y, area.Width, Math.Min(area.Height, imageSize.Height));
                return (new Rectangle(area.X, strip.Bottom + gap, area.Width, Math.Max(0, area.Height - imageSize.Height - gap)), Place(strip));
            }
            case TextImageRelation.TextAboveImage:
            {
                var strip = new Rectangle(area.X, area.Bottom - Math.Min(area.Height, imageSize.Height), area.Width, Math.Min(area.Height, imageSize.Height));
                return (new Rectangle(area.X, area.Y, area.Width, Math.Max(0, area.Height - imageSize.Height - gap)), Place(strip));
            }
            default: return (area, Place(area));
        }
    }

    private static Rectangle Align(Rectangle within, Size size, ContentAlignment align)
    {
        var x = align is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft ? within.X
            : align is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? within.Right - size.Width : within.X + (within.Width - size.Width) / 2;
        var y = align is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight ? within.Y
            : align is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight ? within.Bottom - size.Height : within.Y + (within.Height - size.Height) / 2;
        return new Rectangle(x, y, size.Width, size.Height);
    }

    private static HorizontalAlignment HorizontalOf(ContentAlignment a) => a is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter ? HorizontalAlignment.Center
        : a is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    private Size ImageDrawSize => _image == null ? Size.Empty : _image.Size;

    private MarkupLayout BuildLayout(int width, bool underline)
    {
        var maxLines = _maxLines;
        return MarkupLayout.Create(BuildRuns(underline), Font, _colors, Math.Max(1, width), _wordWrap, HorizontalOf(_textAlign), maxLines, _autoEllipsis, ForeColor);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var pad = Padding.Size;
        var width = proposedSize.Width > 0 && _wordWrap ? proposedSize.Width - pad.Width : 10000;
        var imageSize = ImageDrawSize;
        if (_relation is TextImageRelation.ImageBeforeText or TextImageRelation.TextBeforeImage) width -= imageSize.Width + 6;
        using var layout = BuildLayout(Math.Max(1, width), false);
        var text = layout.Size;
        if (IsRotatedSideways) text = new Size(text.Height, text.Width);
        var total = text;
        if (_image != null)
        {
            total = _relation switch
            {
                TextImageRelation.ImageBeforeText or TextImageRelation.TextBeforeImage => new Size(text.Width + imageSize.Width + 6, Math.Max(text.Height, imageSize.Height)),
                TextImageRelation.ImageAboveText or TextImageRelation.TextAboveImage => new Size(Math.Max(text.Width, imageSize.Width), text.Height + imageSize.Height + 6),
                _ => new Size(Math.Max(text.Width, imageSize.Width), Math.Max(text.Height, imageSize.Height))
            };
        }

        return new Size(total.Width + pad.Width, total.Height + pad.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var area = ContentArea;
        var imageRect = Rectangle.Empty;
        (_textArea, imageRect) = Arrange(area, ImageDrawSize);
        var underline = ShowKeyboardCues && _mode == LabelMarkupMode.Plain && _useMnemonic;
        if (_image != null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Enabled) g.DrawImage(_image, imageRect);
            else ControlPaint.DrawImageDisabled(g, _image, imageRect.X, imageRect.Y, BackColor.A == 0 ? (Parent?.BackColor ?? _colors.Surface) : BackColor);
        }

        if (_textArea.Width <= 0 || _textArea.Height <= 0 || Text.Length == 0)
        {
            _truncated = false;
            return;
        }

        var sideways = IsRotatedSideways;
        var logical = sideways ? new Size(_textArea.Height, _textArea.Width) : _textArea.Size;
        DropLayout();
        _layout = BuildLayout(logical.Width, underline);
        _truncated = _layout.Truncated || _layout.Size.Height > logical.Height;

        var origin = TextOrigin(_layout.Size, logical);
        var color = Enabled ? ForeColor : _colors.Disabled;
        if (_rotation == LabelTextRotation.None)
        {
            var state = g.Save();
            g.SetClip(_textArea, CombineMode.Intersect);
            _layout.Draw(g, new Point(_textArea.X + origin.X, _textArea.Y + origin.Y), Enabled ? null : color);
            g.Restore(state);
        }
        else DrawRotated(g, logical, origin, color);
    }

    private Point TextOrigin(Size content, Size logical)
    {
        var vertical = _textAlign is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight ? 0
            : _textAlign is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight ? logical.Height - content.Height : (logical.Height - content.Height) / 2;
        return new Point(0, Math.Max(0, vertical));
    }

    private void DrawRotated(Graphics g, Size logical, Point origin, Color color)
    {
        var back = BackColor.A == 255 ? BackColor : Parent?.BackColor ?? _colors.Surface;
        using var bitmap = new Bitmap(Math.Max(1, logical.Width), Math.Max(1, logical.Height));
        using (var bg = Graphics.FromImage(bitmap))
        {
            bg.Clear(back);
            _layout!.Draw(bg, origin, Enabled ? null : color);
        }

        bitmap.RotateFlip(_rotation switch { LabelTextRotation.Rotate90 => RotateFlipType.Rotate90FlipNone, LabelTextRotation.Rotate180 => RotateFlipType.Rotate180FlipNone, _ => RotateFlipType.Rotate270FlipNone });
        g.DrawImageUnscaled(bitmap, _textArea.Location);
    }

    private string? LinkAtPoint(Point p) => _layout == null || _rotation != LabelTextRotation.None ? null : _layout.LinkAt(p, new Point(_textArea.X + TextOrigin(_layout.Size, _textArea.Size).X, _textArea.Y + TextOrigin(_layout.Size, _textArea.Size).Y));

    private string[] Links => _layout?.Pieces.Where(p => p.Href != null).Select(p => p.Href!).Distinct().ToArray() ?? Array.Empty<string>();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hasLink = LinkAtPoint(e.Location) != null;
        Cursor = hasLink ? Cursors.Hand : Cursors.Default;
        if (ShowTooltipWhenTruncated && _truncated && !_tipShown && !hasLink)
        {
            _tipShown = true;
            _tip.Show(this, PlainText, new Point(e.X, e.Y + S(20)));
        }
    }

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _tipShown = false;
        _tip.Hide();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || LinkAtPoint(e.Location) is not { } href) return;
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(href));
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Enter && _linkFocus >= 0 || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Enter || _linkFocus < 0 || _linkFocus >= Links.Length) return;
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(Links[_linkFocus]));
        e.Handled = true;
    }

    protected override bool ProcessMnemonic(char charCode)
    {
        if (!_useMnemonic || _mode != LabelMarkupMode.Plain || !IsMnemonic(charCode, Text)) return false;
        var target = _labelFor ?? Parent?.GetNextControl(this, true);
        if (target == null || !target.CanFocus) return false;
        target.Focus();
        return true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () => null, null, null, null, () => PlainText);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            DropLayout();
            _tip.Dispose();
        }

        base.Dispose(disposing);
    }
}
