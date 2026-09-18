using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.Services;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Manifest;
using ModsBeforeFriday.Core.Models;
using ModsBeforeFriday.Core.Utilities;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed record DowngradeChoice(string Label, string? Version);

public sealed class HomeViewModel : ObservableObject
{
    private readonly IQuestService _quest;
    private readonly AppState _state;
    private readonly IUserInteractionService _interaction;
    private DeviceInfo? _selectedDevice;
    private bool _isBusy;
    private string _statusText = "Connect a Quest to begin.";
    private string _analysisTitle = "No device selected";
    private string _analysisMessage = "Connect a Quest through the desktop ADB server to inspect the Beat Saber installation.";
    private InstallationAnalysis? _analysis;
    private AndroidManifestDocument? _manifest;
    private DowngradeChoice? _selectedDowngrade;

    public HomeViewModel(IQuestService quest, AppState state, IUserInteractionService interaction)
    {
        _quest = quest;
        _state = state;
        _interaction = interaction;
        RefreshDevicesCommand = new AsyncCommand(RefreshDevicesAsync);
        ConnectCommand = new AsyncCommand(ConnectAsync, () => SelectedDevice is not null && !IsBusy);
        DisconnectCommand = new RelayCommand(Disconnect, () => _state.IsDeviceConnected);
        PatchCommand = new AsyncCommand(PatchAsync, () => CanPatch && !IsBusy);
        RepairCommand = new AsyncCommand(RepairAsync, () => CanRepair && !IsBusy);
        UninstallCommand = new AsyncCommand(UninstallAsync, () => CanUninstall && !IsBusy);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = [];

    public ObservableCollection<DowngradeChoice> VersionChoices { get; } = [];

    public ObservableCollection<ManifestOptionItemViewModel> ManifestOptions { get; } = [];

    public AsyncCommand RefreshDevicesCommand { get; }

    public AsyncCommand ConnectCommand { get; }

    public RelayCommand DisconnectCommand { get; }

    public AsyncCommand PatchCommand { get; }

    public AsyncCommand RepairCommand { get; }

    public AsyncCommand UninstallCommand { get; }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                ConnectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DowngradeChoice? SelectedDowngrade
    {
        get => _selectedDowngrade;
        set => SetProperty(ref _selectedDowngrade, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommands();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string AnalysisTitle
    {
        get => _analysisTitle;
        private set => SetProperty(ref _analysisTitle, value);
    }

    public string AnalysisMessage
    {
        get => _analysisMessage;
        private set => SetProperty(ref _analysisMessage, value);
    }

    public bool HasConnectedDevice => _state.IsDeviceConnected;

    public bool ShowPatchOptions => _analysis?.State == InstallationState.NeedsPatching;

    public bool CanPatch => ShowPatchOptions && _manifest is not null;

    public bool CanRepair => _analysis?.State == InstallationState.ModdedReady
        && _state.ModStatus is { } status
        && (status.ModloaderInstallStatus != InstallStatus.Ready || status.CoreMods?.CoreModInstallStatus != InstallStatus.Ready);

    public bool CanUninstall => _state.ModStatus?.AppInfo is not null;

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
        if (_state.SelectedDevice is not null)
        {
            SelectedDevice = Devices.FirstOrDefault(device => device.Serial == _state.SelectedDevice.Serial) ?? _state.SelectedDevice;
            await InspectConnectedDeviceAsync(_state.SelectedDevice);
        }
    }

    public async Task SelectVersionAsync(DowngradeChoice? choice)
    {
        SelectedDowngrade = choice;
        if (_state.SelectedDevice is null || _state.ModStatus?.AppInfo is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await PrepareManifestAsync(choice?.Version);
            StatusText = "Manifest options are ready.";
        }, "Unable to prepare manifest");
    }

    private async Task RefreshDevicesAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusText = "Looking for ADB devices...";
            var devices = await _quest.GetDevicesAsync();
            Devices.Clear();
            foreach (var device in devices) Devices.Add(device);

            if (SelectedDevice is null && devices.Count == 1)
            {
                SelectedDevice = devices[0];
            }

            StatusText = devices.Count == 0
                ? "No ADB devices found. Connect your Quest by USB and enable USB debugging."
                : $"Found {devices.Count} ADB device{(devices.Count == 1 ? string.Empty : "s")}.";
        }, "Unable to query ADB devices");
    }

    private async Task ConnectAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        if (SelectedDevice.State == DeviceConnectionState.Unauthorized)
        {
            await _interaction.ShowMessageAsync(
                "Authorize this computer",
                "Put on the headset, accept the USB debugging prompt, choose 'Always allow from this computer', then refresh devices.");
            return;
        }

        if (!SelectedDevice.IsReady)
        {
            await _interaction.ShowMessageAsync("Device unavailable", $"ADB reports this device as {SelectedDevice.State}.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusText = "Reading Quest details...";
            var enriched = await _quest.EnrichDeviceAsync(SelectedDevice);
            _state.SelectedDevice = enriched;
            SelectedDevice = enriched;
            OnPropertyChanged(nameof(HasConnectedDevice));
            DisconnectCommand.NotifyCanExecuteChanged();
            await InspectConnectedDeviceAsync(enriched);
        }, "Failed to connect to Quest");
    }

    private async Task InspectConnectedDeviceAsync(DeviceInfo device)
    {
        StatusText = "Checking Beat Saber installation...";
        var status = await _quest.GetModStatusAsync(
            device,
            NormalizeOptionalUri(_state.Settings.CoreModOverrideUrl),
            _state.AgentProgress);
        _state.ModStatus = status;
        _analysis = InstallationAnalyzer.Analyze(status, _state.Settings.DeveloperMode);
        PopulateVersionChoices();
        UpdateAnalysisText();

        if (_analysis.State == InstallationState.NeedsPatching)
        {
            await PrepareManifestAsync(SelectedDowngrade?.Version);
        }
        else
        {
            _manifest = null;
            ManifestOptions.Clear();
        }

        OnStatusChanged();
        StatusText = "Ready.";
    }

    private async Task PrepareManifestAsync(string? downgradeVersion)
    {
        var device = _state.SelectedDevice ?? throw new InvalidOperationException("No device is selected.");
        var status = _state.ModStatus ?? throw new InvalidOperationException("No mod status has been loaded.");
        var sourceXml = downgradeVersion is null
            ? status.AppInfo?.ManifestXml ?? throw new InvalidOperationException("Beat Saber is not installed.")
            : await _quest.GetDowngradedManifestAsync(device, downgradeVersion, _state.AgentProgress);

        _manifest = new AndroidManifestDocument(sourceXml);
        _manifest.ApplyPatchingDefaults();
        ManifestOptions.Clear();
        foreach (var option in ModsBeforeFriday.Core.Manifest.ManifestOptions.Displayed)
        {
            ManifestOptions.Add(new ManifestOptionItemViewModel(_manifest, option));
        }

        OnPropertyChanged(nameof(CanPatch));
        PatchCommand.NotifyCanExecuteChanged();
    }

    private async Task PatchAsync()
    {
        if (_state.SelectedDevice is null || _manifest is null)
        {
            return;
        }

        var accepted = await _interaction.ConfirmAsync(
            "Mod Beat Saber",
            "Mods and custom songs are unsupported by Beat Games and may cause bugs or crashes. Do not disconnect the headset while patching.",
            "Mod the app");
        if (!accepted)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusText = "Patching Beat Saber. Keep the Quest connected...";
            var devicePreV51 = string.Equals(_state.SelectedDevice.Model, "Quest", StringComparison.OrdinalIgnoreCase)
                && _state.SelectedDevice.AndroidVersion is < 11;
            var result = await _quest.PatchAsync(
                _state.SelectedDevice,
                new PatchOptions(
                    _manifest.ToXml(),
                    SelectedDowngrade?.Version,
                    Remodding: false,
                    AllowNoCoreMods: _state.Settings.DeveloperMode,
                    DevicePreV51: devicePreV51,
                    OverrideCoreModUrl: NormalizeOptionalUri(_state.Settings.CoreModOverrideUrl)),
                progress: _state.AgentProgress);

            if (result.DidRemoveDlc)
            {
                await _interaction.ShowMessageAsync(
                    "Installed DLC was temporarily removed",
                    "Restart the headset first, then redownload the DLC in-game.");
            }

            await InspectConnectedDeviceAsync(_state.SelectedDevice);
        }, "Failed to patch Beat Saber");
    }

    private async Task RepairAsync()
    {
        if (_state.SelectedDevice is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusText = "Repairing modloader and core mods...";
            _ = await _quest.QuickFixAsync(
                _state.SelectedDevice,
                wipeExistingMods: false,
                NormalizeOptionalUri(_state.Settings.CoreModOverrideUrl),
                _state.AgentProgress);
            await InspectConnectedDeviceAsync(_state.SelectedDevice);
        }, "Failed to repair installation");
    }

    private async Task UninstallAsync()
    {
        if (_state.SelectedDevice is null)
        {
            return;
        }

        if (!await _interaction.ConfirmAsync(
            "Uninstall Beat Saber",
            "This removes Beat Saber and installed mods. Custom songs and other data stored outside the application package are not removed by this command.",
            "Uninstall"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _quest.UninstallGameAsync(_state.SelectedDevice);
            await InspectConnectedDeviceAsync(_state.SelectedDevice);
        }, "Failed to uninstall Beat Saber");
    }

    private void Disconnect()
    {
        _state.Disconnect();
        _analysis = null;
        _manifest = null;
        VersionChoices.Clear();
        ManifestOptions.Clear();
        AnalysisTitle = "No device selected";
        AnalysisMessage = "Connect a Quest through the desktop ADB server to inspect the Beat Saber installation.";
        StatusText = "Disconnected.";
        OnStatusChanged();
    }

    private void PopulateVersionChoices()
    {
        VersionChoices.Clear();
        SelectedDowngrade = null;
        if (_analysis is null || _state.ModStatus?.AppInfo is null)
        {
            return;
        }

        var current = _state.ModStatus.AppInfo.Version;
        var currentSupported = _state.ModStatus.CoreMods?.SupportedVersions.Contains(current, StringComparer.Ordinal) == true;
        if (currentSupported || _state.Settings.DeveloperMode)
        {
            VersionChoices.Add(new DowngradeChoice($"Keep installed version ({BeatSaberVersionComparer.TrimBuildSuffix(current)})", null));
        }

        foreach (var version in _analysis.DowngradeChoices)
        {
            VersionChoices.Add(new DowngradeChoice($"Downgrade to {BeatSaberVersionComparer.TrimBuildSuffix(version)}", version));
        }

        SelectedDowngrade = _analysis.RecommendedDowngrade is null
            ? VersionChoices.FirstOrDefault(choice => choice.Version is null) ?? VersionChoices.FirstOrDefault()
            : VersionChoices.FirstOrDefault(choice => choice.Version == _analysis.RecommendedDowngrade);
    }

    private void UpdateAnalysisText()
    {
        if (_analysis is null || _state.ModStatus is null)
        {
            return;
        }

        (AnalysisTitle, AnalysisMessage) = _analysis.State switch
        {
            InstallationState.GameNotInstalled => ("Beat Saber is not installed", "Install Beat Saber from the Meta store, then refresh this page."),
            InstallationState.DeviceHasNoInternet => ("Quest has no internet connection", "The Rust agent needs the headset's internet connection to download the modloader, diffs, and core mods."),
            InstallationState.AwaitingDowngradeDiff => ("Waiting for downgrade support", $"No compatible downgrade diff is available yet for {_state.ModStatus.AppInfo?.Version}."),
            InstallationState.UnsupportedVersion => ("Version is not currently moddable", $"Beat Saber {_state.ModStatus.AppInfo?.Version} is not supported and there is no compatible downgrade path in the current index."),
            InstallationState.UnsupportedAlreadyModded => ("Reinstall required before downgrading", "The installed APK is already modified, so binary downgrade diffs cannot be applied safely. Reinstall the current store version first."),
            InstallationState.MissingObb => ("OBB file is missing", "The installation is incomplete. Uninstall Beat Saber and reinstall it from the Meta store before modding."),
            InstallationState.NeedsPatching => ("Ready to mod", SelectedDowngrade?.Version is null ? "The installed version is compatible with mods." : "A compatible version can be installed using MBF's downgrade patch."),
            InstallationState.ModdedReady => BuildModdedReadyMessage(_state.ModStatus),
            InstallationState.IncompatibleModLoader => ("Incompatible modloader detected", $"MBF supports Scotland2 for this flow, but the APK reports {_state.ModStatus.AppInfo?.LoaderInstalled}."),
            _ => ("Installation checked", "Review the detected status below."),
        };
    }

    private static (string, string) BuildModdedReadyMessage(ModStatus status)
    {
        var healthy = status.ModloaderInstallStatus == InstallStatus.Ready
            && status.CoreMods?.CoreModInstallStatus == InstallStatus.Ready;
        return healthy
            ? ("App is modded", "The modloader and core mods are installed and up to date. Use the Mods page to manage additional mods.")
            : ("Modded, but repair is recommended", "The modloader or core mods are missing or outdated. Use Repair to reinstall the required components.");
    }

    private void OnStatusChanged()
    {
        OnPropertyChanged(nameof(HasConnectedDevice));
        OnPropertyChanged(nameof(ShowPatchOptions));
        OnPropertyChanged(nameof(CanPatch));
        OnPropertyChanged(nameof(CanRepair));
        OnPropertyChanged(nameof(CanUninstall));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        PatchCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

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

    private static string? NormalizeOptionalUri(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
