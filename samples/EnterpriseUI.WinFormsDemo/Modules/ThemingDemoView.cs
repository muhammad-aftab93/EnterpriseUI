using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ThemingDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly ComboBox _cmbThemes;
    private readonly ListView _tokensList;
    private readonly Panel _swatchPanel;

    public ThemingDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };

        var lblSelect = new Label
        {
            Text = "Enterprise Theme Palette:",
            Location = new Point(8, 14),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _cmbThemes = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(185, 10),
            Width = 190
        };
        _cmbThemes.Items.AddRange(new object[]
        {
            "Fluent (Light)",
            "Fluent (Dark)",
            "Material (Light)",
            "Material (Dark)",
            "Office (Light)",
            "Office (Dark)",
            "Windows (Light)",
            "Windows (Dark)",
            "Modern (Light)",
            "Modern (Dark)",
            "High Contrast (Black)",
            "High Contrast (White)"
        });
        _cmbThemes.SelectedIndex = 0;
        _cmbThemes.SelectedIndexChanged += (_, _) => ApplyTheme(_cmbThemes.SelectedIndex);

        var btnCustomColor = new Button
        {
            Text = "🎨 Theme Builder: Pick Custom Brand Color",
            Location = new Point(390, 9),
            Size = new Size(260, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnCustomColor.Click += (_, _) =>
        {
            using var cd = new ColorDialog { FullOpen = true, Color = Color.FromArgb(0, 120, 212) };
            if (cd.ShowDialog() == DialogResult.OK)
            {
                var hex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                var customTheme = ThemeBuilder.Create($"Custom-Brand-{hex}")
                    .BasedOn(ThemeManager.Instance.CurrentTheme.Type)
                    .WithColorScheme(ThemeManager.Instance.CurrentTheme.Scheme)
                    .WithPrimaryColor(hex)
                    .Build();

                ThemeManager.Instance.ApplyTheme(customTheme);
                LoadTokens(customTheme);
                _baseView.LogEvent("CustomThemeBuilt", $"Created and applied custom brand theme with Primary={hex}");
            }
        };

        _swatchPanel = new Panel
        {
            Location = new Point(665, 9),
            Size = new Size(340, 28)
        };

        topPanel.Controls.AddRange(new Control[] { lblSelect, _cmbThemes, btnCustomColor, _swatchPanel });

        _tokensList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9.5f)
        };
        _tokensList.Columns.Add("Design Token Key", 240);
        _tokensList.Columns.Add("Resolved Runtime Value", 260);
        _tokensList.Columns.Add("Token Classification", 180);

        _baseView.DemoContainer.Controls.Add(_tokensList);
        _baseView.DemoContainer.Controls.Add(topPanel);

        LoadTokens(ThemeManager.Instance.CurrentTheme);
    }

    private void ApplyTheme(int index)
    {
        ITheme theme = index switch
        {
            0 => BuiltInThemes.CreateFluent(ColorScheme.Light),
            1 => BuiltInThemes.CreateFluent(ColorScheme.Dark),
            2 => BuiltInThemes.CreateMaterial(ColorScheme.Light),
            3 => BuiltInThemes.CreateMaterial(ColorScheme.Dark),
            4 => BuiltInThemes.CreateOffice(ColorScheme.Light),
            5 => BuiltInThemes.CreateOffice(ColorScheme.Dark),
            6 => BuiltInThemes.CreateWindows(ColorScheme.Light),
            7 => BuiltInThemes.CreateWindows(ColorScheme.Dark),
            8 => BuiltInThemes.CreateModern(ColorScheme.Light),
            9 => BuiltInThemes.CreateModern(ColorScheme.Dark),
            10 => BuiltInThemes.CreateHighContrast(isDark: true),
            11 => BuiltInThemes.CreateHighContrast(isDark: false),
            _ => BuiltInThemes.CreateFluent(ColorScheme.Light)
        };

        ThemeManager.Instance.ApplyTheme(theme);
        LoadTokens(theme);
        _baseView.LogEvent("ThemeSwitched", $"Applied theme: {theme.Name} ({theme.Scheme})");
    }

    private void LoadTokens(ITheme theme)
    {
        _tokensList.Items.Clear();

        var tokens = new (string Name, string Group)[]
        {
            (DesignTokens.Primary, "Color / Accent"),
            (DesignTokens.PrimaryHover, "Color / State"),
            (DesignTokens.PrimaryActive, "Color / State"),
            (DesignTokens.PrimaryText, "Color / Text"),
            (DesignTokens.Secondary, "Color / Accent"),
            (DesignTokens.Success, "Color / Semantic"),
            (DesignTokens.Warning, "Color / Semantic"),
            (DesignTokens.Danger, "Color / Semantic"),
            (DesignTokens.Info, "Color / Semantic"),
            (DesignTokens.Background, "Surface / Canvas"),
            (DesignTokens.Surface, "Surface / Container"),
            (DesignTokens.SurfaceAlt, "Surface / Header"),
            (DesignTokens.BorderColor, "Border / Line"),
            (DesignTokens.TextColor, "Typography / Color"),
            (DesignTokens.TextMuted, "Typography / Color"),
            (DesignTokens.FontFamily, "Typography / Family"),
            (DesignTokens.FontSizeBase, "Typography / Size"),
            (DesignTokens.BorderRadius, "Geometry / Corner"),
            (DesignTokens.ControlHeight, "Layout / Dimension"),
            (DesignTokens.ControlPaddingX, "Layout / Spacing")
        };

        foreach (var tok in tokens)
        {
            var val = theme.GetToken(tok.Name) ?? "inherited";
            _tokensList.Items.Add(new ListViewItem(new[] { tok.Name, val, tok.Group }));
        }

        // Render preview swatches
        _swatchPanel.Controls.Clear();
        string[] swatchTokens = { DesignTokens.Primary, DesignTokens.Success, DesignTokens.Warning, DesignTokens.Danger, DesignTokens.Surface, DesignTokens.Background };
        int x = 0;
        foreach (var st in swatchTokens)
        {
            var color = WinFormsThemeEngine.GetColor(theme, st, Color.Gray);
            var box = new Panel
            {
                Bounds = new Rectangle(x, 2, 48, 24),
                BackColor = color,
                BorderStyle = BorderStyle.FixedSingle
            };
            x += 54;
            _swatchPanel.Controls.Add(box);
        }
    }
}
