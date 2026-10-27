using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Aetheris.Studio.Core.ViewModels;

namespace Aetheris.Studio;

public sealed partial class MainWindow : Window
{
    public AppViewModel? ViewModel { get; set; }

    private const int MinWindowWidth = 1000;
    private const int MinWindowHeight = 700;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
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

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogService = App.Services?.GetService<Core.IDialogService>();
        if (dialogService is null) return;

        await dialogService.ShowErrorAsync(
            "Aetheris Studio",
            "Version 1.0.0\n\n" +
            "Profile werden verschlüsselt (AES-256-GCM, DPAPI) abgelegt unter:\n" +
            "%LOCALAPPDATA%\\Aetheris\\Profiles\n\n" +
            "QR-Transfer: Aetheris Mobile öffnen und Chunked QR scannen.");
    }

    private OverlappedPresenter? Presenter => AppWindow?.Presenter as OverlappedPresenter;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Presenter?.Minimize();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Presenter?.State == OverlappedPresenterState.Maximized)
            Presenter?.Restore();
        else
            Presenter?.Maximize();
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}