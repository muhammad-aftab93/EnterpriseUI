using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.RichText;

public class TextRun
{
    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string? Color { get; set; }
    public double FontSize { get; set; } = 14;
}

public class ParagraphBlock
{
    public List<TextRun> Runs { get; } = new();
    public string Alignment { get; set; } = "left";
}

public class RichTextDocument
{
    public List<ParagraphBlock> Paragraphs { get; } = new();

    public string ToPlainText()
    {
        return string.Join("\n", Paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))));
    }
}

/// <summary>
/// Superset Rich Text WYSIWYG editor combining DevExpress RichEditControl and Telerik RadRichTextBox.
/// </summary>
public class EnterpriseRichTextEditor : ObservableObject, IInteractiveControl, IThemeableControl, IAccessibleControl
{
    private RichTextDocument _document = new();

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public RichTextDocument Document
    {
        get => _document;
        set => SetProperty(ref _document, value);
    }

    public bool IsFocused { get; private set; }
    public bool IsReadOnly { get; set; }
    public int TabIndex { get; set; }

    public AriaRole Role { get; set; } = AriaRole.TextBox;
    public string? AriaLabel { get; set; } = "Rich Text Editor";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public ValueTask<bool> FocusAsync() => ValueTask.FromResult(true);
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

