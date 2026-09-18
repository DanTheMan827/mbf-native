# UI behavior

## Device/navigation state

- Home, Logs, and Settings are always available.
- Mods and Tools are disabled until a ready ADB device has been inspected and the installed Beat Saber APK reports the supported Scotland2 modloader.
- If the verified-modded state is lost while Mods or Tools is active, the shell navigates back to Home.
- If exactly one ADB device is returned, Home selects it and immediately attempts to connect. Unauthorized/offline states still surface their normal guidance.
- Connect is disabled while a device is connected; Disconnect is enabled only while a device is connected.
- Patch Options are visible only while the installation analysis says the game needs patching.
- When a ready device transitions into a verified Scotland2-modded state, the shell automatically enables and selects the Mods page. Refreshes that remain in the already-modded state do not steal navigation focus.

## Rust-agent operations

Every operation that launches the MBF Rust agent opens a blocking application-level overlay before the request begins. The overlay:

- spans the title/content surface and blocks interaction with the application;
- shows only the log entries emitted by the current agent invocation;
- auto-scrolls as new messages arrive;
- closes when the invocation completes or fails;
- retains the same messages in the persistent Logs page;
- prevents application close while the operation is active.

ADB-only operations such as force-stop/restart/uninstall and logcat capture do not use the Rust-agent overlay.

## Mods

The Mods page has two tabs:

- **Your Mods**: installed updates first, then installed non-core mods, then core mods.
- **Install Mods**: catalog updates first, then mods not currently installed.

Install cards show the mod cover when available, selection above the Install/Update action, and Source/Report bug actions below the description. Report bug is shown for GitHub-backed mods.
