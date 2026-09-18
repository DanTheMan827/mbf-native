using System.Windows.Input;

using ModsBeforeFriday.Application.Services;

namespace ModsBeforeFriday.Application.ViewModels;

public sealed class AsyncCommand<T> : ICommand
{
    private readonly Func<T, Task> _execute;
    private readonly Func<T, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncCommand(Func<T, Task> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && parameter is T typed && (_canExecute?.Invoke(typed) ?? true);

    public async void Execute(object? parameter)
    {
        if (parameter is not T typed || !CanExecute(typed))
        {
            return;
        }

        _isExecuting = true;
        NotifyCanExecuteChanged();
        try
        {
            await _execute(typed);
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
