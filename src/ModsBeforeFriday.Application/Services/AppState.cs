using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.ViewModels;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.Services;

public sealed class AppState : ObservableObject
{
    private DeviceInfo? _selectedDevice;
    private ModStatus? _modStatus;
    private bool _isAgentOperationActive;
    private string _agentOperationTitle = "Working with MBF agent";
    private int _operationGeneration;

    public AppState(AppSettings settings)
    {
        Settings = settings;
        var uiSink = new Progress<RoutedAgentLog>(routed =>
        {
            AgentLogs.Add(routed.Entry);
            if (routed.IncludeInCurrentOperation && routed.Generation == _operationGeneration)
            {
                CurrentOperationLogs.Add(routed.Entry);
            }
        });
        AgentProgress = new AgentProgressRouter(this, uiSink);
    }

    public AppSettings Settings { get; }

    public ObservableCollection<AgentLogEntry> AgentLogs { get; } = [];

    public ObservableCollection<AgentLogEntry> CurrentOperationLogs { get; } = [];

    public IProgress<AgentLogEntry> AgentProgress { get; }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(IsDeviceConnected));
                OnPropertyChanged(nameof(IsModdedDeviceConnected));
            }
        }
    }

    public ModStatus? ModStatus
    {
        get => _modStatus;
        set
        {
            if (SetProperty(ref _modStatus, value))
            {
                OnPropertyChanged(nameof(IsModdedDeviceConnected));
            }
        }
    }

    public bool IsDeviceConnected => SelectedDevice?.IsReady == true;

    public bool IsModdedDeviceConnected => IsDeviceConnected
        && ModStatus?.AppInfo?.LoaderInstalled == ModLoader.Scotland2;

    public bool IsAgentOperationActive
    {
        get => _isAgentOperationActive;
        private set => SetProperty(ref _isAgentOperationActive, value);
    }

    public string AgentOperationTitle
    {
        get => _agentOperationTitle;
        private set => SetProperty(ref _agentOperationTitle, value);
    }

    public async Task RunAgentOperationAsync(string title, Func<Task> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(operation);

        BeginAgentOperation(title);
        try
        {
            await operation();
        }
        finally
        {
            EndAgentOperation();
        }
    }

    public async Task<T> RunAgentOperationAsync<T>(string title, Func<Task<T>> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(operation);

        BeginAgentOperation(title);
        try
        {
            return await operation();
        }
        finally
        {
            EndAgentOperation();
        }
    }

    public void Disconnect()
    {
        SelectedDevice = null;
        ModStatus = null;
    }

    private void BeginAgentOperation(string title)
    {
        if (IsAgentOperationActive)
        {
            throw new InvalidOperationException("Another MBF agent operation is already running.");
        }

        unchecked
        {
            _operationGeneration++;
        }

        AgentOperationTitle = title;
        CurrentOperationLogs.Clear();
        IsAgentOperationActive = true;
    }

    private void EndAgentOperation()
    {
        IsAgentOperationActive = false;
    }

    private sealed record RoutedAgentLog(AgentLogEntry Entry, int Generation, bool IncludeInCurrentOperation);

    private sealed class AgentProgressRouter : IProgress<AgentLogEntry>
    {
        private readonly AppState _owner;
        private readonly IProgress<RoutedAgentLog> _sink;

        public AgentProgressRouter(AppState owner, IProgress<RoutedAgentLog> sink)
        {
            _owner = owner;
            _sink = sink;
        }

        public void Report(AgentLogEntry value)
        {
            _sink.Report(new RoutedAgentLog(
                value,
                _owner._operationGeneration,
                _owner._isAgentOperationActive));
        }
    }
}
