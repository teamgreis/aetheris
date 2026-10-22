using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aetheris.Studio;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetTitleBar(TitleBarGrid);
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