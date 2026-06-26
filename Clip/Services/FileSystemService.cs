using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Clip.Services;

public sealed partial class FileSystemService
{
    private static readonly HashSet<char> InvalidFileNameChars = Path.GetInvalidFileNameChars().ToHashSet();

    public string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "download";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(InvalidFileNameChars.Contains(character) ? '_' : character);
        }

        var safe = builder.ToString().Trim(' ', '.');
        return string.IsNullOrWhiteSpace(safe) ? "download" : safe;
    }

    public bool OpenFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool OpenContainingFolder(string? outputPath, string? fallbackFolder)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{outputPath}\"")
                {
                    UseShellExecute = true
                });
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fallbackFolder) && Directory.Exists(fallbackFolder))
            {
                Process.Start(new ProcessStartInfo(fallbackFolder) { UseShellExecute = true });
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public string? ResolveDownloadedFile(
        string? reportedPath,
        string? saveFolder,
        string? mediaId = null,
        DateTimeOffset? createdAt = null)
    {
        if (!string.IsNullOrWhiteSpace(reportedPath) && File.Exists(reportedPath))
        {
            return Path.GetFullPath(reportedPath);
        }

        if (string.IsNullOrWhiteSpace(saveFolder) || !Directory.Exists(saveFolder))
        {
            return null;
        }

        mediaId = string.IsNullOrWhiteSpace(mediaId)
            ? ExtractMediaId(reportedPath)
            : mediaId.Trim();

        List<FileInfo> files;
        try
        {
            files = Directory
                .EnumerateFiles(saveFolder)
                .Where(IsFinishedMediaFile)
                .Select(path => new FileInfo(path))
                .Where(file =>
                    createdAt is null ||
                    file.LastWriteTimeUtc >= createdAt.Value.UtcDateTime.AddSeconds(-5))
                .ToList();
        }
        catch
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(mediaId))
        {
            var idToken = $"[{mediaId}]";
            var byId = files
                .Where(file => file.Name.Contains(idToken, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (byId is not null)
            {
                return byId.FullName;
            }
        }

        if (createdAt is not null)
        {
            return files
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }

        return null;
    }

    private static string? ExtractMediaId(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var match = MediaIdRegex().Match(Path.GetFileName(path));
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static bool IsFinishedMediaFile(string path)
    {
        var extension = Path.GetExtension(path);
        return !extension.Equals(".part", StringComparison.OrdinalIgnoreCase) &&
               !extension.Equals(".ytdl", StringComparison.OrdinalIgnoreCase) &&
               !extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) &&
               !Path.GetFileName(path).Contains(".temp.", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\[(?<id>[^\[\]]+)\](?:\.compressed)?\.[^.]+$", RegexOptions.Compiled)]
    private static partial Regex MediaIdRegex();
}
