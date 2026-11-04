using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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

/// <summary>
/// Custom-painted themed TextBox control (RadTextBox / RadTextBoxControl equivalent)
/// with focus ring, NullText placeholder, prefix icon, clear button (✕), and validation border.
/// </summary>
public class EnterpriseTextBoxControl : UserControl, IThemeableControl
{
    private readonly TextBox _innerTextBox;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private bool _isFocused;
    private bool _isHovered;

    public string? ThemeOverride { get; set; }
    public string PrefixGlyph { get; set; } = "✎";
    public bool ShowClearButton { get; set; } = true;
    public bool HasValidationError { get; set; }

    public string PlaceholderText
    {
        get => _innerTextBox.PlaceholderText;
        set => _innerTextBox.PlaceholderText = value;
    }

    public override string Text
    {
        get => _innerTextBox.Text;
        set
        {
            _innerTextBox.Text = value;
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    public EnterpriseTextBoxControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(280, 32);
        Padding = new Padding(28, 6, 26, 4);

        _innerTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f)
        };
        _innerTextBox.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
        _innerTextBox.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };
        _innerTextBox.TextChanged += (_, e) =>
        {
            Invalidate();
            OnTextChanged(e);
            ValueChanged?.Invoke(this, e);
        };

        Controls.Add(_innerTextBox);
        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        MouseDown += OnSurfaceMouseDown;

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
        OnThemeChanged(_theme);
    }

    private void OnSurfaceMouseDown(object? sender, MouseEventArgs e)
    {
        if (ShowClearButton && !string.IsNullOrEmpty(_innerTextBox.Text) && e.X >= Width - 26)
        {
            _innerTextBox.Clear();
            _innerTextBox.Focus();
            return;
        }
        _innerTextBox.Focus();
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));
        BackColor = surface;
        _innerTextBox.BackColor = surface;
        _innerTextBox.ForeColor = text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var border = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.BorderColor), Color.FromArgb(190, 196, 206));
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));

        g.Clear(surface);

        Color activeBorder = HasValidationError
            ? Color.FromArgb(220, 53, 69)
            : _isFocused
                ? primary
                : _isHovered
                    ? Color.FromArgb(140, primary)
                    : border;

        float penWidth = (_isFocused || HasValidationError) ? 2f : 1.2f;
        using var pen = new Pen(activeBorder, penWidth);
        g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

        // Draw prefix glyph
        using var glyphFont = new Font("Segoe UI Symbol", 9f);
        using var glyphBrush = new SolidBrush(_isFocused ? primary : Color.Gray);
        g.DrawString(PrefixGlyph, glyphFont, glyphBrush, new PointF(6, (Height - 16) / 2f));

        // Draw Clear button (✕) when non-empty
        if (ShowClearButton && !string.IsNullOrEmpty(_innerTextBox.Text))
        {
            using var clearBrush = new SolidBrush(Color.Gray);
            g.DrawString("✕", glyphFont, clearBrush, new PointF(Width - 22, (Height - 16) / 2f));
        }
    }
}

/// <summary>
/// TextBox with embedded Left and Right command buttons (RadButtonTextBox &amp; RadBrowseEditor equivalent).
/// </summary>
public class EnterpriseButtonTextBoxControl : UserControl, IThemeableControl
{
    private readonly TextBox _textBox;
    private readonly Button _leftButton;
    private readonly Button _rightButton;
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;

    public string? ThemeOverride { get; set; }
    public bool EnableFileBrowseOnRightClick { get; set; }

    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value;
    }

    public string LeftButtonText
    {
        get => _leftButton.Text;
        set
        {
            _leftButton.Text = value;
            _leftButton.Visible = !string.IsNullOrEmpty(value);
        }
    }

    public string RightButtonText
    {
        get => _rightButton.Text;
        set => _rightButton.Text = value;
    }

    public event EventHandler? LeftButtonClick;
    public event EventHandler? RightButtonClick;

    public EnterpriseButtonTextBoxControl()
    {
        Size = new Size(300, 32);
        Padding = new Padding(2);
        BorderStyle = BorderStyle.FixedSingle;

        _leftButton = new Button
        {
            Text = "🔍",
            Dock = DockStyle.Left,
            Width = 32,
            FlatStyle = FlatStyle.Flat
        };
        _leftButton.FlatAppearance.BorderSize = 0;
        _leftButton.Click += (_, e) => LeftButtonClick?.Invoke(this, e);

        _rightButton = new Button
        {
            Text = "📂 Browse",
            Dock = DockStyle.Right,
            Width = 76,
            FlatStyle = FlatStyle.Flat
        };
        _rightButton.FlatAppearance.BorderSize = 0;
        _rightButton.Click += (_, e) =>
        {
            if (EnableFileBrowseOnRightClick)
            {
                using var ofd = new OpenFileDialog { Title = "RadBrowseEditor — Select File", Filter = "All Files (*.*)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _textBox.Text = ofd.FileName;
                }
            }
            RightButtonClick?.Invoke(this, e);
        };

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f),
            Text = @"C:\EnterpriseUI\Config\production.json"
        };

        Controls.Add(_textBox);
        Controls.Add(_leftButton);
        Controls.Add(_rightButton);

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
        OnThemeChanged(_theme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var text = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));

        BackColor = surface;
        _textBox.BackColor = surface;
        _textBox.ForeColor = text;
        _leftButton.BackColor = Color.FromArgb(20, primary);
        _leftButton.ForeColor = primary;
        _rightButton.BackColor = primary;
        _rightButton.ForeColor = Color.White;
    }
}

/// <summary>
/// Custom GDI+ themed Button control (RadButton equivalent) with variants, icon glyph, and badge counter.
/// </summary>
public class EnterpriseButtonControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private bool _hovered;
    private bool _pressed;

    public string? ThemeOverride { get; set; }
    public EnterpriseButtonVariant Variant { get; set; } = EnterpriseButtonVariant.Primary;
    public string? BadgeText { get; set; }

    public EnterpriseButtonControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
        Size = new Size(150, 34);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _hovered = false; _pressed = false; Invalidate(); };
        MouseDown += (_, _) => { _pressed = true; Invalidate(); };
        MouseUp += (_, _) => { _pressed = false; Invalidate(); };

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var textToken = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.TextColor), Color.FromArgb(30, 35, 45));

        g.Clear(Parent?.BackColor ?? surface);

        Color fill = Variant switch
        {
            EnterpriseButtonVariant.Primary => primary,
            EnterpriseButtonVariant.Success => Color.FromArgb(16, 124, 65),
            EnterpriseButtonVariant.Danger => Color.FromArgb(216, 59, 1),
            EnterpriseButtonVariant.Secondary => Color.FromArgb(96, 104, 116),
            _ => surface
        };

        if (_pressed) fill = ControlPaint.Dark(fill, 0.12f);
        else if (_hovered && Variant != EnterpriseButtonVariant.Outline) fill = ControlPaint.Light(fill, 0.12f);

        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(Variant == EnterpriseButtonVariant.Outline ? primary : fill, 1.5f);

        g.FillRectangle(fillBrush, rect);
        g.DrawRectangle(borderPen, rect);

        Color fg = Variant == EnterpriseButtonVariant.Outline ? primary : Color.White;
        using var textBrush = new SolidBrush(fg);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, textBrush, rect, sf);

        if (!string.IsNullOrEmpty(BadgeText))
        {
            var badgeRect = new Rectangle(Width - 28, 4, 22, 16);
            using var badgeBrush = new SolidBrush(Color.FromArgb(220, 53, 69));
            using var badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            g.FillEllipse(badgeBrush, badgeRect);
            g.DrawString(BadgeText, badgeFont, Brushes.White, badgeRect, sf);
        }
    }
}

/// <summary>
/// Dual-zone SplitButton and DropDownButton control (RadSplitButton &amp; RadDropDownButton equivalent).
/// </summary>
public class EnterpriseSplitButtonControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private readonly ContextMenuStrip _menu;
    private bool _hovered;

    public string? ThemeOverride { get; set; }
    public bool IsSplitMode { get; set; } = true;
    public event EventHandler? PrimaryActionClicked;
    public event Action<string>? MenuItemSelected;

    public EnterpriseSplitButtonControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(175, 34);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Text = "Export Report";

        _menu = new ContextMenuStrip();
        AddOption("Export as PDF Document (.pdf)");
        AddOption("Export as Excel Workbook (.xlsx)");
        AddOption("Export as Word Document (.docx)");
        AddOption("Export as Compressed Archive (.zip)");

        MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
        MouseDown += OnSplitMouseDown;

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void AddOption(string itemText)
    {
        _menu.Items.Add(itemText, null, (_, _) => MenuItemSelected?.Invoke(itemText));
    }

    private void OnSplitMouseDown(object? sender, MouseEventArgs e)
    {
        if (!IsSplitMode || e.X >= Width - 32)
        {
            _menu.Show(this, new Point(0, Height));
        }
        else
        {
            PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        if (_hovered) primary = ControlPaint.Light(primary, 0.1f);

        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var fillBrush = new SolidBrush(primary);
        g.FillRectangle(fillBrush, rect);

        int splitX = Width - 30;
        if (IsSplitMode)
        {
            using var sepPen = new Pen(Color.FromArgb(140, Color.White), 1f);
            g.DrawLine(sepPen, splitX, 5, splitX, Height - 6);
        }

        var textRect = new Rectangle(4, 0, splitX - 4, Height);
        var arrowRect = new Rectangle(splitX, 0, 30, Height);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, Brushes.White, textRect, sf);
        g.DrawString("▾", Font, Brushes.White, arrowRect, sf);
    }
}

/// <summary>
/// Press-and-hold continuous firing RepeatButton control (RadRepeatButton equivalent).
/// </summary>
public class EnterpriseRepeatButtonControl : Button, IThemeableControl
{
    private readonly System.Windows.Forms.Timer _repeatTimer;
    public string? ThemeOverride { get; set; }
    public int RepeatCount { get; private set; }
    public event Action<int>? RepeatedClick;

    public EnterpriseRepeatButtonControl()
    {
        FlatStyle = FlatStyle.Flat;
        Size = new Size(170, 34);
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Text = "⏩ Hold to Repeat (0)";

        _repeatTimer = new System.Windows.Forms.Timer { Interval = 90 };
        _repeatTimer.Tick += (_, _) => FireTick();

        MouseDown += (_, _) =>
        {
            FireTick();
            _repeatTimer.Start();
        };
        MouseUp += (_, _) => _repeatTimer.Stop();
        MouseLeave += (_, _) => _repeatTimer.Stop();

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    private void FireTick()
    {
        RepeatCount++;
        Text = $"⏩ Hold to Repeat ({RepeatCount})";
        RepeatedClick?.Invoke(RepeatCount);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        var primary = WinFormsColorHelper.ParseHexColor(newTheme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        BackColor = Color.FromArgb(22, primary);
        ForeColor = primary;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repeatTimer?.Stop();
            _repeatTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Animated pill ToggleSwitch control (RadToggleSwitch equivalent) with ON/OFF labels.
/// </summary>
public class EnterpriseToggleSwitchControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private bool _isOn = true;

    public string? ThemeOverride { get; set; }
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

    public EnterpriseToggleSwitchControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(76, 30);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 8f, FontStyle.Bold);

        Click += (_, _) => IsOn = !_isOn;
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));

        g.Clear(Parent?.BackColor ?? surface);

        Color trackColor = _isOn ? primary : Color.FromArgb(150, 156, 166);
        using var trackBrush = new SolidBrush(trackColor);

        int r = Height - 4;
        g.FillEllipse(trackBrush, 2, 2, r, r);
        g.FillEllipse(trackBrush, Width - r - 2, 2, r, r);
        g.FillRectangle(trackBrush, 2 + r / 2, 2, Width - r - 4, r);

        int thumbSize = r - 6;
        int thumbX = _isOn ? Width - thumbSize - 5 : 5;
        using var thumbBrush = new SolidBrush(Color.White);
        g.FillEllipse(thumbBrush, thumbX, 5, thumbSize, thumbSize);

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var labelRect = _isOn ? new Rectangle(6, 2, Width - thumbSize - 10, r) : new Rectangle(thumbSize + 6, 2, Width - thumbSize - 10, r);
        g.DrawString(_isOn ? OnText : OffText, Font, Brushes.White, labelRect, sf);
    }
}

/// <summary>
/// Vector TrackBar and ProgressBar controls (RadTrackBar &amp; RadProgressBar equivalents).
/// </summary>
public class EnterpriseProgressBarControl : Control, IThemeableControl
{
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    private int _value = 68;

    public string? ThemeOverride { get; set; }
    public int Minimum { get; set; } = 0;
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

    public EnterpriseProgressBarControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(280, 26);
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var surface = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Surface), Color.White);
        var primary = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var border = WinFormsColorHelper.ParseHexColor(_theme.GetToken(DesignTokens.BorderColor), Color.FromArgb(200, 206, 214));

        g.Clear(surface);

        var trackRect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var bgBrush = new SolidBrush(Color.FromArgb(235, 238, 244));
        g.FillRectangle(bgBrush, trackRect);

        double pct = Maximum > Minimum ? (double)(_value - Minimum) / (Maximum - Minimum) : 0.0;
        int fillW = (int)(trackRect.Width * pct);
        if (fillW > 0)
        {
            using var fillBrush = new SolidBrush(primary);
            g.FillRectangle(fillBrush, trackRect.Left, trackRect.Top, fillW, trackRect.Height);
        }

        using var borderPen = new Pen(border);
        g.DrawRectangle(borderPen, trackRect);

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var textBrush = new SolidBrush(pct > 0.52 ? Color.White : Color.FromArgb(30, 35, 45));
        g.DrawString($"{pct * 100:F0}% Complete", Font, textBrush, trackRect, sf);
    }
}

/// <summary>
/// Owner-drawn FontDropDownList control (RadFontDropDownList equivalent) rendering each font in its own typeface.
/// </summary>
public class EnterpriseFontDropDownControl : ComboBox
{
    public EnterpriseFontDropDownControl()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 22;
        Width = 240;

        var commonFonts = new[] { "Segoe UI", "Consolas", "Arial", "Calibri", "Cambria", "Georgia", "Tahoma", "Trebuchet MS", "Verdana", "Courier New" };
        foreach (var f in commonFonts)
        {
            Items.Add(f);
        }
        SelectedIndex = 0;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index >= 0 && e.Index < Items.Count)
        {
            string fontName = Items[e.Index]?.ToString() ?? "Segoe UI";
            Font? previewFont = null;
            try
            {
                previewFont = new Font(fontName, 9.5f);
            }
            catch
            {
                previewFont = Font;
            }

            using var brush = new SolidBrush(e.ForeColor);
            e.Graphics.DrawString($"{fontName} (AaBbCc)", previewFont, brush, new PointF(e.Bounds.Left + 4, e.Bounds.Top + 2));
            if (!ReferenceEquals(previewFont, Font)) previewFont.Dispose();
        }
        e.DrawFocusRectangle();
    }
}

/// <summary>
/// Collapsible header container (RadCollapsiblePanel &amp; RadGroupBox equivalent).
/// </summary>
public class EnterpriseCollapsiblePanelControl : Panel, IThemeableControl
{
    private readonly Button _headerButton;
    private readonly Panel _contentHost;
    private bool _isExpanded = true;
    private int _expandedHeight = 140;

    public string? ThemeOverride { get; set; }
    public Panel ContentPanel => _contentHost;

    public string HeaderTitle
    {
        get => _headerButton.Text;
        set => _headerButton.Text = (_isExpanded ? "▼  " : "▶  ") + value;
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
                Height = 34;
            }
            string rawTitle = _headerButton.Text.Length > 3 ? _headerButton.Text.Substring(3) : _headerButton.Text;
            _headerButton.Text = (_isExpanded ? "▼  " : "▶  ") + rawTitle;
            ExpandedChanged?.Invoke(_isExpanded);
        }
    }

    public event Action<bool>? ExpandedChanged;

    public EnterpriseCollapsiblePanelControl()
    {
        Size = new Size(460, 140);
        BorderStyle = BorderStyle.FixedSingle;

        _headerButton = new Button
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "▼  RadCollapsiblePanel — Click Header to Collapse / Expand",
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _headerButton.FlatAppearance.BorderSize = 0;
        _headerButton.Click += (_, _) => IsExpanded = !_isExpanded;

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        Controls.Add(_contentHost);
        Controls.Add(_headerButton);

        ThemeManager.Instance.ThemeChanged += (_, t) => OnThemeChanged(t);
        OnThemeChanged(ThemeManager.Instance.CurrentTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        var primary = WinFormsColorHelper.ParseHexColor(newTheme.GetToken(DesignTokens.Primary), Color.FromArgb(0, 120, 212));
        var surface = WinFormsColorHelper.ParseHexColor(newTheme.GetToken(DesignTokens.Surface), Color.White);
        BackColor = surface;
        _headerButton.BackColor = Color.FromArgb(24, primary);
        _headerButton.ForeColor = primary;
    }
}

