# MBF agent protocol

Port source: `DanTheMan827/ModsBeforeFriday`, branch `main`, inspected commit `a500998f7479358cea97c12ad32a5391c513448b` (2026-04-06).

The source website uploads an ARM64 Android Rust executable to:

```text
/data/local/tmp/mbf-agent
```

It writes exactly one JSON request followed by `\n` to the process stdin. The Rust executable writes newline-delimited JSON to stdout. Zero or more `LogMsg` records may precede one final response record.

Every request is flattened with common agent parameters:

```json
{
  "agent_parameters": {
    "game_id": "com.beatgames.beatsaber",
    "ignore_package_id": false
  },
  "type": "GetModStatus",
  "override_core_mod_url": null
}
```

Supported requests mirrored by the C# DTOs:

- `GetModStatus`
- `SetModsEnabled`
- `RemoveMod`
- `Import`
- `ImportUrl`
- `Patch`
- `FixPlayerData`
- `GetDowngradedManifest`
- `QuickFix`

Supported response types:

- `LogMsg`
- `ModStatus`
- `Mods`
- `ModSyncResult`
- `Patched`
- `ImportResult`
- `FixedPlayerData`
- `DowngradedManifest`

`MbfAgentClient` deliberately treats an `Error` `LogMsg` as the actionable failure only when the process does not later produce a final response, matching the web client's semantics.

## Binary parity

`eng/Fetch-MbfAgent.ps1` downloads the **deployed binary**, rather than compiling a parallel C# implementation or reimplementing the patcher. Its SHA-1 is recorded in `Assets/Agent/agent.lock.json`. At runtime the backend asks the Quest for the existing agent SHA-1; it only uploads the staged binary when the hashes differ.

This matches `mbf-site/src/Agent.ts`'s deployment strategy and ensures the native UI continues to use MBF's Rust implementation for patching, downgrading, dependencies, imports, and repairs.
