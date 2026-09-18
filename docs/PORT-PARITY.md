# mbf-site parity checklist

The native client ports behavior rather than embedding the website.

| mbf-site behavior | Native location | Status |
|---|---|---|
| Device discovery / authorization state | `ModsBeforeFriday.Adb` + Home | Implemented through desktop ADB server |
| Android release / Quest 1 pre-v51 detection | `QuestService` | Implemented |
| Agent SHA-1 check, upload, chmod | `MbfAgentClient` | Implemented |
| Rust NDJSON request/response protocol | Core DTOs + `MbfAgentClient` | Implemented |
| Get mod status | `QuestService` | Implemented |
| Installation state / downgrade decision | `InstallationAnalyzer` | Implemented |
| Fetch downgraded manifest | `QuestService` | Implemented |
| Default manifest patch changes | `AndroidManifestDocument` | Implemented |
| Permission/feature toggles | Core manifest + Home/Tools | Implemented |
| Advanced manifest XML import/export | Tools | Implemented |
| Patch / downgrade / repatch | Home + Tools | Implemented |
| Optional PNG VR splash | Tools | Implemented |
| Quick fix | Home | Implemented |
| Wipe non-core mods + reinstall core | Tools | Implemented |
| Installed mod enable/disable batching | Mods | Implemented |
| Remove mod | Mods | Implemented |
| Import local files | Mods | Implemented |
| Import URL | Mods | Implemented |
| Auto-enable compatible imported QMOD | Mods | Implemented |
| Warn and leave mismatched QMOD disabled | Mods | Implemented |
| Per-version + global mod repo retrieval | `ModCatalogService` | Implemented |
| Latest SemVer per mod | `ModCatalogService` | Implemented |
| Update-first + alphabetical listing | `ModCatalogService` | Implemented |
| Core-mod update suppression | `ModCatalogService` | Implemented |
| Force-stop / restart game | Tools | Implemented |
| Uninstall Beat Saber | Home/Tools | Implemented |
| Fix PlayerData | Tools | Implemented |
| Agent operation log | Logs | Implemented |
| logcat capture/save | Logs | Implemented |
| `dev=true` equivalent | Settings: Developer mode | Implemented |
| `setcores` equivalent | Settings: Core mod override URL | Implemented |
| `game_id` / `ignore_package_id` equivalent | Settings (restart required) | Implemented |
| Browser/WebUSB-only compatibility messages | N/A | Intentionally removed |
| Web visual effects / React components | N/A | Replaced by native WinUI 3 |

## Deliberate native differences

- The browser used direct WebUSB ADB. Windows uses the desktop ADB daemon so it coexists with standard Android tooling and inherits ADB's key store/authentication.
- After state-changing operations, the native client favors re-reading authoritative status from the Rust agent instead of synthesizing more client-side state than necessary.
- ADB daemon transport is an explicit abstraction so TCP is not baked into the MBF backend.
