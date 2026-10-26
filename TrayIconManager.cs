using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CodePin;

public class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 100;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    public const int NIIF_NONE = 0x00000000;
    public const int NIIF_INFO = 0x00000001;
    public const int NIIF_WARNING = 0x00000002;
    public const int NIIF_ERROR = 0x00000003;

    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    public const int NIN_BALLOONUSERCLICK = 0x0405;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly IntPtr _hwnd;
    private readonly string _tip;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isCreated = false;
    private readonly uint _wmTaskbarCreated;

    public uint TaskbarCreatedMessage => _wmTaskbarCreated;

    public Action? OnLeftClick { get; set; }
    public Action? OnDoubleClick { get; set; }
    public Action? OnRightClick { get; set; }
    public Action? OnBalloonClick { get; set; }

    public TrayIconManager(IntPtr hwnd, string tip = "CodePin - Ghim Ảnh & Cửa Sổ Nổi")
    {
        _hwnd = hwnd;
        _tip = tip;
        _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

        LoadBestIcon();
        CreateTrayIcon();
    }

    private void LoadBestIcon()
    {
        try
        {
            // 1. Try loading app.ico from BaseDirectory
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            }

            // 2. Try extracting from current executable process
            if (_hIcon == IntPtr.Zero)
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    _hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                }
            }

            // 3. Fallback to standard system application icon
            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
            }
        }
        catch
        {
            _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512);
        }
    }

    private void CreateTrayIcon()
    {
        if (_hwnd == IntPtr.Zero || _hIcon == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = _tip.Length > 127 ? _tip.Substring(0, 127) : _tip
        };

        _isCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void RecreateIcon()
    {
        CreateTrayIcon();
    }

    public void ShowBalloon(string title, string message, int iconType = NIIF_INFO)
    {
        if (!_isCreated || _hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_INFO,
            szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title,
            szInfo = message.Length > 255 ? message.Substring(0, 255) : message,
            dwInfoFlags = iconType
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public void HandleMessage(int mouseMsg)
    {
        switch (mouseMsg)
        {
            case WM_LBUTTONUP:
                OnLeftClick?.Invoke();
                break;
            case WM_LBUTTONDBLCLK:
                OnDoubleClick?.Invoke();
                break;
            case WM_RBUTTONUP:
                OnRightClick?.Invoke();
                break;
            case NIN_BALLOONUSERCLICK:
                OnBalloonClick?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        if (_isCreated && _hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isCreated = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
