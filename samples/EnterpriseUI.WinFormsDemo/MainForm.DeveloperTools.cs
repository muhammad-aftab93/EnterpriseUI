using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Documents;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo;

public partial class MainForm
{
    private void ShowControlSpy() => ShowHierarchyTool(new EnterpriseControlSpy());

    private void ShowElementHierarchyEditor() => ShowHierarchyTool(new EnterpriseElementHierarchyEditor());

    private void ShowHierarchyTool(EnterpriseHierarchyToolForm tool)
    {
        if (_demoHost.Controls.Count == 0)
        {
            tool.Dispose();
            EnterpriseMessageBox.ShowMessage("Load a demo before inspecting its controls.", "Developer Tools", owner: this);
            return;
        }

        using (tool)
        {
            tool.Inspect(_demoHost.Controls[0]);
            tool.ShowDialog(this);
        }
    }

    private void ShowFormConverter()
    {
        using var source = new Form
        {
            Text = "Standard Preview Form",
            ClientSize = new Size(420, 180),
            StartPosition = FormStartPosition.CenterParent
        };
        source.Controls.Add(new Label
        {
            Text = "This child control is preserved during form conversion.",
            AutoSize = true,
            Location = new Point(20, 24)
        });
        var converter = new EnterpriseFormConverter();
        using var converted = converter.ConvertTo(source, typeof(ConvertedPreviewForm));
        converted.ShowDialog(this);
    }

    private void ShowShapeEditor()
    {
        using var window = new Form
        {
            Text = "Shape Editor",
            ClientSize = new Size(560, 420),
            StartPosition = FormStartPosition.CenterParent
        };
        var editor = new EnterpriseShapeEditor { Dock = DockStyle.Fill };
        editor.SetPoints(new[] { new PointF(0.5f, 0.1f), new PointF(0.9f, 0.5f), new PointF(0.5f, 0.9f), new PointF(0.1f, 0.5f) });
        editor.SetSegmentCurved(0, true);
        editor.SetSegmentCurved(2, true);
        window.Controls.Add(editor);
        window.ShowDialog(this);
    }

    private void ShowVisualStyleBuilder()
    {
        var theme = ThemeManager.Instance.CurrentTheme as Theme ?? BuiltInThemes.CreateFluent(ColorScheme.Light);
        using var builder = new EnterpriseVisualStyleBuilder(theme);
        builder.ShowDialog(this);
    }

    private void ShowColorPicker()
    {
        var current = ColorTranslator.FromHtml(ThemeManager.Instance.CurrentTheme.GetToken(DesignTokens.Primary, "#275D73"));
        using var picker = new EnterpriseColorDialog { SelectedColor = current };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _statusDemo.Text = $"Picked color: {picker.HexValue}  |";
    }

    private void ShowImageViewer()
    {
        using var files = new EnterpriseFileDialog
        {
            Title = "Open an image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            DefaultExtension = "png"
        };
        if (files.ShowOpenDialog(this) != DialogResult.OK) return;

        using var window = new Form
        {
            Text = Path.GetFileName(files.FileName),
            ClientSize = new Size(900, 650),
            StartPosition = FormStartPosition.CenterParent
        };
        var viewer = new EnterprisePictureBoxControl { Dock = DockStyle.Fill };
        viewer.LoadImage(files.FileName);
        window.Controls.Add(viewer);
        window.ShowDialog(this);
    }

    private void ShowMessageAndZipDemo()
    {
        const string content = "The archive round-trip completed successfully.";
        var bytes = EnterpriseZipLibrary.CreateTextArchive(new Dictionary<string, string> { ["demo/status.txt"] = content });
        var extracted = EnterpriseZipLibrary.ReadTextEntries(bytes);
        EnterpriseMessageBox.ShowMessage("ZIP library round-trip", "Document Utilities",
            MessageBoxButtons.OK, MessageBoxIcon.Information,
            $"Entry count: {extracted.Count}{Environment.NewLine}Entry content: {extracted["demo/status.txt"]}", this);
    }

    public sealed class ConvertedPreviewForm : Form
    {
        public ConvertedPreviewForm() => Text = "Converted Preview Form";
    }
}