namespace ModsBeforeFriday.Adb.Abstractions;

/// <summary>
/// Represents one bidirectional connection to an ADB server transport.
/// </summary>
public interface IAdbServerConnection : IAsyncDisposable
{
    /// <summary>Gets the raw byte stream connected to the ADB server.</summary>
    Stream Stream { get; }

    /// <summary>
    /// Half-closes the transport write side while keeping reads open. This is a
    /// low-level transport primitive and must only be used by ADB services whose
    /// protocol explicitly requires transport EOF. In particular, classic
    /// <c>shell:</c> sessions must not use this as a substitute for shell-v2
    /// <c>CLOSE_STDIN</c>, because doing so can truncate subprocess output.
    /// </summary>
    ValueTask CompleteWritesAsync(CancellationToken cancellationToken = default);
}
