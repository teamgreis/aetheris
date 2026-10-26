using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aetheris.Studio;

public sealed partial class MainWindow : Window
{
    private const int MinWindowWidth = 1000;
    private const int MinWindowHeight = 700;

    public MainWindow()
    {
        InitializeComponent();
        SetTitleBar(TitleBarGrid);

        // Enforce minimum window size (XAML MinWidth/MinHeight are not
        // available on WinUI 3 Window, so clamp on size changes instead).
        AppWindow.Changed += OnAppWindowChanged;
    }

    private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange) return;

        var size = sender.Size;
        if (size.Width >= MinWindowWidth && size.Height >= MinWindowHeight) return;

        sender.ResizeClient(new Windows.Graphics.SizeInt32(
            Math.Max(size.Width, MinWindowWidth),
            Math.Max(size.Height, MinWindowHeight)));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) { }
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Minimize();
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}