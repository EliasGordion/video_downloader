using Clip.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Numerics;

namespace Clip.Views;

public sealed partial class ContentView : Microsoft.UI.Xaml.Controls.UserControl
{
    private Button? _pressedButton;

    public ContentView()
    {
        InitializeComponent();
        AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnPointerReleased), true);
        AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerReleased), true);
    }

    public MainViewModel Main { get; private set; } = null!;
    public DownloadViewModel Downloads { get; private set; } = null!;
    public SettingsViewModel Settings { get; private set; } = null!;

    public void SetViewModels(MainViewModel main, DownloadViewModel downloads, SettingsViewModel settings)
    {
        Main = main;
        Downloads = downloads;
        Settings = settings;
        Bindings.Update();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _pressedButton = FindParentButton(args.OriginalSource as DependencyObject);
        if (_pressedButton is { IsEnabled: true })
        {
            AnimateButtonScale(_pressedButton, 0.96f);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_pressedButton is not null)
        {
            AnimateButtonScale(_pressedButton, 1f);
            _pressedButton = null;
        }
    }

    private static Button? FindParentButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button button)
            {
                return button;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static void AnimateButtonScale(Button button, float scale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(button);
        visual.CenterPoint = new Vector3(
            (float)button.ActualWidth / 2,
            (float)button.ActualHeight / 2,
            0);

        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3(scale, scale, 1));
        animation.Duration = TimeSpan.FromMilliseconds(120);
        visual.StartAnimation("Scale", animation);
    }
}
