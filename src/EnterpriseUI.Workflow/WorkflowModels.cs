using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Workflow;

public enum StepState
{
    Pending,
    Active,
    Completed,
    Failed,
    Skipped
}

public class WorkflowStep : ObservableObject
{
    private StepState _state = StepState.Pending;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public StepState State { get => _state; set => SetProperty(ref _state, value); }
}

/// <summary>
/// Superset Workflow Process Designer and Execution Engine.
/// </summary>
public class EnterpriseWorkflowEngine : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<WorkflowStep> Steps { get; } = new();
    public int CurrentStepIndex { get; private set; }

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Workflow Designer";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public void AdvanceNextStep()
    {
        if (CurrentStepIndex < Steps.Count)
        {
            Steps[CurrentStepIndex].State = StepState.Completed;
            CurrentStepIndex++;
            if (CurrentStepIndex < Steps.Count)
            {
                Steps[CurrentStepIndex].State = StepState.Active;
            }
            OnPropertyChanged(nameof(Steps));
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

