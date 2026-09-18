using System.Net.Sockets;
using ModsBeforeFriday.Adb.Abstractions;

namespace ModsBeforeFriday.Adb.Transports;

/// <summary>Connects to the standard ADB server smart socket, normally 127.0.0.1:5037.</summary>
public sealed class TcpAdbServerTransport : IAdbServerTransport
{
    public TcpAdbServerTransport(string host = "127.0.0.1", int port = 5037)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        Host = host;
        Port = port;
    }

    public string Host { get; }

    public int Port { get; }

    public string Description => $"tcp://{Host}:{Port}";

    public async ValueTask<IAdbServerConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var client = new TcpClient
        {
            NoDelay = true,
        };

        try
        {
            await client.ConnectAsync(Host, Port, cancellationToken).ConfigureAwait(false);
            return new TcpAdbServerConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private sealed class TcpAdbServerConnection : IAdbServerConnection
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private bool _writesCompleted;

        public TcpAdbServerConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public Stream Stream => _stream;

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_writesCompleted)
            {
                _client.Client.Shutdown(SocketShutdown.Send);
                _writesCompleted = true;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            _client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
