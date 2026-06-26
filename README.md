# Clip

Clip is a native Windows downloader app built with C# 12, .NET 8, WinUI 3, and Windows App SDK.

## Download

Use the ZIP file from GitHub Releases, not `Code > Download ZIP`.

After extracting the release ZIP, run:

```text
Start Clip.cmd
```

or:

```text
Clip.exe
```

The source code ZIP from GitHub is for developers. It does not include a ready-to-run app.

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
powershell -ExecutionPolicy Bypass -File .\scripts\Build-PortableRelease.ps1
```

This creates:

```text
release/Clip-win-x64.zip
```

Upload that ZIP to GitHub Releases. Inside the archive, `Start Clip.cmd` and `Clip.exe` are at the top level, so users do not need to search through build folders.

To refresh the bundled tools without building a release:

```powershell
.\scripts\Download-Binaries.ps1
```

## Notes

The tray icon, clipboard monitoring, JSON settings, download history, and process execution are implemented for unpacked development builds. MSIX can be added later, but the portable build is the priority because it keeps bundled process execution simple.
