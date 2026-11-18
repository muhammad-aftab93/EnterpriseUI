using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EnterpriseUI.WinForms;

public enum EnterpriseTaskbarProgressState { None, Indeterminate, Normal, Error, Paused }

/// <summary>A button shown under the taskbar thumbnail of a window.</summary>
public sealed class EnterpriseTaskbarThumbnailButton
{
    private Icon? _icon;
    private string? _toolTip;
    private bool _enabled = true;
    private bool _visible = true;

    public EnterpriseTaskbarThumbnailButton(string id, Icon icon, string? toolTip = null)
    {
        Id = id;
        _icon = icon;
        _toolTip = toolTip;
    }

    internal EnterpriseTaskbarButton? Owner { get; set; }

    public string Id { get; }
    public object? Tag { get; set; }
    public bool DismissOnClick { get; set; }

    public Icon? Icon
    {
        get => _icon;
        set { _icon = value; Owner?.UpdateThumbnails(); }
    }

    public string? ToolTip
    {
        get => _toolTip;
        set { _toolTip = value; Owner?.UpdateThumbnails(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; Owner?.UpdateThumbnails(); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; Owner?.UpdateThumbnails(); }
    }
}

public sealed class EnterpriseTaskbarThumbnailEventArgs : EventArgs
{
    public EnterpriseTaskbarThumbnailEventArgs(EnterpriseTaskbarThumbnailButton button) => Button = button;

    public EnterpriseTaskbarThumbnailButton Button { get; }
}

public sealed class EnterpriseTaskbarThumbnailButtonCollection : Collection<EnterpriseTaskbarThumbnailButton>
{
    public const int MaximumButtons = 7;
    private readonly EnterpriseTaskbarButton _owner;

    internal EnterpriseTaskbarThumbnailButtonCollection(EnterpriseTaskbarButton owner) => _owner = owner;

    protected override void InsertItem(int index, EnterpriseTaskbarThumbnailButton item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Count >= MaximumButtons) throw new InvalidOperationException($"A window supports at most {MaximumButtons} thumbnail buttons.");
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.ResetThumbnails();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.ResetThumbnails();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) item.Owner = null;
        base.ClearItems();
        _owner.ResetThumbnails();
    }

    protected override void SetItem(int index, EnterpriseTaskbarThumbnailButton item)
    {
        this[index].Owner = null;
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.ResetThumbnails();
    }
}

/// <summary>Windows taskbar integration for a form: progress state and value, an overlay badge icon and thumbnail buttons; does nothing on systems without the taskbar interfaces.</summary>
[DefaultEvent("ThumbnailButtonClick")]
public class EnterpriseTaskbarButton : Component
{
    private const int WmCommand = 0x111, ThumbnailClicked = 0x1800, FirstThumbnailId = 100;
    private static readonly int TaskbarButtonCreatedMessage = RegisterWindowMessage("TaskbarButtonCreated");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string name);

    private ITaskbarList3? _taskbar;
    private Hook? _hook;
    private Form? _form;
    private EnterpriseTaskbarProgressState _state;
    private int _value;
    private int _maximum = 100;
    private Icon? _overlay;
    private string _overlayDescription = string.Empty;
    private bool _thumbnailsAdded;

    public EnterpriseTaskbarButton()
    {
        ThumbnailButtons = new EnterpriseTaskbarThumbnailButtonCollection(this);
    }

    public EnterpriseTaskbarButton(IContainer container) : this() => container.Add(this);

    /// <summary>Window whose taskbar button is controlled.</summary>
    [DefaultValue(null)]
    public Form? Form
    {
        get => _form;
        set
        {
            if (_form == value) return;
            Detach();
            _form = value;
            if (value == null) return;
            value.HandleCreated += OnHandleCreated;
            value.HandleDestroyed += OnHandleDestroyed;
            if (value.IsHandleCreated) Attach();
        }
    }

    [DefaultValue(EnterpriseTaskbarProgressState.None)]
    public EnterpriseTaskbarProgressState ProgressState
    {
        get => _state;
        set
        {
            _state = value;
            ApplyProgress();
        }
    }

    [DefaultValue(0)]
    public int ProgressValue
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, _maximum);
            ApplyProgress();
        }
    }

    [DefaultValue(100)]
    public int ProgressMaximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            _value = Math.Min(_value, _maximum);
            ApplyProgress();
        }
    }

    /// <summary>Small badge drawn over the taskbar button; null removes it.</summary>
    [DefaultValue(null)]
    public Icon? OverlayIcon
    {
        get => _overlay;
        set
        {
            _overlay = value;
            ApplyOverlay();
        }
    }

    [DefaultValue("")]
    public string OverlayDescription
    {
        get => _overlayDescription;
        set
        {
            _overlayDescription = value ?? string.Empty;
            ApplyOverlay();
        }
    }

    [Browsable(false)]
    public EnterpriseTaskbarThumbnailButtonCollection ThumbnailButtons { get; }

    /// <summary>Whether the taskbar interfaces were created for the current window.</summary>
    [Browsable(false)]
    public bool IsAvailable => _taskbar != null;

    public event EventHandler<EnterpriseTaskbarThumbnailEventArgs>? ThumbnailButtonClick;

    public void SetProgress(int value, EnterpriseTaskbarProgressState state = EnterpriseTaskbarProgressState.Normal)
    {
        _state = state;
        _value = Math.Clamp(value, 0, _maximum);
        ApplyProgress();
    }

    public void SetOverlay(Icon? icon, string description = "")
    {
        _overlay = icon;
        _overlayDescription = description;
        ApplyOverlay();
    }

    public void ClearOverlay() => SetOverlay(null);

    /// <summary>Raises <see cref="ThumbnailButtonClick"/> as if the user pressed the thumbnail button.</summary>
    public void PerformThumbnailButtonClick(EnterpriseTaskbarThumbnailButton button)
    {
        if (!button.Enabled || !ThumbnailButtons.Contains(button)) return;
        ThumbnailButtonClick?.Invoke(this, new EnterpriseTaskbarThumbnailEventArgs(button));
    }

    internal void ResetThumbnails()
    {
        _thumbnailsAdded = false;
        ApplyThumbnails();
    }

    internal void UpdateThumbnails() => ApplyThumbnails();

    private void OnHandleCreated(object? sender, EventArgs e) => Attach();

    private void OnHandleDestroyed(object? sender, EventArgs e) => ReleaseHook();

    private void Attach()
    {
        if (_form is not { IsHandleCreated: true }) return;
        ReleaseHook();
        _hook = new Hook(this);
        _hook.AssignHandle(_form.Handle);
        EnsureTaskbar();
        ApplyAll();
    }

    private void ReleaseHook()
    {
        if (_hook == null) return;
        _hook.ReleaseHandle();
        _hook = null;
        _thumbnailsAdded = false;
    }

    private void Detach()
    {
        if (_form == null) return;
        _form.HandleCreated -= OnHandleCreated;
        _form.HandleDestroyed -= OnHandleDestroyed;
        ReleaseHook();
        _form = null;
    }

    private void EnsureTaskbar()
    {
        if (_taskbar != null) return;
        try
        {
            var taskbar = (ITaskbarList3)new TaskbarList();
            taskbar.HrInit();
            _taskbar = taskbar;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or PlatformNotSupportedException or NotSupportedException)
        {
            _taskbar = null;
        }
    }

    private void ApplyAll()
    {
        ApplyProgress();
        ApplyOverlay();
        _thumbnailsAdded = false;
        ApplyThumbnails();
    }

    private IntPtr Handle => _form is { IsHandleCreated: true } ? _form.Handle : IntPtr.Zero;

    private void ApplyProgress()
    {
        var handle = Handle;
        if (_taskbar == null || handle == IntPtr.Zero) return;
        try
        {
            var flag = _state switch
            {
                EnterpriseTaskbarProgressState.Indeterminate => 1,
                EnterpriseTaskbarProgressState.Normal => 2,
                EnterpriseTaskbarProgressState.Error => 4,
                EnterpriseTaskbarProgressState.Paused => 8,
                _ => 0
            };
            _taskbar.SetProgressState(handle, flag);
            if (_state is EnterpriseTaskbarProgressState.Normal or EnterpriseTaskbarProgressState.Error or EnterpriseTaskbarProgressState.Paused)
                _taskbar.SetProgressValue(handle, (ulong)_value, (ulong)_maximum);
        }
        catch (COMException)
        {
        }
    }

    private void ApplyOverlay()
    {
        var handle = Handle;
        if (_taskbar == null || handle == IntPtr.Zero) return;
        try { _taskbar.SetOverlayIcon(handle, _overlay?.Handle ?? IntPtr.Zero, _overlayDescription); }
        catch (COMException) { }
    }

    private void ApplyThumbnails()
    {
        var handle = Handle;
        if (_taskbar == null || handle == IntPtr.Zero || ThumbnailButtons.Count == 0) return;
        var buttons = new ThumbButton[ThumbnailButtons.Count];
        for (var i = 0; i < buttons.Length; i++)
        {
            var b = ThumbnailButtons[i];
            buttons[i] = new ThumbButton
            {
                Mask = 2 | 4 | 8,
                Id = (uint)(FirstThumbnailId + i),
                Icon = b.Icon?.Handle ?? IntPtr.Zero,
                Tip = b.ToolTip ?? string.Empty,
                Flags = (b.Enabled ? 0u : 1u) | (b.DismissOnClick ? 2u : 0u) | (b.Visible ? 0u : 8u)
            };
        }

        try
        {
            if (_thumbnailsAdded) _taskbar.ThumbBarUpdateButtons(handle, (uint)buttons.Length, buttons);
            else
            {
                _taskbar.ThumbBarAddButtons(handle, (uint)buttons.Length, buttons);
                _thumbnailsAdded = true;
            }
        }
        catch (COMException)
        {
        }
    }

    private void OnCommand(int wParam)
    {
        if (((wParam >> 16) & 0xFFFF) != ThumbnailClicked) return;
        var index = (wParam & 0xFFFF) - FirstThumbnailId;
        if (index >= 0 && index < ThumbnailButtons.Count) PerformThumbnailButtonClick(ThumbnailButtons[index]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Detach();
            if (_taskbar != null)
            {
                Marshal.ReleaseComObject(_taskbar);
                _taskbar = null;
            }
        }

        base.Dispose(disposing);
    }

    private sealed class Hook : NativeWindow
    {
        private readonly EnterpriseTaskbarButton _owner;

        public Hook(EnterpriseTaskbarButton owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TaskbarButtonCreatedMessage) _owner.ApplyAll();
            else if (m.Msg == WmCommand) _owner.OnCommand((int)(m.WParam.ToInt64() & 0xFFFFFFFF));
            base.WndProc(ref m);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct ThumbButton
    {
        public uint Mask;
        public uint Id;
        public uint Bitmap;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Tip;
        public uint Flags;
    }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    private class TaskbarList
    {
    }

    [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, int flags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMdi);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMdi, uint reserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray)] ThumbButton[] buttons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray)] ThumbButton[] buttons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
        void SetOverlayIcon(IntPtr hwnd, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string description);
    }
}
