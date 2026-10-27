using Microsoft.UI.Xaml.Controls;
using Aetheris.Studio.Core.ViewModels;

namespace Aetheris.Studio.Views;

public sealed partial class ProfileDetailView : UserControl
{
    public ProfileDetailView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileDetailViewModel vm)
            vm.LoadedCommand.Execute(null);
    }
}
