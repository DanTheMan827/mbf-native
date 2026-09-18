using System.Text;
using Microsoft.Extensions.Logging;
using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Adb.Models;
using ModsBeforeFriday.Backend.Agent;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Agent;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Backend.Devices;

internal sealed class QuestService : IQuestService
{
    private readonly IAdbClient _adb;
    private readonly IBackendHealthService _health;
    private readonly MbfAgentClient _agent;
    private readonly string _gameId;
    private readonly ILogger<QuestService> _logger;

    public QuestService(
        IAdbClient adb,
        IBackendHealthService health,
        MbfAgentClient agent,
        string gameId,
        ILogger<QuestService> logger)
    {
        _adb = adb;
        _health = health;
        _agent = agent;
        _gameId = gameId;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        await _health.EnsureAdbServerAsync(cancellationToken).ConfigureAwait(false);
        var devices = await _adb.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        return devices.Select(MapDevice).ToArray();
    }

    public async Task<DeviceInfo> EnrichDeviceAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        var release = (await _adb.ExecuteShellAsync(device.Serial, "getprop ro.build.version.release", cancellationToken)
            .ConfigureAwait(false)).Trim();
        var majorText = release.Split('.', 2)[0];
        var androidVersion = int.TryParse(majorText, out var parsed) ? parsed : (int?)null;
        return device with { AndroidVersion = androidVersion };
    }

    public async Task<ModStatus> GetModStatusAsync(
        DeviceInfo device,
        string? overrideCoreModUrl = null,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        var response = await _agent.SendAsync<ModStatusAgentResponse>(
            device.Serial,
            new GetModStatusRequest { AgentParameters = default!, OverrideCoreModUrl = overrideCoreModUrl },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.ToDomain();
    }

    public async Task<ModSyncResult> SetModsEnabledAsync(
        DeviceInfo device,
        IReadOnlyDictionary<string, bool> statuses,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentNullException.ThrowIfNull(statuses);
        var response = await _agent.SendAsync<ModSyncResultAgentResponse>(
            device.Serial,
            new SetModsEnabledRequest { AgentParameters = default!, Statuses = statuses },
            progress,
            cancellationToken).ConfigureAwait(false);
        return new ModSyncResult(response.InstalledMods.ToDomain(), response.Failures);
    }

    public async Task<IReadOnlyList<ModInfo>> RemoveModAsync(
        DeviceInfo device,
        string modId,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        var response = await _agent.SendAsync<ModsAgentResponse>(
            device.Serial,
            new RemoveModRequest { AgentParameters = default!, Id = modId },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.InstalledMods.ToDomain();
    }

    public async Task<ImportResult> ImportFileAsync(
        DeviceInfo device,
        string localPath,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException("Import file was not found.", localPath);
        }

        var fileName = Path.GetFileName(localPath);
        var remotePath = $"{MbfAgentClient.RemoteUploadsPath}/{fileName}";
        await _adb.ExecuteShellAsync(device.Serial, $"mkdir -p {MbfAgentClient.RemoteUploadsPath}", cancellationToken).ConfigureAwait(false);
        await using (var source = File.OpenRead(localPath))
        {
            await _adb.PushAsync(device.Serial, source, remotePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var response = await _agent.SendAsync<ImportResultAgentResponse>(
            device.Serial,
            new ImportFileRequest { AgentParameters = default!, FromPath = remotePath },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.ToDomain();
    }

    public async Task<ImportResult> ImportUrlAsync(
        DeviceInfo device,
        Uri uri,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Import URLs must be absolute HTTP or HTTPS URLs.", nameof(uri));
        }

        var response = await _agent.SendAsync<ImportResultAgentResponse>(
            device.Serial,
            new ImportUrlRequest { AgentParameters = default!, FromUrl = uri.AbsoluteUri },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.ToDomain();
    }

    public async Task<string> GetDowngradedManifestAsync(
        DeviceInfo device,
        string gameVersion,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        var response = await _agent.SendAsync<DowngradedManifestAgentResponse>(
            device.Serial,
            new GetDowngradedManifestRequest { AgentParameters = default!, Version = gameVersion },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.ManifestXml;
    }

    public async Task<PatchResult> PatchAsync(
        DeviceInfo device,
        PatchOptions options,
        string? localSplashScreenPath = null,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        ArgumentNullException.ThrowIfNull(options);

        string? remoteSplashPath = null;
        if (!string.IsNullOrWhiteSpace(localSplashScreenPath))
        {
            if (!File.Exists(localSplashScreenPath))
            {
                throw new FileNotFoundException("Splash screen image was not found.", localSplashScreenPath);
            }

            await _adb.ExecuteShellAsync(device.Serial, $"mkdir -p {MbfAgentClient.RemoteUploadsPath}", cancellationToken).ConfigureAwait(false);
            remoteSplashPath = $"{MbfAgentClient.RemoteUploadsPath}/{Path.GetFileName(localSplashScreenPath)}";
            await using var source = File.OpenRead(localSplashScreenPath);
            await _adb.PushAsync(device.Serial, source, remoteSplashPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var response = await _agent.SendAsync<PatchedAgentResponse>(
            device.Serial,
            new PatchRequest
            {
                AgentParameters = default!,
                DowngradeTo = options.DowngradeTo,
                ManifestMod = options.ManifestXml,
                VrSplashPath = remoteSplashPath,
                Remodding = options.Remodding,
                AllowNoCoreMods = options.AllowNoCoreMods,
                DevicePreV51 = options.DevicePreV51,
                OverrideCoreModUrl = options.OverrideCoreModUrl,
            },
            progress,
            cancellationToken).ConfigureAwait(false);

        return new PatchResult(response.InstalledMods.ToDomain(), response.DidRemoveDlc);
    }

    public async Task<IReadOnlyList<ModInfo>> QuickFixAsync(
        DeviceInfo device,
        bool wipeExistingMods,
        string? overrideCoreModUrl = null,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        var response = await _agent.SendAsync<ModsAgentResponse>(
            device.Serial,
            new QuickFixRequest
            {
                AgentParameters = default!,
                OverrideCoreModUrl = overrideCoreModUrl,
                WipeExistingMods = wipeExistingMods,
            },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.InstalledMods.ToDomain();
    }

    public async Task<bool> FixPlayerDataAsync(
        DeviceInfo device,
        IProgress<AgentLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        var response = await _agent.SendAsync<FixedPlayerDataAgentResponse>(
            device.Serial,
            new FixPlayerDataRequest { AgentParameters = default! },
            progress,
            cancellationToken).ConfigureAwait(false);
        return response.Existed;
    }

    public async Task ForceStopGameAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        _ = await _adb.ExecuteShellAsync(device.Serial, $"am force-stop {_gameId}", cancellationToken).ConfigureAwait(false);
    }

    public async Task RestartGameAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        _ = await _adb.ExecuteShellAsync(
            device.Serial,
            $"sh -c 'am force-stop {_gameId}; monkey -p {_gameId} -c android.intent.category.LAUNCHER 1'",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UninstallGameAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        EnsureReady(device);
        _ = await _adb.ExecuteShellAsync(device.Serial, $"pm uninstall {_gameId}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CaptureLogcatAsync(DeviceInfo device, CancellationToken cancellationToken)
    {
        EnsureReady(device);
        _ = await _adb.ExecuteShellAsync(device.Serial, "logcat -c", CancellationToken.None).ConfigureAwait(false);
        await using var session = await _adb.OpenShellAsync(device.Serial, "logcat", CancellationToken.None).ConfigureAwait(false);

        var result = new StringBuilder();
        var buffer = new char[8192];
        using var reader = new StreamReader(session.Stream, Encoding.UTF8, false, leaveOpen: true);
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                result.Append(buffer, 0, read);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Logcat capture cancelled after {Length} characters.", result.Length);
        }

        return result.ToString();
    }

    private static DeviceInfo MapDevice(AdbDevice device)
    {
        var state = device.State switch
        {
            AdbDeviceState.Device => DeviceConnectionState.Ready,
            AdbDeviceState.Offline => DeviceConnectionState.Offline,
            AdbDeviceState.Unauthorized => DeviceConnectionState.Unauthorized,
            AdbDeviceState.NoPermissions => DeviceConnectionState.Unavailable,
            _ => DeviceConnectionState.Unknown,
        };

        return new DeviceInfo(device.Serial, device.DisplayName, device.Model, state);
    }

    private static void EnsureReady(DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (!device.IsReady)
        {
            throw new InvalidOperationException($"Device {device.Serial} is not ready (state: {device.State}).");
        }
    }
}
