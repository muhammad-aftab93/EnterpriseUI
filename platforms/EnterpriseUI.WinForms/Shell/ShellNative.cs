using System.Runtime.InteropServices;

namespace EnterpriseUI.WinForms;

internal static class ShellNative
{
    public const int WmNcCalcSize = 0x83, WmNcHitTest = 0x84, WmNcPaint = 0x85, WmNcActivate = 0x86, WmNcLButtonDown = 0xA1, WmSysCommand = 0x112;
    public const int HtClient = 1, HtCaption = 2, HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14, HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17, HtTransparent = -1;
    private const int SmCxSizeFrame = 32, SmCxPaddedBorder = 92;
    private const int DwmUseImmersiveDarkMode = 20, DwmWindowCornerPreference = 33;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hwnd, bool revert);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Thickness of the invisible sizing frame a maximized window extends beyond the screen.</summary>
    public static int MaximizedOverhang(uint dpi) => GetSystemMetricsForDpi(SmCxSizeFrame, dpi) + GetSystemMetricsForDpi(SmCxPaddedBorder, dpi);

    public static Point PointFromLParam(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new Point(unchecked((short)(value & 0xFFFF)), unchecked((short)((value >> 16) & 0xFFFF)));
    }

    public static Graphics? WindowGraphics(IntPtr hwnd, out IntPtr dc)
    {
        dc = GetWindowDC(hwnd);
        return dc == IntPtr.Zero ? null : Graphics.FromHdc(dc);
    }

    public static void ReleaseWindowGraphics(IntPtr hwnd, IntPtr dc) => _ = ReleaseDC(hwnd, dc);

    /// <summary>Dark title and rounded corners; best effort, silently ignored on systems without them.</summary>
    public static void ApplyWindowEffects(IntPtr hwnd, bool dark, bool rounded)
    {
        try
        {
            var darkValue = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref darkValue, sizeof(int));
            var corner = rounded ? 2 : 1;
            _ = DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref corner, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    /// <summary>Starts the system move loop, as if the caption had been pressed.</summary>
    public static void BeginCaptionDrag(IntPtr hwnd)
    {
        ReleaseCapture();
        SendMessage(hwnd, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    /// <summary>Opens the window's system menu at a screen point and runs the chosen command.</summary>
    public static void ShowSystemMenu(IntPtr hwnd, Point screen)
    {
        var menu = GetSystemMenu(hwnd, false);
        if (menu == IntPtr.Zero) return;
        const uint ReturnCommand = 0x100;
        var command = TrackPopupMenuEx(menu, ReturnCommand, screen.X, screen.Y, hwnd, IntPtr.Zero);
        if (command != 0) PostMessage(hwnd, WmSysCommand, (IntPtr)command, IntPtr.Zero);
    }
}
