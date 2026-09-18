using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Core.Agent;
using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Backend.Agent;

internal sealed class MbfAgentClient
{
    public const string RemoteAgentPath = "/data/local/tmp/mbf-agent";
    public const string RemoteUploadsPath = "/data/local/tmp/mbf/uploads";

    private readonly IAdbClient _adb;
    private readonly AgentBinaryProvider _binaryProvider;
    private readonly AgentRequestParameters _parameters;
    private readonly ILogger<MbfAgentClient> _logger;

    public MbfAgentClient(
        IAdbClient adb,
        AgentBinaryProvider binaryProvider,
        AgentRequestParameters parameters,
        ILogger<MbfAgentClient> logger)
    {
        _adb = adb;
        _binaryProvider = binaryProvider;
        _parameters = parameters;
        _logger = logger;
    }

    public async Task EnsureAgentAsync(
        string serial,
        IProgress<AgentLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        Report(progress, AgentLogLevel.Info, "Preparing MBF agent.");
        var binary = await _binaryProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var existingSha1 = (await _adb.ExecuteShellAsync(
            serial,
            $"sha1sum {RemoteAgentPath} | cut -f 1 -d ' '",
            cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();

#pragma warning disable CA1848 // Use the LoggerMessage delegates
        _logger.LogDebug("Local MBF agent SHA1 {LocalSha1}; device SHA1 {RemoteSha1}", binary.Sha1, existingSha1);
#pragma warning restore CA1848 // Use the LoggerMessage delegates
        if (string.Equals(existingSha1, binary.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            Report(progress, AgentLogLevel.Info, "Agent is up to date.");
            return;
        }

        Report(progress, AgentLogLevel.Info, "Uploading MBF agent to the Quest.");
        await _adb.ExecuteShellAsync(serial, $"rm -f {RemoteAgentPath}", cancellationToken).ConfigureAwait(false);
        await using (var source = File.OpenRead(binary.Path))
        {
            await _adb.PushAsync(serial, source, RemoteAgentPath, unixMode: 0x81A4, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        await _adb.ExecuteShellAsync(serial, $"chmod 755 {RemoteAgentPath}", cancellationToken).ConfigureAwait(false);
        Report(progress, AgentLogLevel.Info, "Agent is ready.");
    }

    public async Task<TResponse> SendAsync<TResponse>(
        string serial,
        AgentRequest request,
        IProgress<AgentLogEntry>? progress,
        CancellationToken cancellationToken)
        where TResponse : AgentResponse
    {
        request = request with { AgentParameters = _parameters };
        await EnsureAgentAsync(serial, progress, cancellationToken).ConfigureAwait(false);

        var json = AgentJson.Serialize(request);
        _logger.LogTrace("MBF agent request: {Request}", json);

        await using var session = await _adb.OpenShellAsync(serial, RemoteAgentPath, cancellationToken).ConfigureAwait(false);
        var payload = Encoding.UTF8.GetBytes(json + "\n");
        await session.Stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await session.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // mbf-agent reads exactly one newline-terminated JSON request. It does not
        // require stdin EOF. Keep the classic ADB shell connection fully open and
        // read until the agent exits; half-closing the smart-socket TCP connection
        // is not equivalent to shell-v2 CLOSE_STDIN and can truncate agent output.
        using var reader = new StreamReader(session.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        AgentResponse? finalResponse = null;
        LogMessageAgentResponse? lastError = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            AgentResponse message;
            try
            {
                message = AgentJson.DeserializeResponse(line);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"MBF agent emitted invalid JSON: {line}", exception);
            }

            if (message is LogMessageAgentResponse log)
            {
                _logger.Log(ToLogLevel(log.Level), "MBF agent: {Message}", log.Message);
                progress?.Report(log.ToDomain());
                if (log.Level == AgentLogLevelDto.Error)
                {
                    lastError = log;
                }

                continue;
            }

            finalResponse = message;
        }

        if (finalResponse is TResponse typed)
        {
            return typed;
        }

        if (finalResponse is not null)
        {
            throw new InvalidDataException(
                $"Expected MBF agent response {typeof(TResponse).Name}, but received {finalResponse.GetType().Name}.");
        }

        if (lastError is not null)
        {
            throw new InvalidOperationException(lastError.Message);
        }

        throw new InvalidDataException("The MBF agent exited without a final response.");
    }

    private static Microsoft.Extensions.Logging.LogLevel ToLogLevel(AgentLogLevelDto level) => level switch
    {
        AgentLogLevelDto.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        AgentLogLevelDto.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
        AgentLogLevelDto.Info => Microsoft.Extensions.Logging.LogLevel.Information,
        AgentLogLevelDto.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        AgentLogLevelDto.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
        _ => Microsoft.Extensions.Logging.LogLevel.Information,
    };

    private static void Report(IProgress<AgentLogEntry>? progress, AgentLogLevel level, string message) =>
        progress?.Report(new AgentLogEntry(DateTimeOffset.Now, level, message));
}
