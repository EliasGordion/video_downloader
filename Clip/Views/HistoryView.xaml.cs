using Clip.Models;
using Clip.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Clip.Views;

public sealed partial class HistoryView : Microsoft.UI.Xaml.Controls.UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.OpenFileCommand);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.OpenFolderCommand);
    }

    private void OnRemoveFromHistoryClick(object sender, RoutedEventArgs e)
    {
        ExecuteItemCommand(sender, viewModel => viewModel.RemoveHistoryItemCommand);
    }

    private void ExecuteItemCommand(object sender, Func<DownloadViewModel, ICommand> commandSelector)
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
