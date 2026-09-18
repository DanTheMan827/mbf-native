namespace ModsBeforeFriday.Adb.Exceptions;

public sealed class AdbServerException : AdbException
{
    public AdbServerException(string service, string message)
        : base($"ADB service '{service}' failed: {message}")
    {
        Service = service;
    }

    public string Service { get; }
}
