using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Aetheris.Studio.Core;

namespace Aetheris.Studio;

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> SaveFileAsync(string suggestedFileName, string defaultExtension, IEnumerable<(string Name, string[] Extensions)> fileTypeChoices)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = defaultExtension
        };

        foreach (var (name, extensions) in fileTypeChoices)
        {
            picker.FileTypeChoices.Add(name, extensions);
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> OpenFileAsync(string defaultExtension, IEnumerable<(string Name, string[] Extensions)> fileTypeChoices)
    {
        var picker = new FileOpenPicker
        {
            DefaultFileExtension = defaultExtension
        };

        foreach (var (name, extensions) in fileTypeChoices)
        {
            picker.FileTypeFilter.Add(name, extensions);
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
