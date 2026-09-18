namespace ModsBeforeFriday.Adb.Abstractions;

/// <summary>
/// Represents one bidirectional connection to an ADB server transport.
/// </summary>
public interface IAdbServerConnection : IAsyncDisposable
{
    /// <summary>Gets the raw byte stream connected to the ADB server.</summary>
    Stream Stream { get; }

    /// <summary>
    /// Half-closes the client-to-server side while keeping the read side open.
    /// This is required by classic ADB shell services that read stdin until EOF.
    /// </summary>
    ValueTask CompleteWritesAsync(CancellationToken cancellationToken = default);
}
