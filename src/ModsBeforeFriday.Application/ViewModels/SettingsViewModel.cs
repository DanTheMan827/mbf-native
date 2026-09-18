using ModsBeforeFriday.Application.Services;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly SettingsStore _store;
    private readonly IUserInteractionService _interaction;
    private string _statusText = "Developer options are off by default.";

    public SettingsViewModel(AppState state, SettingsStore store, IUserInteractionService interaction)
    {
        _state = state;
        _store = store;
        _interaction = interaction;
        SaveCommand = new AsyncCommand(SaveAsync);
    }

    public AsyncCommand SaveCommand { get; }

    public bool DeveloperMode
    {
        get => _state.Settings.DeveloperMode;
        set
        {
            if (_state.Settings.DeveloperMode != value)
            {
                _state.Settings.DeveloperMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string CoreModOverrideUrl
    {
        get => _state.Settings.CoreModOverrideUrl ?? string.Empty;
        set
        {
            if (_state.Settings.CoreModOverrideUrl != value)
            {
                _state.Settings.CoreModOverrideUrl = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyChanged();
            }
        }
    }

    public string GameId
    {
        get => _state.Settings.GameId;
        set
        {
            if (_state.Settings.GameId != value)
            {
                _state.Settings.GameId = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnorePackageId
    {
        get => _state.Settings.IgnorePackageId;
        set
        {
            if (_state.Settings.IgnorePackageId != value)
            {
                _state.Settings.IgnorePackageId = value;
                OnPropertyChanged();
            }
        }
    }

    public string AdbHost
    {
        get => _state.Settings.AdbHost;
        set
        {
            if (_state.Settings.AdbHost != value)
            {
                _state.Settings.AdbHost = value;
                OnPropertyChanged();
            }
        }
    }

    public double AdbPort
    {
        get => _state.Settings.AdbPort;
        set
        {
            var port = (int)Math.Clamp(Math.Round(value), 1, 65535);
            if (_state.Settings.AdbPort != port)
            {
                _state.Settings.AdbPort = port;
                OnPropertyChanged();
            }
        }
    }

    public string AdbExecutablePath
    {
        get => _state.Settings.AdbExecutablePath ?? string.Empty;
        set
        {
            if (_state.Settings.AdbExecutablePath != value)
            {
                _state.Settings.AdbExecutablePath = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private async Task SaveAsync()
    {
        if (!string.IsNullOrWhiteSpace(CoreModOverrideUrl)
            && !Uri.TryCreate(CoreModOverrideUrl, UriKind.Absolute, out _))
        {
            await _interaction.ShowMessageAsync("Invalid core-mod URL", "Enter a complete absolute URL or leave the field empty.");
            return;
        }

        await _store.SaveAsync(_state.Settings);
        StatusText = "Settings saved. Developer mode and core-mod overrides apply immediately; ADB endpoint, game ID, and package-ID settings apply after restarting the app.";
    }
}
