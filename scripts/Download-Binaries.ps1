$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "Clip\Resources\bin"
$ffmpegZip = Join-Path $root "ffmpeg-release-essentials.zip"

New-Item -ItemType Directory -Force -Path $bin | Out-Null

$ytdlp = Join-Path $bin "yt-dlp.exe"
Invoke-WebRequest `
  -Uri "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" `
  -OutFile $ytdlp

Invoke-WebRequest `
  -Uri "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" `
  -OutFile $ffmpegZip

$extractRoot = Join-Path $root ".ffmpeg-extract"
if (Test-Path $extractRoot) {
  Remove-Item -LiteralPath $extractRoot -Recurse -Force
}

Expand-Archive -LiteralPath $ffmpegZip -DestinationPath $extractRoot -Force
Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1 | Copy-Item -Destination (Join-Path $bin "ffmpeg.exe") -Force
Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter "ffprobe.exe" | Select-Object -First 1 | Copy-Item -Destination (Join-Path $bin "ffprobe.exe") -Force

Remove-Item -LiteralPath $extractRoot -Recurse -Force
Remove-Item -LiteralPath $ffmpegZip -Force

Write-Host "Bundled tools refreshed in $bin"
