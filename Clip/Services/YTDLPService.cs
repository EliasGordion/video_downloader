using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clip.Models;

namespace Clip.Services;

public sealed partial class YTDLPService
{
    private static readonly TimeSpan DownloadStartupTimeout = TimeSpan.FromSeconds(60);
    private readonly FileSystemService _fileSystemService;

    public YTDLPService(FileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public async Task<VideoMetadata> AnalyzeAsync(
        string url,
        Platform platform,
        bool useBrowserCookies,
        string preferredBrowser,
        CancellationToken cancellationToken)
    {
        EnsureFile(ClipConstants.YtdlpPath, "yt-dlp.exe is missing from Resources\\bin.");

        var inputUrl = url;

        async Task<ProcessResult> AnalyzeWithCookieBrowserAsync(string cookieBrowser)
        {
            return await RunCaptureAsync(
                ClipConstants.YtdlpPath,
                args =>
                {
                    args.Add("--dump-json");
                    args.Add("--no-playlist");
                    args.Add("--no-warnings");
                    args.Add("--skip-download");
                    AddCookieArguments(args, !string.IsNullOrWhiteSpace(cookieBrowser), cookieBrowser);
                    args.Add(inputUrl);
                },
                cancellationToken);
        }

        var result = await AnalyzeWithCookieBrowserAsync("");
        if (result.ExitCode != 0 &&
            useBrowserCookies &&
            IsCookieOrAccessError(result.Error))
        {
            foreach (var cookieBrowser in GetCookieBrowserAttempts(true, preferredBrowser))
            {
                result = await AnalyzeWithCookieBrowserAsync(cookieBrowser);

                if (result.ExitCode == 0 ||
                    !ShouldTryNextCookieBrowser(result.Error, true, preferredBrowser))
                {
                    break;
                }
            }
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(GetActionableError(
                result.Error,
                "Could not analyze this URL.",
                useBrowserCookies,
                preferredBrowser));
        }

        try
        {
            using var document = JsonDocument.Parse(result.Output);
            var root = document.RootElement;
            var metadata = new VideoMetadata
            {
                Title = GetString(root, "title") ?? "Untitled video",
                Author = GetString(root, "uploader") ?? GetString(root, "channel") ?? "",
                ThumbnailUrl = GetString(root, "thumbnail") ?? "",
                SourceUrl = GetString(root, "webpage_url") ?? url,
                DurationSeconds = GetDouble(root, "duration") ?? 0,
                Platform = platform
            };

            metadata.Formats = ParseFormats(root);
            return metadata;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("yt-dlp returned metadata in an unexpected format.", ex);
        }
    }

    public async Task<string> DownloadAsync(
        DownloadItem item,
        Action<YtdlpProgress> progress,
        CancellationToken cancellationToken)
    {
        EnsureFile(ClipConstants.YtdlpPath, "yt-dlp.exe is missing from Resources\\bin.");
        EnsureFile(ClipConstants.FfmpegPath, "ffmpeg.exe is missing from Resources\\bin.");

        Directory.CreateDirectory(item.SaveFolder);

        var inputUrl = item.Url;

        async Task<DownloadAttemptResult> RunWithFormatFallbackAsync(DownloadItem attempt)
        {
            var attemptResult = await RunDownloadAttemptAsync(
                attempt,
                inputUrl,
                progress,
                useFallbackFormat: false,
                cancellationToken);

            if (attemptResult.ExitCode == 0 || !IsFormatUnavailable(attemptResult.RawError))
            {
                return attemptResult;
            }

            progress(new YtdlpProgress(0, "Trying another available format...", ""));
            return await RunDownloadAttemptAsync(
                attempt,
                inputUrl,
                progress,
                useFallbackFormat: true,
                cancellationToken);
        }

        var publicAttempt = item.CloneForRetry();
        publicAttempt.UseBrowserCookies = false;
        publicAttempt.PreferredBrowser = "";

        var result = await RunWithFormatFallbackAsync(publicAttempt);
        if (result.ExitCode == 0)
        {
            return result.OutputPath;
        }

        if (item.UseBrowserCookies && IsCookieOrAccessError(result.RawError))
        {
            foreach (var cookieBrowser in GetCookieBrowserAttempts(true, item.PreferredBrowser))
            {
                progress(new YtdlpProgress(
                    0,
                    $"This video needs access. Trying {BrowserDisplayName(cookieBrowser)} cookies...",
                    ""));

                var cookieAttempt = item.CloneForRetry();
                cookieAttempt.UseBrowserCookies = true;
                cookieAttempt.PreferredBrowser = cookieBrowser;

                result = await RunWithFormatFallbackAsync(cookieAttempt);
                if (result.ExitCode == 0)
                {
                    return result.OutputPath;
                }

                if (!ShouldTryNextCookieBrowser(result.RawError, true, item.PreferredBrowser))
                {
                    break;
                }
            }
        }

        throw new InvalidOperationException(GetActionableError(
            result?.RawError ?? "",
            "Download failed.",
            item.UseBrowserCookies,
            item.PreferredBrowser));
    }

    private async Task<DownloadAttemptResult> RunDownloadAttemptAsync(
        DownloadItem item,
        string inputUrl,
        Action<YtdlpProgress> progress,
        bool useFallbackFormat,
        CancellationToken cancellationToken)
    {
        var outputPath = "";
        var mediaId = "";
        var currentPercent = 0d;
        var outputLock = new object();
        var attemptStartedAt = DateTimeOffset.UtcNow;
        var downloadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var process = CreateProcess(ClipConstants.YtdlpPath);
        AddDownloadArguments(process.StartInfo.ArgumentList, item, inputUrl, useFallbackFormat);

        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var parsedMediaId = TryParseMediaId(line);
            if (!string.IsNullOrWhiteSpace(parsedMediaId))
            {
                lock (outputLock)
                {
                    mediaId = parsedMediaId;
                }

                downloadStarted.TrySetResult();
                progress(new YtdlpProgress(
                    currentPercent,
                    item.IsClip ? "Preparing clip..." : "Preparing download...",
                    outputPath));
            }

            var parsedPath = TryParseOutputPath(line);
            if (!string.IsNullOrWhiteSpace(parsedPath))
            {
                lock (outputLock)
                {
                    outputPath = parsedPath;
                }

                progress(new YtdlpProgress(currentPercent, "Downloading...", parsedPath));
                downloadStarted.TrySetResult();
                return;
            }

            var percent = TryParsePercent(line);
            if (percent is not null)
            {
                downloadStarted.TrySetResult();
                currentPercent = Math.Min(percent.Value, 99.5);
                progress(new YtdlpProgress(currentPercent, BuildProgressMessage(line, currentPercent), outputPath));
                return;
            }

            if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(Math.Min(currentPercent, 99.5), "Merging streams...", outputPath));
            }
            else if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(Math.Min(currentPercent, 99.5), "Extracting audio...", outputPath));
            }
            else if (line.Contains("[VideoRemuxer]", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(Math.Min(currentPercent, 99.5), "Remuxing video...", outputPath));
            }
            else if (line.Contains("[Fixup", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("[MoveFiles]", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(Math.Min(currentPercent, 99.5), "Finalizing file...", outputPath));
            }
            else if (line.Contains("[download]", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(currentPercent, "Downloading...", outputPath));
            }
            else if (line.Contains("Downloading 1 time ranges", StringComparison.OrdinalIgnoreCase))
            {
                progress(new YtdlpProgress(currentPercent, "Preparing selected range...", outputPath));
            }
        }

        var errorBuilder = new StringBuilder();
        process.OutputDataReceived += (_, args) => HandleLine(args.Data);
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                errorBuilder.AppendLine(args.Data);
            }

            HandleLine(args.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start yt-dlp.");
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var startupTimeoutTask = Task.Delay(DownloadStartupTimeout, cancellationToken);
            var startupResult = await Task.WhenAny(
                exitTask,
                downloadStarted.Task,
                startupTimeoutTask);

            if (startupResult == startupTimeoutTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                process.WaitForExit();

                return new DownloadAttemptResult(
                    -1,
                    "",
                    errorBuilder.AppendLine(
                        "ERROR: yt-dlp timed out before the download started.")
                        .ToString());
            }

            await exitTask;
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            return new DownloadAttemptResult(process.ExitCode, "", errorBuilder.ToString());
        }

        lock (outputLock)
        {
            var resolvedPath = _fileSystemService.ResolveDownloadedFile(
                outputPath,
                item.SaveFolder,
                mediaId,
                attemptStartedAt);

            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return new DownloadAttemptResult(process.ExitCode, resolvedPath, errorBuilder.ToString());
            }
        }

        return new DownloadAttemptResult(
            -1,
            "",
            errorBuilder.AppendLine("ERROR: yt-dlp finished without reporting the output file.").ToString());
    }

    private static void AddDownloadArguments(
        ICollection<string> args,
        DownloadItem item,
        string inputUrl,
        bool useFallbackFormat)
    {
        args.Add("--newline");
        args.Add("--no-color");
        args.Add("--socket-timeout");
        args.Add("30");
        args.Add("--retries");
        args.Add("3");
        args.Add("--fragment-retries");
        args.Add("3");
        args.Add("--progress");
        args.Add("--progress-template");
        args.Add("download:clip-progress:%(progress._percent_str)s:%(progress._eta_str)s");
        args.Add("--print");
        args.Add("before_dl:clip-media-id:%(id)s");
        args.Add("--print");
        args.Add("after_move:clip-output:%(filepath)s");
        args.Add("--encoding");
        args.Add("utf-8");
        args.Add("--no-playlist");
        args.Add("--windows-filenames");
        args.Add("--no-mtime");
        args.Add("--ffmpeg-location");
        args.Add(ClipConstants.BinaryDirectory);
        args.Add("--paths");
        args.Add(item.SaveFolder);
        args.Add("--output");
        args.Add("%(title).180B [%(id)s].%(ext)s");

        AddFormatArguments(args, item, useFallbackFormat);

        if (item.IsClip && item.ClipRange is not null)
        {
            args.Add("--download-sections");
            args.Add($"*{FormatTimestamp(item.ClipRange.StartSeconds)}-{FormatTimestamp(item.ClipRange.EndSeconds)}");
            args.Add("--force-keyframes-at-cuts");
        }

        AddCookieArguments(args, item.UseBrowserCookies, item.PreferredBrowser);

        args.Add(inputUrl);
    }

    private static void AddFormatArguments(
        ICollection<string> args,
        DownloadItem item,
        bool useFallbackFormat)
    {
        var height = ResolutionToHeight(item.Resolution);

        if (item.Format.Equals("mp3", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-f");
            args.Add("bestaudio/best");
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("--audio-quality");
            args.Add("0");
            return;
        }

        if (item.Format.Equals("webm", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-f");
            args.Add(useFallbackFormat
                ? BuildGenericSelector(height)
                : BuildHeightSelector("bv*[ext=webm]+ba[ext=webm]/b[ext=webm]/best", height));
            args.Add("--merge-output-format");
            args.Add("webm");
            args.Add("--recode-video");
            args.Add("webm");
            return;
        }

        if (item.Format.Equals("mov", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-f");
            args.Add(useFallbackFormat
                ? BuildGenericSelector(height)
                : BuildHeightSelector("bv*+ba/best", height));
            args.Add("--remux-video");
            args.Add("mov");
            return;
        }

        args.Add("-f");
        args.Add(useFallbackFormat ? BuildGenericSelector(height) : BuildMp4Selector(height));
        args.Add("--merge-output-format");
        args.Add("mp4");
        args.Add("--recode-video");
        args.Add("mp4");
    }

    private static string BuildHeightSelector(string selector, int? height)
    {
        if (height is null)
        {
            return selector;
        }

        return selector
            .Replace("bv*", $"bv*[height<={height.Value}]")
            .Replace("b[", $"b[height<={height.Value}][")
            .Replace("best", $"best[height<={height.Value}]/best");
    }

    private static string BuildMp4Selector(int? height)
    {
        var heightFilter = height is null ? "" : $"[height<={height.Value}]";
        return string.Join(
            '/',
            $"bv*{heightFilter}[ext=mp4][vcodec^=avc1]+ba[ext=m4a]",
            $"bv*{heightFilter}[ext=mp4]+ba[ext=m4a]",
            $"b{heightFilter}[ext=mp4]",
            $"best{heightFilter}[ext=mp4]",
            $"best{heightFilter}",
            "best");
    }

    private static string BuildGenericSelector(int? height)
    {
        var heightFilter = height is null ? "" : $"[height<={height.Value}]";
        return string.Join(
            '/',
            $"bv*{heightFilter}+ba",
            $"b{heightFilter}",
            $"best{heightFilter}",
            "best");
    }

    private static int? ResolutionToHeight(string resolution)
    {
        return resolution switch
        {
            "4K" => 2160,
            "1440p" => 1440,
            "1080p" => 1080,
            "720p" => 720,
            "480p" => 480,
            "360p" => 360,
            _ => null
        };
    }

    private static string FormatTimestamp(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss\.fff")
            : time.ToString(@"m\:ss\.fff");
    }

    private static Process CreateProcess(string fileName)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };

        PrependBinaryDirectoryToPath(startInfo);
        return new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    private static async Task<ProcessResult> RunCaptureAsync(
        string fileName,
        Action<ICollection<string>> configureArguments,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(fileName);
        configureArguments(process.StartInfo.ArgumentList);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {Path.GetFileName(fileName)}.");
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static void AddCookieArguments(ICollection<string> args, bool useBrowserCookies, string preferredBrowser)
    {
        if (!useBrowserCookies)
        {
            return;
        }

        var browser = NormalizeCookieBrowser(preferredBrowser);
        if (string.IsNullOrWhiteSpace(browser))
        {
            return;
        }

        args.Add("--cookies-from-browser");
        args.Add(browser);
    }

    private static IReadOnlyList<string> GetCookieBrowserAttempts(bool useBrowserCookies, string preferredBrowser)
    {
        if (!useBrowserCookies)
        {
            return [""];
        }

        return IsAutoCookieBrowser(preferredBrowser)
            ? ["edge", "chrome", "brave", "firefox"]
            : [NormalizeCookieBrowser(preferredBrowser)];
    }

    private static bool IsAutoCookieBrowser(string? preferredBrowser)
    {
        return string.IsNullOrWhiteSpace(preferredBrowser) ||
               preferredBrowser.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
               preferredBrowser.Equals("Default", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCookieBrowser(string? preferredBrowser)
    {
        var browser = (preferredBrowser ?? "").Trim().ToLowerInvariant();
        return browser switch
        {
            "" => "",
            "auto" => "edge",
            "default" => "edge",
            "microsoft edge" => "edge",
            "edge" => "edge",
            "google chrome" => "chrome",
            "chrome" => "chrome",
            "firefox" => "firefox",
            "mozilla firefox" => "firefox",
            "brave" => "brave",
            "brave browser" => "brave",
            _ => browser
        };
    }

    private static string BrowserDisplayName(string preferredBrowser)
    {
        return NormalizeCookieBrowser(preferredBrowser) switch
        {
            "edge" => "Edge",
            "chrome" => "Chrome",
            "firefox" => "Firefox",
            "brave" => "Brave",
            _ => "browser"
        };
    }

    private static bool ShouldTryNextCookieBrowser(string rawError, bool useBrowserCookies, string preferredBrowser)
    {
        return useBrowserCookies &&
               IsAutoCookieBrowser(preferredBrowser) &&
               (IsCookieOrAccessError(rawError) ||
                rawError.Contains(
                    "timed out before the download started",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCookieOrAccessError(string rawError)
    {
        return IsBrowserCookieFailure(rawError) ||
               rawError.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase) ||
               rawError.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) ||
               rawError.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
               rawError.Contains("login", StringComparison.OrdinalIgnoreCase) ||
               rawError.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
               rawError.Contains("cookies", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrependBinaryDirectoryToPath(ProcessStartInfo startInfo)
    {
        var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var fallback = @"C:\Windows\System32;C:\Windows";
        startInfo.Environment["PATH"] = $"{ClipConstants.BinaryDirectory};{existingPath};{fallback}";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
    }

    private static string? TryParseOutputPath(string line)
    {
        foreach (var regex in new[] { OutputMarkerRegex(), DestinationRegex(), QuotedOutputRegex() })
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                return match.Groups["path"].Value.Trim();
            }
        }

        return null;
    }

    private static string? TryParseMediaId(string line)
    {
        var match = MediaIdMarkerRegex().Match(line);
        return match.Success ? match.Groups["id"].Value.Trim() : null;
    }

    private static double? TryParsePercent(string line)
    {
        var match = ProgressRegex().Match(line);
        if (!match.Success)
        {
            match = ProgressTemplateRegex().Match(line);
        }

        var value = match.Success
            ? match.Groups["percent"].Value.Replace(',', '.')
            : "";

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : null;
    }

    private static string BuildProgressMessage(string line, double percent)
    {
        var eta = EtaRegex().Match(line);
        if (!eta.Success)
        {
            eta = ProgressTemplateEtaRegex().Match(line);
        }

        return eta.Success
            ? $"Downloading... {percent:0.#}% ETA {eta.Groups["eta"].Value.Trim()}"
            : $"Downloading... {percent:0.#}%";
    }

    private static List<FormatOption> ParseFormats(JsonElement root)
    {
        var formats = new List<FormatOption>();
        if (root.TryGetProperty("formats", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var ext = GetString(item, "ext");
                if (!string.IsNullOrWhiteSpace(ext) &&
                    formats.All(format => !format.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    formats.Add(new FormatOption(ext.ToUpperInvariant(), ext));
                }
            }
        }

        return formats.Count == 0
            ? [new("MP4", "mp4"), new("WebM", "webm"), new("MP3", "mp3", true)]
            : formats;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }

    private static void EnsureFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(message, path);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string GetActionableError(
        string rawError,
        string fallback,
        bool useBrowserCookies,
        string preferredBrowser)
    {
        if (IsBrowserCookieFailure(rawError))
        {
            return useBrowserCookies
                ? $"Browser cookies are enabled, but Windows blocked cookie decryption for {CookieBrowserDescription(preferredBrowser)}. Choose another browser in Settings, or turn cookies off for public videos."
                : "Browser cookies could not be decrypted with Windows DPAPI. Try turning off browser cookies for public videos, or run Clip under the same Windows user that owns the browser profile.";
        }

        if (rawError.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase))
        {
            return useBrowserCookies
                ? $"This video is unavailable (HTTP 410). Browser cookies are enabled for {CookieBrowserDescription(preferredBrowser)}, but the site still refused access. The video may be deleted, private, expired, or signed in under another browser profile."
                : "This video is unavailable (HTTP 410). It may be deleted, private, expired, or require browser cookies. Turn on browser cookies and choose the browser where you are signed in.";
        }

        if (rawError.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase))
        {
            return useBrowserCookies
                ? $"Access was denied by the site (HTTP 403). Browser cookies are enabled for {CookieBrowserDescription(preferredBrowser)}, but that session was not accepted. Choose the browser where you are signed in and try again."
                : "Access was denied by the site (HTTP 403). Turn on browser cookies, choose the browser where you are signed in, and try again.";
        }

        if (rawError.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
            rawError.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            rawError.Contains("cookies", StringComparison.OrdinalIgnoreCase))
        {
            return useBrowserCookies
                ? $"This site needs an active browser session. Browser cookies are enabled for {CookieBrowserDescription(preferredBrowser)}, but Clip could not use a signed-in session. Choose the browser where you are signed in."
                : "This site needs an active browser session. Turn on browser cookies and choose the browser where you are signed in.";
        }

        if (IsFormatUnavailable(rawError))
        {
            return "The site did not provide a downloadable format. Try again without browser cookies. If the problem continues, refresh the bundled yt-dlp files.";
        }

        var lines = rawError
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Reverse();

        var error = lines.FirstOrDefault(line =>
            line.Contains("ERROR:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("HTTP Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(error))
        {
            return fallback;
        }

        return error.Replace("ERROR:", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string CookieBrowserDescription(string preferredBrowser)
    {
        return IsAutoCookieBrowser(preferredBrowser)
            ? "Auto"
            : BrowserDisplayName(preferredBrowser);
    }

    private static bool IsBrowserCookieFailure(string text)
    {
        return text.Contains("failed to decrypt with DPAPI", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("DPAPI", StringComparison.OrdinalIgnoreCase) ||
               (text.Contains("NoneType", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("decode", StringComparison.OrdinalIgnoreCase)) ||
               (text.Contains("could not decrypt", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("cookie", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFormatUnavailable(string text)
    {
        return text.Contains("Requested format is not available", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("No video formats found", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("only images are available", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?<percent>\d+(?:[\.,]\d+)?)\s*%", RegexOptions.Compiled)]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"ETA\s+(?<eta>\S+)", RegexOptions.Compiled)]
    private static partial Regex EtaRegex();

    [GeneratedRegex(@"clip-progress:\s*(?<percent>\d+(?:[\.,]\d+)?)\s*%", RegexOptions.Compiled)]
    private static partial Regex ProgressTemplateRegex();

    [GeneratedRegex(@"clip-progress:[^:]*:(?<eta>.+)$", RegexOptions.Compiled)]
    private static partial Regex ProgressTemplateEtaRegex();

    [GeneratedRegex(@"Destination:\s+(?<path>.+)$", RegexOptions.Compiled)]
    private static partial Regex DestinationRegex();

    [GeneratedRegex(@"^clip-output:(?<path>.+)$", RegexOptions.Compiled)]
    private static partial Regex OutputMarkerRegex();

    [GeneratedRegex(@"^clip-media-id:(?<id>.+)$", RegexOptions.Compiled)]
    private static partial Regex MediaIdMarkerRegex();

    [GeneratedRegex(@"""(?<path>[A-Za-z]:\\[^""]+)""", RegexOptions.Compiled)]
    private static partial Regex QuotedOutputRegex();

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
    private sealed record DownloadAttemptResult(int ExitCode, string OutputPath, string RawError);
}

public sealed record YtdlpProgress(double Percent, string Message, string OutputPath);
