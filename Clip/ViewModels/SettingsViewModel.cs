using System.Collections.ObjectModel;
using Clip.Models;
using Clip.Services;

namespace Clip.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private AppSettings _settings;
    private CancellationTokenSource? _saveDebounceCancellation;
    private LanguageOption _selectedLanguage;

    public SettingsViewModel(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _settings.Language = LocalizationService.NormalizeLanguage(_settings.Language);
        _localizationService = new LocalizationService(_settings.Language);

        var defaults = new AppSettings();
        _settings.DefaultSaveFolder = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
            ? defaults.DefaultSaveFolder
            : _settings.DefaultSaveFolder;
        _settings.DefaultFormat = FindOption(Formats, _settings.DefaultFormat) ?? defaults.DefaultFormat;
        _settings.DefaultResolution = FindOption(Resolutions, _settings.DefaultResolution) ?? defaults.DefaultResolution;
        _settings.Theme = FindOption(Themes, _settings.Theme) ?? defaults.Theme;
        _settings.PreferredBrowser = FindOption(Browsers, _settings.PreferredBrowser) ?? defaults.PreferredBrowser;
        _settings.MaxConcurrentDownloads = Math.Clamp(
            _settings.MaxConcurrentDownloads,
            1,
            ClipConstants.MaxConcurrentDownloadsLimit);
        _selectedLanguage = FindLanguageOption(_settings.Language) ?? Languages[0];
    }

    public ObservableCollection<string> Formats { get; } = new(["MP4", "MOV", "WebM", "MP3"]);
    public ObservableCollection<string> Resolutions { get; } = new(["4K", "1440p", "1080p", "720p", "480p", "360p"]);
    public ObservableCollection<string> Themes { get; } = new(["System", "Light", "Dark"]);
    public ObservableCollection<string> Browsers { get; } = new(["Auto", "Edge", "Chrome", "Firefox", "Brave"]);
    public ObservableCollection<int> ConcurrentOptions { get; } = new([1, 2, 3]);
    public ObservableCollection<LanguageOption> Languages { get; } = new([
        new LanguageOption(LocalizationService.RussianCode, "RU", "Русский"),
        new LanguageOption(LocalizationService.EnglishCode, "EN", "English")
    ]);

    public AppSettings Snapshot => _settings;
    public AppText Text => _localizationService.Text;

    public string DefaultSaveFolder
    {
        get => _settings.DefaultSaveFolder;
        set
        {
            if (_settings.DefaultSaveFolder == value)
            {
                return;
            }

            _settings.DefaultSaveFolder = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public string DefaultFormat
    {
        get => _settings.DefaultFormat;
        set
        {
            if (_settings.DefaultFormat == value)
            {
                return;
            }

            _settings.DefaultFormat = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public string DefaultResolution
    {
        get => _settings.DefaultResolution;
        set
        {
            if (_settings.DefaultResolution == value)
            {
                return;
            }

            _settings.DefaultResolution = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public int MaxConcurrentDownloads
    {
        get => _settings.MaxConcurrentDownloads;
        set
        {
            var safe = Math.Clamp(value, 1, ClipConstants.MaxConcurrentDownloadsLimit);
            if (_settings.MaxConcurrentDownloads == safe)
            {
                return;
            }

            _settings.MaxConcurrentDownloads = safe;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public bool ClipboardMonitoringEnabled
    {
        get => _settings.ClipboardMonitoringEnabled;
        set
        {
            if (_settings.ClipboardMonitoringEnabled == value)
            {
                return;
            }

            _settings.ClipboardMonitoringEnabled = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public bool AutoAnalyzeClipboard
    {
        get => _settings.AutoAnalyzeClipboard;
        set
        {
            if (_settings.AutoAnalyzeClipboard == value)
            {
                return;
            }

            _settings.AutoAnalyzeClipboard = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public string Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme == value)
            {
                return;
            }

            _settings.Theme = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null ||
                string.Equals(_settings.Language, value.Code, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguage = value;
            _settings.Language = LocalizationService.NormalizeLanguage(value.Code);
            _localizationService.SetLanguage(_settings.Language);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Text));
            SaveQuietly();
        }
    }

    public bool UseBrowserCookies
    {
        get => _settings.UseBrowserCookies;
        set
        {
            if (_settings.UseBrowserCookies == value)
            {
                return;
            }

            _settings.UseBrowserCookies = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public string PreferredBrowser
    {
        get => _settings.PreferredBrowser;
        set
        {
            if (_settings.PreferredBrowser == value)
            {
                return;
            }

            _settings.PreferredBrowser = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public bool ShowToastNotifications
    {
        get => _settings.ShowToastNotifications;
        set
        {
            if (_settings.ShowToastNotifications == value)
            {
                return;
            }

            _settings.ShowToastNotifications = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public bool KeepInTrayOnClose
    {
        get => _settings.KeepInTrayOnClose;
        set
        {
            if (_settings.KeepInTrayOnClose == value)
            {
                return;
            }

            _settings.KeepInTrayOnClose = value;
            OnPropertyChanged();
            SaveQuietly();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await _settingsService.SaveAsync(_settings, cancellationToken);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void SaveQuietly()
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _saveDebounceCancellation, cancellation);
        previous?.Cancel();
        _ = SaveAfterDelayAsync(cancellation);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(250, cancellation.Token);
            await SaveAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch
        {
            // Settings save errors are surfaced next time the app loads defaults.
        }
        finally
        {
            Interlocked.CompareExchange(ref _saveDebounceCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private static string? FindOption(IEnumerable<string> options, string? value)
    {
        return options.FirstOrDefault(option =>
            option.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private LanguageOption? FindLanguageOption(string? value)
    {
        return Languages.FirstOrDefault(option =>
            option.Code.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
}
