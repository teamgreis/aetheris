using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Aetheris.Studio.Core;

namespace Aetheris.Studio;

public sealed class DialogService : IDialogService
{
    public async Task<string?> PromptForNameAsync(string title)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Erstellen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow?.Content?.XamlRoot,
            Content = new TextBox { PlaceholderText = "Profilname", Margin = new Thickness(0, 8, 0, 0) }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Content is TextBox tb)
            return tb.Text;
        return null;
    }

    public async Task<bool> ConfirmAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Bestätigen",
            Content = message,
            PrimaryButtonText = "Ja",
            CloseButtonText = "Nein",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
