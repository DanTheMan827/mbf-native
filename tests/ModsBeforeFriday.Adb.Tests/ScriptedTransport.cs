using ModsBeforeFriday.Adb.Abstractions;

namespace ModsBeforeFriday.Adb.Tests;

internal sealed class ScriptedTransport : IAdbServerTransport
{
    private readonly Queue<byte[]> _responses;

    public ScriptedTransport(params byte[][] responses)
    {
        _responses = new Queue<byte[]>(responses);
    }

    public string Description => "scripted://test";

    public List<DuplexScriptStream> OpenedStreams { get; } = [];

    public ValueTask<IAdbServerConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_responses.Count == 0) throw new InvalidOperationException("No scripted response remains.");
        var stream = new DuplexScriptStream(_responses.Dequeue());
        OpenedStreams.Add(stream);
        return ValueTask.FromResult<IAdbServerConnection>(new Connection(this, stream));
    }

    public int CompleteWritesCallCount { get; private set; }

    private sealed class Connection(ScriptedTransport owner, DuplexScriptStream stream) : IAdbServerConnection
    {
        public Stream Stream => stream;
        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken = default)
        {
            owner.CompleteWritesCallCount++;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class DuplexScriptStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();

    public DuplexScriptStream(byte[] input) => _input = new MemoryStream(input, writable: false);

    public byte[] WrittenBytes => _output.ToArray();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _input.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _input.ReadAsync(buffer, cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _output.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _output.WriteAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
