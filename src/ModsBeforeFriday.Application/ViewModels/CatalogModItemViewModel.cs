using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class CatalogModItemViewModel : ObservableObject
{
    private bool _isSelected;

    public CatalogModItemViewModel(ModCatalogEntry entry)
    {
        Entry = entry;
        _isSelected = entry.NeedsUpdate;
    }

    public ModCatalogEntry Entry { get; }
    public ModCatalogMod Mod => Entry.Mod;
    public string ActionText => Entry.NeedsUpdate ? "Update" : "Install";
    public string UpdateLabel => Entry.NeedsUpdate ? "Update available" : "Available";
    public bool HasCover => !string.IsNullOrWhiteSpace(Mod.Cover);
    public bool CanReportBug => Uri.TryCreate(Mod.Source, UriKind.Absolute, out var uri)
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
