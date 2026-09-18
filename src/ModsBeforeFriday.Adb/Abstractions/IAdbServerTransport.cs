namespace ModsBeforeFriday.Adb.Abstractions;

/// <summary>
/// Creates byte-stream connections to an ADB server. The ADB smart-socket protocol
/// is deliberately layered above this interface so TCP, named pipes, tunnels, and
/// test transports can be substituted without changing ADB or MBF logic.
/// </summary>
public interface IAdbServerTransport
{
    string Description { get; }

    ValueTask<IAdbServerConnection> ConnectAsync(CancellationToken cancellationToken = default);
}
