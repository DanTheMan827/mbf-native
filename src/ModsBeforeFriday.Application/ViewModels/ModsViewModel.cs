using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.Services;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Models;
using ModsBeforeFriday.Core.Utilities;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class ModsViewModel : ObservableObject
{
    private readonly IQuestService _quest;
    private readonly IModCatalogService _catalog;
    private readonly AppState _state;
    private readonly IUserInteractionService _interaction;
    private bool _isBusy;
    private string _statusText = "Connect a modded Quest to manage mods.";
    private string _importUrl = string.Empty;

    public ModsViewModel(
        IQuestService quest,
        IModCatalogService catalog,
        AppState state,
        IUserInteractionService interaction)
    {
        _quest = quest;
        _catalog = catalog;
        _state = state;
        _interaction = interaction;

        RefreshCommand = new AsyncCommand(RefreshAsync, HasModdedDevice);
        SyncChangesCommand = new AsyncCommand(SyncChangesAsync, () => HasPendingChanges && !IsBusy);
        InstallSelectedCommand = new AsyncCommand(InstallSelectedAsync, () => AvailableMods.Any(item => item.IsSelected) && !IsBusy);
        ImportFilesCommand = new AsyncCommand(ImportFilesAsync, HasModdedDevice);
        ImportUrlCommand = new AsyncCommand(ImportUrlAsync, () => HasModdedDevice() && Uri.TryCreate(ImportUrl, UriKind.Absolute, out _));
    }

    public ObservableCollection<InstalledModItemViewModel> InstalledMods { get; } = [];

    public ObservableCollection<CatalogModItemViewModel> AvailableMods { get; } = [];

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand SyncChangesCommand { get; }
    public AsyncCommand InstallSelectedCommand { get; }
    public AsyncCommand ImportFilesCommand { get; }
    public AsyncCommand ImportUrlCommand { get; }

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

    public string ImportUrl
    {
        get => _importUrl;
        set
        {
            if (SetProperty(ref _importUrl, value)) ImportUrlCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasPendingChanges => InstalledMods.Any(item => item.HasPendingChange);

    public async Task InitializeAsync()
    {
        if (!HasModdedDevice() || _state.ModStatus is null)
        {
            InstalledMods.Clear();
            AvailableMods.Clear();
            StatusText = "Connect a Quest and finish patching Beat Saber first.";
            return;
        }

        await RunBusyAsync(LoadCatalogFromCurrentStatusAsync, "Failed to load mods");
    }

    public void NotifyModToggleChanged()
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        SyncChangesCommand.NotifyCanExecuteChanged();
    }

    public async Task RemoveAsync(InstalledModItemViewModel item)
    {
        if (_state.SelectedDevice is null || item.IsCore)
        {
            return;
        }

        if (!await _interaction.ConfirmAsync("Remove mod", $"Remove {item.Name}?", "Remove"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusText = $"Removing {item.Name}...";
            _ = await _state.RunAgentOperationAsync(
                $"Removing {item.Name}",
                () => _quest.RemoveModAsync(_state.SelectedDevice, item.Id, _state.AgentProgress));
            await ReloadStatusAndCatalogAsync();
        }, "Failed to remove mod");
    }

    public async Task InstallAsync(CatalogModItemViewModel item)
    {
        await RunBusyAsync(async () =>
        {
            await InstallCatalogModAsync(item.Mod);
            await ReloadStatusAndCatalogAsync();
        }, $"Failed to {item.ActionText.ToLowerInvariant()} {item.Mod.Name}");
    }

    public async Task OpenSourceAsync(CatalogModItemViewModel item)
    {
        if (Uri.TryCreate(item.Mod.Source, UriKind.Absolute, out var uri))
        {
            await _interaction.OpenUriAsync(uri);
        }
    }

    public async Task OpenReportBugAsync(CatalogModItemViewModel item)
    {
        if (!item.CanReportBug || !Uri.TryCreate(item.Mod.Source.TrimEnd('/') + "/issues", UriKind.Absolute, out var uri))
        {
            return;
        }

        await _interaction.OpenUriAsync(uri);
    }

    private async Task RefreshAsync()
    {
        if (_state.SelectedDevice is null || _state.ModStatus?.AppInfo is null)
        {
            InstalledMods.Clear();
            AvailableMods.Clear();
            StatusText = "Connect a Quest and finish patching Beat Saber first.";
            return;
        }

        await RunBusyAsync(ReloadStatusAndCatalogAsync, "Failed to load mods");
    }

    private async Task ReloadStatusAndCatalogAsync()
    {
        var device = _state.SelectedDevice ?? throw new InvalidOperationException("No Quest is connected.");
        var status = await _state.RunAgentOperationAsync(
            "Refreshing mod status",
            () => _quest.GetModStatusAsync(
                device,
                NormalizeOptional(_state.Settings.CoreModOverrideUrl),
                _state.AgentProgress));
        _state.ModStatus = status;

        await LoadCatalogAsync(status);
    }

    private async Task LoadCatalogFromCurrentStatusAsync()
    {
        var status = _state.ModStatus ?? throw new InvalidOperationException("No mod status has been loaded.");
        await LoadCatalogAsync(status);
    }

    private async Task LoadCatalogAsync(ModStatus status)
    {
        IReadOnlyList<ModCatalogEntry> catalog = [];
        if (status.AppInfo is not null && status.AppInfo.LoaderInstalled == ModLoader.Scotland2)
        {
            StatusText = "Loading mod repository...";
            catalog = await _catalog.GetAvailableModsAsync(status.AppInfo.Version, status.InstalledMods);
        }

        var updatesById = catalog
            .Where(entry => entry.NeedsUpdate)
            .ToDictionary(entry => entry.Mod.Id, StringComparer.Ordinal);

        InstalledMods.Clear();
        foreach (var mod in status.InstalledMods
                     .Select(mod => new
                     {
                         Mod = mod,
                         Update = updatesById.GetValueOrDefault(mod.Id),
                     })
                     .OrderByDescending(item => item.Update is not null)
                     .ThenBy(item => item.Mod.IsCore)
                     .ThenBy(item => item.Mod.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new InstalledModItemViewModel(mod.Mod, mod.Update);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(InstalledModItemViewModel.IsEnabled)) NotifyModToggleChanged();
            };
            InstalledMods.Add(item);
        }

        AvailableMods.Clear();
        foreach (var entry in catalog)
        {
            var item = new CatalogModItemViewModel(entry);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CatalogModItemViewModel.IsSelected)) InstallSelectedCommand.NotifyCanExecuteChanged();
            };
            AvailableMods.Add(item);
        }

        OnPropertyChanged(nameof(HasPendingChanges));
        NotifyCommands();
        StatusText = $"{InstalledMods.Count} installed mod{(InstalledMods.Count == 1 ? string.Empty : "s")}; {AvailableMods.Count} available install/update{(AvailableMods.Count == 1 ? string.Empty : "s")}.";
    }

    private async Task SyncChangesAsync()
    {
        if (_state.SelectedDevice is null)
        {
            return;
        }

        var changes = InstalledMods
            .Where(item => item.HasPendingChange)
            .ToDictionary(item => item.Id, item => item.IsEnabled, StringComparer.Ordinal);
        if (changes.Count == 0)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusText = "Synchronizing mod changes...";
            var result = await _state.RunAgentOperationAsync(
                "Synchronizing mod changes",
                () => _quest.SetModsEnabledAsync(_state.SelectedDevice, changes, _state.AgentProgress));
            if (!string.IsNullOrWhiteSpace(result.Failures))
            {
                await _interaction.ShowMessageAsync("Some mod changes failed", result.Failures);
            }

            await ReloadStatusAndCatalogAsync();
        }, "Failed to synchronize mods");
    }

    private async Task InstallSelectedAsync()
    {
        var selected = AvailableMods.Where(item => item.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            foreach (var item in selected)
            {
                await InstallCatalogModAsync(item.Mod);
            }

            await ReloadStatusAndCatalogAsync();
        }, "Failed to install selected mods");
    }

    private async Task InstallCatalogModAsync(ModCatalogMod mod)
    {
        if (_state.SelectedDevice is null)
        {
            throw new InvalidOperationException("No Quest is connected.");
        }

        StatusText = $"Importing {mod.Name} {mod.Version}...";
        var result = await _state.RunAgentOperationAsync(
            $"Importing {mod.Name}",
            () => _quest.ImportUrlAsync(_state.SelectedDevice, new Uri(mod.Download), _state.AgentProgress));
        await ProcessImportResultAsync(result);
    }

    private async Task ImportFilesAsync()
    {
        if (_state.SelectedDevice is null)
        {
            return;
        }

        var files = await _interaction.PickFilesAsync("*");
        if (files.Count == 0)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            foreach (var file in files)
            {
                StatusText = $"Importing {Path.GetFileName(file)}...";
                var result = await _state.RunAgentOperationAsync(
                    $"Importing {Path.GetFileName(file)}",
                    () => _quest.ImportFileAsync(_state.SelectedDevice, file, _state.AgentProgress));
                await ProcessImportResultAsync(result);
            }

            await ReloadStatusAndCatalogAsync();
        }, "Failed to import file");
    }

    private async Task ImportUrlAsync()
    {
        if (_state.SelectedDevice is null || !Uri.TryCreate(ImportUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _state.RunAgentOperationAsync(
                "Importing mod from URL",
                () => _quest.ImportUrlAsync(_state.SelectedDevice, uri, _state.AgentProgress));
            ImportUrl = string.Empty;
            await ProcessImportResultAsync(result);
            await ReloadStatusAndCatalogAsync();
        }, "Failed to import URL");
    }

    private async Task ProcessImportResultAsync(ImportResult result)
    {
        switch (result.Result)
        {
            case ImportedSong:
                StatusText = $"Imported song {result.UsedFilename}.";
                break;
            case ImportedFileCopy copy:
                StatusText = $"Copied {result.UsedFilename} to {copy.CopiedTo}.";
                break;
            case NonQuestModDetected:
                await _interaction.ShowMessageAsync(
                    "PC mod detected",
                    $"{result.UsedFilename} is a PC mod. Quest mods use the .QMOD format.");
                break;
            case ImportedMod imported:
                var importedMod = imported.InstalledMods.FirstOrDefault(mod => mod.Id == imported.ImportedId)
                    ?? throw new InvalidDataException($"Imported mod '{imported.ImportedId}' was not in the returned mod list.");
                var gameVersion = _state.ModStatus?.AppInfo?.Version;
                var mismatch = importedMod.GameVersion is not null
                    && gameVersion is not null
                    && !string.Equals(importedMod.GameVersion, gameVersion, StringComparison.Ordinal);
                if (mismatch)
                {
                    await _interaction.ShowMessageAsync(
                        "Mod imported but not enabled",
                        $"{importedMod.Name} targets Beat Saber {BeatSaberVersionComparer.TrimBuildSuffix(importedMod.GameVersion!)}; the connected installation is {BeatSaberVersionComparer.TrimBuildSuffix(gameVersion!)}.");
                    break;
                }

                if (_state.SelectedDevice is null) break;
                var sync = await _state.RunAgentOperationAsync(
                    $"Enabling {importedMod.Name}",
                    () => _quest.SetModsEnabledAsync(
                        _state.SelectedDevice,
                        new Dictionary<string, bool> { [imported.ImportedId] = true },
                        _state.AgentProgress));
                if (!string.IsNullOrWhiteSpace(sync.Failures))
                {
                    await _interaction.ShowMessageAsync("Mod install failed", sync.Failures);
                }
                else
                {
                    StatusText = $"Installed {importedMod.Name} {importedMod.Version}.";
                }
                break;
        }
    }

    private bool HasModdedDevice() => _state.SelectedDevice is not null
        && _state.ModStatus?.AppInfo?.LoaderInstalled == ModLoader.Scotland2;

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
        RefreshCommand.NotifyCanExecuteChanged();
        SyncChangesCommand.NotifyCanExecuteChanged();
        InstallSelectedCommand.NotifyCanExecuteChanged();
        ImportFilesCommand.NotifyCanExecuteChanged();
        ImportUrlCommand.NotifyCanExecuteChanged();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
