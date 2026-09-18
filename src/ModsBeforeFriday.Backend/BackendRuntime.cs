using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModsBeforeFriday.Adb.Abstractions;
using ModsBeforeFriday.Adb.Protocol;
using ModsBeforeFriday.Adb.Transports;
using ModsBeforeFriday.Backend.Agent;
using ModsBeforeFriday.Backend.Configuration;
using ModsBeforeFriday.Backend.Devices;
using ModsBeforeFriday.Backend.Mods;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Agent;

namespace ModsBeforeFriday.Backend;

/// <summary>
/// Composition root for all UI-independent MBF functionality. UI projects consume
/// only the interfaces exposed here and never instantiate ADB protocol objects.
/// </summary>
public sealed class BackendRuntime : IDisposable
{
    private readonly HttpClient _httpClient;

    private BackendRuntime(
        IQuestService quest,
        IModCatalogService mods,
        IBackendHealthService health,
        HttpClient httpClient)
    {
        Quest = quest;
        Mods = mods;
        Health = health;
        _httpClient = httpClient;
    }

    public IQuestService Quest { get; }

    public IModCatalogService Mods { get; }

    public IBackendHealthService Health { get; }

    public static BackendRuntime CreateDefault(
        BackendOptions options,
        ILoggerFactory? loggerFactory = null,
        string adbServerHost = "127.0.0.1",
        int adbServerPort = 5037)
    {
        var transport = new TcpAdbServerTransport(adbServerHost, adbServerPort);
        return Create(transport, options, loggerFactory);
    }

    /// <summary>
    /// Creates the full backend over any ADB-server transport. This is the extension
    /// point for named pipes, remote tunnels, brokered transports, and tests.
    /// </summary>
    public static BackendRuntime Create(
        IAdbServerTransport transport,
        BackendOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        loggerFactory ??= NullLoggerFactory.Instance;

        IAdbClient adb = new AdbSmartSocketClient(transport);
        var health = new AdbServerHealthService(
            adb,
            options.AdbExecutablePath,
            options.StartLocalAdbServerIfUnavailable,
            loggerFactory.CreateLogger<AdbServerHealthService>());
        var binaryProvider = new AgentBinaryProvider(options.AgentBinaryPath);
        var parameters = new AgentRequestParameters(options.GameId, options.IgnorePackageId);
        var agent = new MbfAgentClient(adb, binaryProvider, parameters, loggerFactory.CreateLogger<MbfAgentClient>());
        var quest = new QuestService(adb, health, agent, options.GameId, loggerFactory.CreateLogger<QuestService>());

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2),
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ModsBeforeFriday-Windows/1.0");
        var mods = new ModCatalogService(httpClient, options, loggerFactory.CreateLogger<ModCatalogService>());

        return new BackendRuntime(quest, mods, health, httpClient);
    }

    public void Dispose() => _httpClient.Dispose();
}
