using System;
using System.Collections.ObjectModel;
using ModsBeforeFriday.Application.Services;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class LogsViewModel : ObservableObject, IDisposable
{
    private readonly IQuestService _quest;
    private readonly AppState _state;
    private readonly IUserInteractionService _interaction;
    private CancellationTokenSource? _logcatCancellation;
    private Task<string>? _logcatTask;
    private bool _isCapturing;
    private string? _lastLogcat;
    private string _statusText = "Agent activity is recorded automatically.";

    public LogsViewModel(IQuestService quest, AppState state, IUserInteractionService interaction)
    {
        _quest = quest;
        _state = state;
        _interaction = interaction;
        AgentLogs = state.AgentLogs;

        StartLogcatCommand = new AsyncCommand(StartLogcatAsync, () => CanCapture && !IsCapturing);
        StopLogcatCommand = new AsyncCommand(StopLogcatAsync, () => IsCapturing);
        SaveLogcatCommand = new AsyncCommand(SaveLogcatAsync, () => !string.IsNullOrEmpty(_lastLogcat));
        ClearAgentLogsCommand = new RelayCommand(ClearAgentLogs, () => AgentLogs.Count > 0);
        AgentLogs.CollectionChanged += (_, _) => ClearAgentLogsCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<AgentLogEntry> AgentLogs { get; }

    public AsyncCommand StartLogcatCommand { get; }
    public AsyncCommand StopLogcatCommand { get; }
    public AsyncCommand SaveLogcatCommand { get; }
    public RelayCommand ClearAgentLogsCommand { get; }

    public bool CanCapture => _state.SelectedDevice?.IsReady == true;

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (SetProperty(ref _isCapturing, value))
            {
                StartLogcatCommand.NotifyCanExecuteChanged();
                StopLogcatCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasCapturedLog => !string.IsNullOrEmpty(_lastLogcat);

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private Task StartLogcatAsync()
    {
        var device = _state.SelectedDevice;
        if (device is null)
        {
            return Task.CompletedTask;
        }

        _logcatCancellation?.Dispose();
        _logcatCancellation = new CancellationTokenSource();
        _lastLogcat = null;
        OnPropertyChanged(nameof(HasCapturedLog));
        SaveLogcatCommand.NotifyCanExecuteChanged();
        _logcatTask = _quest.CaptureLogcatAsync(device, _logcatCancellation.Token);
        IsCapturing = true;
        StatusText = "Capturing adb logcat. Reproduce the problem in the headset, then stop capture.";
        return Task.CompletedTask;
    }

    private async Task StopLogcatAsync()
    {
        if (_logcatCancellation is null || _logcatTask is null)
        {
            return;
        }

        StatusText = "Finalizing logcat capture...";
        _logcatCancellation.Cancel();
        try
        {
            _lastLogcat = await _logcatTask;
            StatusText = $"Captured {_lastLogcat.Length:N0} characters of logcat output.";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            await _interaction.ShowMessageAsync("Logcat capture failed", exception.Message);
        }
        finally
        {
            IsCapturing = false;
            _logcatCancellation.Dispose();
            _logcatCancellation = null;
            _logcatTask = null;
            OnPropertyChanged(nameof(HasCapturedLog));
            SaveLogcatCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveLogcatAsync()
    {
        if (!string.IsNullOrEmpty(_lastLogcat))
        {
            await _interaction.SaveTextAsync("logcat.log", _lastLogcat, "ADB log", ".log");
        }
    }

    private void ClearAgentLogs()
    {
        AgentLogs.Clear();
        StatusText = "Agent log cleared.";
    }

    // Dispose implementation to satisfy CA1001
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing) return;

        // If a capture is in progress, request cancellation and dispose the CTS.
        try
        {
            _logcatCancellation?.Cancel();
        }
        catch
        {
            // Ignore cancellation exceptions during dispose
        }

        _logcatCancellation?.Dispose();
        _logcatCancellation = null;

        // Clear reference to the task so we don't hold onto it longer than necessary.
        _logcatTask = null;
    }
}
