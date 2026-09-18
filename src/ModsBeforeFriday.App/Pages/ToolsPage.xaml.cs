using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.ViewModels;

namespace ModsBeforeFriday.App.Pages;

public sealed partial class ToolsPage : Page
{
    private bool _initialized;

    public ToolsPage()
    {
        InitializeComponent();
        ViewModel = new ToolsViewModel(App.CurrentApp.Backend.Quest, App.CurrentApp.State, App.CurrentApp.Interaction);
        Loaded += ToolsPage_Loaded;
    }

    public ToolsViewModel ViewModel { get; }

    private async void ToolsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await ViewModel.InitializeAsync();
    }
}
