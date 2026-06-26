using System.Text.Json;
using Windows.Storage;
using Clip.Models;

namespace Clip.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public SettingsService()
    {
        LocalFolderPath = GetLocalFolderPath();
        SettingsPath = Path.Combine(LocalFolderPath, ClipConstants.SettingsFileName);
        HistoryPath = Path.Combine(LocalFolderPath, ClipConstants.HistoryFileName);
    }

    public string LocalFolderPath { get; }
    public string SettingsPath { get; }
    public string HistoryPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(LocalFolderPath);
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(LocalFolderPath);
        var tempPath = SettingsPath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }

        if (File.Exists(SettingsPath))
        {
            File.Replace(tempPath, SettingsPath, null, true);
        }
        else
        {
            File.Move(tempPath, SettingsPath);
        }
    }

    private static string GetLocalFolderPath()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ClipConstants.AppName);
        }
    }
}
