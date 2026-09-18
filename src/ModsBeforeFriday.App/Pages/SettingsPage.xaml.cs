using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.ViewModels;

namespace ModsBeforeFriday.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(App.CurrentApp.State, App.CurrentApp.SettingsStore, App.CurrentApp.Interaction);
    }

    public SettingsViewModel ViewModel { get; }
}
