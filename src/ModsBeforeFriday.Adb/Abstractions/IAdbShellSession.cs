namespace ModsBeforeFriday.Adb.Abstractions;

/// <summary>
/// A live classic ADB <c>shell:</c> service. The stream remains bidirectional until
/// the remote command exits or the session is disposed. Classic shell does not have
/// a safe protocol-level close-stdin operation; callers that need that capability
/// should use a shell-v2 implementation instead of half-closing the daemon transport.
/// </summary>
public interface IAdbShellSession : IAsyncDisposable
{
    Stream Stream { get; }
}
