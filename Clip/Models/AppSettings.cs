namespace Clip.Models;

public sealed class AppSettings
{
    public string DefaultSaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string DefaultFormat { get; set; } = "MP4";
    public string DefaultResolution { get; set; } = "1080p";
    public string Language { get; set; } = "ru";
    public int MaxConcurrentDownloads { get; set; } = ClipConstants.DefaultMaxConcurrentDownloads;
    public bool ClipboardMonitoringEnabled { get; set; } = true;
    public bool AutoAnalyzeClipboard { get; set; }
    public string Theme { get; set; } = "System";
    public bool UseBrowserCookies { get; set; }
    public string PreferredBrowser { get; set; } = "Auto";
    public bool ShowToastNotifications { get; set; } = true;
    public bool KeepInTrayOnClose { get; set; } = true;
    public double WindowWidth { get; set; } = 1120;
    public double WindowHeight { get; set; } = 760;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
}
