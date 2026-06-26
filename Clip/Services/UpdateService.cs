namespace Clip.Services;

public sealed class UpdateService
{
    public Task<UpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateInfo(false, "", ""));
    }
}

public sealed record UpdateInfo(bool HasUpdate, string Version, string Url);
