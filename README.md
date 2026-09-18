# Mods Before Friday for Windows

A **native C# / WinUI 3** Windows 11 client for the `mbf-site` workflow. It uses the same MBF Rust agent binary for all patching/modding work; the Windows code is a native presentation and orchestration layer, not a replacement patcher.

## Technology baseline

- .NET 10 LTS / C# 14
- Windows App SDK 2.4.0 / WinUI 3
- Windows 11 minimum (10.0.22000)
- Desktop Acrylic window backdrop
- Adaptive native `NavigationView` sidebar (`Expanded` -> `Compact` -> `Minimal` as the window narrows)
- x64 and ARM64 configurations

## Architecture at a glance

- `ModsBeforeFriday.Adb` - ADB smart-socket + sync protocols over **pluggable `IAdbServerTransport`**. TCP/5037 is only one transport.
- `ModsBeforeFriday.Core` - UI-free domain models, MBF JSON DTOs, install-state/version logic, Android manifest editing.
- `ModsBeforeFriday.Backend` - UI-free Rust-agent deployment/interaction, Quest operations, mod repository retrieval/listing, imports, logcat, and ADB daemon lifecycle.
- `ModsBeforeFriday.App` - thin WinUI presentation: pages/view-models, dialogs, pickers, backdrop, title bar, navigation.
- `tests/*` - protocol and domain tests that do not require WinUI or a Quest.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the dependency rules and custom transport extension point.

## Bootstrap

The repository intentionally does **not** check in the MBF agent or Google platform-tools binaries.

From PowerShell 7 on Windows:

```powershell
./eng/Bootstrap.ps1
```

This does two things:

1. Downloads the currently deployed `mbf-agent` from `https://mbf.bsquest.xyz/mbf-agent`, records its SHA-1, and stages it under `Assets/Agent`.
2. Downloads Google's current Windows platform-tools and stages them under `Assets/PlatformTools` so the app can start `adb.exe` when no daemon is listening.

You can pin the exact agent expected by CI/release engineering:

```powershell
./eng/Bootstrap.ps1 -ExpectedAgentSha1 '0123456789ABCDEF...'
```

or point at another deployment:

```powershell
./eng/Bootstrap.ps1 -AgentUrl 'https://example.invalid/mbf-agent'
```

At runtime the app compares the staged binary's SHA-1 with `/data/local/tmp/mbf-agent` and only pushes/chmods it when needed.

## Build

Install Visual Studio with **.NET desktop development** and the Windows App SDK/Windows SDK tooling, or use the .NET 10.0.401 SDK from a Developer PowerShell.

```powershell
dotnet restore ./ModsBeforeFriday.sln
dotnet test ./ModsBeforeFriday.sln -c Release
dotnet build ./src/ModsBeforeFriday.App/ModsBeforeFriday.App.csproj -c Release -p:Platform=x64
```

Open `ModsBeforeFriday.sln` in Visual Studio for normal WinUI development.

## Device flow

The native app talks to the desktop ADB **server**, not directly to USB. The default transport connects to `127.0.0.1:5037`. If a daemon is not running, the backend attempts to launch the bundled/configured `adb.exe`. ADB itself owns USB discovery, RSA keys, and the headset authorization prompt.

A custom daemon transport can be injected without changing the ADB protocol or MBF layers:

```csharp
IAdbServerTransport transport = new MyNamedPipeTransport(...);
using var backend = BackendRuntime.Create(transport, options, loggerFactory);
```

The WinUI app itself does not reference the ADB project; that example belongs in a backend host/composition layer when adding a new transport.

## Ported functionality

The current source includes device discovery, installation inspection, supported/downgrade decision logic, manifest permissions, patch/downgrade/repatch, mod listing/update detection, enable/disable/remove, file and URL imports, quick-fix/core-only repair, player-data repair, force-stop/restart/uninstall, operation logs, and logcat capture.

See [`docs/PORT-PARITY.md`](docs/PORT-PARITY.md) for the detailed mapping to `mbf-site`.

## Upstream provenance

The port was mapped from `DanTheMan827/ModsBeforeFriday` `main`, inspected at commit `a500998f7479358cea97c12ad32a5391c513448b`. The Rust protocol DTOs mirror `mbf-agent/src/models/request.rs` and `response.rs`; the orchestration behavior follows `mbf-site/src/Agent.ts`, `DeviceModder.tsx`, `ModManager.tsx`, `ModRepoBrowser.tsx`, `OptionsMenu.tsx`, `PermissionsMenu.tsx`, and `AndroidManifest.ts`.

See [`docs/MBF-PROTOCOL.md`](docs/MBF-PROTOCOL.md) before updating to a newer upstream agent.
