using System.Globalization;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Grids;
using EnterpriseUI.ThemeDesigner;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class ThemePlaygroundView : UserControl
{
    private readonly ThemeDesignerEngine _designer = new();
    private readonly DataGridView _tokens;
    private readonly ComboBox _themes;
    private readonly TextBox _source;
    private readonly TextBox _json;
    private readonly Label _status;
    private readonly Panel _preview;
    private readonly EnterpriseDataGrid<PreviewRow> _grid;
    private readonly NumericUpDown _density;
    private readonly NumericUpDown _spacing;
    private readonly NumericUpDown _fontSize;
    private readonly System.Windows.Forms.Timer _debounce;
    private bool _loading;
    private Font? _previewFont;
    private readonly ITheme _originalTheme;

    public ThemePlaygroundView(DemoItem metadata)
    {
        _ = metadata;
        Dock = DockStyle.Fill;
        _originalTheme = ThemeManager.Instance.CurrentTheme;
        _designer.LoadTheme(ThemeBuilder.Create("Playground").BasedOn(_originalTheme).Build());
        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true };
        _themes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _themes.Items.AddRange(ThemeManager.Instance.AvailableThemes.Select(theme => (object)theme.Name).ToArray());
        _themes.SelectedItem = _originalTheme.Name;
        _themes.SelectedIndexChanged += (_, _) =>
        {
            if (_themes.SelectedItem is string name)
            {
                var theme = ThemeManager.Instance.AvailableThemes.Single(theme => theme.Name == name);
                _designer.LoadTheme(ThemeBuilder.Create("Playground").BasedOn(theme).Build());
                LoadTokens(); ApplyPreview();
            }
        };
        toolbar.Items.Add(new ToolStripLabel("Theme"));
        toolbar.Items.Add(new ToolStripControlHost(_themes));
        _density = Number(24, 64, DesignTokens.ControlHeight);
        _spacing = Number(0, 32, DesignTokens.ControlPaddingX);
        _fontSize = Number(8, 24, DesignTokens.FontSizeBase);
        foreach (var (label, control) in new[] { ("Height", _density), ("Spacing", _spacing), ("Font px", _fontSize) })
        {
            toolbar.Items.Add(new ToolStripLabel(label));
            toolbar.Items.Add(new ToolStripControlHost(control));
        }
        void Command(string title, Action action)
        {
            var item = new ToolStripButton(title) { ToolTipText = title };
            item.Click += (_, _) => { try { action(); } catch (Exception exception) { if (_status != null) _status.Text = exception.Message; } };
            toolbar.Items.Add(item);
        }
        Command("Color", PickColor); Command("Font", PickFont); Command("Import", ImportTheme); Command("Export", ExportTheme);
        Command("Reset", () => { _designer.LoadTheme(ThemeBuilder.Create("Playground").BasedOn(_originalTheme).Build()); LoadTokens(); ApplyPreview(); });
        _tokens = new DataGridView
        {
            Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window
        };
        _tokens.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Token", ReadOnly = true, FillWeight = 42 });
        _tokens.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", FillWeight = 58 });
        _tokens.CellEndEdit += (_, _) => { if (!_loading) { _debounce?.Stop(); _debounce?.Start(); } };
        _tokens.DataError += (_, e) => { e.ThrowException = false; };
        _preview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var editors = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, WrapContents = true };
        editors.Controls.Add(new TextBox { Text = "Customer", Width = 140 });
        editors.Controls.Add(new CheckBox { Text = "Active", Checked = true, AutoSize = true });
        editors.Controls.Add(new Button { Text = "Save", Width = 80, ForeColor = SystemColors.ControlText });
        _grid = new EnterpriseDataGrid<PreviewRow> { Dock = DockStyle.Fill, ShowGroupPanel = false, ShowSearchPanel = false, EnableMasterDetail = false, AriaLabel = "Theme preview", ShowSummaryFooter = false };
        _grid.Columns.Add(new GridColumn { Field = nameof(PreviewRow.Name), Title = "Customer", Width = 200 });
        _grid.Columns.Add(new GridColumn { Field = nameof(PreviewRow.Status), Title = "Status", Width = 120 });
        _grid.Columns.Add(new GridColumn { Field = nameof(PreviewRow.Amount), Title = "Amount", Width = 120, FormatString = "C2" });
        _grid.DataSource = new[] { new PreviewRow("Contoso", "Active", 1250m), new PreviewRow("Fabrikam", "Pending", 2840m), new PreviewRow("Northwind", "Complete", 930m) };
        _preview.Controls.Add(_grid); _preview.Controls.Add(editors);
        _source = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f) };
        _json = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = _source.Font };
        var output = new TabControl { Dock = DockStyle.Fill };
        var csharp = new TabPage("C# Source"); csharp.Controls.Add(_source);
        var json = new TabPage("Theme JSON"); json.Controls.Add(_json);
        output.TabPages.Add(csharp); output.TabPages.Add(json);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        right.Controls.Add(_preview, 0, 0); right.Controls.Add(output, 0, 1);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        body.Controls.Add(_tokens, 0, 0); body.Controls.Add(right, 1, 0);
        _status = new Label { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        Controls.Add(body); Controls.Add(_status); Controls.Add(toolbar);
        _debounce = new System.Windows.Forms.Timer { Interval = 180 };
        _debounce.Tick += (_, _) => { _debounce.Stop(); CommitTokenEdits(); };
        LoadTokens(); ApplyPreview();
    }

    private NumericUpDown Number(decimal minimum, decimal maximum, string token)
    {
        var input = new NumericUpDown { Minimum = minimum, Maximum = maximum, Width = 58 };
        input.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _designer.UpdateToken(token, input.Value.ToString(CultureInfo.InvariantCulture) + "px");
            LoadTokens(); ApplyPreview();
        };
        return input;
    }

    private void LoadTokens()
    {
        _loading = true;
        try
        {
            _tokens.Rows.Clear();
            foreach (var (key, value) in _designer.ActiveTheme.DesignTokens.OrderBy(token => token.Key)) _tokens.Rows.Add(key, value);
            void SetNumber(NumericUpDown input, string token)
            {
                if (decimal.TryParse(_designer.ActiveTheme.GetToken(token).Replace("px", "", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    input.Value = Math.Clamp(value, input.Minimum, input.Maximum);
            }
            SetNumber(_density, DesignTokens.ControlHeight); SetNumber(_spacing, DesignTokens.ControlPaddingX); SetNumber(_fontSize, DesignTokens.FontSizeBase);
        }
        finally { _loading = false; }
    }

    private bool CommitTokenEdits()
    {
        try
        {
            var document = ThemeDocument.FromTheme(_designer.ActiveTheme);
            foreach (DataGridViewRow row in _tokens.Rows) document.Tokens[Convert.ToString(row.Cells[0].Value)!] = Convert.ToString(row.Cells[1].Value) ?? "";
            _designer.LoadTheme(document.Build());
            LoadTokens();
            ApplyPreview();
            return true;
        }
        catch (Exception exception) { _status.Text = exception.Message; return false; }
    }

    private void ApplyPreview()
    {
        var theme = _designer.ActiveTheme;
        ThemeManager.Instance.ApplyTheme(theme);
        var palette = GridPalette.FromTheme(theme);
        _preview.BackColor = ColorTranslator.FromHtml(palette.Surface);
        _preview.ForeColor = ColorTranslator.FromHtml(palette.Text);
        _preview.Padding = new Padding((int)_spacing.Value);
        var family = theme.GetToken(DesignTokens.FontFamily, "Segoe UI").Split(',')[0].Trim().Trim('\'', '"');
        var previousFont = _previewFont;
        _previewFont = new Font(family, (float)_fontSize.Value * 72f / 96f);
        _preview.Font = _grid.Font = _previewFont;
        _grid.ItemHeight = (double)_density.Value;
        foreach (Control row in _preview.Controls)
            if (row is FlowLayoutPanel panel)
                foreach (Control input in panel.Controls)
                {
                    input.Font = _previewFont;
                    input.Height = (int)_density.Value;
                    if (input is Button button) { button.UseVisualStyleBackColor = true; button.ForeColor = SystemColors.ControlText; }
                    else { input.BackColor = _preview.BackColor; input.ForeColor = _preview.ForeColor; }
                }
        previousFont?.Dispose();
        var export = _designer.Export();
        _source.Text = export.CSharpBuilderCode;
        _json.Text = export.DocumentJson;
        _status.Text = $"{theme.Name} | {theme.DesignTokens.Count} tokens | {theme.Scheme}";
    }

    private void PickColor()
    {
        var row = _tokens.CurrentRow;
        if (row == null) return;
        using var dialog = new ColorDialog { FullOpen = true };
        if (GridPalette.TryParse(Convert.ToString(row.Cells[1].Value) ?? "", "#FFFFFF", out var hex)) dialog.Color = ColorTranslator.FromHtml(hex);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        row.Cells[1].Value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        CommitTokenEdits();
    }

    private void PickFont()
    {
        using var dialog = new FontDialog { Font = _previewFont ?? Font };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        _designer.UpdateToken(DesignTokens.FontFamily, dialog.Font.FontFamily.Name);
        _designer.UpdateToken(DesignTokens.FontSizeBase, (dialog.Font.SizeInPoints * 96f / 72f).ToString(CultureInfo.InvariantCulture) + "px");
        LoadTokens(); ApplyPreview();
    }

    private void ImportTheme()
    {
        using var dialog = new OpenFileDialog { Filter = "Theme JSON|*.json" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        _designer.Import(File.ReadAllText(dialog.FileName), name => ThemeManager.Instance.AvailableThemes.FirstOrDefault(theme => theme.Name == name));
        LoadTokens(); ApplyPreview();
    }

    private void ExportTheme()
    {
        _tokens.EndEdit();
        if (!CommitTokenEdits()) return;
        using var dialog = new SaveFileDialog { Filter = "Theme JSON|*.json", FileName = "enterpriseui-theme.json" };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) File.WriteAllText(dialog.FileName, _designer.Export().DocumentJson);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _debounce?.Dispose(); _previewFont?.Dispose(); }
        base.Dispose(disposing);
    }

    public sealed record PreviewRow(string Name, string Status, decimal Amount);

    internal async Task VerifyAsync(string directory)
    {
        await _grid.RefreshDataAsync();
        if (_grid.TotalRecordCount != 3) throw new InvalidOperationException("Theme preview must show three rows.");
        var density = _tokens.Rows.Cast<DataGridViewRow>().Single(row => Equals(row.Cells[0].Value, DesignTokens.ControlHeight));
        density.Cells[1].Value = "42px";
        if (!CommitTokenEdits() || _grid.ItemHeight != 42) throw new InvalidOperationException("Token edits must update preview density.");
        var valid = _designer.ActiveTheme;
        var primary = _tokens.Rows.Cast<DataGridViewRow>().Single(row => Equals(row.Cells[0].Value, DesignTokens.Primary));
        primary.Cells[1].Value = "#000000; injected";
        if (CommitTokenEdits() || !ReferenceEquals(valid, _designer.ActiveTheme)) throw new InvalidOperationException("Invalid edits must preserve the preview.");
        LoadTokens();
        ApplyPreview();
        foreach (var size in new[] { new Size(1500, 850), new Size(900, 650) })
        {
            Size = size;
            if (FindForm() is { } form) { form.ClientSize = size; form.PerformLayout(); }
            PerformLayout();
            using var image = new Bitmap(Width, Height);
            DrawToBitmap(image, ClientRectangle);
            image.Save(Path.Combine(directory, $"theme-playground-{Width}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        foreach (var name in _themes.Items.Cast<string>().ToArray())
        {
            _themes.SelectedItem = name;
            await _grid.RefreshDataAsync();
            var input = _preview.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<TextBox>().Single();
            if (input.BackColor == input.ForeColor || _grid.TotalRecordCount != 3)
                throw new InvalidOperationException($"Unreadable or empty preview: {name}.");
            using var image = new Bitmap(Width, Height);
            DrawToBitmap(image, ClientRectangle);
            if (name is "Studio-Dark" or "HighContrast-Black")
                image.Save(Path.Combine(directory, $"theme-playground-{name}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}