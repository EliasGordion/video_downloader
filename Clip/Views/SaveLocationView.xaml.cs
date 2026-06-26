using Clip.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Clip.Views;

public sealed partial class SaveLocationView : Microsoft.UI.Xaml.Controls.UserControl
{
    public SaveLocationView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settings || App.MainAppWindow is null)
        {
            return;
        }

        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                settings.DefaultSaveFolder = folder.Path;
            }
        }
        catch
        {
            // The picker can be cancelled while the window is closing.
        }
    }
}
