using Clip.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Clip.Views;

public sealed partial class URLInputView : Microsoft.UI.Xaml.Controls.UserControl
{
    public URLInputView()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            !e.DataView.Contains(StandardDataFormats.Text))
        {
            return;
        }

        try
        {
            var text = await e.DataView.GetTextAsync();
            await viewModel.SetUrlFromExternalSourceAsync(text, analyze: false);
        }
        catch
        {
            viewModel.StatusMessage = viewModel.Text.CouldNotReadDroppedText;
        }
    }
}
