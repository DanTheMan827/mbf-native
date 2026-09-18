using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModsBeforeFriday.Application.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace ModsBeforeFriday.App.Services;

public sealed class WindowsUserInteractionService : IUserInteractionService
{
    private readonly Window _window;

    public WindowsUserInteractionService(Window window)
    {
        _window = window;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string primaryText = "Continue")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = GetXamlRoot(),
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "Close",
            XamlRoot = GetXamlRoot(),
        };
        _ = await dialog.ShowAsync();
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(params string[] fileTypeFilters)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Clear();
        foreach (var filter in NormalizeFilters(fileTypeFilters)) picker.FileTypeFilter.Add(filter);
        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    public async Task<string?> PickFileAsync(params string[] fileTypeFilters)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Clear();
        foreach (var filter in NormalizeFilters(fileTypeFilters)) picker.FileTypeFilter.Add(filter);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task SaveTextAsync(string suggestedFileName, string text, string fileTypeDescription = "Text document", string extension = ".txt")
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
        };
        InitializePicker(picker);
        picker.FileTypeChoices.Add(fileTypeDescription, [extension]);
        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            await FileIO.WriteTextAsync(file, text);
        }
    }

    public async Task OpenUriAsync(Uri uri) => _ = await Launcher.LaunchUriAsync(uri);

    private XamlRoot GetXamlRoot() =>
        (_window.Content as FrameworkElement)?.XamlRoot
        ?? throw new InvalidOperationException("The window content has not been attached to a XamlRoot yet.");

    private void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private static IReadOnlyList<string> NormalizeFilters(IReadOnlyList<string> filters) =>
        filters.Count == 0 ? ["*"] : filters;
}
