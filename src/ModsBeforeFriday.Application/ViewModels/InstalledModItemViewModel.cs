using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class InstalledModItemViewModel : ObservableObject
{
    private bool _isEnabled;

    public InstalledModItemViewModel(ModInfo mod, ModCatalogEntry? update = null)
    {
        Mod = mod;
        Update = update;
        _isEnabled = mod.IsEnabled;
    }

    public ModInfo Mod { get; }

    public ModCatalogEntry? Update { get; }

    public string Id => Mod.Id;
    public string Name => Mod.Name;
    public string Version => Mod.Version;
    public string Description => Mod.Description ?? "No description provided.";
    public bool IsCore => Mod.IsCore;
    public bool CanRemove => !Mod.IsCore;
    public bool OriginalEnabled => Mod.IsEnabled;
    public bool HasUpdate => Update?.NeedsUpdate == true;
    public string UpdateText => HasUpdate ? $"Update available: {Update!.Mod.Version}" : string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(HasPendingChange));
            }
        }
    }

    public bool HasPendingChange => IsEnabled != OriginalEnabled;
}
