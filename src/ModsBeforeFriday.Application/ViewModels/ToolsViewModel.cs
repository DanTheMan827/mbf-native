using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.Services;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Manifest;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class ToolsViewModel : ObservableObject
{
    private readonly IQuestService _quest;
    private readonly AppState _state;
    private readonly IUserInteractionService _interaction;
    private bool _isBusy;
    private string _statusText = "Connect a modded Quest to use maintenance tools.";
    private AndroidManifestDocument? _manifest;
    private string? _splashScreenPath;

    public ToolsViewModel(IQuestService quest, AppState state, IUserInteractionService interaction)
    {
        _quest = quest;
        _state = state;
        _interaction = interaction;

        ForceStopCommand = new AsyncCommand(ForceStopAsync, CanUseDevice);
        RestartCommand = new AsyncCommand(RestartAsync, CanUseDevice);
        ReinstallCoreModsCommand = new AsyncCommand(ReinstallCoreModsAsync, CanUseModdedDevice);
        FixPlayerDataCommand = new AsyncCommand(FixPlayerDataAsync, CanUseDevice);
        UninstallCommand = new AsyncCommand(UninstallAsync, CanUseDevice);
        RepatchCommand = new AsyncCommand(RepatchAsync, () => CanUseModdedDevice() && _manifest is not null && !IsBusy);
        ChooseSplashCommand = new AsyncCommand(ChooseSplashAsync, CanUseModdedDevice);
        ClearSplashCommand = new RelayCommand(ClearSplash);
        ExportManifestCommand = new AsyncCommand(ExportManifestAsync, () => _manifest is not null && !IsBusy);
        ImportManifestCommand = new AsyncCommand(ImportManifestAsync, CanUseModdedDevice);
    }

    public ObservableCollection<ManifestOptionItemViewModel> ManifestOptions { get; } = [];

    public AsyncCommand ForceStopCommand { get; }
    public AsyncCommand RestartCommand { get; }
    public AsyncCommand ReinstallCoreModsCommand { get; }
    public AsyncCommand FixPlayerDataCommand { get; }
    public AsyncCommand UninstallCommand { get; }
    public AsyncCommand RepatchCommand { get; }
    public AsyncCommand ChooseSplashCommand { get; }
    public RelayCommand ClearSplashCommand { get; }
    public AsyncCommand ExportManifestCommand { get; }
    public AsyncCommand ImportManifestCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) NotifyCommands();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SplashScreenDisplay => string.IsNullOrWhiteSpace(_splashScreenPath)
        ? "Default splash screen"
        : Path.GetFileName(_splashScreenPath);

    public async Task InitializeAsync()
    {
        if (_state.ModStatus?.AppInfo is null)
        {
            StatusText = "Connect a Quest and load its Beat Saber installation first.";
            return;
        }

        LoadManifest(_state.ModStatus.AppInfo.ManifestXml, applyDefaults: true);
        StatusText = "Maintenance tools ready.";
        NotifyCommands();
        await Task.CompletedTask;
    }

    private async Task ForceStopAsync() => await RunBusyAsync(async () =>
    {
        await _quest.ForceStopGameAsync(RequireDevice());
        StatusText = "Beat Saber was stopped.";
    }, "Failed to stop Beat Saber");

    private async Task RestartAsync() => await RunBusyAsync(async () =>
    {
        await _quest.RestartGameAsync(RequireDevice());
        StatusText = "Beat Saber was restarted.";
    }, "Failed to restart Beat Saber");

    private async Task ReinstallCoreModsAsync()
    {
        if (!await _interaction.ConfirmAsync(
            "Reinstall only core mods",
            "This removes all non-core mods, then reinstalls the core mods and modloader.",
            "Reinstall core mods"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            _ = await _state.RunAgentOperationAsync(
                "Reinstalling core mods",
                () => _quest.QuickFixAsync(
                    RequireDevice(),
                    wipeExistingMods: true,
                    NormalizeOptional(_state.Settings.CoreModOverrideUrl),
                    _state.AgentProgress));
            await ReloadStatusAsync();
            StatusText = "Non-core mods removed and core mods reinstalled.";
        }, "Failed to reinstall core mods");
    }

    private async Task FixPlayerDataAsync() => await RunBusyAsync(async () =>
    {
        var existed = await _state.RunAgentOperationAsync(
            "Fixing player data",
            () => _quest.FixPlayerDataAsync(RequireDevice(), _state.AgentProgress));
        await _interaction.ShowMessageAsync(
            "Player data repair",
            existed ? "PlayerData.dat was backed up and repaired." : "No PlayerData.dat file was found that needed repair.");
        StatusText = existed ? "Player data repaired." : "No player data file needed repair.";
    }, "Failed to fix player data");

    private async Task UninstallAsync()
    {
        if (!await _interaction.ConfirmAsync(
            "Uninstall Beat Saber",
            "This removes Beat Saber and installed mods from the headset.",
            "Uninstall"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _quest.UninstallGameAsync(RequireDevice());
            _state.ModStatus = null;
            StatusText = "Beat Saber uninstalled.";
        }, "Failed to uninstall Beat Saber");
    }

    private async Task RepatchAsync()
    {
        if (_manifest is null)
        {
            return;
        }

        if (!await _interaction.ConfirmAsync(
            "Repatch Beat Saber",
            "Repatching updates Android permissions/features and can replace the VR splash screen. Keep the headset connected.",
            "Repatch"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _state.RunAgentOperationAsync(
                "Repatching Beat Saber",
                () => _quest.PatchAsync(
                    RequireDevice(),
                    new PatchOptions(
                        _manifest.ToXml(),
                        DowngradeTo: null,
                        Remodding: true,
                        AllowNoCoreMods: false,
                        DevicePreV51: false,
                        OverrideCoreModUrl: NormalizeOptional(_state.Settings.CoreModOverrideUrl)),
                    _splashScreenPath,
                    _state.AgentProgress));
            if (result.DidRemoveDlc)
            {
                await _interaction.ShowMessageAsync("DLC notice", "Restart the headset before redownloading installed DLC in-game.");
            }

            await ReloadStatusAsync();
            StatusText = "Beat Saber repatched successfully.";
        }, "Failed to repatch Beat Saber");
    }

    private async Task ChooseSplashAsync()
    {
        var path = await _interaction.PickFileAsync(".png");
        if (path is not null)
        {
            _splashScreenPath = path;
            OnPropertyChanged(nameof(SplashScreenDisplay));
        }
    }

    private void ClearSplash()
    {
        _splashScreenPath = null;
        OnPropertyChanged(nameof(SplashScreenDisplay));
    }

    private async Task ExportManifestAsync()
    {
        if (_manifest is not null)
        {
            await _interaction.SaveTextAsync("AndroidManifest.xml", _manifest.ToXml(), "Android manifest", ".xml");
        }
    }

    private async Task ImportManifestAsync()
    {
        var path = await _interaction.PickFileAsync(".xml");
        if (path is null)
        {
            return;
        }

        try
        {
            LoadManifest(await File.ReadAllTextAsync(path), applyDefaults: false);
            StatusText = "Custom AndroidManifest.xml loaded.";
        }
        catch (Exception exception)
        {
            await _interaction.ShowMessageAsync("Invalid Android manifest", exception.Message);
        }
    }

    private void LoadManifest(string xml, bool applyDefaults)
    {
        _manifest = new AndroidManifestDocument(xml);
        if (applyDefaults)
        {
            _manifest.ApplyPatchingDefaults();
        }

        ManifestOptions.Clear();
        foreach (var option in ModsBeforeFriday.Core.Manifest.ManifestOptions.Displayed)
        {
            ManifestOptions.Add(new ManifestOptionItemViewModel(_manifest, option));
        }

        RepatchCommand.NotifyCanExecuteChanged();
        ExportManifestCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadStatusAsync()
    {
        var status = await _state.RunAgentOperationAsync(
            "Refreshing Beat Saber status",
            () => _quest.GetModStatusAsync(
                RequireDevice(),
                NormalizeOptional(_state.Settings.CoreModOverrideUrl),
                _state.AgentProgress));
        _state.ModStatus = status;
        if (status.AppInfo is not null)
        {
            LoadManifest(status.AppInfo.ManifestXml, applyDefaults: true);
        }
    }

    private DeviceInfo RequireDevice() => _state.SelectedDevice
        ?? throw new InvalidOperationException("No Quest is connected.");

    private bool CanUseDevice() => _state.SelectedDevice?.IsReady == true && !IsBusy;

    private bool CanUseModdedDevice() => CanUseDevice() && _state.ModStatus?.AppInfo?.LoaderInstalled == ModLoader.Scotland2;

    private async Task RunBusyAsync(Func<Task> operation, string errorTitle)
    {
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            await _interaction.ShowMessageAsync(errorTitle, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyCommands()
    {
        ForceStopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        ReinstallCoreModsCommand.NotifyCanExecuteChanged();
        FixPlayerDataCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        RepatchCommand.NotifyCanExecuteChanged();
        ChooseSplashCommand.NotifyCanExecuteChanged();
        ExportManifestCommand.NotifyCanExecuteChanged();
        ImportManifestCommand.NotifyCanExecuteChanged();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
