using ModsBeforeFriday.Adb.Models;

namespace ModsBeforeFriday.Adb.Abstractions;

public interface IAdbClient
{
    Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<string> ExecuteShellAsync(
        string serial,
        string command,
        CancellationToken cancellationToken = default);

    Task<IAdbShellSession> OpenShellAsync(
        string serial,
        string command,
        CancellationToken cancellationToken = default);

    Task PushAsync(
        string serial,
        Stream source,
        string remotePath,
        int unixMode = 0x81A4,
        DateTimeOffset? modified = null,
        CancellationToken cancellationToken = default);
}
