namespace Clip;

public static class ClipConstants
{
    public const string AppName = "Clip";
    public const string SettingsFileName = "settings.json";
    public const string HistoryFileName = "history.json";
    public const int DefaultMaxConcurrentDownloads = 3;
    public const int MaxConcurrentDownloadsLimit = 3;
    public const string ResourcesDirectoryName = "Resources";
    public const string BinaryDirectoryName = "bin";
    public const string YtdlpExe = "yt-dlp.exe";
    public const string FfmpegExe = "ffmpeg.exe";
    public const string FfprobeExe = "ffprobe.exe";

    public static string BinaryDirectory =>
        Path.Combine(AppContext.BaseDirectory, ResourcesDirectoryName, BinaryDirectoryName);

    public static string YtdlpPath => Path.Combine(BinaryDirectory, YtdlpExe);
    public static string FfmpegPath => Path.Combine(BinaryDirectory, FfmpegExe);
    public static string FfprobePath => Path.Combine(BinaryDirectory, FfprobeExe);
}
