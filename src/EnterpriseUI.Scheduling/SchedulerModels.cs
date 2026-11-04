using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Scheduling;

public enum SchedulerViewType
{
    Day,
    Week,
    WorkWeek,
    Month,
    Timeline,
    Agenda
}

public class Appointment : ObservableObject
{
    private DateTime _start;
    private DateTime _end;
    private string _subject = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Subject { get => _subject; set => SetProperty(ref _subject, value); }
    public string? Description { get; set; }
    public DateTime Start { get => _start; set => SetProperty(ref _start, value); }
    public DateTime End { get => _end; set => SetProperty(ref _end, value); }
    public bool IsAllDay { get; set; }
    public string? RecurrenceRule { get; set; } // RFC 5545 compliant (e.g., FREQ=WEEKLY;BYDAY=MO,WE,FR)
    public string? RecurrenceException { get; set; }
    public string? Location { get; set; }
    public string? ResourceId { get; set; }
    public string? Color { get; set; }
}

public class SchedulerResource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078d4";
}

/// <summary>
/// Superset Scheduler and Calendar combining Telerik Scheduler and DevExpress SchedulerControl.
/// </summary>
public class Scheduler : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private DateTime _selectedDate = DateTime.Today;
    private SchedulerViewType _activeView = SchedulerViewType.Week;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public DateTime SelectedDate { get => _selectedDate; set => SetProperty(ref _selectedDate, value); }
    public SchedulerViewType ActiveView { get => _activeView; set => SetProperty(ref _activeView, value); }
    public List<Appointment> Appointments { get; } = new();
    public List<SchedulerResource> Resources { get; } = new();
    public TimeSpan DayStartTime { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan DayEndTime { get; set; } = TimeSpan.FromHours(18);
    public int TimeSlotMinutes { get; set; } = 30;

    public AriaRole Role { get; set; } = AriaRole.Region;
    public string? AriaLabel { get; set; } = "Schedule Calendar";
    public string? AriaDescription { get; set; }
    public string? AriaLabelledBy { get; set; }
    public string? AriaDescribedBy { get; set; }
    public bool AriaHidden { get; set; }
    public bool AriaExpanded { get; set; }
    public bool AriaDisabled { get; set; }
    public bool AriaRequired { get; set; }
    public bool AriaInvalid { get; set; }
    public string? ThemeOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void OnThemeChanged(ITheme newTheme) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

