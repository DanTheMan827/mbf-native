using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.ViewModels;

namespace ModsBeforeFriday.App.Pages;

public sealed partial class ModsPage : Page
{
    private bool _initialized;

    public ModsPage()
    {
        InitializeComponent();
        ViewModel = new ModsViewModel(
            App.CurrentApp.Backend.Quest,
            App.CurrentApp.Backend.Mods,
            App.CurrentApp.State,
            App.CurrentApp.Interaction);
        Loaded += ModsPage_Loaded;
    }

    public ModsViewModel ViewModel { get; }

    private async void ModsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await ViewModel.InitializeAsync();
    }

    private async void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InstalledModItemViewModel item)
        {
            await ViewModel.RemoveAsync(item);
        }
    }

    private async void InstallMod_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CatalogModItemViewModel item)
        {
            await ViewModel.InstallAsync(item);
        }
    }

    private async void OpenSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CatalogModItemViewModel item)
        {
            await ViewModel.OpenSourceAsync(item);
        }
    }
}
