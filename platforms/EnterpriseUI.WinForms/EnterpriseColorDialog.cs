using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

[ToolboxItem(false)]
public sealed class EnterpriseColorDialog : Form
{
    private readonly EventHandler<ITheme> _themeHandler;
    private readonly TrackBar _hue = new() { Minimum = 0, Maximum = 359, TickFrequency = 30 };
    private readonly TrackBar _saturation = new() { Minimum = 0, Maximum = 100, TickFrequency = 10 };
    private readonly TrackBar _brightness = new() { Minimum = 0, Maximum = 100, TickFrequency = 10 };
    private readonly TrackBar _alpha = new() { Minimum = 0, Maximum = 255, TickFrequency = 32 };
    private readonly NumericUpDown[] _channels = new NumericUpDown[4];
    private readonly TextBox _hex = new() { CharacterCasing = CharacterCasing.Upper, MaxLength = 9 };
    private readonly Panel _preview = new() { BorderStyle = BorderStyle.FixedSingle };
    private readonly FlowLayoutPanel _palette = new() { AutoScroll = true, Dock = DockStyle.Fill, WrapContents = true };
    private bool _updating;
    private Color _selectedColor = Color.DodgerBlue;

    public EnterpriseColorDialog()
    {
        Text = "Select Color";
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 430);
        MinimumSize = new Size(480, 380);
        Font = new Font("Segoe UI", 10f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        var channels = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, Padding = new Padding(4) };
        channels.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        channels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        channels.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        AddChannel(channels, 0, "H", _hue, 359);
        AddChannel(channels, 1, "S", _saturation, 100);
        AddChannel(channels, 2, "V", _brightness, 100);
        AddChannel(channels, 3, "A", _alpha, 255);
        root.Controls.Add(channels, 0, 0);

        var rgb = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, Padding = new Padding(4) };
        for (var index = 0; index < 4; index++)
        {
            rgb.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            rgb.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _channels[index] = new NumericUpDown { Minimum = 0, Maximum = 255, Dock = DockStyle.Fill };
            rgb.Controls.Add(new Label { Text = "RGBA"[index].ToString(), AutoSize = true, Anchor = AnchorStyles.Left }, index * 2, 0);
            rgb.Controls.Add(_channels[index], index * 2 + 1, 0);
            _channels[index].ValueChanged += (_, _) => UpdateFromChannels();
        }
        root.Controls.Add(rgb, 0, 1);

        var paletteGroup = new GroupBox { Text = "Palette", Dock = DockStyle.Fill, Padding = new Padding(6) };
        paletteGroup.Controls.Add(_palette);
        root.Controls.Add(paletteGroup, 0, 2);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(8, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(new Label { Text = "Preview", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        right.Controls.Add(_preview, 0, 1);
        right.Controls.Add(new Label { Text = "Hexadecimal", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        right.Controls.Add(_hex, 0, 3);
        var pickerButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var screenPick = new Button { Text = "Pick from screen...", AutoSize = true };
        var saveSwatch = new Button { Text = "Add to custom colors", AutoSize = true };
        screenPick.Click += (_, _) => PickScreenColor();
        saveSwatch.Click += (_, _) => AddCustomColor();
        pickerButtons.Controls.Add(screenPick);
        pickerButtons.Controls.Add(saveSwatch);
        right.Controls.Add(pickerButtons, 0, 4);
        root.Controls.Add(right, 1, 0);
        root.SetRowSpan(right, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(76, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(76, 28) };
        actions.Controls.Add(ok);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 4);
        root.SetColumnSpan(actions, 2);
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(root);

        _hue.ValueChanged += (_, _) => UpdateFromHsv();
        _saturation.ValueChanged += (_, _) => UpdateFromHsv();
        _brightness.ValueChanged += (_, _) => UpdateFromHsv();
        _alpha.ValueChanged += (_, _) => UpdateFromHsv();
        _hex.Validated += (_, _) => ApplyHex();
        _hex.KeyDown += (_, args) =>
        {
            if (args.KeyCode != Keys.Enter) return;
            ApplyHex();
            args.Handled = true;
            args.SuppressKeyPress = true;
        };

        AddPaletteColors();
        UpdateEditorsFromColor(_selectedColor);
        ApplyTheme(ThemeManager.Instance.CurrentTheme);
        _themeHandler = (_, theme) => OnThemeChanged(theme);
        ThemeManager.Instance.ThemeChanged += _themeHandler;
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor == value) return;
            _selectedColor = value;
            UpdateEditorsFromColor(value);
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IList<Color> CustomColors { get; } = new List<Color>();
    public string HexValue
    {
        get => $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        set
        {
            if (!TryParseHex(value, out var color)) throw new ArgumentException("Enter a color as #RRGGBB or #AARRGGBB.", nameof(value));
            SelectedColor = color;
        }
    }

    public event EventHandler? ColorChanged;

    public void AddCustomColor()
    {
        if (!CustomColors.Contains(SelectedColor)) CustomColors.Add(SelectedColor);
        AddSwatch(SelectedColor);
    }

    public void PickScreenColor(Point screenPoint)
    {
        using var bitmap = new Bitmap(1, 1);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(screenPoint, Point.Empty, new Size(1, 1));
        SelectedColor = bitmap.GetPixel(0, 0);
    }

    public void PickScreenColor()
    {
        var bounds = SystemInformation.VirtualScreen;
        using var screenshot = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(screenshot)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        using var picker = new ScreenColorPickerForm(bounds, screenshot);
        if (picker.ShowDialog(this) == DialogResult.OK) SelectedColor = picker.SelectedColor;
    }

    private sealed class ScreenColorPickerForm : Form
    {
        public Color SelectedColor { get; private set; }

        public ScreenColorPickerForm(Rectangle bounds, Image screenshot)
        {
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Cursor = Cursors.Cross;
            BackgroundImage = screenshot;
            BackgroundImageLayout = ImageLayout.Stretch;
            KeyPreview = true;
            KeyDown += (_, args) =>
            {
                if (args.KeyCode != Keys.Escape) return;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            MouseClick += (_, args) =>
            {
                SelectedColor = ((Bitmap)BackgroundImage!).GetPixel(args.X, args.Y);
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }

    private void AddChannel(TableLayoutPanel layout, int row, string label, TrackBar slider, int maximum)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        slider.Dock = DockStyle.Fill;
        slider.Maximum = maximum;
        layout.Controls.Add(slider, 1, row);
        if (row == 0) layout.Controls.Add(new Label { Text = "Hue", AutoSize = true, Anchor = AnchorStyles.Left }, 2, row);
        else if (row == 1) layout.Controls.Add(new Label { Text = "Saturation", AutoSize = true, Anchor = AnchorStyles.Left }, 2, row);
        else if (row == 2) layout.Controls.Add(new Label { Text = "Brightness", AutoSize = true, Anchor = AnchorStyles.Left }, 2, row);
        else layout.Controls.Add(new Label { Text = "Opacity", AutoSize = true, Anchor = AnchorStyles.Left }, 2, row);
    }

    private void AddPaletteColors()
    {
        foreach (var color in new[] { Color.Black, Color.White, Color.Gray, Color.Red, Color.Orange, Color.Yellow,
            Color.Green, Color.Cyan, Color.Blue, Color.Purple, Color.Magenta, Color.Brown, Color.Coral, Color.Teal,
            Color.Navy, Color.Olive, Color.Silver, Color.Pink, Color.Gold, Color.DodgerBlue }) AddSwatch(color);
        foreach (var color in CustomColors) AddSwatch(color);
    }

    private void AddSwatch(Color color)
    {
        if (_palette.Controls.Cast<Control>().OfType<Button>().Any(button => button.BackColor.ToArgb() == color.ToArgb())) return;
        var swatch = new Button { BackColor = color, Tag = color, Size = new Size(25, 25), Margin = new Padding(2), FlatStyle = FlatStyle.Flat, AccessibleName = $"Select {color.Name}" };
        swatch.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
        swatch.Click += (_, _) => SelectedColor = color;
        _palette.Controls.Add(swatch);
    }

    private void UpdateFromHsv()
    {
        if (_updating) return;
        var color = FromHsv(_hue.Value, _saturation.Value / 100d, _brightness.Value / 100d, _alpha.Value);
        SetColor(color);
    }

    private void UpdateFromChannels()
    {
        if (_updating) return;
        SetColor(Color.FromArgb((int)_channels[3].Value, (int)_channels[0].Value, (int)_channels[1].Value, (int)_channels[2].Value));
    }

    private void SetColor(Color color)
    {
        _updating = true;
        _selectedColor = color;
        UpdateEditorsFromColor(color);
        _updating = false;
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateEditorsFromColor(Color color)
    {
        _updating = true;
        ToHsv(color, out var hue, out var saturation, out var brightness);
        _hue.Value = Math.Clamp((int)Math.Round(hue), _hue.Minimum, _hue.Maximum);
        _saturation.Value = Math.Clamp((int)Math.Round(saturation * 100), _saturation.Minimum, _saturation.Maximum);
        _brightness.Value = Math.Clamp((int)Math.Round(brightness * 100), _brightness.Minimum, _brightness.Maximum);
        _alpha.Value = color.A;
        _channels[0].Value = color.R;
        _channels[1].Value = color.G;
        _channels[2].Value = color.B;
        _channels[3].Value = color.A;
        _hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _preview.BackColor = color;
        _updating = false;
    }

    private void OnThemeChanged(ITheme theme)
    {
        if (IsDisposed) return;
        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(new Action(() => ApplyTheme(theme))); } catch (InvalidOperationException) { }
            return;
        }

        ApplyTheme(theme);
    }

    private void ApplyTheme(ITheme theme)
    {
        var highContrast = SystemInformation.HighContrast;
        var surface = highContrast ? SystemColors.Window : EnterpriseThemeEngine.GetColor(theme, DesignTokens.Surface, SystemColors.Window);
        var alternate = highContrast ? SystemColors.Window : EnterpriseThemeEngine.GetColor(theme, DesignTokens.SurfaceAlt, SystemColors.Control);
        var text = highContrast ? SystemColors.WindowText : EnterpriseThemeEngine.GetColor(theme, DesignTokens.TextColor, SystemColors.WindowText);
        var border = highContrast ? SystemColors.WindowText : EnterpriseThemeEngine.GetColor(theme, DesignTokens.BorderColor, SystemColors.ControlDark);

        void Apply(Control control)
        {
            if (ReferenceEquals(control, _preview))
            {
                control.ForeColor = text;
                return;
            }

            if (control is Button { Tag: Color swatchColor } swatch)
            {
                swatch.BackColor = swatchColor;
                swatch.ForeColor = FormChrome.TextOn(swatchColor);
                swatch.FlatAppearance.BorderColor = border;
                return;
            }

            control.BackColor = control is TextBox or NumericUpDown ? alternate : surface;
            control.ForeColor = text;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = border;
            }
            foreach (Control child in control.Controls) Apply(child);
        }

        Apply(this);
        _preview.BackColor = SelectedColor;
        Invalidate(true);
    }

    private void ApplyHex()
    {
        if (TryParseHex(_hex.Text, out var color)) SelectedColor = color;
        else UpdateEditorsFromColor(SelectedColor);
    }

    private static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            color = Color.FromArgb(255, (int)(rgb >> 16), (int)((rgb >> 8) & 255), (int)(rgb & 255));
            return true;
        }
        if (hex.Length == 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            color = Color.FromArgb((int)(argb >> 24), (int)((argb >> 16) & 255), (int)((argb >> 8) & 255), (int)(argb & 255));
            return true;
        }
        return false;
    }

    private static Color FromHsv(double hue, double saturation, double value, int alpha)
    {
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var match = value - chroma;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromArgb(alpha, (int)Math.Round((red + match) * 255), (int)Math.Round((green + match) * 255), (int)Math.Round((blue + match) * 255));
    }

    private static void ToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var delta = maximum - minimum;
        hue = delta == 0 ? 0 : maximum == red ? 60 * ((green - blue) / delta % 6)
            : maximum == green ? 60 * ((blue - red) / delta + 2) : 60 * ((red - green) / delta + 4);
        if (hue < 0) hue += 360;
        saturation = maximum == 0 ? 0 : delta / maximum;
        value = maximum;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.Instance.ThemeChanged -= _themeHandler;
        base.Dispose(disposing);
    }
}