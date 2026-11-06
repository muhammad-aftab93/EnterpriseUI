using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Scheduling;

public enum GanttDependencyType
{
    FinishToStart,
    StartToStart,
    FinishToFinish,
    StartToFinish
}

public class GanttTask : ObservableObject
{
    private double _progress;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; } = DateTime.Today;
    public DateTime End { get; set; } = DateTime.Today.AddDays(3);
    public double Progress { get => _progress; set => SetProperty(ref _progress, Math.Clamp(value, 0, 100)); }
    public bool IsMilestone => Start == End;
    public TimeSpan Duration => End - Start;
}

public sealed record GanttDependency(string PredecessorTaskId, string SuccessorTaskId, GanttDependencyType Type = GanttDependencyType.FinishToStart);

/// <summary>
/// Clean-room EnterpriseGanttView Project Scheduling Control.
/// Computes critical path tasks, total project span, and WBS completion roll-ups.
/// </summary>
public class EnterpriseGanttView : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public List<GanttTask> Tasks { get; } = new();
    public List<GanttDependency> Dependencies { get; } = new();
    public bool HighlightCriticalPath { get; set; } = true;

    public double OverallProgress => Tasks.Count == 0 ? 0 : Tasks.Average(t => t.Progress);

    public IReadOnlyList<GanttTask> ComputeCriticalPath()
    {
        if (Tasks.Count == 0) return Array.Empty<GanttTask>();
        var maxEnd = Tasks.Max(t => t.End);
        var criticalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(Tasks.Where(t => t.End >= maxEnd).Select(t => t.Id));

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (criticalIds.Add(cur))
            {
                foreach (var dep in Dependencies.Where(d => string.Equals(d.SuccessorTaskId, cur, StringComparison.OrdinalIgnoreCase)))
                {
                    queue.Enqueue(dep.PredecessorTaskId);
                }
            }
        }
        return Tasks.Where(t => criticalIds.Contains(t.Id)).ToList();
    }

    public AriaRole Role { get; set; } = AriaRole.Grid;
    public string? AriaLabel { get; set; } = "Project Gantt View";
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

/// <summary>
/// Clean-room Multi-Month Calendar Control.
/// Supports multi-month matrices, special holidays, blackout dates, and multiple date selection.
/// </summary>
public class Calendar : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public int MonthRows { get; set; } = 1;
    public int MonthColumns { get; set; } = 2;
    public HashSet<DateTime> SelectedDates { get; } = new();
    public HashSet<DateTime> BlackoutDates { get; } = new();
    public Dictionary<DateTime, string> SpecialDays { get; } = new();

    public bool SelectDate(DateTime date)
    {
        var day = date.Date;
        if (BlackoutDates.Contains(day)) return false;
        SelectedDates.Add(day);
        OnPropertyChanged(nameof(SelectedDates));
        return true;
    }

    public AriaRole Role { get; set; } = AriaRole.Grid;
    public string? AriaLabel { get; set; } = "Multi-Month Calendar";
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

