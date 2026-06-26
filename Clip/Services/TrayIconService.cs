using System.Runtime.InteropServices;

namespace Clip.Services;

public sealed class TrayIconService : IDisposable
{
    private const int TrayId = 1;
    private const int TrayCallbackMessage = 0x8000 + 42;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private static readonly nint HwndMessage = new(-3);
    private static readonly nint IdiApplication = new(32512);

    private readonly string _className = $"ClipTrayWindow-{Guid.NewGuid():N}";
    private readonly WndProc _wndProc;
    private nint _windowHandle;
    private nint _iconHandle;
    private ushort _classAtom;
    private bool _disposed;

    public TrayIconService()
    {
        _wndProc = WindowProcedure;
        CreateMessageWindow();
        AddIcon();
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? PasteRequested;
    public event EventHandler? DownloadsRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? QuitRequested;

    public void SetTooltip(string text)
    {
        if (_windowHandle == 0)
        {
            return;
        }

        var data = CreateNotifyIconData();
        data.uFlags = NifTip;
        data.szTip = SafeTip(text);
        Shell_NotifyIcon(NimModify, ref data);
    }

    public void ShowBalloonTip(string title, string message)
    {
        SetTooltip($"{title}: {message}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_windowHandle != 0)
        {
            var data = CreateNotifyIconData();
            Shell_NotifyIcon(NimDelete, ref data);
            DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        if (_classAtom != 0)
        {
            UnregisterClass(_className, GetModuleHandle(null));
            _classAtom = 0;
        }
    }

    private void CreateMessageWindow()
    {
        var instance = GetModuleHandle(null);
        var windowClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = instance,
            lpszClassName = _className
        };

        _classAtom = RegisterClassEx(ref windowClass);
        if (_classAtom == 0)
        {
            return;
        }

        _windowHandle = CreateWindowEx(
            0,
            _className,
            _className,
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            0,
            instance,
            0);
    }

    private void AddIcon()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        _iconHandle = LoadIcon(0, IdiApplication);
        var data = CreateNotifyIconData();
        data.uFlags = NifMessage | NifIcon | NifTip;
        data.uCallbackMessage = TrayCallbackMessage;
        data.hIcon = _iconHandle;
        data.szTip = "Clip";
        Shell_NotifyIcon(NimAdd, ref data);
    }

    private NotifyIconData CreateNotifyIconData()
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = TrayId,
            szTip = "Clip",
            szInfo = "",
            szInfoTitle = ""
        };
    }

    private nint WindowProcedure(nint hWnd, uint message, nint wParam, nint lParam)
    {
        if (message == TrayCallbackMessage)
        {
            var mouseMessage = lParam.ToInt32();
            if (mouseMessage == WmLButtonUp)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
                return 0;
            }

            if (mouseMessage == WmRButtonUp)
            {
                ShowContextMenu();
                return 0;
            }
        }

        return DefWindowProc(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_windowHandle == 0 || !GetCursorPos(out var point))
        {
            return;
        }

        var menu = CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        var text = LocalizationService.CurrentText;
        AppendMenu(menu, MfString, 1001, text.TrayShowClip);
        AppendMenu(menu, MfString, 1002, text.TrayPasteUrl);
        AppendMenu(menu, MfString, 1003, text.TrayDownloads);
        AppendMenu(menu, MfString, 1004, text.TraySettings);
        AppendMenu(menu, MfSeparator, 0, null);
        AppendMenu(menu, MfString, 1005, text.TrayQuit);

        SetForegroundWindow(_windowHandle);
        var command = TrackPopupMenu(
            menu,
            TpmRightButton | TpmReturnCmd,
            point.X,
            point.Y,
            0,
            _windowHandle,
            0);

        DestroyMenu(menu);

        switch (command)
        {
            case 1001:
                ShowRequested?.Invoke(this, EventArgs.Empty);
                break;
            case 1002:
                PasteRequested?.Invoke(this, EventArgs.Empty);
                break;
            case 1003:
                DownloadsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case 1004:
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case 1005:
                QuitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private static string SafeTip(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? "Clip"
            : text.Length > 127 ? text[..127] : text;
    }

    private delegate nint WndProc(nint hWnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public int uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
