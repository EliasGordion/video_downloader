using Clip.Models;
using Clip.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Clip.Views;

public sealed partial class DownloadListView : Microsoft.UI.Xaml.Controls.UserControl
{
    public DownloadListView()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.CancelCommand);
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.RetryCommand);
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.OpenFileCommand);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.OpenFolderCommand);
    }

    private void ExecuteItemCommand(object sender, Func<DownloadViewModel, RelayCommand> commandSelector)
    {
        if (sender is not FrameworkElement { DataContext: DownloadItem item } ||
            DataContext is not DownloadViewModel viewModel)
        {
            return;
        }

        var command = commandSelector(viewModel);
        if (command.CanExecute(item))
        {
            command.Execute(item);
        }
    }
}
