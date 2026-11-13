using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicIsland.Services;

internal static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;  // Hidden from Alt+Tab
    private const int WS_EX_NOACTIVATE = 0x08000000;  // Clicking it doesn't steal focus

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    public static void MakeToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// NOACTIVATE is turned off temporarily while typing a note, so the window can receive the keyboard.
    /// </summary>
    public static void SetNoActivate(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        style = enabled ? style | WS_EX_NOACTIVATE : style & ~WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    /// <summary>
    /// True while a full-screen app (game, video, presentation) is running.
    /// 2 = QUNS_BUSY, 3 = QUNS_RUNNING_D3D_FULL_SCREEN, 4 = QUNS_PRESENTATION_MODE
    /// </summary>
    public static bool IsFullscreenAppRunning()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state is 2 or 3 or 4;
    }
}
