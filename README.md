# Clip

Clip is a native Windows downloader app built with C# 12, .NET 8, WinUI 3, and Windows App SDK.

It uses local copies of `yt-dlp.exe`, `ffmpeg.exe`, and `ffprobe.exe` from:

```text
Clip/Resources/bin/
```

These executable files are not committed to the repository. Download them before running or publishing:

```powershell
.\scripts\Download-Binaries.ps1
```

The app does not assume Python, yt-dlp, or ffmpeg are installed globally. Runtime paths are resolved from `AppContext.BaseDirectory`, so portable publish output keeps the same layout.

## Development

```powershell
dotnet restore
.\scripts\Download-Binaries.ps1
dotnet build -c Debug
dotnet run --project Clip
```

## Portable release

```powershell
dotnet publish Clip `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=false
```

The publish folder must contain:

```text
Clip.exe
Resources/bin/yt-dlp.exe
Resources/bin/ffmpeg.exe
Resources/bin/ffprobe.exe
```

To refresh the bundled tools:

```powershell
.\scripts\Download-Binaries.ps1
```

## Notes

The tray icon, clipboard monitoring, JSON settings, download history, and process execution are implemented for unpacked development builds. MSIX can be added later, but the portable build is the priority because it keeps bundled process execution simple.
