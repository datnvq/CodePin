using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CodePin;

public class WindowInfo
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool IsTopmost { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(ProcessName) 
        ? Title 
        : $"{Title} ({ProcessName})";
}

public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;
        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsWindowTopmost(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        int style = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (style & WS_EX_TOPMOST) != 0;
    }

    public static bool ToggleWindowTopmost(IntPtr hWnd, out bool isNowTopmost, out string title)
    {
        title = GetWindowTitle(hWnd);
        if (hWnd == IntPtr.Zero)
        {
            isNowTopmost = false;
            return false;
        }

        bool currentlyTopmost = IsWindowTopmost(hWnd);
        isNowTopmost = !currentlyTopmost;

        SetWindowPos(
            hWnd,
            isNowTopmost ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE
        );

        return true;
    }

    public static List<WindowInfo> GetOpenWindows(IntPtr excludeHwnd)
    {
        var list = new List<WindowInfo>();
        IntPtr shellHwnd = GetShellWindow();

        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == excludeHwnd || hWnd == shellHwnd) return true;
            if (!IsWindowVisible(hWnd)) return true;

            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            // Ignore system background utilities
            if (string.IsNullOrWhiteSpace(title) || title == "Program Manager" || title == "Settings") return true;

            string procName = string.Empty;
            try
            {
                GetWindowThreadProcessId(hWnd, out uint procId);
                if (procId != 0)
                {
                    using var proc = Process.GetProcessById((int)procId);
                    procName = proc.ProcessName;
                }
            }
            catch
            {
                // Process may have exited or insufficient access
            }

            list.Add(new WindowInfo
            {
                Hwnd = hWnd,
                Title = title,
                ProcessName = procName,
                IsTopmost = IsWindowTopmost(hWnd)
            });

            return true;
        }, IntPtr.Zero);

        return list;
    }
}
