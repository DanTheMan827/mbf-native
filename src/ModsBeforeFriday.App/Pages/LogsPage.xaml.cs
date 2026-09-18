using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.ViewModels;

namespace ModsBeforeFriday.App.Pages;

public sealed partial class LogsPage : Page
{
    public LogsPage()
    {
        InitializeComponent();
        ViewModel = new LogsViewModel(App.CurrentApp.Backend.Quest, App.CurrentApp.State, App.CurrentApp.Interaction);
    }

    public LogsViewModel ViewModel { get; }
}
