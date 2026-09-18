using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.ViewModels;

namespace ModsBeforeFriday.App.Pages;

public sealed partial class HomePage : Page
{
    private bool _initialized;

    public HomePage()
    {
        InitializeComponent();
        ViewModel = new HomeViewModel(App.CurrentApp.Backend.Quest, App.CurrentApp.State, App.CurrentApp.Interaction);
        Loaded += HomePage_Loaded;
    }

    public HomeViewModel ViewModel { get; }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ViewModel.InitializeAsync();
    }

    private async void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        await ViewModel.SelectVersionAsync((sender as ComboBox)?.SelectedItem as DowngradeChoice);
    }
}
