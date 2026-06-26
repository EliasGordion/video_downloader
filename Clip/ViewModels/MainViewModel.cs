using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Clip.Models;
using Clip.Services;

namespace Clip.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly YTDLPService _ytdlpService;
    private readonly URLDetector _urlDetector;
    private readonly SettingsViewModel _settings;
    private readonly ToastService _toastService;
    private string _url = "";
    private string _statusMessage = "Ready";
    private bool _isAnalyzing;
    private VideoMetadata? _metadata;
    private FormatOption _selectedFormat;
    private string _selectedResolution;
    private bool _isCustomTargetSize;
    private double _customTargetSizeMb = 25;
    private bool _isClipEnabled;
    private CancellationTokenSource? _analysisCancellation;

    public MainViewModel(
        YTDLPService ytdlpService,
        URLDetector urlDetector,
        SettingsViewModel settings,
        ToastService toastService)
    {
        _ytdlpService = ytdlpService;
        _urlDetector = urlDetector;
        _settings = settings;
        _toastService = toastService;
        _statusMessage = Text.Ready;
        _settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.Text))
            {
                RefreshLocalizedStatusMessage();
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(PreviewTitle));
                OnPropertyChanged(nameof(PreviewSubtitle));
                OnPropertyChanged(nameof(PreviewPlatformLabel));
                OnPropertyChanged(nameof(DetectedPlatformLabel));
            }
        };

        Formats = new ObservableCollection<FormatOption>
        {
            new("MP4", "mp4"),
            new("MOV", "mov"),
            new("WebM", "webm"),
            new("MP3", "mp3", true)
        };

        Resolutions = new ObservableCollection<string>(settings.Resolutions);
        _selectedFormat = Formats.FirstOrDefault(item => item.Label == settings.DefaultFormat) ?? Formats[0];
        _selectedResolution = settings.DefaultResolution;

        PasteCommand = new AsyncRelayCommand(PasteFromClipboardAsync);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsAnalyzing);
        ClearCommand = new RelayCommand(Clear);
        SelectFormatCommand = new RelayCommand(parameter =>
        {
            if (parameter is FormatOption format)
            {
                SelectedFormat = format;
            }
        });
        SelectResolutionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string resolution)
            {
                SelectedResolution = resolution;
            }
        });
    }

    public ObservableCollection<FormatOption> Formats { get; }
    public ObservableCollection<string> Resolutions { get; }
    public ClipRange ClipRange { get; } = new();
    public AppText Text => _settings.Text;

    public AsyncRelayCommand PasteCommand { get; }
    public AsyncRelayCommand AnalyzeCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SelectFormatCommand { get; }
    public RelayCommand SelectResolutionCommand { get; }

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                if (Metadata is not null)
                {
                    Metadata = null;
                }

                OnPropertyChanged(nameof(DetectedPlatformLabel));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public VideoMetadata? Metadata
    {
        get => _metadata;
        set
        {
            if (SetProperty(ref _metadata, value))
            {
                if (value is not null)
                {
                    ClipRange.DurationSeconds = Math.Max(1, value.DurationSeconds);
                    ClipRange.StartSeconds = 0;
                    ClipRange.EndSeconds = Math.Max(1, value.DurationSeconds);
                }
                else
                {
                    IsClipEnabled = false;
                }

                OnPropertyChanged(nameof(HasMetadata));
                OnPropertyChanged(nameof(PreviewTitle));
                OnPropertyChanged(nameof(PreviewSubtitle));
                OnPropertyChanged(nameof(PreviewThumbnailUrl));
                OnPropertyChanged(nameof(PreviewPlatformLabel));
                OnPropertyChanged(nameof(PreviewDurationText));
            }
        }
    }

    public bool HasMetadata => Metadata is not null;
    public string PreviewTitle => Metadata?.Title ?? Text.PreviewPlaceholderTitle;
    public string PreviewSubtitle => Metadata is null
        ? Text.PreviewPlaceholderSubtitle
        : string.IsNullOrWhiteSpace(Metadata.Author) ? Text.UnknownAuthor : Metadata.Author;
    public string PreviewThumbnailUrl => Metadata?.ThumbnailUrl ?? "";
    public string PreviewPlatformLabel => Metadata?.PlatformLabel ?? Text.Waiting;
    public string PreviewDurationText => Metadata?.DurationText ?? "--:--";

    public FormatOption SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                if (value.IsAudioOnly)
                {
                    SelectedResolution = "Audio";
                }
                else if (SelectedResolution == "Audio")
                {
                    SelectedResolution = _settings.DefaultResolution;
                }

                OnPropertyChanged(nameof(IsAudioOnly));
                OnPropertyChanged(nameof(IsVideoFormat));
            }
        }
    }

    public string SelectedResolution
    {
        get => _selectedResolution;
        set => SetProperty(ref _selectedResolution, value);
    }

    public bool IsAudioOnly => SelectedFormat.IsAudioOnly;
    public bool IsVideoFormat => !IsAudioOnly;

    public bool IsCustomTargetSize
    {
        get => _isCustomTargetSize;
        set => SetProperty(ref _isCustomTargetSize, value);
    }

    public double CustomTargetSizeMb
    {
        get => _customTargetSizeMb;
        set => SetProperty(ref _customTargetSizeMb, Math.Max(1, value));
    }

    public bool IsClipEnabled
    {
        get => _isClipEnabled;
        set => SetProperty(ref _isClipEnabled, value);
    }

    public string DetectedPlatformLabel
    {
        get
        {
            var platform = _urlDetector.DetectPlatform(Url);
            return platform == Platform.Unknown ? Text.LinkNotDetected : platform.ToString();
        }
    }

    public async Task SetUrlFromExternalSourceAsync(string text, bool analyze)
    {
        Url = text;
        if (analyze)
        {
            await AnalyzeAsync();
        }
    }

    public bool TryGetCurrentVideoUrl(out string normalizedUrl, out Platform platform)
    {
        return _urlDetector.TryGetVideoUrl(Url, out normalizedUrl, out platform);
    }

    public void CancelAnalysis()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation = null;
        IsAnalyzing = false;
    }

    public async Task AnalyzeAsync()
    {
        _analysisCancellation?.Cancel();

        if (!_urlDetector.TryGetVideoUrl(Url, out var normalizedUrl, out var platform))
        {
            _analysisCancellation = null;
            IsAnalyzing = false;
            StatusMessage = Text.PasteSupportedUrlFirst;
            Metadata = null;
            return;
        }

        var analysisCancellation = new CancellationTokenSource();
        _analysisCancellation = analysisCancellation;
        Url = normalizedUrl;
        IsAnalyzing = true;
        StatusMessage = Text.AnalyzingVideo;

        try
        {
            var metadata = await _ytdlpService.AnalyzeAsync(
                normalizedUrl,
                platform,
                _settings.UseBrowserCookies,
                _settings.PreferredBrowser,
                analysisCancellation.Token);

            if (ReferenceEquals(_analysisCancellation, analysisCancellation))
            {
                Metadata = metadata;
                StatusMessage = Text.PreviewReady;
            }

            if (ReferenceEquals(_analysisCancellation, analysisCancellation) &&
                _settings.ShowToastNotifications)
            {
                _toastService.Show("Clip", Text.VideoPreviewReadyToast);
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_analysisCancellation, analysisCancellation))
            {
                StatusMessage = Text.AnalysisCancelled;
            }
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_analysisCancellation, analysisCancellation))
            {
                Metadata = null;
                StatusMessage = ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_analysisCancellation, analysisCancellation))
            {
                _analysisCancellation = null;
                IsAnalyzing = false;
            }

            analysisCancellation.Dispose();
        }
    }

    private async Task PasteFromClipboardAsync()
    {
        try
        {
            var package = Clipboard.GetContent();
            if (!package.Contains(StandardDataFormats.Text))
            {
                StatusMessage = Text.ClipboardHasNoText;
                return;
            }

            Url = await package.GetTextAsync();
            StatusMessage = Text.UrlPasted;
        }
        catch
        {
            StatusMessage = Text.CouldNotReadClipboard;
        }
    }

    private void Clear()
    {
        CancelAnalysis();
        Url = "";
        Metadata = null;
        StatusMessage = Text.Ready;
    }

    private void RefreshLocalizedStatusMessage()
    {
        if (MatchesKnownStatus(text => text.Ready))
        {
            StatusMessage = Text.Ready;
        }
        else if (MatchesKnownStatus(text => text.PasteSupportedUrlFirst))
        {
            StatusMessage = Text.PasteSupportedUrlFirst;
        }
        else if (MatchesKnownStatus(text => text.AnalyzingVideo))
        {
            StatusMessage = Text.AnalyzingVideo;
        }
        else if (MatchesKnownStatus(text => text.PreviewReady))
        {
            StatusMessage = Text.PreviewReady;
        }
        else if (MatchesKnownStatus(text => text.AnalysisCancelled))
        {
            StatusMessage = Text.AnalysisCancelled;
        }
        else if (MatchesKnownStatus(text => text.ClipboardHasNoText))
        {
            StatusMessage = Text.ClipboardHasNoText;
        }
        else if (MatchesKnownStatus(text => text.UrlPasted))
        {
            StatusMessage = Text.UrlPasted;
        }
        else if (MatchesKnownStatus(text => text.CouldNotReadClipboard))
        {
            StatusMessage = Text.CouldNotReadClipboard;
        }
        else if (MatchesKnownStatus(text => text.CouldNotReadDroppedText))
        {
            StatusMessage = Text.CouldNotReadDroppedText;
        }
        else if (MatchesKnownStatus(text => text.PasteValidVideoUrlBeforeDownloading))
        {
            StatusMessage = Text.PasteValidVideoUrlBeforeDownloading;
        }
        else if (MatchesKnownStatus(text => text.AddedToDownloads))
        {
            StatusMessage = Text.AddedToDownloads;
        }
        else if (MatchesKnownStatus(text => text.RemovedFromHistory))
        {
            StatusMessage = Text.RemovedFromHistory;
        }
        else if (MatchesKnownStatus(text => text.CouldNotUpdateHistory))
        {
            StatusMessage = Text.CouldNotUpdateHistory;
        }
    }

    private bool MatchesKnownStatus(Func<AppText, string> selector)
    {
        return string.Equals(StatusMessage, selector(AppText.Russian), StringComparison.Ordinal) ||
               string.Equals(StatusMessage, selector(AppText.English), StringComparison.Ordinal);
    }
}
