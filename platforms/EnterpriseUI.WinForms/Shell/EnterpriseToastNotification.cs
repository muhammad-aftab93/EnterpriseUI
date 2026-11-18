using System.ComponentModel;
using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.Notifications;

namespace EnterpriseUI.WinForms;

/// <summary>Themed toast notifications that stack above the taskbar, with actions, a history and an optional link to the shared <see cref="ToastService"/>.</summary>
[DefaultEvent("Clicked")]
public class EnterpriseToastNotification : Component
{
    private readonly EnterpriseNotifyIcon _host = new();
    private readonly SynchronizationContext _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    private bool _listen;
    private TimeSpan _timeout = TimeSpan.FromSeconds(6);

    public EnterpriseToastNotification()
    {
        _host.Visible = false;
        _host.NotificationClicked += (_, e) => Clicked?.Invoke(this, e);
        _host.ActionInvoked += (_, e) => ActionInvoked?.Invoke(this, e);
        _host.ToastClosed += (_, e) => Closed?.Invoke(this, e);
    }

    public EnterpriseToastNotification(IContainer container) : this() => container.Add(this);

    /// <summary>History and unread count of everything shown through this component.</summary>
    [Browsable(false)]
    public NotificationCenter Center => _host.Center;

    [DefaultValue(ToastPlacement.BottomRight)]
    public ToastPlacement Placement
    {
        get => _host.ToastPlacement;
        set => _host.ToastPlacement = value;
    }

    [DefaultValue(340)]
    public int ToastWidth
    {
        get => _host.ToastWidth;
        set => _host.ToastWidth = value;
    }

    /// <summary>Older toasts are dismissed (they stay in the history) when more are showing.</summary>
    [DefaultValue(3)]
    public int MaxVisibleToasts
    {
        get => _host.MaxVisibleToasts;
        set => _host.MaxVisibleToasts = value;
    }

    /// <summary>How long a toast stays when no timeout is given; <see cref="TimeSpan.Zero"/> keeps it until dismissed.</summary>
    public TimeSpan DefaultTimeout
    {
        get => _timeout;
        set => _timeout = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private bool ShouldSerializeDefaultTimeout() => _timeout != TimeSpan.FromSeconds(6);
    private void ResetDefaultTimeout() => _timeout = TimeSpan.FromSeconds(6);

    /// <summary>Shows a toast for every message sent to <see cref="ToastService.Instance"/>.</summary>
    [DefaultValue(false)]
    public bool ListenToToastService
    {
        get => _listen;
        set
        {
            if (_listen == value) return;
            _listen = value;
            if (value) ToastService.Instance.ToastShown += OnServiceToast;
            else ToastService.Instance.ToastShown -= OnServiceToast;
        }
    }

    [Browsable(false)]
    public int VisibleCount => _host.VisibleToastCount;

    public event EventHandler<NotifyToastEventArgs>? Clicked;
    public event EventHandler<NotifyToastEventArgs>? ActionInvoked;
    public event EventHandler<NotifyToastEventArgs>? Closed;

    /// <summary>Shows a toast; can be called from any thread.</summary>
    public AppNotification Show(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, TimeSpan? timeout = null, params NotificationAction[] actions)
    {
        var notification = new AppNotification { Title = title, Message = message, Severity = severity, Timeout = timeout ?? _timeout };
        notification.Actions.AddRange(actions);
        _ui.Send(_ => _host.Center.Add(notification), null);
        return notification;
    }

    public void CloseAll() => _host.CloseToasts();

    public void MarkAllRead() => _host.MarkAllRead();

    private void OnServiceToast(ToastNotification toast)
    {
        var severity = toast.Type switch
        {
            NotificationType.Success => NotificationSeverity.Success,
            NotificationType.Warning => NotificationSeverity.Warning,
            NotificationType.Error => NotificationSeverity.Error,
            _ => NotificationSeverity.Info
        };
        Show(toast.Title, toast.Message, severity, TimeSpan.FromMilliseconds(toast.DurationMs));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ListenToToastService = false;
            _host.Dispose();
        }

        base.Dispose(disposing);
    }
}
