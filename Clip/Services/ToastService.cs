using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Clip.Services;

public sealed class ToastService
{
    public void Register()
    {
        try
        {
            AppNotificationManager.Default.Register();
        }
        catch
        {
            // Toast registration can fail for unpackaged builds. The app still works.
        }
    }

    public void Show(string title, string body)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Toasts are optional.
        }
    }

    public void Unregister()
    {
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
        }
    }
}
