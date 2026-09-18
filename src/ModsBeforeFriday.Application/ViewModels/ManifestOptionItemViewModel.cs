using ModsBeforeFriday.Core.Manifest;

using ModsBeforeFriday.Application.Services;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class ManifestOptionItemViewModel : ObservableObject
{
    private readonly AndroidManifestDocument _manifest;
    private bool _isEnabled;

    public ManifestOptionItemViewModel(AndroidManifestDocument manifest, ManifestOptionDefinition definition)
    {
        _manifest = manifest;
        Definition = definition;
        _isEnabled = manifest.IsOptionEnabled(definition);
    }

    public ManifestOptionDefinition Definition { get; }

    public string Name => Definition.Name;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                _manifest.SetOption(Definition, value);
            }
        }
    }
}
