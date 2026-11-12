namespace EnterpriseUI.Editors.Interactivity;

public enum NotificationSeverity { Info, Success, Warning, Error }

public sealed record NotificationAction(string Id, string Text, bool IsPrimary = false);

public sealed class AppNotification
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public bool IsRead { get; internal set; }

    /// <summary>Notifications with the same non-empty group replace each other instead of piling up.</summary>
    public string? Group { get; set; }

    /// <summary>How long the toast stays; zero keeps it until dismissed.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(6);
    public List<NotificationAction> Actions { get; } = new();
    public object? Tag { get; set; }
}

public sealed class NotificationEventArgs : EventArgs
{
    public NotificationEventArgs(AppNotification notification, string? actionId = null)
    {
        Notification = notification;
        ActionId = actionId;
    }

    public AppNotification Notification { get; }
    public string? ActionId { get; }
}

/// <summary>History and unread counter behind a notify icon; the visual layer shows toasts for <see cref="Added"/>.</summary>
public sealed class NotificationCenter
{
    private readonly List<AppNotification> _items = new();

    public IReadOnlyList<AppNotification> Items => _items;
    public int Capacity { get; set; } = 100;
    public int UnreadCount => _items.Count(n => !n.IsRead);

    public event EventHandler<NotificationEventArgs>? Added;
    public event EventHandler<NotificationEventArgs>? Removed;
    public event EventHandler<NotificationEventArgs>? ActionInvoked;
    public event EventHandler? Changed;

    public AppNotification Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, string? group = null, params NotificationAction[] actions)
    {
        var notification = new AppNotification { Title = title, Message = message, Severity = severity, Group = group };
        notification.Actions.AddRange(actions);
        return Add(notification);
    }

    public AppNotification Add(AppNotification notification)
    {
        if (!string.IsNullOrEmpty(notification.Group))
        {
            var existing = _items.FindIndex(n => n.Group == notification.Group);
            if (existing >= 0)
            {
                var old = _items[existing];
                _items.RemoveAt(existing);
                Removed?.Invoke(this, new NotificationEventArgs(old));
            }
        }

        _items.Add(notification);
        while (_items.Count > Math.Max(1, Capacity)) _items.RemoveAt(0);
        Added?.Invoke(this, new NotificationEventArgs(notification));
        Changed?.Invoke(this, EventArgs.Empty);
        return notification;
    }

    public void MarkRead(Guid id)
    {
        var item = _items.Find(n => n.Id == id);
        if (item == null || item.IsRead) return;
        item.IsRead = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAllRead()
    {
        var any = false;
        foreach (var item in _items.Where(n => !n.IsRead))
        {
            item.IsRead = true;
            any = true;
        }

        if (any) Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Dismiss(Guid id)
    {
        var item = _items.Find(n => n.Id == id);
        if (item == null) return false;
        _items.Remove(item);
        Removed?.Invoke(this, new NotificationEventArgs(item));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        var removed = _items.ToArray();
        _items.Clear();
        foreach (var item in removed) Removed?.Invoke(this, new NotificationEventArgs(item));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs an action button: marks the notification read and raises <see cref="ActionInvoked"/>.</summary>
    public void InvokeAction(Guid id, string actionId)
    {
        var item = _items.Find(n => n.Id == id);
        if (item == null || !item.Actions.Any(a => a.Id == actionId)) return;
        MarkRead(id);
        ActionInvoked?.Invoke(this, new NotificationEventArgs(item, actionId));
    }

    /// <summary>Badge text for an icon: empty for 0, the number up to 99, then "99+".</summary>
    public static string BadgeText(int count) => count <= 0 ? string.Empty : count > 99 ? "99+" : count.ToString();
}
