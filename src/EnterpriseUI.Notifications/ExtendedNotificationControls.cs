using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Notifications;

public enum WaitingBarStyle
{
    MarqueeContinuous,
    SegmentedBlocks,
    RotatingRing,
    DotsWave
}

/// <summary>
/// Clean-room WaitingBar and BusyIndicator (Telerik RadWaitingBar / RadOverlayManager equivalent).
/// </summary>
public class WaitingBar : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    private bool _isWaiting;
    private string _waitingText = "Processing enterprise workload...";

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public WaitingBarStyle WaitingStyle { get; set; } = WaitingBarStyle.MarqueeContinuous;
    public bool IsWaiting { get => _isWaiting; set => SetProperty(ref _isWaiting, value); }
    public string WaitingText { get => _waitingText; set => SetProperty(ref _waitingText, value ?? string.Empty); }

    public void StartWaiting() => IsWaiting = true;
    public void StopWaiting() => IsWaiting = false;

    public AriaRole Role { get; set; } = AriaRole.ProgressBar;
    public string? AriaLabel { get => _waitingText; set => _waitingText = value ?? string.Empty; }
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
/// Clean-room DesktopAlert, ProgressBar, BalloonTip, and Notification Badge
/// (Telerik RadDesktopAlert, RadProgressBar, RadBalloonTip, Badge equivalents).
/// </summary>
public class DesktopAlert : ObservableObject, IUIComponent, IThemeableControl, IAccessibleControl
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public object? Tag { get; set; }
    public IUIComponent? Parent { get; set; }
    public IReadOnlyList<IUIComponent> Children => Array.Empty<IUIComponent>();

    public string CaptionText { get; set; } = "System Alert";
    public string ContentText { get; set; } = string.Empty;
    public int AutoCloseDelaySeconds { get; set; } = 6;
    public bool ShowPinButton { get; set; } = true;
    public bool ShowOptionsButton { get; set; } = true;
    public bool IsPinned { get; set; }

    public void ShowAlert()
    {
        ToastService.Instance.Show(ContentText, CaptionText, NotificationType.Info, AutoCloseDelaySeconds * 1000);
    }

    public AriaRole Role { get; set; } = AriaRole.Alert;
    public string? AriaLabel { get => CaptionText; set => CaptionText = value ?? string.Empty; }
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

