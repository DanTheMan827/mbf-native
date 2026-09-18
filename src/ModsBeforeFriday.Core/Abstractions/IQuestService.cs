using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Core.Abstractions;

public interface IQuestService
{
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<DeviceInfo> EnrichDeviceAsync(DeviceInfo device, CancellationToken cancellationToken = default);

    Task<ModStatus> GetModStatusAsync(
        DeviceInfo device,
        string? overrideCoreModUrl = null,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ModSyncResult> SetModsEnabledAsync(DeviceInfo device, IReadOnlyDictionary<string, bool> statuses, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModInfo>> RemoveModAsync(DeviceInfo device, string modId, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<ImportResult> ImportFileAsync(DeviceInfo device, string localPath, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<ImportResult> ImportUrlAsync(DeviceInfo device, Uri uri, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<string> GetDowngradedManifestAsync(DeviceInfo device, string gameVersion, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<PatchResult> PatchAsync(DeviceInfo device, PatchOptions options, string? localSplashScreenPath = null, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModInfo>> QuickFixAsync(DeviceInfo device, bool wipeExistingMods, string? overrideCoreModUrl = null, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task<bool> FixPlayerDataAsync(DeviceInfo device, IProgress<AgentLogEntry>? progress = null, CancellationToken cancellationToken = default);

    Task ForceStopGameAsync(DeviceInfo device, CancellationToken cancellationToken = default);

    Task RestartGameAsync(DeviceInfo device, CancellationToken cancellationToken = default);

    Task UninstallGameAsync(DeviceInfo device, CancellationToken cancellationToken = default);

    Task<string> CaptureLogcatAsync(DeviceInfo device, CancellationToken cancellationToken);
}
