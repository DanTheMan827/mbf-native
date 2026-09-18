# Architecture

The solution is intentionally split so **WinUI is a replaceable presentation layer**, not the owner of device or modding logic.

```text
ModsBeforeFriday.App (WinUI 3)
        |
        v
ModsBeforeFriday.Core  <-----  ModsBeforeFriday.Backend
   contracts/models             MBF orchestration, catalog IO,
   manifest editor              Rust-agent deployment/protocol
                                      |
                                      v
                              ModsBeforeFriday.Adb
                              ADB smart-socket + sync protocol
                                      |
                                      v
                             IAdbServerTransport
                                      |
                         +------------+------------+
                         |                         |
                 TcpAdbServerTransport       future transports
                 127.0.0.1:5037              pipe/tunnel/broker/etc.
```

## Project boundaries

### `ModsBeforeFriday.Adb`

No MBF, Beat Saber, HTTP, Windows UI, or process-launching concepts. It implements the host ADB **smart-socket protocol** and ADB sync push protocol over `IAdbServerTransport`.

The key abstraction requested for daemon communication is:

```csharp
public interface IAdbServerTransport
{
    string Description { get; }
    ValueTask<IAdbServerConnection> ConnectAsync(CancellationToken cancellationToken = default);
}
```

`IAdbServerConnection` exposes a duplex `Stream` plus `CompleteWritesAsync`, which allows classic ADB shell sessions to half-close stdin while continuing to read stdout. The first concrete transport is TCP (`TcpAdbServerTransport`), which talks to the normal desktop ADB daemon on port 5037.

A named-pipe, SSH-forwarded, brokered, remote, or in-memory implementation only needs to satisfy this transport contract. `AdbSmartSocketClient` and every layer above it remain unchanged.

### `ModsBeforeFriday.Core`

Pure .NET types with no WinUI dependency. It contains domain models, agent wire DTOs, semantic/version comparison, installation-state analysis, and the Android manifest editor. The manifest editor is a port of the behavior in `mbf-site/src/AndroidManifest.ts`.

### `ModsBeforeFriday.Backend`

The website-specific non-UI behavior lives here:

- deploy/check/chmod `/data/local/tmp/mbf-agent`;
- send newline-delimited JSON requests to the Rust agent;
- parse log messages and final typed responses;
- upload imports and splash screens;
- get status, patch, repatch, quick-fix, remove/enable/disable/import mods;
- force-stop/restart/uninstall Beat Saber;
- capture logcat;
- retrieve and merge `mods.bsquest.xyz/{version}.json` and `global.json`;
- select newest SemVer per mod and reproduce mbf-site filtering/update behavior;
- optionally start `adb.exe` if the local daemon is unavailable.

`BackendRuntime.Create(IAdbServerTransport, ...)` is the public composition point for a custom ADB daemon transport. `CreateDefault(...)` selects TCP.

### `ModsBeforeFriday.App`

WinUI 3 only: pages, view-models, `ContentDialog`, file pickers, URI launching, custom title bar, adaptive `NavigationView`, and Acrylic. View-models consume `IQuestService` and `IModCatalogService`; they do not execute ADB commands, deserialize Rust JSON, or fetch mod repositories.

## Dependency rule

A build should fail architectural review if a WinUI type (`Microsoft.UI.*`, `Windows.Storage.Pickers`, etc.) appears in `ModsBeforeFriday.Adb`, `ModsBeforeFriday.Core`, or `ModsBeforeFriday.Backend`.

Conversely, raw ADB service strings (`host:transport:`, `sync:`, `shell:`), the remote agent path, and `mods.bsquest.xyz` should not appear in the WinUI project.
