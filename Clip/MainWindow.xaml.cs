using System.Runtime.InteropServices;
using Clip.Services;
using Clip.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace Clip;

public sealed partial class MainWindow : Window
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    private readonly nint _windowHandle;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService = new();
    private readonly URLDetector _urlDetector = new();
    private readonly FileSystemService _fileSystemService = new();
    private readonly ToastService _toastService = new();
    private TrayIconService? _trayIconService;
    private ClipboardMonitor? _clipboardMonitor;
    private SettingsViewModel? _settingsViewModel;
    private MainViewModel? _mainViewModel;
    private DownloadViewModel? _downloadViewModel;
    private bool _allowExit;
    private bool _exitInProgress;
    private bool _servicesDisposed;

    public MainWindow()
    {
        InitializeComponent();

        _windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Closing += OnAppWindowClosing;

        TryApplyBackdrop();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadAsync();
        _settingsViewModel = new SettingsViewModel(_settingsService, settings);
        ApplyTheme(settings.Theme);
        RestoreWindow(settings);

        var ytdlpService = new YTDLPService(_fileSystemService);
        var ffmpegService = new FFmpegService();
        _mainViewModel = new MainViewModel(ytdlpService, _urlDetector, _settingsViewModel, _toastService);
        _downloadViewModel = new DownloadViewModel(
            _mainViewModel,
            _settingsViewModel,
            ytdlpService,
            ffmpegService,
            _settingsService,
            _fileSystemService,
            _toastService,
            DispatcherQueue);

        RootContent.SetViewModels(_mainViewModel, _downloadViewModel, _settingsViewModel);
        await _downloadViewModel.InitializeAsync();

        _clipboardMonitor = new ClipboardMonitor(DispatcherQueue, _urlDetector);
        _clipboardMonitor.UrlDetected += OnClipboardUrlDetected;
        _clipboardMonitor.SetEnabled(_settingsViewModel.ClipboardMonitoringEnabled);
        _settingsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.ClipboardMonitoringEnabled))
            {
                _clipboardMonitor.SetEnabled(_settingsViewModel.ClipboardMonitoringEnabled);
            }
            else if (args.PropertyName == nameof(SettingsViewModel.Theme))
            {
                ApplyTheme(_settingsViewModel.Theme);
            }
            else if (args.PropertyName == nameof(SettingsViewModel.Text))
            {
                _trayIconService?.SetTooltip(_settingsViewModel.Text.AppRunningTooltip);
            }
        };

        _toastService.Register();
        _trayIconService = new TrayIconService();
        _trayIconService.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowMainWindow);
        _trayIconService.PasteRequested += (_, _) => DispatcherQueue.TryEnqueue(PasteFromTray);
        _trayIconService.DownloadsRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowMainWindow);
        _trayIconService.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowMainWindow);
        _trayIconService.QuitRequested += (_, _) => DispatcherQueue.TryEnqueue(Quit);
    }

    private async void OnClipboardUrlDetected(object? sender, string url)
    {
        if (_mainViewModel is null || _settingsViewModel is null)
        {
            return;
        }

        await _mainViewModel.SetUrlFromExternalSourceAsync(url, _settingsViewModel.AutoAnalyzeClipboard);
    }

    private async void PasteFromTray()
    {
        ShowMainWindow();

        if (_mainViewModel is null)
        {
            return;
        }

        try
        {
            var package = Clipboard.GetContent();
            if (package.Contains(StandardDataFormats.Text))
            {
                await _mainViewModel.SetUrlFromExternalSourceAsync(await package.GetTextAsync(), true);
            }
        }
        catch
        {
            _mainViewModel.StatusMessage = _mainViewModel.Text.CouldNotReadClipboard;
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowExit)
        {
            DisposeServices();
            return;
        }

        args.Cancel = true;

        if (!_exitInProgress && _settingsViewModel?.KeepInTrayOnClose == true)
        {
            HideMainWindow();
            _trayIconService?.SetTooltip(_settingsViewModel.Text.AppRunningTooltip);
            return;
        }

        await BeginExitAsync();
    }

    private void TryApplyBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
        }
    }

    private void ApplyTheme(string theme)
    {
        RootContent.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void RestoreWindow(Models.AppSettings settings)
    {
        _appWindow.Resize(new SizeInt32(
            Math.Max(900, (int)settings.WindowWidth),
            Math.Max(620, (int)settings.WindowHeight)));

        if (settings.WindowX >= 0 && settings.WindowY >= 0)
        {
            _appWindow.Move(new PointInt32(settings.WindowX, settings.WindowY));
        }
    }

    private async Task SaveWindowStateAsync()
    {
        if (_settingsViewModel is null)
        {
            return;
        }

        var settings = _settingsViewModel.Snapshot;
        settings.WindowWidth = _appWindow.Size.Width;
        settings.WindowHeight = _appWindow.Size.Height;
        settings.WindowX = _appWindow.Position.X;
        settings.WindowY = _appWindow.Position.Y;
        await _settingsViewModel.SaveAsync();
    }

    private void ShowMainWindow()
    {
        ShowWindow(_windowHandle, SwShow);
        Activate();
    }

    private void HideMainWindow()
    {
        ShowWindow(_windowHandle, SwHide);
    }

    private async void Quit()
    {
        await BeginExitAsync();
    }

    private async Task BeginExitAsync()
    {
        if (_exitInProgress)
        {
            return;
        }

        _exitInProgress = true;
        try
        {
            _mainViewModel?.CancelAnalysis();
            _downloadViewModel?.CancelAll();
            await SaveWindowStateAsync();
        }
        catch
        {
            // A settings write failure should not prevent the app from closing.
        }
        finally
        {
            _allowExit = true;
            DisposeServices();
            Close();
        }
    }

    private void DisposeServices()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        _clipboardMonitor?.Dispose();
        _trayIconService?.Dispose();
        _toastService.Unregister();
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
