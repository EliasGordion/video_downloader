using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Clip.Services;

public sealed class FFmpegService
{
    public async Task<string> CompressToTargetSizeAsync(
        string inputPath,
        double targetSizeMb,
        Action<string> progress,
        CancellationToken cancellationToken)
    {
        EnsureFile(ClipConstants.FfmpegPath, "ffmpeg.exe is missing from Resources\\bin.");
        EnsureFile(ClipConstants.FfprobePath, "ffprobe.exe is missing from Resources\\bin.");
        EnsureFile(inputPath, "The downloaded media file could not be found.");

        var targetBytes = targetSizeMb * 1024 * 1024;
        if (targetBytes >= new FileInfo(inputPath).Length)
        {
            progress("The file already fits the target size.");
            return inputPath;
        }

        var duration = await GetDurationAsync(inputPath, cancellationToken);
        if (duration <= 0)
        {
            throw new InvalidOperationException("Could not read media duration for compression.");
        }

        var extension = Path.GetExtension(inputPath);
        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? "",
            $"{Path.GetFileNameWithoutExtension(inputPath)}.compressed{extension}");

        var targetBits = targetBytes * 8 * 0.96;
        var totalKbps = (int)(targetBits / duration / 1000);

        progress("Compressing...");

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            if (totalKbps < 32)
            {
                throw new InvalidOperationException("The target size is too small for this audio duration.");
            }

            try
            {
                await RunAsync(
                    ClipConstants.FfmpegPath,
                    args =>
                    {
                        args.Add("-y");
                        args.Add("-i");
                        args.Add(inputPath);
                        args.Add("-b:a");
                        args.Add($"{Math.Clamp(totalKbps, 32, 320)}k");
                        args.Add(outputPath);
                    },
                    cancellationToken);
            }
            catch
            {
                DeletePartialFile(outputPath);
                throw;
            }

            return outputPath;
        }

        var audioKbps = Math.Min(160, Math.Max(64, totalKbps / 5));
        var videoKbps = totalKbps - audioKbps;
        if (videoKbps < 100)
        {
            throw new InvalidOperationException("The target size is too small for this video duration.");
        }

        try
        {
            await RunAsync(
                ClipConstants.FfmpegPath,
                args =>
                {
                    args.Add("-y");
                    args.Add("-i");
                    args.Add(inputPath);
                    args.Add("-c:v");
                    args.Add(extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                        ? "libvpx-vp9"
                        : "libx264");
                    if (extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Add("-deadline");
                        args.Add("good");
                        args.Add("-cpu-used");
                        args.Add("2");
                    }
                    else
                    {
                        args.Add("-preset");
                        args.Add("medium");
                    }

                    args.Add("-b:v");
                    args.Add($"{videoKbps}k");
                    args.Add("-maxrate");
                    args.Add($"{videoKbps}k");
                    args.Add("-bufsize");
                    args.Add($"{videoKbps * 2}k");
                    args.Add("-c:a");
                    args.Add(extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                        ? "libopus"
                        : "aac");
                    args.Add("-b:a");
                    args.Add($"{audioKbps}k");
                    args.Add(outputPath);
                },
                cancellationToken);
        }
        catch
        {
            DeletePartialFile(outputPath);
            throw;
        }

        return outputPath;
    }

    private static async Task<double> GetDurationAsync(string inputPath, CancellationToken cancellationToken)
    {
        var result = await RunCaptureAsync(
            ClipConstants.FfprobePath,
            args =>
            {
                args.Add("-v");
                args.Add("quiet");
                args.Add("-print_format");
                args.Add("json");
                args.Add("-show_format");
                args.Add(inputPath);
            },
            cancellationToken);

        using var document = JsonDocument.Parse(result);
        if (document.RootElement.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durationElement) &&
            double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        return 0;
    }

    private static async Task RunAsync(
        string fileName,
        Action<ICollection<string>> configureArguments,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(fileName, configureArguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildProcessError(
                result.Error,
                "ffmpeg could not finish the conversion."));
        }
    }

    private static async Task<string> RunCaptureAsync(
        string fileName,
        Action<ICollection<string>> configureArguments,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(fileName, configureArguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("ffprobe could not read the media file.");
        }

        return result.Output;
    }

    private static string BuildProcessError(string rawError, string fallback)
    {
        var error = rawError
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(error) ? fallback : $"{fallback} {error}";
    }

    private static void DeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        Action<ICollection<string>> configureArguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = AppContext.BaseDirectory
            }
        };

        var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        process.StartInfo.Environment["PATH"] = $"{ClipConstants.BinaryDirectory};{existingPath};C:\\Windows\\System32;C:\\Windows";
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
