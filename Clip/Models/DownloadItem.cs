using System.Text.Json.Serialization;
using Clip.Services;

namespace Clip.Models;

public sealed class DownloadItem : ObservableObject
{
    private double _progress;
    private string _status = "Queued";
    private string _outputPath = "";
    private string _errorMessage = "";
    private bool _isRunning;
    private bool _isCompleted;
    private bool _isCancelled;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public Platform Platform { get; set; } = Platform.Unknown;
    public string SaveFolder { get; set; } = "";
    public string Format { get; set; } = "mp4";
    public string Resolution { get; set; } = "1080p";
    public double? TargetSizeMb { get; set; }
    public bool UseBrowserCookies { get; set; }
    public string PreferredBrowser { get; set; } = "Auto";
    public bool IsClip { get; set; }
    public ClipRange? ClipRange { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? CompletedAt { get; set; }

    public double Progress
    {
        get => _progress;
        set
        {
            if (SetProperty(ref _progress, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanRetry));
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(CanOpenFile));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanRetry));
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                OnPropertyChanged(nameof(CanRetry));
            }
        }
    }

    [JsonIgnore]
    public bool IsCancelled
    {
        get => _isCancelled;
        set => SetProperty(ref _isCancelled, value);
    }

    [JsonIgnore]
    public CancellationTokenSource? Cancellation { get; set; }

    [JsonIgnore]
    public bool CanCancel => IsRunning || Status == "Queued";

    [JsonIgnore]
    public bool CanRetry =>
        !IsRunning &&
        !IsCompleted &&
        Status is "Failed" or "Cancelled";

    [JsonIgnore]
    public bool CanOpenFile => !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);

    [JsonIgnore]
    public string ProgressText => $"{Progress:0.#}%";

    [JsonIgnore]
    public string StatusDisplay => LocalizationService.StatusText(Status);

    [JsonIgnore]
    public string FormatSummary => $"{Format.ToUpperInvariant()} · {Resolution}";

    [JsonIgnore]
    public string CompletedAtText => (CompletedAt ?? CreatedAt).ToString("g");

    public DownloadItem CloneForRetry()
    {
        return new DownloadItem
        {
            Url = Url,
            Title = Title,
            Platform = Platform,
            SaveFolder = SaveFolder,
            Format = Format,
            Resolution = Resolution,
            TargetSizeMb = TargetSizeMb,
            UseBrowserCookies = UseBrowserCookies,
            PreferredBrowser = PreferredBrowser,
            IsClip = IsClip,
            ClipRange = ClipRange?.Clone()
        };
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(StatusDisplay));
    }
}
