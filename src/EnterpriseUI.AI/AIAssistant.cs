using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Data;

namespace EnterpriseUI.AI;

public sealed record AIChatMessage(string Role, string Content, DateTime Timestamp);

public interface IAIServiceProvider
{
    Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken = default);
    Task<DataSourceRequest> TranslateNaturalLanguageToQueryAsync(string userPrompt, Type entityType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enterprise AI Copilot and Smart Assistant component for UI automation and natural language data intelligence.
/// </summary>
public class AIAssistant : ObservableObject, IUIComponent, IAccessibleControl
{
    private bool _isOpen;
    private string _currentPrompt = string.Empty;
    private bool _isGenerating;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get => _isOpen; set => SetProperty(ref _isOpen, value); }
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<AIChatMessage> History { get; } = new();
    public IAIServiceProvider? ServiceProvider { get; set; }

    public string CurrentPrompt { get => _currentPrompt; set => SetProperty(ref _currentPrompt, value); }
    public bool IsGenerating { get => _isGenerating; private set => SetProperty(ref _isGenerating, value); }

    public AriaRole Role { get; set; } = AriaRole.Dialog;
    public string? AriaLabel { get; set; } = "AI Assistant Copilot";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get => !_isOpen; set => _isOpen = !value; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }

    public async Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;

        History.Add(new AIChatMessage("User", prompt, DateTime.UtcNow));
        CurrentPrompt = string.Empty;
        IsGenerating = true;

        try
        {
            var response = ServiceProvider != null
                ? await ServiceProvider.GenerateCompletionAsync(prompt, cancellationToken)
                : $"AI Suggestion for '{prompt}': Consider applying auto-summary and filtering high-value records.";

            History.Add(new AIChatMessage("Assistant", response, DateTime.UtcNow));
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() => History.Clear();
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

