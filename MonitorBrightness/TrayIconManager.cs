using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// Win32 system tray icon manager using Shell_NotifyIcon.
/// </summary>
public class TrayIconManager : IDisposable
{
    private const int WM_TRAYICON = 0x8000 + 1;
    private const int NIM_ADD = 0x00;
    private const int NIM_DELETE = 0x02;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int NIF_MESSAGE = 0x01;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll")]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadImage(IntPtr hInstance, string lpName, uint uType, int cx, int cy, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private bool _added;

    public event Action? OnTrayIconClicked;

    public void Create(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // Try loading custom icon from app directory
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
        {
            _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        }
        // Fallback to shell icon
        if (_hIcon == IntPtr.Zero)
            _hIcon = ExtractIcon(IntPtr.Zero, "shell32.dll", 15);

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Monitor Brightness Control"
        };

        _added = Shell_NotifyIconW(NIM_ADD, ref nid);
        if (!_added)
            AppLogger.Warn("Failed to create system tray icon");
    }

    public bool HandleMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                OnTrayIconClicked?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void Remove()
    {
        if (!_added) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
        };
        if (!Shell_NotifyIconW(NIM_DELETE, ref nid))
            AppLogger.Warn("Failed to remove system tray icon");

        _added = false;
    }

    public void Dispose()
    {
        Remove();
        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
