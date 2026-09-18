using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.ViewModels;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.Services;

public sealed class AppState : ObservableObject
{
    private DeviceInfo? _selectedDevice;
    private ModStatus? _modStatus;

    public AppState(AppSettings settings)
    {
        Settings = settings;
        AgentProgress = new Progress<AgentLogEntry>(entry => AgentLogs.Add(entry));
    }

    public AppSettings Settings { get; }

    public ObservableCollection<AgentLogEntry> AgentLogs { get; } = [];

    public IProgress<AgentLogEntry> AgentProgress { get; }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(IsDeviceConnected));
            }
        }
    }

    public ModStatus? ModStatus
    {
        get => _modStatus;
        set => SetProperty(ref _modStatus, value);
    }

    public bool IsDeviceConnected => SelectedDevice?.IsReady == true;

    public void Disconnect()
    {
        SelectedDevice = null;
        ModStatus = null;
    }
}
