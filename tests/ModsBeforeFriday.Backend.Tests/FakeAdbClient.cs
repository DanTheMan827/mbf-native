using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Adb.Models;

namespace ModsBeforeFriday.Backend.Tests;

internal sealed class FakeAdbClient : IAdbClient
{
    public string ShellOutput { get; set; } = string.Empty;
    public string SessionResponse { get; set; } = string.Empty;
    public string? CapturedSessionInput { get; private set; }

    public Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdbDevice>>([]);

    public Task<string> ExecuteShellAsync(string serial, string command, CancellationToken cancellationToken = default) =>
        Task.FromResult(ShellOutput);

    public Task<IAdbShellSession> OpenShellAsync(string serial, string command, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAdbShellSession>(new Session(this, SessionResponse));

    public Task PushAsync(string serial, Stream source, string remotePath, int unixMode = 0x81A4, DateTimeOffset? modified = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private sealed class Session : IAdbShellSession
    {
        private readonly FakeAdbClient _owner;
        private readonly DuplexStream _stream;

        public Session(FakeAdbClient owner, string response)
        {
            _owner = owner;
            _stream = new DuplexStream(response);
        }

        public Stream Stream => _stream;

        public ValueTask DisposeAsync()
        {
            _owner.CapturedSessionInput = _stream.WrittenText;
            _stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DuplexStream : Stream
    {
        private readonly MemoryStream _input;
        private readonly MemoryStream _output = new();

        public DuplexStream(string response) => _input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(response));
        public string WrittenText => System.Text.Encoding.UTF8.GetString(_output.ToArray());
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _output.WriteAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
