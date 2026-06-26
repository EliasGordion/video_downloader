$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Clip"
$tools = Join-Path $root "Clip\Resources\bin"
$releaseRoot = Join-Path $root "release"
$packageName = "Clip-win-x64"
$publishDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"

$requiredTools = @(
  "yt-dlp.exe",
  "ffmpeg.exe",
  "ffprobe.exe"
)

$missingTools = $requiredTools | Where-Object {
  -not (Test-Path -LiteralPath (Join-Path $tools $_))
}

if ($missingTools.Count -gt 0) {
  & (Join-Path $PSScriptRoot "Download-Binaries.ps1")
}

foreach ($tool in $requiredTools) {
  $path = Join-Path $tools $tool
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing required tool: $path"
  }
}

if (Test-Path -LiteralPath $publishDir) {
  Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
  Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

dotnet restore $project
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=false `
  -o $publishDir

$expectedOutput = @(
  "Clip.exe",
  "Resources\bin\yt-dlp.exe",
  "Resources\bin\ffmpeg.exe",
  "Resources\bin\ffprobe.exe"
)

foreach ($item in $expectedOutput) {
  $path = Join-Path $publishDir $item
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing release file: $path"
  }
}

$launcher = Join-Path $publishDir "Start Clip.cmd"
Set-Content -LiteralPath $launcher -Encoding ASCII -Value @(
  "@echo off",
  "start """" ""%~dp0Clip.exe"""
)

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Release folder: $publishDir"
Write-Host "Release ZIP:    $zipPath"
Write-Host "Upload the ZIP to GitHub Releases. Users should unzip it and run Start Clip.cmd or Clip.exe."
