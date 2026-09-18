using System.Text;
using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Adb.Exceptions;
using ModsBeforeFriday.Adb.Models;

namespace ModsBeforeFriday.Adb.Protocol;

/// <summary>
/// Implements the host-side ADB smart-socket and sync protocols over an arbitrary
/// <see cref="IAdbServerTransport"/>.
/// </summary>
public sealed class AdbSmartSocketClient : IAdbClient
{
    private static readonly byte[] SyncSend = "SEND"u8.ToArray();
    private static readonly byte[] SyncData = "DATA"u8.ToArray();
    private static readonly byte[] SyncDone = "DONE"u8.ToArray();
    private const int SyncChunkSize = 64 * 1024;

    private readonly IAdbServerTransport _transport;

    public AdbSmartSocketClient(IAdbServerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await AdbProtocolIO.WriteServiceRequestAsync(connection.Stream, "host:devices-l", cancellationToken).ConfigureAwait(false);
        await AdbProtocolIO.ExpectOkayAsync(connection.Stream, "host:devices-l", cancellationToken).ConfigureAwait(false);
        var payload = await AdbProtocolIO.ReadLengthPrefixedStringAsync(connection.Stream, cancellationToken).ConfigureAwait(false);

        var devices = payload
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseDeviceLine)
            .ToArray();

        return devices;
    }

    public async Task<string> ExecuteShellAsync(
        string serial,
        string command,
        CancellationToken cancellationToken = default)
    {
        await using var shell = await OpenShellAsync(serial, command, cancellationToken).ConfigureAwait(false);

        // Do not half-close the underlying ADB server socket here. This is the
        // classic `shell:` service, not shell-v2. The remote command will close
        // the service when it exits, and half-closing the daemon transport can
        // cause output to be discarded before it reaches the client.
        using var reader = new StreamReader(shell.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IAdbShellSession> OpenShellAsync(
        string serial,
        string command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var connection = await OpenDeviceServiceAsync(serial, $"shell:{command}", cancellationToken).ConfigureAwait(false);
        return new ShellSession(connection);
    }

    public async Task PushAsync(
        string serial,
        Stream source,
        string remotePath,
        int unixMode = 0x81A4,
        DateTimeOffset? modified = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        await using var connection = await OpenDeviceServiceAsync(serial, "sync:", cancellationToken).ConfigureAwait(false);
        var stream = connection.Stream;

        var sendArgument = Encoding.UTF8.GetBytes($"{remotePath},{unixMode}");
        await AdbProtocolIO.WriteSyncPacketAsync(stream, SyncSend, (uint)sendArgument.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(sendArgument, cancellationToken).ConfigureAwait(false);

        var buffer = new byte[SyncChunkSize];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await AdbProtocolIO.WriteSyncPacketAsync(stream, SyncData, (uint)read, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var timestamp = modified ?? DateTimeOffset.UtcNow;
        var unixSeconds = checked((uint)timestamp.ToUnixTimeSeconds());
        await AdbProtocolIO.WriteSyncPacketAsync(stream, SyncDone, unixSeconds, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var response = await AdbProtocolIO.ReadSyncHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        if (response.Id == "OKAY")
        {
            return;
        }

        if (response.Id == "FAIL")
        {
            var errorBytes = await AdbProtocolIO.ReadExactlyAsync(stream, checked((int)response.Value), cancellationToken).ConfigureAwait(false);
            throw new AdbException($"ADB sync push failed: {Encoding.UTF8.GetString(errorBytes)}");
        }

        throw new AdbException($"Unexpected ADB sync response '{response.Id}'.");
    }

    private async ValueTask<IAdbServerConnection> OpenDeviceServiceAsync(
        string serial,
        string service,
        CancellationToken cancellationToken)
    {
        var connection = await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transportService = $"host:transport:{serial}";
            await AdbProtocolIO.WriteServiceRequestAsync(connection.Stream, transportService, cancellationToken).ConfigureAwait(false);
            await AdbProtocolIO.ExpectOkayAsync(connection.Stream, transportService, cancellationToken).ConfigureAwait(false);

            await AdbProtocolIO.WriteServiceRequestAsync(connection.Stream, service, cancellationToken).ConfigureAwait(false);
            await AdbProtocolIO.ExpectOkayAsync(connection.Stream, service, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static AdbDevice ParseDeviceLine(string line)
    {
        // ADB normally separates the serial and state with a tab, but daemon
        // implementations/versions are allowed to use runs of whitespace.
        // Parse the line as whitespace-delimited tokens instead of requiring
        // one exact delimiter. This also handles aligned emulator rows.
        var tokens = line.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 2)
        {
            throw new AdbException($"Malformed ADB device row '{line}'.");
        }

        var serial = tokens[0];
        var attributeStart = 2;
        var stateText = tokens[1];

        // `adb devices -l` can report this state as two words. Keep it as one
        // logical state before parsing the remaining key:value metadata.
        if (tokens.Length >= 3 &&
            tokens[1].Equals("no", StringComparison.OrdinalIgnoreCase) &&
            tokens[2].Equals("permissions", StringComparison.OrdinalIgnoreCase))
        {
            stateText = "no permissions";
            attributeStart = 3;
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens.Skip(attributeStart))
        {
            var colon = token.IndexOf(':');
            if (colon > 0)
            {
                attributes[token[..colon]] = token[(colon + 1)..];
            }
        }

        long? transportId = null;
        if (attributes.TryGetValue("transport_id", out var transportIdText) &&
            long.TryParse(transportIdText, out var parsedTransportId))
        {
            transportId = parsedTransportId;
        }

        attributes.TryGetValue("model", out var model);
        attributes.TryGetValue("product", out var product);
        attributes.TryGetValue("device", out var device);

        return new AdbDevice(
            serial,
            ParseState(stateText),
            model?.Replace('_', ' '),
            product,
            device,
            transportId,
            attributes);
    }

    private static AdbDeviceState ParseState(string value) => value.ToLowerInvariant() switch
    {
        "device" => AdbDeviceState.Device,
        "offline" => AdbDeviceState.Offline,
        "unauthorized" => AdbDeviceState.Unauthorized,
        "no permissions" => AdbDeviceState.NoPermissions,
        "recovery" => AdbDeviceState.Recovery,
        "sideload" => AdbDeviceState.Sideload,
        "bootloader" => AdbDeviceState.Bootloader,
        _ => AdbDeviceState.Unknown,
    };

    private sealed class ShellSession : IAdbShellSession
    {
        private readonly IAdbServerConnection _connection;

        public ShellSession(IAdbServerConnection connection)
        {
            _connection = connection;
        }

        public Stream Stream => _connection.Stream;

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
