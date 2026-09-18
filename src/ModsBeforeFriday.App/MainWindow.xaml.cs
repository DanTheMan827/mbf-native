using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using ModsBeforeFriday.App.Pages;

namespace ModsBeforeFriday.App;

public sealed partial class MainWindow : Window
{
    private bool _wasModdedDeviceConnected;

    public MainWindow()
    {
        InitializeComponent();
        RootLayout.DataContext = App.CurrentApp.State;
        App.CurrentApp.State.PropertyChanged += State_PropertyChanged;
        App.CurrentApp.State.CurrentOperationLogs.CollectionChanged += CurrentOperationLogs_CollectionChanged;
        _wasModdedDeviceConnected = App.CurrentApp.State.IsModdedDeviceConnected;
        UpdateNavigationAvailability();
        UpdateOperationInteractivity();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        ConfigureTitleBar();
    }

    private void ConfigureTitleBar()
    {
        var titleBar = AppWindow.TitleBar;

        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonBackgroundColor = Colors.Transparent;

        titleBar.ButtonHoverForegroundColor = Colors.White;
        titleBar.ButtonHoverBackgroundColor =
            ColorHelper.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

        titleBar.ButtonPressedForegroundColor = Colors.White;
        titleBar.ButtonPressedBackgroundColor =
            ColorHelper.FromArgb(0x55, 0xFF, 0xFF, 0xFF);

        titleBar.ButtonInactiveForegroundColor =
            ColorHelper.FromArgb(0x99, 0xFF, 0xFF, 0xFF);

        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    public void NavigateHome()
    {
        RootNavigation.SelectedItem = HomeNavigationItem;
        Navigate(typeof(HomePage));
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        if ((tag is "mods" or "tools") && !App.CurrentApp.State.IsModdedDeviceConnected)
        {
            NavigateHome();
            return;
        }

        Navigate(tag switch
        {
            "home" => typeof(HomePage),
            "mods" => typeof(ModsPage),
            "tools" => typeof(ToolsPage),
            "logs" => typeof(LogsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage),
        });
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModsBeforeFriday.Application.Services.AppState.IsAgentOperationActive))
        {
            UpdateOperationInteractivity();
            return;
        }

        if (e.PropertyName != nameof(ModsBeforeFriday.Application.Services.AppState.IsModdedDeviceConnected))
        {
            return;
        }

        var isModded = App.CurrentApp.State.IsModdedDeviceConnected;
        UpdateNavigationAvailability();

        if (isModded && !_wasModdedDeviceConnected)
        {
            // Defer navigation until the status-setting call has unwound so the Home view model
            // can finish applying the verified status before the Mods page starts loading.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!App.CurrentApp.State.IsModdedDeviceConnected)
                {
                    return;
                }

                RootNavigation.SelectedItem = ModsNavigationItem;
                Navigate(typeof(ModsPage));
            });
        }
        else if (!isModded
                 && RootNavigation.SelectedItem is NavigationViewItem item
                 && item.Tag is string tag
                 && tag is "mods" or "tools")
        {
            NavigateHome();
        }

        _wasModdedDeviceConnected = isModded;
    }

    private void UpdateNavigationAvailability()
    {
        var enabled = App.CurrentApp.State.IsModdedDeviceConnected;
        ModsNavigationItem.IsEnabled = enabled;
        ToolsNavigationItem.IsEnabled = enabled;
    }

    private void UpdateOperationInteractivity()
    {
        var active = App.CurrentApp.State.IsAgentOperationActive;
        RootNavigation.IsEnabled = !active;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = !active;
            presenter.IsMaximizable = !active;
            presenter.IsResizable = !active;
        }

        if (active)
        {
            OperationLogList.Focus(FocusState.Programmatic);
        }
    }

    private void CurrentOperationLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (OperationLogList.Items.Count == 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => OperationLogList.ScrollIntoView(OperationLogList.Items[OperationLogList.Items.Count - 1]));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (App.CurrentApp.State.IsAgentOperationActive)
        {
            args.Cancel = true;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        App.CurrentApp.State.PropertyChanged -= State_PropertyChanged;
        App.CurrentApp.State.CurrentOperationLogs.CollectionChanged -= CurrentOperationLogs_CollectionChanged;
    }

    private void Navigate(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
