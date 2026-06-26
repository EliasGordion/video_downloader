using System.Text.Json;

namespace Clip.Models;

public sealed class DownloadHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public List<DownloadItem> Items { get; set; } = [];

    public static async Task<DownloadHistory> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new DownloadHistory();
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<DownloadHistory>(stream, JsonOptions, cancellationToken)
                ?? new DownloadHistory();
        }
        catch (JsonException)
        {
            return new DownloadHistory();
        }
        catch (IOException)
        {
            return new DownloadHistory();
        }
    }

    public async Task SaveAtomicAsync(string path, CancellationToken cancellationToken = default)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, this, JsonOptions, cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null, true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
