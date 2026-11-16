using System.ComponentModel;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseFileDialogSelectionEventArgs : EventArgs
{
    public IReadOnlyList<string> Paths { get; }
    public EnterpriseFileDialogSelectionEventArgs(IEnumerable<string> paths) => Paths = paths.ToArray();
}

[DefaultProperty(nameof(Filter))]
[ToolboxItem(true)]
public sealed class EnterpriseFileDialog : Component
{
    private IReadOnlyList<string> _selectedFiles = Array.Empty<string>();

    [Category("Behavior")]
    public string Title { get; set; } = "Select file";

    [Category("Behavior")]
    public string Filter { get; set; } = "All files (*.*)|*.*";

    [Category("Behavior")]
    public string DefaultExtension { get; set; } = string.Empty;

    [Category("Behavior")]
    public string InitialDirectory { get; set; } = string.Empty;

    [Category("Behavior")]
    public bool MultiSelect { get; set; }

    [Category("Behavior")]
    public bool CheckFileExists { get; set; } = true;

    [Category("Behavior")]
    public bool AddExtension { get; set; } = true;

    [Category("Behavior")]
    public bool OverwritePrompt { get; set; } = true;

    [Category("Behavior")]
    public string FolderDescription { get; set; } = "Select a folder";

    [Browsable(false)]
    public string FileName { get; set; } = string.Empty;

    [Browsable(false)]
    public string FolderPath { get; private set; } = string.Empty;

    [Browsable(false)]
    public IReadOnlyList<string> SelectedFiles => _selectedFiles;

    public event EventHandler<EnterpriseFileDialogSelectionEventArgs>? SelectionChanged;

    public DialogResult ShowOpenDialog(IWin32Window? owner = null)
    {
        using var dialog = new OpenFileDialog
        {
            Title = Title,
            Filter = NormalizeFilter(Filter),
            DefaultExt = DefaultExtension,
            InitialDirectory = ExistingDirectory(InitialDirectory),
            FileName = FileName,
            Multiselect = MultiSelect,
            CheckFileExists = CheckFileExists,
            AddExtension = AddExtension,
            RestoreDirectory = true
        };
        if (ShowDialog(dialog, owner) != DialogResult.OK) return DialogResult.Cancel;
        FileName = dialog.FileName;
        _selectedFiles = dialog.FileNames;
        SelectionChanged?.Invoke(this, new EnterpriseFileDialogSelectionEventArgs(_selectedFiles));
        return DialogResult.OK;
    }

    public DialogResult ShowSaveDialog(IWin32Window? owner = null)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Title,
            Filter = NormalizeFilter(Filter),
            DefaultExt = DefaultExtension,
            InitialDirectory = ExistingDirectory(InitialDirectory),
            FileName = FileName,
            AddExtension = AddExtension,
            OverwritePrompt = OverwritePrompt,
            RestoreDirectory = true
        };
        if (ShowDialog(dialog, owner) != DialogResult.OK) return DialogResult.Cancel;
        FileName = dialog.FileName;
        _selectedFiles = new[] { FileName };
        SelectionChanged?.Invoke(this, new EnterpriseFileDialogSelectionEventArgs(_selectedFiles));
        return DialogResult.OK;
    }

    public DialogResult ShowFolderDialog(IWin32Window? owner = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = FolderDescription,
            SelectedPath = ExistingDirectory(InitialDirectory),
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };
        if (ShowDialog(dialog, owner) != DialogResult.OK) return DialogResult.Cancel;
        FolderPath = dialog.SelectedPath;
        _selectedFiles = new[] { FolderPath };
        SelectionChanged?.Invoke(this, new EnterpriseFileDialogSelectionEventArgs(_selectedFiles));
        return DialogResult.OK;
    }

    private static DialogResult ShowDialog(CommonDialog dialog, IWin32Window? owner) =>
        owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

    private static string ExistingDirectory(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? Path.GetFullPath(path) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private static string NormalizeFilter(string filter) =>
        string.IsNullOrWhiteSpace(filter) ? "All files (*.*)|*.*" : filter;
}