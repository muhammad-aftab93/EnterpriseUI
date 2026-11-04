using EnterpriseUI.Abstractions.Accessibility;
using EnterpriseUI.Abstractions.Controls;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Core.Mvvm;

namespace EnterpriseUI.Notifications;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ToastNotification(
    string Id,
    string Title,
    string Message,
    NotificationType Type,
    DateTime Timestamp,
    int DurationMs = 4000);

/// <summary>
/// Enterprise Toast, Banner, and Notification Center provider.
/// </summary>
public sealed class ToastService : ObservableObject
{
    private static readonly Lazy<ToastService> _instance = new(() => new ToastService());
    public static ToastService Instance => _instance.Value;

    public event Action<ToastNotification>? ToastShown;
    public List<ToastNotification> NotificationHistory { get; } = new();

    public void Show(string message, string title = "Notification", NotificationType type = NotificationType.Info, int durationMs = 4000)
    {
        var toast = new ToastNotification(Guid.NewGuid().ToString("N"), title, message, type, DateTime.UtcNow, durationMs);
        NotificationHistory.Add(toast);
        ToastShown?.Invoke(toast);
        OnPropertyChanged(nameof(NotificationHistory));
    }

    public void Success(string message, string title = "Success") => Show(message, title, NotificationType.Success);
    public void Warning(string message, string title = "Warning") => Show(message, title, NotificationType.Warning);
    public void Error(string message, string title = "Error") => Show(message, title, NotificationType.Error);
    public void Info(string message, string title = "Information") => Show(message, title, NotificationType.Info);
}

