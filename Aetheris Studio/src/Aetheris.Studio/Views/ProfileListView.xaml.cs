using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Aetheris.Studio.Core.ViewModels;

namespace Aetheris.Studio.Views;

public sealed partial class ProfileListView : UserControl
{
    public ProfileListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileListViewModel vm)
            vm.LoadedCommand.Execute(null);
    }
}