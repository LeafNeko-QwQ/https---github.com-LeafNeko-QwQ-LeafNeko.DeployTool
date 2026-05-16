using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LeafNeko.DeployTool.Helpers;

/// <summary>
/// 轻量系统托盘图标，基于 Shell_NotifyIcon P/Invoke（零第三方依赖）
/// </summary>
public class TrayIcon : IDisposable
{
    private const int WmTrayMouse = 0x8001;
    private const int WmTaskbarCreated = 0x001F;
    private static readonly int WmTrayCallback = RegisterWindowMessage("LeafNekoDeployToolTray");

    private readonly HwndSource _hwnd;
    private readonly IntPtr _iconHandle;
    private bool _visible;

    public event Action? DoubleClick;
    public event Action? ExitRequested;

    public string ToolTip { get; set; } = "LeafNeko 装机助手";

    public TrayIcon()
    {
        _iconHandle = LoadCustomIcon();
        _hwnd = CreateMessageWindow(WndProc);
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            UpdateIcon(value);
        }
    }

    private void UpdateIcon(bool visible)
    {
        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd.Handle,
            uID = 1,
            uFlags = 0x00000001 | 0x00000002 | 0x00000004, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = WmTrayCallback,
            hIcon = visible ? _iconHandle : IntPtr.Zero
        };
        data.szTip = ToolTip;

        var msg = visible ? 0x00000000 : 0x00000002; // NIM_ADD : NIM_DELETE
        Shell_NotifyIcon(msg, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmTrayCallback)
        {
            var l = lParam.ToInt64() & 0xFFFF;
            if (l == 0x0203) // WM_LBUTTONDBLCLK
                DoubleClick?.Invoke();
            else if (l == 0x0205) // WM_RBUTTONUP
                ShowContextMenu();
        }
        else if (msg == WmTaskbarCreated)
        {
            if (_visible)
                UpdateIcon(true);
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, 0, 1, "显示主窗口");
        AppendMenu(menu, 0, 2, "退出");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd.Handle);
        var cmd = TrackPopupMenu(menu, 0x0100 | 0x0002, pt.X, pt.Y, 0, _hwnd.Handle, IntPtr.Zero);
        DestroyMenu(menu);

        if (cmd == 1)
            DoubleClick?.Invoke();
        else if (cmd == 2)
            ExitRequested?.Invoke();
    }

    private static IntPtr LoadCustomIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            if (File.Exists(iconPath))
                return LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x00000010);
        }
        catch { }
        return LoadIcon(IntPtr.Zero, 32512); // IDI_APPLICATION
    }

    public void Dispose()
    {
        if (_visible)
        {
            var data = new NotifyIconData { cbSize = Marshal.SizeOf<NotifyIconData>(), hWnd = _hwnd.Handle, uID = 1 };
            Shell_NotifyIcon(0x00000002, ref data);
        }
        if (_iconHandle != IntPtr.Zero)
            DestroyIcon(_iconHandle);
        _hwnd.Dispose();
    }

    // ========== P/Invoke ==========

    [DllImport("shell32.dll")]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, int type, int cx, int cy, int fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, int lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, int uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point pt);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 创建仅用于接收托盘消息的隐藏窗口（HwndSource 原生创建，无 raw Win32 窗口）。
    /// </summary>
    private static HwndSource CreateMessageWindow(HwndSourceHook hook)
    {
        var parameters = new HwndSourceParameters("LeafNekoTraySource")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080 // WS_EX_TOOLWINDOW
        };
        var source = new HwndSource(parameters);
        source.AddHook(hook);
        return source;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
