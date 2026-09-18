using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Core.Abstractions;

namespace ModsBeforeFriday.Backend.Devices;

internal sealed class AdbServerHealthService : IBackendHealthService, IDisposable
{
    private readonly IAdbClient _client;
    private readonly string? _configuredAdbPath;
    private readonly bool _mayStartServer;
    private readonly ILogger<AdbServerHealthService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AdbServerHealthService(
        IAdbClient client,
        string? configuredAdbPath,
        bool mayStartServer,
        ILogger<AdbServerHealthService> logger)
    {
        _client = client;
        _configuredAdbPath = configuredAdbPath;
        _mayStartServer = mayStartServer;
        _logger = logger;
    }

    public async Task EnsureAdbServerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _client.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (Exception firstFailure) when (_mayStartServer && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(firstFailure, "ADB server probe failed; attempting to start the daemon.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                _ = await _client.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // Start below.
            }

            var adbPath = ResolveAdbExecutable();
            var startInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("start-server");

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start the ADB server process.");
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    "ADB is not running and adb.exe could not be started. Run eng/Fetch-PlatformTools.ps1 or configure AdbExecutablePath.",
                    exception);
            }

            using (process)
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException($"adb start-server failed with exit code {process.ExitCode}: {stderr}");
                }
            }

            _ = await _client.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolveAdbExecutable()
    {
        if (!string.IsNullOrWhiteSpace(_configuredAdbPath))
        {
            return _configuredAdbPath;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("ADB_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "PlatformTools", "adb.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        return "adb.exe";
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
