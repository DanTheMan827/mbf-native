using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using ModsBeforeFriday.Adb.Exceptions;

namespace ModsBeforeFriday.Adb.Protocol;

internal static class AdbProtocolIO
{
    public static async ValueTask WriteServiceRequestAsync(
        Stream stream,
        string service,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(service);
        if (payload.Length > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(service), "ADB services are limited to 65535 UTF-8 bytes.");
        }

        var prefix = Encoding.ASCII.GetBytes(payload.Length.ToString("X4", CultureInfo.InvariantCulture));
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask ExpectOkayAsync(
        Stream stream,
        string service,
        CancellationToken cancellationToken)
    {
        var status = await ReadExactlyAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        var statusText = Encoding.ASCII.GetString(status);
        if (statusText == "OKAY")
        {
            return;
        }

        if (statusText == "FAIL")
        {
            var message = await ReadLengthPrefixedStringAsync(stream, cancellationToken).ConfigureAwait(false);
            throw new AdbServerException(service, message);
        }

        throw new AdbException($"Unexpected ADB status '{statusText}' while opening '{service}'.");
    }

    public static async ValueTask<byte[]> ReadLengthPrefixedPayloadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var prefix = await ReadExactlyAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        var prefixText = Encoding.ASCII.GetString(prefix);
        if (!int.TryParse(prefixText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var length))
        {
            throw new AdbException($"Invalid ADB hexadecimal length prefix '{prefixText}'.");
        }

        return await ReadExactlyAsync(stream, length, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<string> ReadLengthPrefixedStringAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadLengthPrefixedPayloadAsync(stream, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }

    public static async ValueTask<byte[]> ReadExactlyAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        await ReadExactlyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        return buffer;
    }

    public static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException($"ADB stream ended with {buffer.Length - offset} bytes still expected.");
            }

            offset += read;
        }
    }

    public static async ValueTask WriteSyncPacketAsync(
        Stream stream,
        ReadOnlyMemory<byte> id,
        uint value,
        CancellationToken cancellationToken)
    {
        if (id.Length != 4)
        {
            throw new ArgumentException("ADB sync packet identifiers are exactly four bytes.", nameof(id));
        }

        var header = new byte[8];
        id.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), value);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<(string Id, uint Value)> ReadSyncHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = await ReadExactlyAsync(stream, 8, cancellationToken).ConfigureAwait(false);
        return (
            Encoding.ASCII.GetString(header, 0, 4),
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)));
    }
}
