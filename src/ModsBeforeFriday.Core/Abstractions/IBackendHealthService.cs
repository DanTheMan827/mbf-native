namespace ModsBeforeFriday.Core.Abstractions;

public interface IBackendHealthService
{
    /// <summary>Ensures a local ADB server is reachable, starting a bundled/installed daemon when configured.</summary>
    Task EnsureAdbServerAsync(CancellationToken cancellationToken = default);
}
