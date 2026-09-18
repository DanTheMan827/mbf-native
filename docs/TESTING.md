# Testing

## Automated tests

```powershell
dotnet test .\ModsBeforeFriday.sln -c Release
```

Test projects intentionally have no WinUI dependency:

- `ModsBeforeFriday.Adb.Tests`: smart-socket framing, device parsing, shell service sequencing, sync push framing.
- `ModsBeforeFriday.Core.Tests`: agent JSON shape, Beat Saber version ordering, SemVer, manifest changes, installation-state analysis.
- `ModsBeforeFriday.Application.Tests`: UI-independent presentation state, manifest toggles, mod update metadata, navigation eligibility, and agent-operation lifecycle.
- `ModsBeforeFriday.Backend.Tests`: agent deployment hash behavior, log relay, final-response/error handling, and mod-catalog cover/update filtering using fakes.

## Physical-device smoke test

1. Run `eng/Bootstrap.ps1`.
2. Connect a Quest with Developer Mode/USB debugging enabled.
3. Confirm `adb devices -l` reports the headset as `device`.
4. Start the WinUI app and refresh devices.
5. Connect and verify the agent checksum/upload log.
6. On a clean supported Beat Saber install, inspect the manifest options and patch.
7. Install a repository mod, toggle it off/on, and sync.
8. Import a local `.qmod` and a song `.zip`.
9. Force-stop/restart the game from Tools.
10. Start/stop logcat and save the resulting `.log`.
11. Repatch with one optional permission and an optional PNG splash.
12. Exercise Quick Fix on a known test device before using destructive wipe/uninstall actions.

## Protocol regression test when upstream changes

When `mbf-agent/src/models/request.rs`, `response.rs`, or `mbf-site/src/Agent.ts` changes upstream:

1. Diff the Rust enum variants and field names against `Core/Agent` DTOs.
2. Add/adjust JSON fixture tests before changing production code. Assert parsed JSON values/property names rather than relying on a specific legal JSON escape representation.
3. Fetch the newly deployed agent and commit the resulting `agent.lock.json` in release builds (the binary itself remains ignored).
4. Run the physical-device smoke test.
