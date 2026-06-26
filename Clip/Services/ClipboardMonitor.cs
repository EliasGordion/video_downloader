using Windows.ApplicationModel.DataTransfer;
using Clip.Models;
using Microsoft.UI.Dispatching;

namespace Clip.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private readonly DispatcherQueueTimer _timer;
    private readonly URLDetector _urlDetector;
    private string _lastText = "";
    private bool _isEnabled;

    public ClipboardMonitor(DispatcherQueue dispatcherQueue, URLDetector urlDetector)
    {
        _urlDetector = urlDetector;
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1.5);
        _timer.Tick += OnTick;
    }

    public event EventHandler<string>? UrlDetected;

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (enabled)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            var package = Clipboard.GetContent();
            if (!package.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var text = await package.GetTextAsync();
            if (string.Equals(text, _lastText, StringComparison.Ordinal))
            {
                return;
            }

            _lastText = text;
            if (_urlDetector.TryGetVideoUrl(text, out var url, out var platform) &&
                platform != Platform.Unknown)
            {
                UrlDetected?.Invoke(this, url);
            }
        }
        catch
        {
            // Clipboard access can fail while another process owns it.
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
