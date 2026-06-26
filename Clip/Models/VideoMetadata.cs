namespace Clip.Models;

public sealed class VideoMetadata : ObservableObject
{
    private string _title = "";
    private string _author = "";
    private string _thumbnailUrl = "";
    private string _sourceUrl = "";
    private double _durationSeconds;
    private Platform _platform = Platform.Unknown;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Author
    {
        get => _author;
        set => SetProperty(ref _author, value);
    }

    public string ThumbnailUrl
    {
        get => _thumbnailUrl;
        set => SetProperty(ref _thumbnailUrl, value);
    }

    public string SourceUrl
    {
        get => _sourceUrl;
        set => SetProperty(ref _sourceUrl, value);
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            if (SetProperty(ref _durationSeconds, value))
            {
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public Platform Platform
    {
        get => _platform;
        set
        {
            if (SetProperty(ref _platform, value))
            {
                OnPropertyChanged(nameof(PlatformLabel));
            }
        }
    }

    public List<FormatOption> Formats { get; set; } = [];

    public string PlatformLabel => Platform == Platform.Unknown ? "yt-dlp" : Platform.ToString();
    public string DurationText => ClipRange.FormatTime(DurationSeconds);
}
